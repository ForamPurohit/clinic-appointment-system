using ClinicAppointment.API.Notifications;
using ClinicAppointment.API.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace ClinicAppointment.Tests.Notifications;

/// <summary>Covers UC-19 in development mode: with no SMTP host, emails are written as .eml files.</summary>
public class SmtpEmailSenderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"clinic-mail-{Guid.NewGuid():N}");

    private sealed class FakeEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Testing";
    }

    private SmtpEmailSender Sender() => new(
        Options.Create(new EmailSettings { SmtpHost = "", PickupDirectory = "outbox", FromAddress = "clinic@test.com" }),
        new FakeEnvironment { ContentRootPath = _root });

    [Fact]
    public async Task NoSmtpHost_CreatesPickupFolderAndWritesEmlFile()
    {
        await Sender().SendAsync(new EmailMessage("rahul@test.com", "Rahul", "Appointment confirmed", "<p>Hi</p>"), CancellationToken.None);

        var file = Assert.Single(Directory.GetFiles(Path.Combine(_root, "outbox"), "*.eml"));
        var content = await File.ReadAllTextAsync(file);
        Assert.Contains("rahul@test.com", content);
        Assert.Contains("Subject: Appointment confirmed", content);
        Assert.Contains("clinic@test.com", content);
    }

    [Fact]
    public async Task EachEmail_GetsItsOwnFile()
    {
        var sender = Sender();
        await sender.SendAsync(new EmailMessage("a@test.com", "A", "One", "<p>1</p>"), CancellationToken.None);
        await sender.SendAsync(new EmailMessage("b@test.com", "B", "Two", "<p>2</p>"), CancellationToken.None);

        Assert.Equal(2, Directory.GetFiles(Path.Combine(_root, "outbox"), "*.eml").Length);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
