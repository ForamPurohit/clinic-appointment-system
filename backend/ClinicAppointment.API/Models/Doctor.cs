namespace ClinicAppointment.API.Models;

public class Doctor
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int SpecializationId { get; set; }
    public Specialization Specialization { get; set; } = null!;

    public string? Bio { get; set; }
    public int YearsOfExperience { get; set; }
    public decimal ConsultationFee { get; set; }

    // Working hours used to generate bookable time slots
    public TimeOnly WorkStartTime { get; set; } = new(9, 0);
    public TimeOnly WorkEndTime { get; set; } = new(17, 0);
    public int SlotDurationMinutes { get; set; } = 30;

    // Comma separated DayOfWeek values, e.g. "1,2,3,4,5" = Monday to Friday
    public string WorkingDays { get; set; } = "1,2,3,4,5";

    public bool IsActive { get; set; } = true;

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<DoctorLeave> Leaves { get; set; } = new List<DoctorLeave>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();

    public IReadOnlyList<int> GetWorkingDays() =>
        WorkingDays.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .Select(int.Parse)
                   .ToList();
}
