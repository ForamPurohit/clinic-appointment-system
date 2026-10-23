using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Exceptions;
using ClinicAppointment.API.Models;
using ClinicAppointment.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ClinicAppointment.Tests.Services;

/// <summary>Covers UC-04 (book), UC-05 (view), UC-06 (cancel), UC-08 (record outcome), UC-12 (admin view).</summary>
public class AppointmentServiceTests
{
    private static readonly DateTime Monday = TestData.Monday;

    // ======================= Booking (UC-04) =======================

    [Fact]
    public async Task Book_ValidSlot_CreatesScheduledAppointment()
    {
        using var data = new TestData();

        var result = await data.Appointments().BookAsync(data.Patient.UserId, data.Booking(Monday.AddHours(9)));

        Assert.Equal("Scheduled", result.Status);
        Assert.Equal(Monday.AddHours(9), result.StartTime);
        Assert.Equal(Monday.AddHours(9).AddMinutes(30), result.EndTime);
        Assert.Equal("Dr. Active", result.DoctorName);
        Assert.Equal("Pat One", result.PatientName);

        await using var check = data.NewContext();
        Assert.Equal(1, await check.Appointments.CountAsync());
    }

    [Fact]
    public async Task Book_StartTimeWithSeconds_IsStoredOnTheMinute()
    {
        using var data = new TestData();
        var withSeconds = Monday.AddHours(9).AddSeconds(45).AddMilliseconds(120);

        var result = await data.Appointments().BookAsync(data.Patient.UserId, data.Booking(withSeconds));

        Assert.Equal(Monday.AddHours(9), result.StartTime);
    }

    [Fact]
    public async Task Book_TrimsReason()
    {
        using var data = new TestData();
        var request = data.Booking(Monday.AddHours(9));
        request.Reason = "   Fever and cough   ";

        var result = await data.Appointments().BookAsync(data.Patient.UserId, request);

        Assert.Equal("Fever and cough", result.Reason);
    }

