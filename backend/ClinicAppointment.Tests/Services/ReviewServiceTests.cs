using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Exceptions;
using ClinicAppointment.API.Models;
using ClinicAppointment.API.Services;
using ClinicAppointment.Tests.Helpers;

namespace ClinicAppointment.Tests.Services;

/// <summary>Covers UC-16 (rate a visit) and UC-17 (read doctor reviews).</summary>
public class ReviewServiceTests
{
    private static readonly DateTime Monday = TestData.Monday;

    private static CreateReviewRequest Stars(int rating, string? comment = null) => new() { Rating = rating, Comment = comment };

    private static Task<Appointment> CompletedVisitAsync(TestData data, int daysAgo = 1, Patient? patient = null) =>
        data.AddAppointmentAsync(data.Doctor, patient ?? data.Patient, Monday.AddDays(-daysAgo).AddHours(9), AppointmentStatus.Completed);

    [Fact]
    public async Task Create_ForOwnCompletedVisit_SavesReview()
    {
        using var data = new TestData();
        var visit = await CompletedVisitAsync(data);

        var review = await data.Reviews().CreateAsync(data.Patient.UserId, visit.Id, Stars(4, "  Very thorough.  "));

        Assert.Equal(4, review.Rating);
        Assert.Equal("Very thorough.", review.Comment);
        Assert.Equal("Pat O.", review.PatientName);
        Assert.Equal(data.Doctor.Id, review.DoctorId);
    }

    [Fact]
    public async Task Create_ShowsOnTheAppointment()
    {
        using var data = new TestData();
        var visit = await CompletedVisitAsync(data);
        await data.Reviews().CreateAsync(data.Patient.UserId, visit.Id, Stars(5));

        var mine = await data.Appointments().GetForPatientAsync(data.Patient.UserId);

        Assert.Equal(5, mine.Single().Rating);
    }

    [Fact]
    public async Task Create_Twice_ThrowsConflict()
    {
        using var data = new TestData();
        var visit = await CompletedVisitAsync(data);
        await data.Reviews().CreateAsync(data.Patient.UserId, visit.Id, Stars(5));

        await Assert.ThrowsAsync<ConflictException>(() =>
            data.Reviews().CreateAsync(data.Patient.UserId, visit.Id, Stars(1)));
    }

    [Theory]
    [InlineData(AppointmentStatus.Scheduled)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public async Task Create_ForVisitNotCompleted_ThrowsBadRequest(AppointmentStatus status)
    {
        using var data = new TestData();
        var visit = await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddDays(-1).AddHours(9), status);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Reviews().CreateAsync(data.Patient.UserId, visit.Id, Stars(3)));
    }

    [Fact]
    public async Task Create_ForSomeoneElsesVisit_ThrowsForbidden()
    {
        using var data = new TestData();
        var visit = await CompletedVisitAsync(data);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            data.Reviews().CreateAsync(data.OtherPatient.UserId, visit.Id, Stars(3)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Create_RatingOutOfRange_ThrowsBadRequest(int rating)
    {
        using var data = new TestData();
        var visit = await CompletedVisitAsync(data);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Reviews().CreateAsync(data.Patient.UserId, visit.Id, Stars(rating)));
    }

    [Fact]
    public async Task Create_AfterReviewWindow_ThrowsBadRequest()
    {
        using var data = new TestData();
        var visit = await CompletedVisitAsync(data, daysAgo: ReviewService.ReviewWindowDays + 1);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            data.Reviews().CreateAsync(data.Patient.UserId, visit.Id, Stars(4)));
    }

    [Fact]
    public async Task Create_MissingAppointment_ThrowsNotFound()
    {
        using var data = new TestData();
        await Assert.ThrowsAsync<NotFoundException>(() => data.Reviews().CreateAsync(data.Patient.UserId, 404, Stars(4)));
    }

    [Fact]
    public async Task GetForDoctor_ReturnsAverageCountAndNewestFirst()
    {
        using var data = new TestData();
        var first = await CompletedVisitAsync(data, daysAgo: 3);
        var second = await CompletedVisitAsync(data, daysAgo: 2, patient: data.OtherPatient);
        await data.AddReviewAsync(first, 5, "Great");
        await Task.Delay(5); // distinct CreatedAt values
        await data.AddReviewAsync(second, 2, "Long wait");

        var result = await data.Reviews().GetForDoctorAsync(data.Doctor.Id);

        Assert.Equal(3.5, result.AverageRating);
        Assert.Equal(2, result.ReviewCount);
        Assert.Equal("Long wait", result.Reviews[0].Comment);
    }

    [Fact]
    public async Task GetForDoctor_Take_LimitsListButNotTheCount()
    {
        using var data = new TestData();
        await data.AddReviewAsync(await CompletedVisitAsync(data, 3), 5);
        await data.AddReviewAsync(await CompletedVisitAsync(data, 2), 3);

        var result = await data.Reviews().GetForDoctorAsync(data.Doctor.Id, take: 1);

        Assert.Single(result.Reviews);
        Assert.Equal(2, result.ReviewCount);
        Assert.Equal(4.0, result.AverageRating);
    }

    [Fact]
    public async Task GetForDoctor_NoReviews_HasNullAverage()
    {
        using var data = new TestData();

        var result = await data.Reviews().GetForDoctorAsync(data.Doctor.Id);

        Assert.Null(result.AverageRating);
        Assert.Equal(0, result.ReviewCount);
        Assert.Empty(result.Reviews);
    }

    [Fact]
    public async Task GetForDoctor_UnknownDoctor_ThrowsNotFound()
    {
        using var data = new TestData();
        await Assert.ThrowsAsync<NotFoundException>(() => data.Reviews().GetForDoctorAsync(999));
    }

    [Fact]
    public async Task DoctorProfile_IncludesRoundedAverageRating()
    {
        using var data = new TestData();
        await data.AddReviewAsync(await CompletedVisitAsync(data, 3), 5);
        await data.AddReviewAsync(await CompletedVisitAsync(data, 2), 4);
        await data.AddReviewAsync(await CompletedVisitAsync(data, 1), 4);

        var doctor = await data.Doctors().GetByIdAsync(data.Doctor.Id);
        var list = await data.Doctors().GetAllAsync(null, includeInactive: true);

        Assert.Equal(4.3, doctor.AverageRating);
        Assert.Equal(3, doctor.ReviewCount);
        Assert.Null(list.Single(d => d.FullName == "Dr. Retired").AverageRating);
    }

    [Theory]
    [InlineData("Rahul Verma", "Rahul V.")]
    [InlineData("Asha Devi Rao", "Asha R.")]
    [InlineData("Madonna", "Madonna")]
    [InlineData("  ", "Patient")]
    public void PublicName_HidesSurname(string fullName, string expected)
    {
        Assert.Equal(expected, ReviewService.PublicName(fullName));
    }
}
