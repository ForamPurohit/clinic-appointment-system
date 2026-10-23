using ClinicAppointment.API.Data;
using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Hubs;
using ClinicAppointment.API.Settings;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClinicAppointment.API.Notifications;

/// <summary>
/// Raised by the services after a change is saved. Keeps services free of SignalR and email
/// details, and lets unit tests record what happened with a simple fake.
/// </summary>
public interface IAppointmentEvents
{
    Task BookedAsync(AppointmentDto appointment);
    Task RescheduledAsync(AppointmentDto appointment, DateTime oldStart);
    Task CancelledAsync(AppointmentDto appointment);
    Task StatusChangedAsync(AppointmentDto appointment);
    Task LeaveChangedAsync(int doctorId, DateOnly startDate, DateOnly endDate);
}

public record SlotsChangedMessage(int DoctorId, string Date);
public record ScheduleChangedMessage(string Change, AppointmentDto Appointment);

/// <summary>
/// Pushes live updates through SignalR and queues patient emails.
/// Notification failures are logged but never fail the request that caused them.
/// </summary>
public class AppointmentEvents : IAppointmentEvents
{
    private readonly IHubContext<ClinicHub> _hub;
    private readonly IEmailQueue _emails;
    private readonly AppDbContext _db;
    private readonly EmailSettings _settings;
    private readonly ILogger<AppointmentEvents> _logger;

    public AppointmentEvents(
        IHubContext<ClinicHub> hub,
        IEmailQueue emails,
        AppDbContext db,
        IOptions<EmailSettings> settings,
        ILogger<AppointmentEvents> logger)
    {
        _hub = hub;
        _emails = emails;
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task BookedAsync(AppointmentDto a)
    {
        await SlotsChanged(a.DoctorId, a.StartTime);
        await ScheduleChanged("booked", a);
        await EmailPatient(a, name => EmailTemplates.Booked(a, name, _settings.ClientUrl));
    }

    public async Task RescheduledAsync(AppointmentDto a, DateTime oldStart)
    {
        await SlotsChanged(a.DoctorId, oldStart);
        if (oldStart.Date != a.StartTime.Date)
        {
            await SlotsChanged(a.DoctorId, a.StartTime);
        }
        await ScheduleChanged("rescheduled", a);
        await EmailPatient(a, name => EmailTemplates.Rescheduled(a, oldStart, name, _settings.ClientUrl));
    }

    public async Task CancelledAsync(AppointmentDto a)
    {
        await SlotsChanged(a.DoctorId, a.StartTime);
        await ScheduleChanged("cancelled", a);
        await EmailPatient(a, name => EmailTemplates.Cancelled(a, name, _settings.ClientUrl));
    }

    public async Task StatusChangedAsync(AppointmentDto a)
    {
        if (a.Status == "Cancelled")
        {
            await CancelledAsync(a);
            return;
        }

        await ScheduleChanged("updated", a);
        if (a.Status == "Completed")
        {
            await EmailPatient(a, name => EmailTemplates.ReviewRequest(a, name, _settings.ClientUrl));
        }
    }

    public async Task LeaveChangedAsync(int doctorId, DateOnly startDate, DateOnly endDate)
    {
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            await SlotsChanged(doctorId, date.ToDateTime(TimeOnly.MinValue));
        }
    }

    private Task SlotsChanged(int doctorId, DateTime date) => Safely(() =>
        _hub.Clients.Group(ClinicHub.SlotsGroup(doctorId))
            .SendAsync(ClinicHub.SlotsChanged, new SlotsChangedMessage(doctorId, date.ToString("yyyy-MM-dd"))));

    private Task ScheduleChanged(string change, AppointmentDto a) => Safely(() =>
        _hub.Clients.Group(ClinicHub.ScheduleGroup(a.DoctorId))
            .SendAsync(ClinicHub.ScheduleChanged, new ScheduleChangedMessage(change, a)));

    private Task EmailPatient(AppointmentDto a, Func<string, (string Subject, string Html)> build) => Safely(async () =>
    {
        var patient = await _db.Patients
            .Where(p => p.Id == a.PatientId)
            .Select(p => new { p.User.Email, p.User.FullName })
            .FirstOrDefaultAsync();

        if (patient is null) return;

        var (subject, html) = build(patient.FullName);
        await _emails.QueueAsync(new EmailMessage(patient.Email, patient.FullName, subject, html));
    });

    private async Task Safely(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification failed");
        }
    }
}
