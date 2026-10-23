using ClinicAppointment.API.Models;
using ClinicAppointment.Tests.Helpers;

namespace ClinicAppointment.Tests.Services;

/// <summary>Covers UC-09 (dashboard) and UC-13 (view patients).</summary>
public class AdminServiceTests
{
    private static readonly DateTime Monday = TestData.Monday;

    [Fact]
    public async Task GetStats_CountsEachCategoryCorrectly()
    {
        using var data = new TestData(); // now = Monday 08:00
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(9));                                       // today, upcoming
        await data.AddAppointmentAsync(data.Doctor, data.OtherPatient, Monday.AddHours(9).AddMinutes(30), AppointmentStatus.Cancelled); // today, cancelled
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddDays(1).AddHours(10));                           // upcoming
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddDays(-7).AddHours(9), AppointmentStatus.Completed); // past

        var stats = await data.AdminService().GetStatsAsync();

        Assert.Equal(1, stats.TotalDoctors);           // inactive doctor excluded
        Assert.Equal(2, stats.TotalPatients);
        Assert.Equal(4, stats.TotalAppointments);
        Assert.Equal(1, stats.TodayAppointments);      // cancelled one excluded
        Assert.Equal(2, stats.UpcomingAppointments);
        Assert.Equal(1, stats.CompletedAppointments);
        Assert.Equal(1, stats.CancelledAppointments);
    }

    [Fact]
    public async Task GetStats_EmptyClinic_ReturnsZeros()
    {
        using var data = new TestData();

        var stats = await data.AdminService().GetStatsAsync();

        Assert.Equal(0, stats.TotalAppointments);
        Assert.Equal(0, stats.TodayAppointments);
        Assert.Equal(0, stats.UpcomingAppointments);
    }

    [Fact]
    public async Task GetPatients_NoSearch_ReturnsAllSortedWithVisitCounts()
    {
        using var data = new TestData();
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(9));
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(10));

        var list = await data.AdminService().GetPatientsAsync(null);

        Assert.Equal(new[] { "Pat One", "Pat Two" }, list.Select(p => p.FullName));
        Assert.Equal(2, list[0].AppointmentCount);
        Assert.Equal(0, list[1].AppointmentCount);
    }

    [Theory]
    [InlineData("Pat Two")]
    [InlineData("p2@test")]
    public async Task GetPatients_Search_MatchesNameOrEmail(string term)
    {
        using var data = new TestData();

        var list = await data.AdminService().GetPatientsAsync(term);

        var only = Assert.Single(list);
        Assert.Equal("p2@test.com", only.Email);
    }

    [Fact]
    public async Task GetPatients_SearchWithNoMatch_ReturnsEmpty()
    {
        using var data = new TestData();

        Assert.Empty(await data.AdminService().GetPatientsAsync("zzz"));
    }
}
