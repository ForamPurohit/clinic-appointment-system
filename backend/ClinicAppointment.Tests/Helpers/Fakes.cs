using System.Threading.Channels;
using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace ClinicAppointment.Tests.Helpers;

/// <summary>Records which events a service raised, instead of sending real notifications.</summary>
public class RecordingEvents : IAppointmentEvents
{
    public List<(string Name, AppointmentDto? Appointment)> Raised { get; } = new();
    public List<(int DoctorId, DateOnly Start, DateOnly End)> LeaveChanges { get; } = new();
    public DateTime? LastOldStart { get; private set; }

    public IEnumerable<string> Names => Raised.Select(r => r.Name);

    public Task BookedAsync(AppointmentDto a) { Raised.Add(("Booked", a)); return Task.CompletedTask; }
    public Task CancelledAsync(AppointmentDto a) { Raised.Add(("Cancelled", a)); return Task.CompletedTask; }
    public Task StatusChangedAsync(AppointmentDto a) { Raised.Add(("StatusChanged", a)); return Task.CompletedTask; }

    public Task RescheduledAsync(AppointmentDto a, DateTime oldStart)
    {
        Raised.Add(("Rescheduled", a));
        LastOldStart = oldStart;
        return Task.CompletedTask;
    }

    public Task LeaveChangedAsync(int doctorId, DateOnly start, DateOnly end)
    {
        LeaveChanges.Add((doctorId, start, end));
        return Task.CompletedTask;
    }
}

/// <summary>Keeps queued emails in a list so tests can inspect them.</summary>
public class RecordingEmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateUnbounded<EmailMessage>();

    public List<EmailMessage> Messages { get; } = new();

    public ValueTask QueueAsync(EmailMessage message, CancellationToken ct = default)
    {
        Messages.Add(message);
        return _channel.Writer.WriteAsync(message, ct);
    }

    public ChannelReader<EmailMessage> Reader => _channel.Reader;
}

/// <summary>Collects emails "sent" by the dispatcher. Can be told to fail a number of times first.</summary>
public class RecordingEmailSender : IEmailSender
{
    private int _failuresLeft;

    public RecordingEmailSender(int failFirst = 0) => _failuresLeft = failFirst;

    public List<EmailMessage> Sent { get; } = new();
    public int Attempts { get; private set; }

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        Attempts++;
        if (_failuresLeft > 0)
        {
            _failuresLeft--;
            throw new InvalidOperationException("SMTP server unavailable");
        }

        lock (Sent) Sent.Add(message);
        return Task.CompletedTask;
    }
}

/// <summary>Minimal IHubContext that records every message sent to a group.</summary>
public class FakeHubContext<THub> : IHubContext<THub> where THub : Hub
{
    private readonly FakeClients _clients = new();

    public List<(string Group, string Method, object? Payload)> Sent => _clients.Sent;
    public IHubClients Clients => _clients;
    public IGroupManager Groups => throw new NotSupportedException();

    public bool ThrowOnSend
    {
        get => _clients.ThrowOnSend;
        set => _clients.ThrowOnSend = value;
    }

    private class FakeClients : IHubClients
    {
        public List<(string Group, string Method, object? Payload)> Sent { get; } = new();
        public bool ThrowOnSend { get; set; }

        public IClientProxy Group(string groupName) => new Proxy(this, groupName);

        public IClientProxy All => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Client(string connectionId) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        public IClientProxy User(string userId) => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private class Proxy : IClientProxy
    {
        private readonly FakeClients _owner;
        private readonly string _group;

        public Proxy(FakeClients owner, string group)
        {
            _owner = owner;
            _group = group;
        }

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            if (_owner.ThrowOnSend) throw new InvalidOperationException("Hub unavailable");
            _owner.Sent.Add((_group, method, args.FirstOrDefault()));
            return Task.CompletedTask;
        }
    }
}
