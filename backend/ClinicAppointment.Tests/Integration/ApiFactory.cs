using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClinicAppointment.API.Data;
using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Notifications;
using ClinicAppointment.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace ClinicAppointment.Tests.Integration;

/// <summary>
/// Boots the real API in memory: same controllers, middleware, JWT auth and seeding,
/// but with an in-memory database and a clock fixed at Monday 19 Oct 2026, 08:00.
/// Each test class gets its own database.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public static readonly DateTime Monday = new(2026, 10, 19);

    private readonly string _databaseName = $"api-tests-{Guid.NewGuid()}";

    public FakeTimeProvider Clock { get; }

    /// <summary>Emails the API sent, instead of writing .eml files.</summary>
    public RecordingEmailSender Emails { get; } = new();

    public ApiFactory()
    {
        Clock = new FakeTimeProvider(new DateTimeOffset(Monday.AddHours(8), TimeSpan.Zero));
        Clock.SetLocalTimeZone(TimeZoneInfo.Utc);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }
}

public record ApiError(int Status, string Message);

public static class ApiClientExtensions
{
    public const string PatientEmail = "patient@clinic.com";
    public const string PatientPassword = "Patient@123";
    public const string DoctorEmail = "doctor@clinic.com";
    public const string DoctorPassword = "Doctor@123";
    public const string AdminEmail = "admin@clinic.com";
    public const string AdminPassword = "Admin@123";

    public static async Task<AuthResponse> LoginAsync(this HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return auth;
    }

    /// <summary>Registers a brand-new patient and signs the client in as them.</summary>
    public static async Task<AuthResponse> RegisterNewPatientAsync(this HttpClient client, string? name = null)
    {
        var email = $"patient-{Guid.NewGuid():N}@test.com";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = name ?? "Test Patient",
            email,
            password = "Patient@123"
        });
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return auth;
    }

    public static async Task<DoctorDto> GetSeededDoctorAsync(this HttpClient client)
    {
        var doctors = await client.GetFromJsonAsync<List<DoctorDto>>("/api/doctors");
        return doctors!.Single(d => d.Email == DoctorEmail);
    }
}
