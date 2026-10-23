using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Exceptions;
using ClinicAppointment.API.Models;
using ClinicAppointment.API.Services;
using ClinicAppointment.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ClinicAppointment.Tests.Services;

/// <summary>Covers UC-14 (reschedule an appointment).</summary>
public class RescheduleTests
{
    private static readonly DateTime Monday = TestData.Monday;

    private static RescheduleAppointmentRequest To(DateTime start) => new() { NewStartTime = start };

    private static async Task<AppointmentDto> BookAsync(TestData data, DateTime start, Patient? patient = null) =>
        await data.Appointments().BookAsync((patient ?? data.Patient).UserId, data.Booking(start));

    [Fact]
    public async Task Reschedule_ToOpenSlot_MovesTimesAndCounts()
    {
        using var data = new TestData();
        var booked = await BookAsync(data, Monday.AddHours(9));

        var moved = await data.Appointments().RescheduleAsync(booked.Id, data.Patient.UserId, Roles.Patient, To(Monday.AddDays(1).AddHours(10)));

        Assert.Equal(Monday.AddDays(1).AddHours(10), moved.StartTime);
        Assert.Equal(Monday.AddDays(1).AddHours(10).AddMinutes(30), moved.EndTime);
        Assert.Equal(1, moved.RescheduleCount);
        Assert.Equal("Scheduled", moved.Status);
    }

    [Fact]
    public async Task Reschedule_FreesTheOldSlotForOthers()
    {
        using var data = new TestData();
        var booked = await BookAsync(data, Monday.AddHours(9));
        await data.Appointments().RescheduleAsync(booked.Id, data.Patient.UserId, Roles.Patient, To(Monday.AddHours(11)));

        var other = await BookAsync(data, Monday.AddHours(9), data.OtherPatient);

        Assert.Equal("Scheduled", other.Status);
    }

    [Fact]
    public async Task Reschedule_ToAdjacentSlot_IsNotBlockedByItsOwnCurrentTime()
    {
        using var data = new TestData();
        var booked = await BookAsync(data, Monday.AddHours(9));

        var moved = await data.Appointments().RescheduleAsync(booked.Id, data.Patient.UserId, Roles.Patient, To(Monday.AddHours(9).AddMinutes(30)));

        Assert.Equal(Monday.AddHours(9).AddMinutes(30), moved.StartTime);
    }

    [Fact]
    public async Task Reschedule_ResetsReminderSoTheNewTimeGetsOne()
    {
        using var data = new TestData();
        var booked = await BookAsync(data, Monday.AddHours(9));
        var entity = await data.Db.Appointments.SingleAsync();
        entity.ReminderSentAt = Monday.AddHours(8);
        await data.Db.SaveChangesAsync();

        await data.Appointments().RescheduleAsync(booked.Id, data.Patient.UserId, Roles.Patient, To(Monday.AddDays(2).AddHours(9)));

        await using var check = data.NewContext();
        Assert.Null((await check.Appointments.SingleAsync()).ReminderSentAt);
    }

    [Fact]
    public async Task Reschedule_RaisesEventWithOldStartTime()
    {
        using var data = new TestData();
        var booked = await BookAsync(data, Monday.AddHours(9));

        await data.Appointments().RescheduleAsync(booked.Id, data.Patient.UserId, Roles.Patient, To(Monday.AddHours(10)));

        Assert.Equal("Rescheduled", data.Events.Names.Last());
        Assert.Equal(Monday.AddHours(9), data.Events.LastOldStart);
    }

    [Fact]
    public async Task Reschedule_ToTakenSlot_ThrowsConflict()
    {
        using var data = new TestData();
        var mine = await BookAsync(data, Monday.AddHours(9));
        await BookAsync(data, Monday.AddHours(10), data.OtherPatient);

        await Assert.ThrowsAsync<ConflictException>(() =>
            data.Appointments().RescheduleAsync(mine.Id, data.Patient.UserId, Roles.Patient, To(Monday.AddHours(10))));
    }

