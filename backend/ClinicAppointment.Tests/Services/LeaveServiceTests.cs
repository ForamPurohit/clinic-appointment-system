using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Exceptions;
using ClinicAppointment.API.Models;
using ClinicAppointment.API.Services;
using ClinicAppointment.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ClinicAppointment.Tests.Services;

/// <summary>Covers UC-15 (manage leave) and its effect on UC-03 and UC-04.</summary>
public class LeaveServiceTests
{
    private static readonly DateTime Monday = TestData.Monday;
    private static DateOnly Day(int offset) => DateOnly.FromDateTime(Monday.AddDays(offset));

    private static CreateLeaveRequest Leave(int fromOffset, int toOffset, string? reason = "Conference") =>
        new() { StartDate = Day(fromOffset), EndDate = Day(toOffset), Reason = reason };

    [Fact]
    public async Task Add_ValidPeriod_SavesAndRaisesEvent()
    {
        using var data = new TestData();

        var dto = await data.Leaves().AddAsync(data.Doctor.UserId, Leave(1, 3, "  Conference  "));

        Assert.Equal(Day(1), dto.StartDate);
        Assert.Equal(Day(3), dto.EndDate);
        Assert.Equal("Conference", dto.Reason);
        Assert.Single(data.Events.LeaveChanges);
        await using var check = data.NewContext();
        Assert.Equal(1, await check.DoctorLeaves.CountAsync());
    }

    [Fact]
    public async Task Add_SingleDay_IsAllowed()
    {
        using var data = new TestData();

        var dto = await data.Leaves().AddAsync(data.Doctor.UserId, Leave(2, 2, reason: null));

        Assert.Equal(dto.StartDate, dto.EndDate);
        Assert.Null(dto.Reason);
    }

    [Fact]
    public async Task Add_StartingToday_IsAllowed()
    {
        using var data = new TestData();
        var dto = await data.Leaves().AddAsync(data.Doctor.UserId, Leave(0, 0));
        Assert.Equal(Day(0), dto.StartDate);
    }

    [Fact]
    public async Task Add_EndBeforeStart_ThrowsBadRequest()
    {
        using var data = new TestData();
        await Assert.ThrowsAsync<BadRequestException>(() => data.Leaves().AddAsync(data.Doctor.UserId, Leave(3, 1)));
    }

    [Fact]
    public async Task Add_InThePast_ThrowsBadRequest()
    {
        using var data = new TestData();
        await Assert.ThrowsAsync<BadRequestException>(() => data.Leaves().AddAsync(data.Doctor.UserId, Leave(-2, 1)));
    }

    [Fact]
    public async Task Add_LongerThanLimit_ThrowsBadRequest()
    {
        using var data = new TestData();
        await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Leaves().AddAsync(data.Doctor.UserId, Leave(1, LeaveService.MaxLeaveDays + 1)));
    }

    [Fact]
    public async Task Add_OverlappingExistingLeave_ThrowsConflict()
    {
        using var data = new TestData();
        await data.AddLeaveAsync(data.Doctor, Monday.AddDays(2), Monday.AddDays(4));

        await Assert.ThrowsAsync<ConflictException>(() => data.Leaves().AddAsync(data.Doctor.UserId, Leave(4, 6)));
    }

    [Fact]
    public async Task Add_WhenAppointmentsAreBooked_ThrowsConflictWithCount()
    {
        using var data = new TestData();
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddDays(1).AddHours(9));
        await data.AddAppointmentAsync(data.Doctor, data.OtherPatient, Monday.AddDays(2).AddHours(9));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => data.Leaves().AddAsync(data.Doctor.UserId, Leave(1, 3)));

        Assert.Contains("2 scheduled appointments", ex.Message);
    }

    [Fact]
    public async Task Add_CancelledAppointmentsInPeriod_DoNotBlock()
    {
        using var data = new TestData();
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddDays(1).AddHours(9), AppointmentStatus.Cancelled);

        var dto = await data.Leaves().AddAsync(data.Doctor.UserId, Leave(1, 1));

        Assert.Equal(Day(1), dto.StartDate);
    }

    [Fact]
    public async Task Add_ByNonDoctor_ThrowsNotFound()
    {
        using var data = new TestData();
        await Assert.ThrowsAsync<NotFoundException>(() => data.Leaves().AddAsync(data.Patient.UserId, Leave(1, 1)));
    }

    [Fact]
    public async Task GetUpcoming_HidesFinishedLeave_SortedByStart()
    {
        using var data = new TestData();
        await data.AddLeaveAsync(data.Doctor, Monday.AddDays(-10), Monday.AddDays(-8));
        await data.AddLeaveAsync(data.Doctor, Monday.AddDays(10), Monday.AddDays(11));
        await data.AddLeaveAsync(data.Doctor, Monday.AddDays(-1), Monday.AddDays(1)); // ongoing

        var list = await data.Leaves().GetUpcomingAsync(data.Doctor.Id);

        Assert.Equal(new[] { Day(-1), Day(10) }, list.Select(l => l.StartDate));
    }

    [Fact]
    public async Task GetMine_ReturnsOnlyTheSignedInDoctorsLeave()
    {
        using var data = new TestData();
        await data.AddLeaveAsync(data.Doctor, Monday.AddDays(1), Monday.AddDays(1));
        await data.AddLeaveAsync(data.InactiveDoctor, Monday.AddDays(2), Monday.AddDays(2));

        var mine = await data.Leaves().GetMineAsync(data.Doctor.UserId);

        var only = Assert.Single(mine);
        Assert.Equal(data.Doctor.Id, only.DoctorId);
    }

    [Fact]
    public async Task Delete_OwnLeave_RemovesItAndRaisesEvent()
    {
        using var data = new TestData();
        var leave = await data.AddLeaveAsync(data.Doctor, Monday.AddDays(1), Monday.AddDays(1));

        await data.Leaves().DeleteAsync(data.Doctor.UserId, leave.Id);

        Assert.Empty(await data.NewContext().DoctorLeaves.ToListAsync());
        Assert.Single(data.Events.LeaveChanges);
    }

    [Fact]
    public async Task Delete_AnotherDoctorsLeave_ThrowsNotFound()
    {
        using var data = new TestData();
        var leave = await data.AddLeaveAsync(data.InactiveDoctor, Monday.AddDays(1), Monday.AddDays(1));

        await Assert.ThrowsAsync<NotFoundException>(() => data.Leaves().DeleteAsync(data.Doctor.UserId, leave.Id));
    }

    // ---------- effect on availability and booking ----------

    [Fact]
    public async Task Slots_OnLeaveDay_AreEmpty_AndOtherDaysUnaffected()
    {
        using var data = new TestData();
        await data.AddLeaveAsync(data.Doctor, Monday.AddDays(1), Monday.AddDays(1));

        Assert.Empty(await data.Doctors().GetSlotsAsync(data.Doctor.Id, Day(1)));
        Assert.Equal(6, (await data.Doctors().GetSlotsAsync(data.Doctor.Id, Day(2))).Count);
    }

    [Fact]
    public async Task Book_OnLeaveDay_ThrowsBadRequest()
    {
        using var data = new TestData();
        await data.AddLeaveAsync(data.Doctor, Monday.AddDays(1), Monday.AddDays(3));

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Appointments().BookAsync(data.Patient.UserId, data.Booking(Monday.AddDays(2).AddHours(9))));
        Assert.Contains("on leave", ex.Message);
    }
}
