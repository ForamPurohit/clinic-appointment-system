namespace ClinicAppointment.API.Models;

public enum AppointmentStatus
{
    Scheduled,
    Completed,
    Cancelled,
    NoShow
}

public class Appointment
{
    public int Id { get; set; }

    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;

    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>How many times the patient has moved this appointment.</summary>
    public int RescheduleCount { get; set; }

    /// <summary>Set when the reminder email has been queued, so it's only sent once.</summary>
    public DateTime? ReminderSentAt { get; set; }

    public Review? Review { get; set; }
}
