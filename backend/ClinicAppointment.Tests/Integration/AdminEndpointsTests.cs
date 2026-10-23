using System.Net;
using System.Net.Http.Json;
using ClinicAppointment.API.DTOs;

namespace ClinicAppointment.Tests.Integration;

public class AdminEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdminEndpointsTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Anonymous_GetSpecializations_ReturnsSeededList()
    {
        var list = await _factory.CreateClient().GetFromJsonAsync<List<SpecializationDto>>("/api/specializations");

        Assert.Equal(5, list!.Count);
        Assert.Contains(list, s => s.Name == "Cardiology");
    }

    [Fact]
    public async Task Admin_GetsStats()
    {
        var client = _factory.CreateClient();
        await client.LoginAsync(ApiClientExtensions.AdminEmail, ApiClientExtensions.AdminPassword);

        var stats = await client.GetFromJsonAsync<DashboardStatsDto>("/api/admin/stats");

        Assert.True(stats!.TotalDoctors >= 2); // other tests in this class may add doctors
        Assert.True(stats.TotalPatients >= 1);
    }

    [Theory]
    [InlineData(ApiClientExtensions.PatientEmail, ApiClientExtensions.PatientPassword)]
    [InlineData(ApiClientExtensions.DoctorEmail, ApiClientExtensions.DoctorPassword)]
    public async Task NonAdmin_CallingAdminEndpoints_Returns403(string email, string password)
    {
        var client = _factory.CreateClient();
        await client.LoginAsync(email, password);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/stats")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/patients")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/appointments")).StatusCode);
    }

    [Fact]
    public async Task Admin_CreatesDoctor_WhoCanThenSignIn()
    {
        var admin = _factory.CreateClient();
        await admin.LoginAsync(ApiClientExtensions.AdminEmail, ApiClientExtensions.AdminPassword);
        var specialty = (await admin.GetFromJsonAsync<List<SpecializationDto>>("/api/specializations"))!
            .Single(s => s.Name == "Dermatology");

        var response = await admin.PostAsJsonAsync("/api/doctors", new
        {
            fullName = "Dr. Skin Deep",
            email = "skin@clinic.com",
            password = "Doctor@123",
            specializationId = specialty.Id,
            yearsOfExperience = 4,
            consultationFee = 800,
            workStartTime = "10:00",
            workEndTime = "16:00",
            slotDurationMinutes = 30,
            workingDays = new[] { 2, 4 }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var doctor = _factory.CreateClient();
        var auth = await doctor.LoginAsync("skin@clinic.com", "Doctor@123");
        Assert.Equal("Doctor", auth.User.Role);
    }

    [Fact]
    public async Task Admin_CreateDoctorWithBadTimeFormat_Returns400()
    {
        var admin = _factory.CreateClient();
        await admin.LoginAsync(ApiClientExtensions.AdminEmail, ApiClientExtensions.AdminPassword);

        var response = await admin.PostAsJsonAsync("/api/doctors", new
        {
            fullName = "Dr. Bad Time",
            email = "badtime@clinic.com",
            password = "Doctor@123",
            specializationId = 1,
            workStartTime = "9am",
            workEndTime = "17:00",
            slotDurationMinutes = 30,
            workingDays = new[] { 1 }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("Use HH:mm format.", error!.Message);
    }

    [Fact]
    public async Task Admin_DeleteSpecialtyWithDoctors_Returns400()
    {
        var admin = _factory.CreateClient();
        await admin.LoginAsync(ApiClientExtensions.AdminEmail, ApiClientExtensions.AdminPassword);
        var cardiology = (await admin.GetFromJsonAsync<List<SpecializationDto>>("/api/specializations"))!
            .Single(s => s.Name == "Cardiology");

        var response = await admin.DeleteAsync($"/api/specializations/{cardiology.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
