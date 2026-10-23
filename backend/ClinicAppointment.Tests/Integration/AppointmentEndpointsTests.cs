using System.Net;
using System.Net.Http.Json;
using ClinicAppointment.API.DTOs;

namespace ClinicAppointment.Tests.Integration;

/// <summary>
/// End-to-end booking flows over HTTP. Tests share one database, so each uses its own
/// time slot on Monday with the seeded doctor (09:00-13:00, 30-minute slots).
/// </summary>
public class AppointmentEndpointsTests : IClassFixture<ApiFactory>
{
    private static readonly DateTime Monday = ApiFactory.Monday;
    private readonly ApiFactory _factory;

    public AppointmentEndpointsTests(ApiFactory factory) => _factory = factory;

    private static async Task<HttpResponseMessage> BookAsync(HttpClient client, int doctorId, DateTime start) =>
        await client.PostAsJsonAsync("/api/appointments", new { doctorId, startTime = start, reason = "Integration test visit" });

    private static async Task<List<TimeSlotDto>> SlotsAsync(HttpClient client, int doctorId) =>
        (await client.GetFromJsonAsync<List<TimeSlotDto>>($"/api/doctors/{doctorId}/slots?date=2026-10-19"))!;

    [Fact]
    public async Task Slots_AreAvailableWithoutSigningIn()
    {
        var client = _factory.CreateClient();
        var doctor = await client.GetSeededDoctorAsync();

        var slots = await SlotsAsync(client, doctor.Id);

        Assert.Equal(8, slots.Count); // 09:00 to 13:00 in 30-minute steps
    }

    [Fact]
    public async Task Patient_BooksSlot_SlotThenShowsAsTaken()
    {
        var client = _factory.CreateClient();
        await client.RegisterNewPatientAsync();
        var doctor = await client.GetSeededDoctorAsync();
        var start = Monday.AddHours(9);

        var response = await BookAsync(client, doctor.Id, start);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var booked = await response.Content.ReadFromJsonAsync<AppointmentDto>();
        Assert.Equal("Scheduled", booked!.Status);

        var slots = await SlotsAsync(client, doctor.Id);
        Assert.False(slots.Single(s => s.StartTime == start).IsAvailable);

        var mine = await client.GetFromJsonAsync<List<AppointmentDto>>("/api/appointments/mine");
        Assert.Contains(mine!, a => a.Id == booked.Id);
    }

    [Fact]
    public async Task SecondPatient_BookingSameSlot_Returns409()
    {
        var first = _factory.CreateClient();
        await first.RegisterNewPatientAsync();
        var doctor = await first.GetSeededDoctorAsync();
        var start = Monday.AddHours(9).AddMinutes(30);
        (await BookAsync(first, doctor.Id, start)).EnsureSuccessStatusCode();

        var second = _factory.CreateClient();
        await second.RegisterNewPatientAsync();
        var response = await BookAsync(second, doctor.Id, start);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.Contains("just booked", error!.Message);
    }

    [Fact]
    public async Task Patient_CancelsBooking_SlotOpensAgain()
    {
        var client = _factory.CreateClient();
        await client.RegisterNewPatientAsync();
        var doctor = await client.GetSeededDoctorAsync();
        var start = Monday.AddHours(10);
        var booked = await (await BookAsync(client, doctor.Id, start)).Content.ReadFromJsonAsync<AppointmentDto>();

        var cancel = await client.PatchAsync($"/api/appointments/{booked!.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var slots = await SlotsAsync(client, doctor.Id);
        Assert.True(slots.Single(s => s.StartTime == start).IsAvailable);
    }

    [Fact]
    public async Task Doctor_SeesNewBookingInSchedule()
    {
        var patient = _factory.CreateClient();
        await patient.RegisterNewPatientAsync("Schedule Check");
        var doctorDto = await patient.GetSeededDoctorAsync();
        (await BookAsync(patient, doctorDto.Id, Monday.AddHours(10).AddMinutes(30))).EnsureSuccessStatusCode();

        var doctor = _factory.CreateClient();
        await doctor.LoginAsync(ApiClientExtensions.DoctorEmail, ApiClientExtensions.DoctorPassword);
        var schedule = await doctor.GetFromJsonAsync<List<AppointmentDto>>("/api/appointments/schedule?date=2026-10-19");

        Assert.Contains(schedule!, a => a.PatientName == "Schedule Check" && a.StartTime == Monday.AddHours(10).AddMinutes(30));
    }

    [Fact]
    public async Task Doctor_TryingToBook_Returns403()
    {
        var client = _factory.CreateClient();
        await client.LoginAsync(ApiClientExtensions.DoctorEmail, ApiClientExtensions.DoctorPassword);
        var doctor = await client.GetSeededDoctorAsync();

        var response = await BookAsync(client, doctor.Id, Monday.AddHours(12));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_TryingToBook_Returns401()
    {
        var client = _factory.CreateClient();
        var doctor = await client.GetSeededDoctorAsync();

        var response = await BookAsync(client, doctor.Id, Monday.AddHours(12));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Patient_ViewingAnotherPatientsAppointment_Returns403()
    {
        var owner = _factory.CreateClient();
        await owner.RegisterNewPatientAsync();
        var doctor = await owner.GetSeededDoctorAsync();
        var booked = await (await BookAsync(owner, doctor.Id, Monday.AddHours(11))).Content.ReadFromJsonAsync<AppointmentDto>();

        var stranger = _factory.CreateClient();
        await stranger.RegisterNewPatientAsync();
        var response = await stranger.GetAsync($"/api/appointments/{booked!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Booking_OutsideWorkingHours_Returns400()
    {
        var client = _factory.CreateClient();
        await client.RegisterNewPatientAsync();
        var doctor = await client.GetSeededDoctorAsync();

        var response = await BookAsync(client, doctor.Id, Monday.AddHours(15));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Patient_CallingDoctorSchedule_Returns403()
    {
        var client = _factory.CreateClient();
        await client.RegisterNewPatientAsync();

        var response = await client.GetAsync("/api/appointments/schedule");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
