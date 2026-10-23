using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Exceptions;
using ClinicAppointment.API.Models;
using ClinicAppointment.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ClinicAppointment.Tests.Services;

/// <summary>Covers UC-03 (find doctor and open times) and UC-10 (manage doctors).</summary>
public class DoctorServiceTests
{
    private static readonly DateTime Monday = TestData.Monday;
    private static DateOnly Day(int offset = 0) => DateOnly.FromDateTime(Monday.AddDays(offset));

    // ======================= Listing =======================

    [Fact]
    public async Task GetAll_ByDefault_ExcludesInactiveDoctors()
    {
        using var data = new TestData();

        var list = await data.Doctors().GetAllAsync(null, includeInactive: false);

        var only = Assert.Single(list);
        Assert.Equal("Dr. Active", only.FullName);
    }

    [Fact]
    public async Task GetAll_IncludeInactive_ReturnsAllSortedByName()
    {
        using var data = new TestData();

        var list = await data.Doctors().GetAllAsync(null, includeInactive: true);

        Assert.Equal(new[] { "Dr. Active", "Dr. Retired" }, list.Select(d => d.FullName));
    }

    [Fact]
    public async Task GetAll_FilteredBySpecialization_ReturnsOnlyThatSpecialty()
    {
        using var data = new TestData();
        await data.AddDoctorAsync("Dr. Heart", "heart@test.com", data.Cardiology);

        var list = await data.Doctors().GetAllAsync(data.Cardiology.Id, includeInactive: false);

        var only = Assert.Single(list);
        Assert.Equal("Cardiology", only.SpecializationName);
    }

