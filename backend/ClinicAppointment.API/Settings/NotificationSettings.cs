namespace ClinicAppointment.API.Settings;

public class EmailSettings
{
    /// <summary>Display name and address emails are sent from.</summary>
    public string FromName { get; set; } = "Lakeview Clinic";
    public string FromAddress { get; set; } = "no-reply@lakeviewclinic.test";

    /// <summary>Leave empty to write emails as .eml files to <see cref="PickupDirectory"/> instead of sending them.</summary>
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool EnableSsl { get; set; } = true;

    /// <summary>Folder (relative to the API project) used when no SMTP host is set.</summary>
    public string PickupDirectory { get; set; } = "emails";

    /// <summary>Link back to the Angular app, used in email buttons.</summary>
    public string ClientUrl { get; set; } = "http://localhost:4200";
}

public class ReminderSettings
{
    public bool Enabled { get; set; } = true;
    /// <summary>Send the reminder this many hours before the appointment.</summary>
    public int HoursBefore { get; set; } = 24;
    /// <summary>How often the background job looks for appointments needing a reminder.</summary>
    public int CheckIntervalMinutes { get; set; } = 15;
}
