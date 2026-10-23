using System.Net;
using System.Net.Http.Json;
using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Notifications;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace ClinicAppointment.Tests.Integration;

/// <summary>
/// Rescheduling, leave, reviews, emails and live updates through the real API.
/// The seeded doctor works Mon-Fri 09:00-13:00. Each test uses its own day or slot.
/// </summary>
public class FeatureEndpointsTests : IClassFixture<ApiFactory>
{
    private static readonly DateTime Monday = ApiFactory.Monday;
    private readonly ApiFactory _factory;

    public FeatureEndpointsTests(ApiFactory factory) => _factory = factory;

    private static async Task<AppointmentDto> BookAsync(HttpClient client, int doctorId, DateTime start)
    {
        var response = await client.PostAsJsonAsync("/api/appointments", new { doctorId, startTime = start, reason = "Feature test" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AppointmentDto>())!;
    }

    private HubConnection Hub(string? token = null)
    {
        _factory.CreateClient(); // make sure the test server is running
        return new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/clinic"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (token is not null)
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                }
            })
            .Build();
    }

    // ---------- UC-14 Reschedule ----------

    [Fact]
    public async Task Patient_ReschedulesBooking_OldSlotOpensAndNewSlotIsTaken()
    {
        var client = _factory.CreateClient();
        await client.RegisterNewPatientAsync();
        var doctor = await client.GetSeededDoctorAsync();
        var booked = await BookAsync(client, doctor.Id, Monday.AddDays(1).AddHours(9));

        var response = await client.PatchAsJsonAsync($"/api/appointments/{booked.Id}/reschedule",
            new { newStartTime = Monday.AddDays(1).AddHours(9).AddMinutes(30) });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var moved = await response.Content.ReadFromJsonAsync<AppointmentDto>();
        Assert.Equal(1, moved!.RescheduleCount);

        var slots = (await client.GetFromJsonAsync<List<TimeSlotDto>>($"/api/doctors/{doctor.Id}/slots?date=2026-10-20"))!;
        Assert.True(slots.Single(s => s.StartTime == Monday.AddDays(1).AddHours(9)).IsAvailable);
        Assert.False(slots.Single(s => s.StartTime == Monday.AddDays(1).AddHours(9).AddMinutes(30)).IsAvailable);
    }

    [Fact]
    public async Task Doctor_CannotReschedule_Returns403()
    {
        var patient = _factory.CreateClient();
        await patient.RegisterNewPatientAsync();
        var doctorDto = await patient.GetSeededDoctorAsync();
        var booked = await BookAsync(patient, doctorDto.Id, Monday.AddDays(1).AddHours(10));

        var doctor = _factory.CreateClient();
        await doctor.LoginAsync(ApiClientExtensions.DoctorEmail, ApiClientExtensions.DoctorPassword);
        var response = await doctor.PatchAsJsonAsync($"/api/appointments/{booked.Id}/reschedule",
            new { newStartTime = Monday.AddDays(1).AddHours(11) });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- UC-15 Leave ----------

    [Fact]
    public async Task Doctor_AddsLeave_DayDisappearsFromBooking()
    {
        var doctor = _factory.CreateClient();
        await doctor.LoginAsync(ApiClientExtensions.DoctorEmail, ApiClientExtensions.DoctorPassword);
        var doctorDto = await doctor.GetSeededDoctorAsync();

        var add = await doctor.PostAsJsonAsync("/api/doctors/me/leaves",
            new { startDate = "2026-10-21", endDate = "2026-10-21", reason = "Conference" });
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);

        var anonymous = _factory.CreateClient();
        var leaves = await anonymous.GetFromJsonAsync<List<LeaveDto>>($"/api/doctors/{doctorDto.Id}/leaves");
        Assert.Contains(leaves!, l => l.StartDate == new DateOnly(2026, 10, 21));

        var slots = await anonymous.GetFromJsonAsync<List<TimeSlotDto>>($"/api/doctors/{doctorDto.Id}/slots?date=2026-10-21");
        Assert.Empty(slots!);

        var patient = _factory.CreateClient();
        await patient.RegisterNewPatientAsync();
        var book = await patient.PostAsJsonAsync("/api/appointments",
            new { doctorId = doctorDto.Id, startTime = Monday.AddDays(2).AddHours(9), reason = "Should fail" });
        Assert.Equal(HttpStatusCode.BadRequest, book.StatusCode);
    }

    [Fact]
    public async Task Patient_CannotAddLeave_Returns403()
    {
        var client = _factory.CreateClient();
        await client.RegisterNewPatientAsync();

        var response = await client.PostAsJsonAsync("/api/doctors/me/leaves", new { startDate = "2026-10-23", endDate = "2026-10-23" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- UC-16 / UC-17 Reviews ----------

    [Fact]
    public async Task CompletedVisit_CanBeReviewedOnce_AndShowsOnDoctorProfile()
    {
        var patient = _factory.CreateClient();
        await patient.RegisterNewPatientAsync("Review Writer");
        var doctorDto = await patient.GetSeededDoctorAsync();
        var booked = await BookAsync(patient, doctorDto.Id, Monday.AddHours(9));

        _factory.Clock.SetUtcNow(new DateTimeOffset(Monday.AddHours(9).AddMinutes(15), TimeSpan.Zero));
        var doctor = _factory.CreateClient();
        await doctor.LoginAsync(ApiClientExtensions.DoctorEmail, ApiClientExtensions.DoctorPassword);
        (await doctor.PatchAsJsonAsync($"/api/appointments/{booked.Id}/status", new { status = "Completed" })).EnsureSuccessStatusCode();

        var review = await patient.PostAsJsonAsync($"/api/appointments/{booked.Id}/review", new { rating = 4, comment = "Helpful" });
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);

        var again = await patient.PostAsJsonAsync($"/api/appointments/{booked.Id}/review", new { rating = 1 });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var reviews = await _factory.CreateClient().GetFromJsonAsync<DoctorReviewsDto>($"/api/doctors/{doctorDto.Id}/reviews");
        Assert.Contains(reviews!.Reviews, r => r.Comment == "Helpful" && r.PatientName == "Review W.");
        Assert.True(reviews.ReviewCount >= 1);
    }

    [Fact]
    public async Task Review_WithInvalidRating_Returns400()
    {
        var client = _factory.CreateClient();
        await client.RegisterNewPatientAsync();

        var response = await client.PostAsJsonAsync("/api/appointments/1/review", new { rating = 9 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- UC-19 Emails ----------

    [Fact]
    public async Task Booking_SendsConfirmationEmail()
    {
        var client = _factory.CreateClient();
        var auth = await client.RegisterNewPatientAsync();
        var doctor = await client.GetSeededDoctorAsync();

        await BookAsync(client, doctor.Id, Monday.AddDays(3).AddHours(9));

        var email = await WaitForAsync(() =>
        {
            lock (_factory.Emails.Sent) return _factory.Emails.Sent.FirstOrDefault(m => m.ToAddress == auth.User.Email);
        });
        Assert.StartsWith("Appointment confirmed", email.Subject);
    }

    // ---------- UC-18 Live updates ----------

    [Fact]
    public async Task SlotWatcher_IsToldWhenSomeoneBooks()
    {
        var patient = _factory.CreateClient();
        await patient.RegisterNewPatientAsync();
        var doctor = await patient.GetSeededDoctorAsync();

        await using var hub = Hub();
        var received = new TaskCompletionSource<SlotsChangedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<SlotsChangedMessage>("SlotsChanged", m => received.TrySetResult(m));
        await hub.StartAsync();
        await hub.InvokeAsync("WatchDoctor", doctor.Id);

        await BookAsync(patient, doctor.Id, Monday.AddDays(3).AddHours(10));

        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(doctor.Id, message.DoctorId);
        Assert.Equal("2026-10-22", message.Date);
    }

    [Fact]
    public async Task Doctor_ReceivesScheduleChangeForNewBooking()
    {
        var doctorClient = _factory.CreateClient();
        var doctorAuth = await doctorClient.LoginAsync(ApiClientExtensions.DoctorEmail, ApiClientExtensions.DoctorPassword);

        await using var hub = Hub(doctorAuth.Token);
        var received = new TaskCompletionSource<ScheduleChangedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<ScheduleChangedMessage>("ScheduleChanged", m =>
        {
            if (m.Appointment.PatientName == "Live Update") received.TrySetResult(m);
        });
        await hub.StartAsync();

        var patient = _factory.CreateClient();
        await patient.RegisterNewPatientAsync("Live Update");
        var doctor = await patient.GetSeededDoctorAsync();
        await BookAsync(patient, doctor.Id, Monday.AddDays(3).AddHours(11));

        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("booked", message.Change);
    }

    private static async Task<T> WaitForAsync<T>(Func<T?> probe) where T : class
    {
        for (var i = 0; i < 100; i++)
        {
            var value = probe();
            if (value is not null) return value;
            await Task.Delay(50);
        }
        throw new TimeoutException("Condition not met within 5 seconds.");
    }
}
