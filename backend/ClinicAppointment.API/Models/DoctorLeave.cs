namespace ClinicAppointment.API.Models;

/// <summary>A period when a doctor isn't available for bookings (holiday, conference, sick leave).</summary>
public class DoctorLeave
{
    public int Id { get; set; }

    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool Covers(DateOnly date) => date >= StartDate && date <= EndDate;
}
