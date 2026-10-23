using ClinicAppointment.API.Data;
using ClinicAppointment.API.Models;
using ClinicAppointment.API.Services;
using ClinicAppointment.API.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClinicAppointment.API.Notifications;

/// <summary>Finds scheduled appointments starting soon and queues one reminder email for each.</summary>
public class ReminderJob
{
    private readonly AppDbContext _db;
    private readonly IEmailQueue _emails;
    private readonly TimeProvider _clock;
    private readonly ReminderSettings _reminders;
    private readonly EmailSettings _email;

    public ReminderJob(AppDbContext db, IEmailQueue emails, TimeProvider clock,
        IOptions<ReminderSettings> reminders, IOptions<EmailSettings> email)
    {
        _db = db;
        _emails = emails;
        _clock = clock;
        _reminders = reminders.Value;
        _email = email.Value;
    }

    /// <returns>The number of reminders queued.</returns>
    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        var now = _clock.GetLocalNow().DateTime;
        var until = now.AddHours(_reminders.HoursBefore);

        var due = await _db.Appointments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Doctor).ThenInclude(d => d.Specialization)
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Include(a => a.Review)
            .Where(a => a.Status == AppointmentStatus.Scheduled
                        && a.ReminderSentAt == null
                        && a.StartTime > now
                        && a.StartTime <= until)
            .OrderBy(a => a.StartTime)
            .ToListAsync(ct);

        foreach (var appointment in due)
        {
            var dto = AppointmentService.ToDto(appointment);
            var patient = appointment.Patient.User;
            var (subject, html) = EmailTemplates.Reminder(dto, patient.FullName, _email.ClientUrl);

            await _emails.QueueAsync(new EmailMessage(patient.Email, patient.FullName, subject, html), ct);
            appointment.ReminderSentAt = now;
        }

        if (due.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return due.Count;
    }
}

/// <summary>Runs <see cref="ReminderJob"/> at start-up and then on a fixed interval.</summary>
public class ReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _clock;
    private readonly ReminderSettings _settings;
    private readonly ILogger<ReminderBackgroundService> _logger;

    public ReminderBackgroundService(IServiceScopeFactory scopes, TimeProvider clock,
        IOptions<ReminderSettings> settings, ILogger<ReminderBackgroundService> logger)
    {
        _scopes = scopes;
        _clock = clock;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled) return;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_settings.CheckIntervalMinutes), _clock);

        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var sent = await scope.ServiceProvider.GetRequiredService<ReminderJob>().RunOnceAsync(stoppingToken);
                if (sent > 0)
                {
                    _logger.LogInformation("Queued {Count} appointment reminders", sent);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Reminder job failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
