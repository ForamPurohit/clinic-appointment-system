using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Hubs;
using ClinicAppointment.API.Models;
using ClinicAppointment.API.Notifications;
using ClinicAppointment.API.Settings;
using ClinicAppointment.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ClinicAppointment.Tests.Notifications;

/// <summary>Covers UC-18 (real-time updates) and the emails in UC-19.</summary>
public class AppointmentEventsTests
{
    private static readonly DateTime Monday = TestData.Monday;

    private sealed class Setup : IDisposable
    {
        public TestData Data { get; } = new();
        public FakeHubContext<ClinicHub> Hub { get; } = new();
        public RecordingEmailQueue Emails { get; } = new();
        public AppointmentEvents Events { get; }

        public Setup()
        {
            Events = new AppointmentEvents(Hub, Emails, Data.Db,
                Options.Create(new EmailSettings { ClientUrl = "https://clinic.test" }),
                NullLogger<AppointmentEvents>.Instance);
        }

        public AppointmentDto Appointment(DateTime start, string status = "Scheduled") => new(
            1, Data.Doctor.Id, "Dr. Active", "General Medicine", Data.Patient.Id, "Pat One", null,
            start, start.AddMinutes(30), "Fever <script>", null, status, DateTime.UtcNow, 0, null);

        public void Dispose() => Data.Dispose();
    }

    [Fact]
    public async Task Booked_NotifiesSlotWatchersAndDoctor_AndEmailsPatient()
    {
        using var s = new Setup();
        var a = s.Appointment(Monday.AddHours(9));

        await s.Events.BookedAsync(a);

        Assert.Contains(s.Hub.Sent, m => m.Group == ClinicHub.SlotsGroup(s.Data.Doctor.Id)
                                         && m.Method == ClinicHub.SlotsChanged
                                         && ((SlotsChangedMessage)m.Payload!).Date == "2026-10-19");
        Assert.Contains(s.Hub.Sent, m => m.Group == ClinicHub.ScheduleGroup(s.Data.Doctor.Id)
                                         && ((ScheduleChangedMessage)m.Payload!).Change == "booked");

        var email = Assert.Single(s.Emails.Messages);
        Assert.Equal("p1@test.com", email.ToAddress);
        Assert.StartsWith("Appointment confirmed", email.Subject);
        Assert.Contains("https://clinic.test/patient/appointments", email.HtmlBody);
    }

    [Fact]
    public async Task Rescheduled_ToAnotherDay_RefreshesBothDays()
    {
        using var s = new Setup();
        var a = s.Appointment(Monday.AddDays(2).AddHours(9));

        await s.Events.RescheduledAsync(a, oldStart: Monday.AddHours(9));

        var dates = s.Hub.Sent.Where(m => m.Method == ClinicHub.SlotsChanged)
            .Select(m => ((SlotsChangedMessage)m.Payload!).Date).ToList();
        Assert.Equal(new[] { "2026-10-19", "2026-10-21" }, dates);
        Assert.StartsWith("Appointment moved", Assert.Single(s.Emails.Messages).Subject);
    }

    [Fact]
    public async Task Rescheduled_SameDay_RefreshesThatDayOnce()
    {
        using var s = new Setup();

        await s.Events.RescheduledAsync(s.Appointment(Monday.AddHours(11)), oldStart: Monday.AddHours(9));

        Assert.Single(s.Hub.Sent, m => m.Method == ClinicHub.SlotsChanged);
    }

    [Fact]
    public async Task StatusChanged_ToCompleted_AsksForReview()
    {
        using var s = new Setup();

        await s.Events.StatusChangedAsync(s.Appointment(Monday.AddHours(9), "Completed"));

        Assert.StartsWith("How was your visit", Assert.Single(s.Emails.Messages).Subject);
        Assert.DoesNotContain(s.Hub.Sent, m => m.Method == ClinicHub.SlotsChanged);
    }

    [Fact]
    public async Task StatusChanged_ToCancelled_ActsLikeACancellation()
    {
        using var s = new Setup();

        await s.Events.StatusChangedAsync(s.Appointment(Monday.AddHours(9), "Cancelled"));

        Assert.Contains(s.Hub.Sent, m => m.Method == ClinicHub.SlotsChanged);
        Assert.StartsWith("Appointment cancelled", Assert.Single(s.Emails.Messages).Subject);
    }

    [Fact]
    public async Task StatusChanged_ToNoShow_SendsNoEmail()
    {
        using var s = new Setup();

        await s.Events.StatusChangedAsync(s.Appointment(Monday.AddHours(9), "NoShow"));

        Assert.Empty(s.Emails.Messages);
        Assert.Single(s.Hub.Sent);
    }

    [Fact]
    public async Task LeaveChanged_RefreshesEveryDayInThePeriod()
    {
        using var s = new Setup();

        await s.Events.LeaveChangedAsync(s.Data.Doctor.Id, DateOnly.FromDateTime(Monday), DateOnly.FromDateTime(Monday.AddDays(2)));

        Assert.Equal(3, s.Hub.Sent.Count(m => m.Method == ClinicHub.SlotsChanged));
    }

    [Fact]
    public async Task HubFailure_IsSwallowed_AndEmailStillQueued()
    {
        using var s = new Setup();
        s.Hub.ThrowOnSend = true;

        await s.Events.BookedAsync(s.Appointment(Monday.AddHours(9)));

        Assert.Single(s.Emails.Messages);
    }
}

public class EmailTemplatesTests
{
    private static AppointmentDto Sample(string reason = "Check-up") => new(
        1, 2, "Dr. Priya Sharma", "General Medicine", 3, "Rahul Verma", null,
        new DateTime(2026, 10, 20, 9, 30, 0), new DateTime(2026, 10, 20, 10, 0, 0),
        reason, null, "Scheduled", DateTime.UtcNow, 0, null);

    [Fact]
    public void Booked_ContainsTimeDoctorAndLink()
    {
        var (subject, html) = EmailTemplates.Booked(Sample(), "Rahul Verma", "http://localhost:4200");

        Assert.Equal("Appointment confirmed: Tue 20 Oct 2026, 9:30 AM", subject);
        Assert.Contains("Dr. Priya Sharma", html);
        Assert.Contains("Hello Rahul Verma", html);
        Assert.Contains("http://localhost:4200/patient/appointments", html);
    }

    [Fact]
    public void UserText_IsHtmlEncoded()
    {
        var (_, html) = EmailTemplates.Reminder(Sample("<img src=x onerror=alert(1)>"), "<b>Eve</b>", "http://x");

        Assert.DoesNotContain("<img src=x", html);
        Assert.Contains("&lt;img src=x", html);
        Assert.Contains("&lt;b&gt;Eve&lt;/b&gt;", html);
    }

    [Fact]
    public void Rescheduled_ShowsOldTimeStruckThrough()
    {
        var (_, html) = EmailTemplates.Rescheduled(Sample(), new DateTime(2026, 10, 19, 11, 0, 0), "Rahul", "http://x");

        Assert.Contains("<s>Mon 19 Oct 2026, 11:00 AM</s>", html);
    }
}
