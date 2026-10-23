using System.Net;
using System.Net.Http.Json;
using ClinicAppointment.API.DTOs;

namespace ClinicAppointment.Tests.Integration;

public class AuthEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthEndpointsTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_ThenMe_ReturnsTheNewPatient()
    {
        var client = _factory.CreateClient();
        var auth = await client.RegisterNewPatientAsync("Asha Rao");

        var me = await client.GetFromJsonAsync<UserDto>("/api/auth/me");

        Assert.Equal("Asha Rao", me!.FullName);
        Assert.Equal("Patient", me.Role);
        Assert.Equal(auth.User.Id, me.Id);
    }

    [Fact]
    public async Task Register_InvalidBody_Returns400WithReadableMessage()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "A", email = "not-an-email", password = "123" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal(400, error!.Status);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    [Fact]
    public async Task Register_SeededEmail_Returns409()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "Copy Cat", email = ApiClientExtensions.PatientEmail, password = "Patient@123" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData(ApiClientExtensions.AdminEmail, ApiClientExtensions.AdminPassword, "Admin")]
    [InlineData(ApiClientExtensions.DoctorEmail, ApiClientExtensions.DoctorPassword, "Doctor")]
    [InlineData(ApiClientExtensions.PatientEmail, ApiClientExtensions.PatientPassword, "Patient")]
    public async Task Login_SeededAccounts_ReturnTokenWithRole(string email, string password, string role)
    {
        var client = _factory.CreateClient();

        var auth = await client.LoginAsync(email, password);

        Assert.Equal(role, auth.User.Role);
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401WithMessage()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = ApiClientExtensions.PatientEmail, password = "nope" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("Email or password is incorrect.", error!.Message);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithTamperedToken_Returns401()
    {
        var client = _factory.CreateClient();
        var auth = await client.LoginAsync(ApiClientExtensions.PatientEmail, ApiClientExtensions.PatientPassword);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Token[..^4] + "abcd");

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