    [Fact]
    public async Task Reschedule_ToSameTime_ThrowsBadRequest()
    {
        using var data = new TestData();
        var booked = await BookAsync(data, Monday.AddHours(9));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Appointments().RescheduleAsync(booked.Id, data.Patient.UserId, Roles.Patient, To(Monday.AddHours(9))));
    }

    [Theory]
    [InlineData(-24)] // in the past
    [InlineData(129)] // Saturday 09:00 (non-working day)
    [InlineData(15)]  // Monday 15:00, outside hours
    public async Task Reschedule_ToInvalidTime_ThrowsBadRequest(int hoursFromMonday)
    {
        using var data = new TestData();
        var booked = await BookAsync(data, Monday.AddHours(9));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Appointments().RescheduleAsync(booked.Id, data.Patient.UserId, Roles.Patient, To(Monday.AddHours(hoursFromMonday))));
    }

    [Fact]
    public async Task Reschedule_OntoDoctorsLeave_ThrowsBadRequest()
    {
        using var data = new TestData();
        var booked = await BookAsync(data, Monday.AddHours(9));
        await data.AddLeaveAsync(data.Doctor, Monday.AddDays(1), Monday.AddDays(2));

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Appointments().RescheduleAsync(booked.Id, data.Patient.UserId, Roles.Patient, To(Monday.AddDays(2).AddHours(9))));
        Assert.Contains("on leave", ex.Message);
    }

    [Fact]
    public async Task Reschedule_PatientLimit_BlocksThirdMove()
    {
        using var data = new TestData();
        var booked = await BookAsync(data, Monday.AddHours(9));
        var service = data.Appointments();
        await service.RescheduleAsync(booked.Id, data.Patient.UserId, Roles.Patient, To(Monday.AddHours(10)));
        await service.RescheduleAsync(booked.Id, data.Patient.UserId, Roles.Patient, To(Monday.AddHours(11)));

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.RescheduleAsync(booked.Id, data.Patient.UserId, Roles.Patient, To(Monday.AddHours(9))));
        Assert.Contains($"{AppointmentService.MaxPatientReschedules} times", ex.Message);
    }

    [Fact]
    public async Task Reschedule_ByAdmin_IgnoresPatientLimitAndDoesNotCount()
    {
        using var data = new TestData();
        var booked = await BookAsync(data, Monday.AddHours(9));
        var service = data.Appointments();
        await service.RescheduleAsync(booked.Id, data.Patient.UserId, Roles.Patient, To(Monday.AddHours(10)));
        await service.RescheduleAsync(booked.Id, data.Patient.UserId, Roles.Patient, To(Monday.AddHours(11)));

        var moved = await service.RescheduleAsync(booked.Id, data.Admin.Id, Roles.Admin, To(Monday.AddHours(9)));

        Assert.Equal(2, moved.RescheduleCount);
    }

    [Fact]
    public async Task Reschedule_AnotherPatientsAppointment_ThrowsForbidden()
    {
        using var data = new TestData();
        var booked = await BookAsync(data, Monday.AddHours(9));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            data.Appointments().RescheduleAsync(booked.Id, data.OtherPatient.UserId, Roles.Patient, To(Monday.AddHours(10))));
    }

    [Fact]
    public async Task Reschedule_ByDoctor_ThrowsForbidden()
    {
        using var data = new TestData();
        var booked = await BookAsync(data, Monday.AddHours(9));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            data.Appointments().RescheduleAsync(booked.Id, data.Doctor.UserId, Roles.Doctor, To(Monday.AddHours(10))));
    }

    [Fact]
    public async Task Reschedule_CancelledAppointment_ThrowsBadRequest()
    {
        using var data = new TestData();
        var appt = await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(9), AppointmentStatus.Cancelled);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Appointments().RescheduleAsync(appt.Id, data.Patient.UserId, Roles.Patient, To(Monday.AddHours(10))));
    }

    [Fact]
    public async Task Reschedule_AppointmentAlreadyStarted_ThrowsBadRequest()
    {
        using var data = new TestData();
        var booked = await BookAsync(data, Monday.AddHours(9));
        data.SetNow(Monday.AddHours(9).AddMinutes(5));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Appointments().RescheduleAsync(booked.Id, data.Patient.UserId, Roles.Patient, To(Monday.AddHours(11))));
    }

    [Fact]
    public async Task Reschedule_OverlappingPatientsOtherAppointment_ThrowsConflict()
    {
        using var data = new TestData();
        var cardiologist = await data.AddDoctorAsync("Dr. Heart", "heart@test.com", data.Cardiology);
        var booked = await BookAsync(data, Monday.AddHours(9));
        await data.Appointments().BookAsync(data.Patient.UserId, data.Booking(Monday.AddHours(11), cardiologist));

        await Assert.ThrowsAsync<ConflictException>(() =>
            data.Appointments().RescheduleAsync(booked.Id, data.Patient.UserId, Roles.Patient, To(Monday.AddHours(11))));
    }

    [Fact]
    public async Task Reschedule_Missing_ThrowsNotFound()
    {
        using var data = new TestData();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            data.Appointments().RescheduleAsync(404, data.Admin.Id, Roles.Admin, To(Monday.AddHours(10))));
    }
}