    [Fact]
    public async Task Book_SameSlotTwice_ThrowsConflict()
    {
        using var data = new TestData();
        var service = data.Appointments();
        var slot = Monday.AddHours(10);

        await service.BookAsync(data.Patient.UserId, data.Booking(slot));

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.BookAsync(data.OtherPatient.UserId, data.Booking(slot)));
        Assert.Contains("just booked", ex.Message);
    }

    [Fact]
    public async Task Book_SlotFreedByCancellation_CanBeBookedAgain()
    {
        using var data = new TestData();
        var service = data.Appointments();
        var slot = Monday.AddHours(10);

        var first = await service.BookAsync(data.Patient.UserId, data.Booking(slot));
        await service.CancelAsync(first.Id, data.Patient.UserId, Roles.Patient);

        var second = await service.BookAsync(data.OtherPatient.UserId, data.Booking(slot));

        Assert.Equal("Scheduled", second.Status);
    }

    [Fact]
    public async Task Book_PatientAlreadyBookedWithAnotherDoctorAtSameTime_ThrowsConflict()
    {
        using var data = new TestData();
        var cardiologist = await data.AddDoctorAsync("Dr. Heart", "heart@test.com", data.Cardiology);
        var service = data.Appointments();
        var slot = Monday.AddHours(11);

        await service.BookAsync(data.Patient.UserId, data.Booking(slot));

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.BookAsync(data.Patient.UserId, data.Booking(slot, cardiologist)));
        Assert.Contains("overlaps", ex.Message);
    }

    [Fact]
    public async Task Book_TwoPatientsDifferentSlotsSameDoctor_BothSucceed()
    {
        using var data = new TestData();
        var service = data.Appointments();

        await service.BookAsync(data.Patient.UserId, data.Booking(Monday.AddHours(9)));
        await service.BookAsync(data.OtherPatient.UserId, data.Booking(Monday.AddHours(9).AddMinutes(30)));

        Assert.Equal(2, await data.Db.Appointments.CountAsync());
    }

    [Fact]
    public async Task Book_PastTime_ThrowsBadRequest()
    {
        using var data = new TestData();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Appointments().BookAsync(data.Patient.UserId, data.Booking(Monday.AddDays(-7).AddHours(9))));
        Assert.Contains("future", ex.Message);
    }

    [Fact]
    public async Task Book_ExactlyNow_ThrowsBadRequest()
    {
        using var data = new TestData();
        data.SetNow(Monday.AddHours(9));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Appointments().BookAsync(data.Patient.UserId, data.Booking(Monday.AddHours(9))));
    }

    [Theory]
    [InlineData(8, 30)]  // before opening
    [InlineData(12, 0)]  // starts at closing time
    [InlineData(11, 45)] // would run past closing time
    [InlineData(9, 15)]  // not on a 30-minute boundary
    [InlineData(10, 10)] // not on a 30-minute boundary
    public async Task Book_InvalidTimeOfDay_ThrowsBadRequest(int hour, int minute)
    {
        using var data = new TestData();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Appointments().BookAsync(data.Patient.UserId, data.Booking(Monday.AddHours(hour).AddMinutes(minute))));
    }

    [Theory]
    [InlineData(5)] // Saturday
    [InlineData(6)] // Sunday
    public async Task Book_OnNonWorkingDay_ThrowsBadRequest(int daysAfterMonday)
    {
        using var data = new TestData();
        var start = Monday.AddDays(daysAfterMonday).AddHours(9);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Appointments().BookAsync(data.Patient.UserId, data.Booking(start)));
        Assert.Contains("doesn't work", ex.Message);
    }

    [Fact]
    public async Task Book_LastSlotOfTheDay_Succeeds()
    {
        using var data = new TestData();

        var result = await data.Appointments().BookAsync(data.Patient.UserId, data.Booking(Monday.AddHours(11).AddMinutes(30)));

        Assert.Equal(Monday.AddHours(12), result.EndTime);
    }

    [Fact]
    public async Task Book_WithInactiveDoctor_ThrowsNotFound()
    {
        using var data = new TestData();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            data.Appointments().BookAsync(data.Patient.UserId, data.Booking(Monday.AddHours(9), data.InactiveDoctor)));
    }

    [Fact]
    public async Task Book_WithUnknownDoctor_ThrowsNotFound()
    {
        using var data = new TestData();
        var request = data.Booking(Monday.AddHours(9));
        request.DoctorId = 9999;

        await Assert.ThrowsAsync<NotFoundException>(() => data.Appointments().BookAsync(data.Patient.UserId, request));
    }

    [Fact]
    public async Task Book_ByUserWithoutPatientProfile_ThrowsForbidden()
    {
        using var data = new TestData();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            data.Appointments().BookAsync(data.Doctor.UserId, data.Booking(Monday.AddHours(9))));
    }

    // ======================= Viewing (UC-05, UC-07, UC-12) =======================

    [Fact]
    public async Task GetForPatient_ReturnsOnlyOwnAppointments_NewestFirst()
    {
        using var data = new TestData();
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddDays(-7).AddHours(9), AppointmentStatus.Completed);
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(10));
        await data.AddAppointmentAsync(data.Doctor, data.OtherPatient, Monday.AddHours(11));

        var list = await data.Appointments().GetForPatientAsync(data.Patient.UserId);

        Assert.Equal(2, list.Count);
        Assert.All(list, a => Assert.Equal("Pat One", a.PatientName));
        Assert.True(list[0].StartTime > list[1].StartTime);
    }

    [Fact]
    public async Task GetForDoctor_WithDate_ReturnsOnlyThatDayInTimeOrder()
    {
        using var data = new TestData();
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(11));
        await data.AddAppointmentAsync(data.Doctor, data.OtherPatient, Monday.AddHours(9));
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddDays(1).AddHours(9));

        var list = await data.Appointments().GetForDoctorAsync(data.Doctor.UserId, DateOnly.FromDateTime(Monday));

        Assert.Equal(2, list.Count);
        Assert.Equal(Monday.AddHours(9), list[0].StartTime);
        Assert.Equal(Monday.AddHours(11), list[1].StartTime);
    }

    [Fact]
    public async Task GetForDoctor_WithoutDate_ReturnsAllOwnAppointments()
    {
        using var data = new TestData();
        var other = await data.AddDoctorAsync("Dr. Other", "other@test.com", data.Cardiology);
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(9));
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddDays(2).AddHours(9));
        await data.AddAppointmentAsync(other, data.Patient, Monday.AddHours(10));

        var list = await data.Appointments().GetForDoctorAsync(data.Doctor.UserId, null);

        Assert.Equal(2, list.Count);
        Assert.All(list, a => Assert.Equal(data.Doctor.Id, a.DoctorId));
    }

    [Fact]
    public async Task GetAll_FiltersByStatus_CaseInsensitive()
    {
        using var data = new TestData();
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(9), AppointmentStatus.Cancelled);
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(10));

        var list = await data.Appointments().GetAllAsync(new AppointmentFilter { Status = "cancelled" });

        var only = Assert.Single(list);
        Assert.Equal("Cancelled", only.Status);
    }

    [Fact]
    public async Task GetAll_FiltersByDoctorAndDate()
    {
        using var data = new TestData();
        var other = await data.AddDoctorAsync("Dr. Other", "other@test.com", data.Cardiology);
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(9));
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddDays(1).AddHours(9));
        await data.AddAppointmentAsync(other, data.OtherPatient, Monday.AddHours(9));

        var list = await data.Appointments().GetAllAsync(new AppointmentFilter
        {
            DoctorId = data.Doctor.Id,
            Date = DateOnly.FromDateTime(Monday)
        });

        var only = Assert.Single(list);
        Assert.Equal(Monday.AddHours(9), only.StartTime);
        Assert.Equal(data.Doctor.Id, only.DoctorId);
    }

    [Fact]
    public async Task GetAll_NoFilters_ReturnsEverything()
    {
        using var data = new TestData();
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(9));
        await data.AddAppointmentAsync(data.Doctor, data.OtherPatient, Monday.AddHours(10), AppointmentStatus.Completed);

        var list = await data.Appointments().GetAllAsync(new AppointmentFilter());

        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task GetAll_UnknownStatus_ThrowsBadRequest()
    {
        using var data = new TestData();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Appointments().GetAllAsync(new AppointmentFilter { Status = "Postponed" }));
    }

    // ======================= Access control =======================

    [Fact]
    public async Task GetById_OwnerPatient_DoctorAndAdmin_CanView()
    {
        using var data = new TestData();
        var appt = await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(9));
        var service = data.Appointments();

        Assert.Equal(appt.Id, (await service.GetByIdAsync(appt.Id, data.Patient.UserId, Roles.Patient)).Id);
        Assert.Equal(appt.Id, (await service.GetByIdAsync(appt.Id, data.Doctor.UserId, Roles.Doctor)).Id);
        Assert.Equal(appt.Id, (await service.GetByIdAsync(appt.Id, data.Admin.Id, Roles.Admin)).Id);
    }

    [Fact]
    public async Task GetById_AnotherPatient_ThrowsForbidden()
    {
        using var data = new TestData();
        var appt = await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(9));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            data.Appointments().GetByIdAsync(appt.Id, data.OtherPatient.UserId, Roles.Patient));
    }

    [Fact]
    public async Task GetById_AnotherDoctor_ThrowsForbidden()
    {
        using var data = new TestData();
        var appt = await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(9));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            data.Appointments().GetByIdAsync(appt.Id, data.InactiveDoctor.UserId, Roles.Doctor));
    }

    [Fact]
    public async Task GetById_UnknownRole_ThrowsForbidden()
    {
        using var data = new TestData();
        var appt = await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(9));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            data.Appointments().GetByIdAsync(appt.Id, data.Patient.UserId, "Receptionist"));
    }

    [Fact]
    public async Task GetById_Missing_ThrowsNotFound()
    {
        using var data = new TestData();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            data.Appointments().GetByIdAsync(404, data.Admin.Id, Roles.Admin));
    }

    // ======================= Cancelling (UC-06) =======================

    [Fact]
    public async Task Cancel_OwnUpcomingAppointment_IsSaved()
    {
        using var data = new TestData();
        var booked = await data.Appointments().BookAsync(data.Patient.UserId, data.Booking(Monday.AddHours(10)));

        var result = await data.Appointments().CancelAsync(booked.Id, data.Patient.UserId, Roles.Patient);

        Assert.Equal("Cancelled", result.Status);
        await using var check = data.NewContext();
        Assert.Equal(AppointmentStatus.Cancelled, (await check.Appointments.SingleAsync()).Status);
    }

    [Fact]
    public async Task Cancel_ByAnotherPatient_ThrowsForbidden()
    {
        using var data = new TestData();
        var booked = await data.Appointments().BookAsync(data.Patient.UserId, data.Booking(Monday.AddHours(9)));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            data.Appointments().CancelAsync(booked.Id, data.OtherPatient.UserId, Roles.Patient));
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_ThrowsBadRequest()
    {
        using var data = new TestData();
        var appt = await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(9), AppointmentStatus.Cancelled);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Appointments().CancelAsync(appt.Id, data.Patient.UserId, Roles.Patient));
    }

    [Fact]
    public async Task Cancel_CompletedAppointment_ThrowsBadRequest()
    {
        using var data = new TestData();
        var appt = await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddDays(-1).AddHours(9), AppointmentStatus.Completed);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Appointments().CancelAsync(appt.Id, data.Admin.Id, Roles.Admin));
    }

    [Fact]
    public async Task Cancel_PastAppointmentByPatient_ThrowsBadRequest()
    {
        using var data = new TestData();
        var appt = await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddDays(-3).AddHours(9));

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Appointments().CancelAsync(appt.Id, data.Patient.UserId, Roles.Patient));
        Assert.Contains("Past", ex.Message);
    }

    [Theory]
    [InlineData(Roles.Doctor)]
    [InlineData(Roles.Admin)]
    public async Task Cancel_PastAppointmentByStaff_IsAllowed(string role)
    {
        using var data = new TestData();
        var appt = await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddDays(-3).AddHours(9));
        var userId = role == Roles.Doctor ? data.Doctor.UserId : data.Admin.Id;

        var result = await data.Appointments().CancelAsync(appt.Id, userId, role);

        Assert.Equal("Cancelled", result.Status);
    }

    // ======================= Recording outcomes (UC-08) =======================

    [Fact]
    public async Task UpdateStatus_CompleteAfterStart_SavesStatusAndTrimmedNotes()
    {
        using var data = new TestData();
        var booked = await data.Appointments().BookAsync(data.Patient.UserId, data.Booking(Monday.AddHours(9)));
        data.SetNow(Monday.AddHours(9).AddMinutes(10));

        var result = await data.Appointments().UpdateStatusAsync(booked.Id, data.Doctor.UserId, Roles.Doctor,
            new UpdateAppointmentStatusRequest { Status = "Completed", Notes = "  Rest for 3 days.  " });

        Assert.Equal("Completed", result.Status);
        Assert.Equal("Rest for 3 days.", result.Notes);
    }

    [Fact]
    public async Task UpdateStatus_NoShowAfterStart_Succeeds()
    {
        using var data = new TestData();
        var booked = await data.Appointments().BookAsync(data.Patient.UserId, data.Booking(Monday.AddHours(9)));
        data.SetNow(Monday.AddHours(9).AddMinutes(20));

        var result = await data.Appointments().UpdateStatusAsync(booked.Id, data.Doctor.UserId, Roles.Doctor,
            new UpdateAppointmentStatusRequest { Status = "NoShow" });

        Assert.Equal("NoShow", result.Status);
    }

    [Fact]
    public async Task UpdateStatus_CancelFutureAppointmentAsDoctor_Succeeds()
    {
        using var data = new TestData();
        var booked = await data.Appointments().BookAsync(data.Patient.UserId, data.Booking(Monday.AddHours(11)));

        var result = await data.Appointments().UpdateStatusAsync(booked.Id, data.Doctor.UserId, Roles.Doctor,
            new UpdateAppointmentStatusRequest { Status = "Cancelled" });

        Assert.Equal("Cancelled", result.Status);
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("NoShow")]
    public async Task UpdateStatus_BeforeAppointmentStarts_ThrowsBadRequest(string status)
    {
        using var data = new TestData();
        var booked = await data.Appointments().BookAsync(data.Patient.UserId, data.Booking(Monday.AddHours(11)));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Appointments().UpdateStatusAsync(booked.Id, data.Doctor.UserId, Roles.Doctor,
                new UpdateAppointmentStatusRequest { Status = status }));
    }

    [Fact]
    public async Task UpdateStatus_ByPatient_ThrowsForbidden()
    {
        using var data = new TestData();
        var appt = await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(9));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            data.Appointments().UpdateStatusAsync(appt.Id, data.Patient.UserId, Roles.Patient,
                new UpdateAppointmentStatusRequest { Status = "Cancelled" }));
    }

    [Fact]
    public async Task UpdateStatus_ByAnotherDoctor_ThrowsForbidden()
    {
        using var data = new TestData();
        var appt = await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(9));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            data.Appointments().UpdateStatusAsync(appt.Id, data.InactiveDoctor.UserId, Roles.Doctor,
                new UpdateAppointmentStatusRequest { Status = "Cancelled" }));
    }

    [Theory]
    [InlineData("Scheduled")]
    [InlineData("Done")]
    [InlineData("")]
    public async Task UpdateStatus_InvalidTargetStatus_ThrowsBadRequest(string status)
    {
        using var data = new TestData();
        var appt = await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(9));

        await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Appointments().UpdateStatusAsync(appt.Id, data.Doctor.UserId, Roles.Doctor,
                new UpdateAppointmentStatusRequest { Status = status }));
    }

    [Fact]
    public async Task UpdateStatus_AlreadyCompleted_ThrowsBadRequest()
    {
        using var data = new TestData();
        var appt = await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddDays(-1).AddHours(9), AppointmentStatus.Completed);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Appointments().UpdateStatusAsync(appt.Id, data.Doctor.UserId, Roles.Doctor,
                new UpdateAppointmentStatusRequest { Status = "NoShow" }));
        Assert.Contains("already", ex.Message);
    }

    [Fact]
    public async Task UpdateStatus_MissingAppointment_ThrowsNotFound()
    {
        using var data = new TestData();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            data.Appointments().UpdateStatusAsync(404, data.Admin.Id, Roles.Admin,
                new UpdateAppointmentStatusRequest { Status = "Cancelled" }));
    }

    // ======================= Notifications =======================

    [Fact]
    public async Task Book_RaisesBookedEvent()
    {
        using var data = new TestData();

        var booked = await data.Appointments().BookAsync(data.Patient.UserId, data.Booking(Monday.AddHours(9)));

        var (name, dto) = Assert.Single(data.Events.Raised);
        Assert.Equal("Booked", name);
        Assert.Equal(booked.Id, dto!.Id);
    }

    [Fact]
    public async Task FailedBooking_RaisesNoEvent()
    {
        using var data = new TestData();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Appointments().BookAsync(data.Patient.UserId, data.Booking(Monday.AddHours(15))));

        Assert.Empty(data.Events.Raised);
    }

    [Fact]
    public async Task CancelAndStatusChange_RaiseMatchingEvents()
    {
        using var data = new TestData();
        var service = data.Appointments();
        var first = await service.BookAsync(data.Patient.UserId, data.Booking(Monday.AddHours(9)));
        var second = await service.BookAsync(data.OtherPatient.UserId, data.Booking(Monday.AddHours(10)));

        await service.CancelAsync(first.Id, data.Patient.UserId, Roles.Patient);
        data.SetNow(Monday.AddHours(10).AddMinutes(5));
        await service.UpdateStatusAsync(second.Id, data.Doctor.UserId, Roles.Doctor,
            new UpdateAppointmentStatusRequest { Status = "Completed" });

        Assert.Equal(new[] { "Booked", "Booked", "Cancelled", "StatusChanged" }, data.Events.Names);
    }

    [Fact]
    public async Task GetAll_NumericStatus_ThrowsBadRequest()
    {
        using var data = new TestData();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Appointments().GetAllAsync(new AppointmentFilter { Status = "42" }));
    }
}
