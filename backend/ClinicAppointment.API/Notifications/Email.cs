using System.Net;
using System.Net.Mail;
using System.Threading.Channels;
using ClinicAppointment.API.Settings;
using Microsoft.Extensions.Options;

namespace ClinicAppointment.API.Notifications;

public record EmailMessage(string ToAddress, string ToName, string Subject, string HtmlBody);

/// <summary>In-memory queue so API requests never wait for the mail server.</summary>
public interface IEmailQueue
{
    ValueTask QueueAsync(EmailMessage message, CancellationToken ct = default);
    ChannelReader<EmailMessage> Reader { get; }
}

public class EmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateUnbounded<EmailMessage>(
        new UnboundedChannelOptions { SingleReader = true });

    public ValueTask QueueAsync(EmailMessage message, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(message, ct);

    public ChannelReader<EmailMessage> Reader => _channel.Reader;
}

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

/// <summary>
/// Sends through SMTP when a host is configured. Otherwise writes each email as an .eml file
/// to the pickup folder, which you can open in any mail app during development.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;
    private readonly string _pickupPath;

    public SmtpEmailSender(IOptions<EmailSettings> settings, IWebHostEnvironment env)
    {
        _settings = settings.Value;
        _pickupPath = Path.Combine(env.ContentRootPath, _settings.PickupDirectory);
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        using var mail = new MailMessage
        {
            From = new MailAddress(_settings.FromAddress, _settings.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true
        };
        mail.To.Add(new MailAddress(message.ToAddress, message.ToName));

        using var client = new SmtpClient();

        if (string.IsNullOrWhiteSpace(_settings.SmtpHost))
        {
            Directory.CreateDirectory(_pickupPath);
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = _pickupPath;
        }
        else
        {
            client.Host = _settings.SmtpHost;
            client.Port = _settings.SmtpPort;
            client.EnableSsl = _settings.EnableSsl;
            if (!string.IsNullOrWhiteSpace(_settings.Username))
            {
                client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
            }
        }

        await client.SendMailAsync(mail, ct);
    }
}

/// <summary>Background worker that sends queued emails, retrying failures with back-off.</summary>
public class EmailDispatcherService : BackgroundService
{
    private const int MaxAttempts = 3;

    private readonly IEmailQueue _queue;
    private readonly IEmailSender _sender;
    private readonly ILogger<EmailDispatcherService> _logger;

    public EmailDispatcherService(IEmailQueue queue, IEmailSender sender, ILogger<EmailDispatcherService> logger)
    {
        _queue = queue;
        _sender = sender;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    await _sender.SendAsync(message, stoppingToken);
                    _logger.LogInformation("Email '{Subject}' sent to {To}", message.Subject, message.ToAddress);
                    break;
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Email to {To} failed (attempt {Attempt}/{Max})", message.ToAddress, attempt, MaxAttempts);
                    if (attempt < MaxAttempts)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2 * attempt), stoppingToken);
                    }
                }
            }
        }
    }
}