    [Fact]
    public async Task GetById_MapsProfileFields()
    {
        using var data = new TestData();

        var dto = await data.Doctors().GetByIdAsync(data.Doctor.Id);

        Assert.Equal("09:00", dto.WorkStartTime);
        Assert.Equal("12:00", dto.WorkEndTime);
        Assert.Equal(30, dto.SlotDurationMinutes);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, dto.WorkingDays);
        Assert.Equal("General Medicine", dto.SpecializationName);
        Assert.Equal("dr@test.com", dto.Email);
    }

    [Fact]
    public async Task GetById_Missing_ThrowsNotFound()
    {
        using var data = new TestData();
        await Assert.ThrowsAsync<NotFoundException>(() => data.Doctors().GetByIdAsync(999));
    }

    [Fact]
    public async Task GetByUserId_ReturnsSignedInDoctorsProfile()
    {
        using var data = new TestData();

        var dto = await data.Doctors().GetByUserIdAsync(data.Doctor.UserId);

        Assert.Equal(data.Doctor.Id, dto.Id);
    }

    [Fact]
    public async Task GetByUserId_ForPatientUser_ThrowsNotFound()
    {
        using var data = new TestData();
        await Assert.ThrowsAsync<NotFoundException>(() => data.Doctors().GetByUserIdAsync(data.Patient.UserId));
    }

    // ======================= Creating (UC-10) =======================

    [Fact]
    public async Task Create_ValidRequest_CreatesDoctorWithLoginAccount()
    {
        using var data = new TestData();

        var dto = await data.Doctors().CreateAsync(TestData.NewDoctorRequest(data.Cardiology.Id));

        Assert.Equal("new.hire@clinic.com", dto.Email);
        Assert.Equal("Cardiology", dto.SpecializationName);
        Assert.Equal("10:00", dto.WorkStartTime);
        Assert.Equal(new[] { 1, 3, 5 }, dto.WorkingDays);
        Assert.True(dto.IsActive);

        await using var check = data.NewContext();
        var user = await check.Users.SingleAsync(u => u.Email == "new.hire@clinic.com");
        Assert.Equal(Roles.Doctor, user.Role);
        Assert.NotEqual("Doctor@123", user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("Doctor@123", user.PasswordHash));
    }

    [Fact]
    public async Task Create_EmailAlreadyUsed_ThrowsConflict()
    {
        using var data = new TestData();
        var request = TestData.NewDoctorRequest(data.Cardiology.Id);
        request.Email = "P1@TEST.COM"; // existing patient, different case

        await Assert.ThrowsAsync<ConflictException>(() => data.Doctors().CreateAsync(request));
    }

    [Fact]
    public async Task Create_UnknownSpecialization_ThrowsBadRequest()
    {
        using var data = new TestData();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Doctors().CreateAsync(TestData.NewDoctorRequest(specializationId: 999)));
    }

    [Theory]
    [InlineData("17:00", "09:00", 30)] // ends before it starts
    [InlineData("09:00", "09:00", 30)] // zero-length day
    [InlineData("09:00", "09:20", 30)] // shorter than one slot
    public async Task Create_InvalidWorkingHours_ThrowsBadRequest(string start, string end, int slot)
    {
        using var data = new TestData();
        var request = TestData.NewDoctorRequest(data.Cardiology.Id);
        request.WorkStartTime = start;
        request.WorkEndTime = end;
        request.SlotDurationMinutes = slot;

        await Assert.ThrowsAsync<BadRequestException>(() => data.Doctors().CreateAsync(request));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(-1)]
    public async Task Create_InvalidWorkingDay_ThrowsBadRequest(int day)
    {
        using var data = new TestData();
        var request = TestData.NewDoctorRequest(data.Cardiology.Id);
        request.WorkingDays = new List<int> { 1, day };

        await Assert.ThrowsAsync<BadRequestException>(() => data.Doctors().CreateAsync(request));
    }

    [Fact]
    public async Task Create_DuplicateWorkingDays_AreDeduplicatedAndSorted()
    {
        using var data = new TestData();
        var request = TestData.NewDoctorRequest(data.Cardiology.Id);
        request.WorkingDays = new List<int> { 5, 1, 1, 3, 5 };

        var dto = await data.Doctors().CreateAsync(request);

        Assert.Equal(new[] { 1, 3, 5 }, dto.WorkingDays);
    }

    // ======================= Updating (UC-10) =======================

    [Fact]
    public async Task Update_ChangesProfileAndCanDeactivate()
    {
        using var data = new TestData();
        var request = new UpdateDoctorRequest
        {
            FullName = "Dr. Active Senior",
            SpecializationId = data.Cardiology.Id,
            YearsOfExperience = 12,
            ConsultationFee = 900,
            WorkStartTime = "13:00",
            WorkEndTime = "17:00",
            SlotDurationMinutes = 15,
            WorkingDays = new List<int> { 2, 4 },
            IsActive = false
        };

        var dto = await data.Doctors().UpdateAsync(data.Doctor.Id, request);

        Assert.Equal("Dr. Active Senior", dto.FullName);
        Assert.Equal("Cardiology", dto.SpecializationName);
        Assert.Equal("13:00", dto.WorkStartTime);
        Assert.Equal(15, dto.SlotDurationMinutes);
        Assert.Equal(new[] { 2, 4 }, dto.WorkingDays);
        Assert.False(dto.IsActive);
    }

    [Fact]
    public async Task Update_MissingDoctor_ThrowsNotFound()
    {
        using var data = new TestData();
        var request = new UpdateDoctorRequest { FullName = "X Y", SpecializationId = data.Cardiology.Id };

        await Assert.ThrowsAsync<NotFoundException>(() => data.Doctors().UpdateAsync(999, request));
    }

    // ======================= Time slots (UC-03) =======================

    [Fact]
    public async Task GetSlots_WorkingDay_ReturnsEveryInterval()
    {
        using var data = new TestData();

        var slots = await data.Doctors().GetSlotsAsync(data.Doctor.Id, Day());

        // 09:00 to 12:00 in 30-minute steps = 6 slots
        Assert.Equal(6, slots.Count);
        Assert.Equal(Monday.AddHours(9), slots[0].StartTime);
        Assert.Equal(Monday.AddHours(11).AddMinutes(30), slots[^1].StartTime);
        Assert.Equal(Monday.AddHours(12), slots[^1].EndTime);
        Assert.All(slots, s => Assert.True(s.IsAvailable));
    }

    [Fact]
    public async Task GetSlots_BookedSlot_IsUnavailable()
    {
        using var data = new TestData();
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(9).AddMinutes(30));

        var slots = await data.Doctors().GetSlotsAsync(data.Doctor.Id, Day());

        Assert.False(slots.Single(s => s.StartTime == Monday.AddHours(9).AddMinutes(30)).IsAvailable);
        Assert.Equal(5, slots.Count(s => s.IsAvailable));
    }

    [Fact]
    public async Task GetSlots_CancelledAppointment_DoesNotBlockSlot()
    {
        using var data = new TestData();
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(10), AppointmentStatus.Cancelled);

        var slots = await data.Doctors().GetSlotsAsync(data.Doctor.Id, Day());

        Assert.True(slots.Single(s => s.StartTime == Monday.AddHours(10)).IsAvailable);
    }

    [Fact]
    public async Task GetSlots_OtherDoctorsBookings_DoNotBlockSlot()
    {
        using var data = new TestData();
        var other = await data.AddDoctorAsync("Dr. Other", "other@test.com", data.Cardiology);
        await data.AddAppointmentAsync(other, data.Patient, Monday.AddHours(10));

        var slots = await data.Doctors().GetSlotsAsync(data.Doctor.Id, Day());

        Assert.All(slots, s => Assert.True(s.IsAvailable));
    }

    [Fact]
    public async Task GetSlots_Today_TimesAlreadyPassedAreUnavailable()
    {
        using var data = new TestData();
        data.SetNow(Monday.AddHours(10).AddMinutes(5));

        var slots = await data.Doctors().GetSlotsAsync(data.Doctor.Id, Day());

        // 09:00, 09:30, 10:00 have passed; 10:30, 11:00, 11:30 remain
        Assert.Equal(3, slots.Count(s => s.IsAvailable));
        Assert.Equal(Monday.AddHours(10).AddMinutes(30), slots.First(s => s.IsAvailable).StartTime);
    }

    [Fact]
    public async Task GetSlots_PastDate_AllUnavailable()
    {
        using var data = new TestData();

        var slots = await data.Doctors().GetSlotsAsync(data.Doctor.Id, Day(-7));

        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.False(s.IsAvailable));
    }

    [Theory]
    [InlineData(5)] // Saturday
    [InlineData(6)] // Sunday
    public async Task GetSlots_NonWorkingDay_ReturnsEmpty(int offset)
    {
        using var data = new TestData();

        var slots = await data.Doctors().GetSlotsAsync(data.Doctor.Id, Day(offset));

        Assert.Empty(slots);
    }

    [Fact]
    public async Task GetSlots_RespectsCustomSlotLength()
    {
        using var data = new TestData();
        var created = await data.Doctors().CreateAsync(TestData.NewDoctorRequest(data.Cardiology.Id)); // 10:00-14:00, 20 min, Mon/Wed/Fri

        var slots = await data.Doctors().GetSlotsAsync(created.Id, Day());

        Assert.Equal(12, slots.Count);
        Assert.Equal(TimeSpan.FromMinutes(20), slots[0].EndTime - slots[0].StartTime);
    }

    [Fact]
    public async Task GetSlots_InactiveDoctor_ThrowsNotFound()
    {
        using var data = new TestData();

        await Assert.ThrowsAsync<NotFoundException>(() => data.Doctors().GetSlotsAsync(data.InactiveDoctor.Id, Day()));
    }
}
