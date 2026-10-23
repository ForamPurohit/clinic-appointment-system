using ClinicAppointment.API.Models;
using ClinicAppointment.API.Notifications;
using ClinicAppointment.API.Settings;
using ClinicAppointment.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ClinicAppointment.Tests.Notifications;

/// <summary>Covers UC-19 (reminder emails). "Now" is Monday 08:00; reminders go out 24 hours ahead.</summary>
public class ReminderJobTests
{
    private static readonly DateTime Monday = TestData.Monday;

    private static ReminderJob Job(TestData data, RecordingEmailQueue queue, int hoursBefore = 24) =>
        new(data.Db, queue, data.Clock,
            Options.Create(new ReminderSettings { HoursBefore = hoursBefore }),
            Options.Create(new EmailSettings()));

    [Fact]
    public async Task Run_QueuesRemindersForAppointmentsInsideTheWindow()
    {
        using var data = new TestData();
        var queue = new RecordingEmailQueue();
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(10));                // in 2 hours
        await data.AddAppointmentAsync(data.Doctor, data.OtherPatient, Monday.AddDays(1).AddHours(7)); // in 23 hours
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddDays(1).AddHours(9));      // in 25 hours

        var sent = await Job(data, queue).RunOnceAsync();

        Assert.Equal(2, sent);
        Assert.Equal(new[] { "p1@test.com", "p2@test.com" }, queue.Messages.Select(m => m.ToAddress));
        Assert.All(queue.Messages, m => Assert.StartsWith("Reminder:", m.Subject));
    }

    [Fact]
    public async Task Run_Twice_SendsEachReminderOnlyOnce()
    {
        using var data = new TestData();
        var queue = new RecordingEmailQueue();
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(10));

        await Job(data, queue).RunOnceAsync();
        var secondRun = await Job(data, queue).RunOnceAsync();

        Assert.Equal(0, secondRun);
        Assert.Single(queue.Messages);
        await using var check = data.NewContext();
        Assert.Equal(Monday.AddHours(8), (await check.Appointments.SingleAsync()).ReminderSentAt);
    }

    [Fact]
    public async Task Run_SkipsCancelledAndPastAppointments()
    {
        using var data = new TestData();
        var queue = new RecordingEmailQueue();
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(10), AppointmentStatus.Cancelled);
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(7));

        Assert.Equal(0, await Job(data, queue).RunOnceAsync());
    }

    [Fact]
    public async Task Run_PicksUpAppointmentsAsTimeMovesOn()
    {
        using var data = new TestData();
        var queue = new RecordingEmailQueue();
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddDays(1).AddHours(10)); // 26 hours away

        Assert.Equal(0, await Job(data, queue).RunOnceAsync());

        data.SetNow(Monday.AddHours(11)); // now 23 hours away
        Assert.Equal(1, await Job(data, queue).RunOnceAsync());
    }

    [Fact]
    public async Task Run_RespectsConfiguredWindow()
    {
        using var data = new TestData();
        var queue = new RecordingEmailQueue();
        await data.AddAppointmentAsync(data.Doctor, data.Patient, Monday.AddHours(11)); // 3 hours away

        Assert.Equal(0, await Job(data, queue, hoursBefore: 2).RunOnceAsync());
    }
}

public class EmailDispatcherServiceTests
{
    private static async Task RunUntilAsync(EmailDispatcherService service, Func<bool> done)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await service.StartAsync(cts.Token);
        while (!done() && !cts.IsCancellationRequested)
        {
            await Task.Delay(20);
        }
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task SendsQueuedEmails()
    {
        var queue = new EmailQueue();
        var sender = new RecordingEmailSender();
        var service = new EmailDispatcherService(queue, sender, NullLogger<EmailDispatcherService>.Instance);

        await queue.QueueAsync(new EmailMessage("a@test.com", "A", "One", "<p>1</p>"));
        await queue.QueueAsync(new EmailMessage("b@test.com", "B", "Two", "<p>2</p>"));
        await RunUntilAsync(service, () => sender.Sent.Count == 2);

        Assert.Equal(new[] { "One", "Two" }, sender.Sent.Select(m => m.Subject));
    }

    [Fact]
    public async Task RetriesAfterAFailure()
    {
        var queue = new EmailQueue();
        var sender = new RecordingEmailSender(failFirst: 1);
        var service = new EmailDispatcherService(queue, sender, NullLogger<EmailDispatcherService>.Instance);

        await queue.QueueAsync(new EmailMessage("a@test.com", "A", "Retry me", "<p>x</p>"));
        await RunUntilAsync(service, () => sender.Sent.Count == 1);

        Assert.Equal(2, sender.Attempts);
        Assert.Single(sender.Sent);
    }
}
