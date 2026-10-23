using System.ComponentModel.DataAnnotations;

namespace ClinicAppointment.API.DTOs;

public record DoctorDto(
    int Id,
    int UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    int SpecializationId,
    string SpecializationName,
    string? Bio,
    int YearsOfExperience,
    decimal ConsultationFee,
    string WorkStartTime,
    string WorkEndTime,
    int SlotDurationMinutes,
    IReadOnlyList<int> WorkingDays,
    bool IsActive,
    double? AverageRating,
    int ReviewCount);

public class DoctorProfileRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Phone]
    public string? PhoneNumber { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Choose a specialization.")]
    public int SpecializationId { get; set; }

    [StringLength(1000)]
    public string? Bio { get; set; }

    [Range(0, 60)]
    public int YearsOfExperience { get; set; }

    [Range(0, 100000)]
    public decimal ConsultationFee { get; set; }

    /// <summary>Format "HH:mm", e.g. "09:00"</summary>
    [Required, RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Use HH:mm format.")]
    public string WorkStartTime { get; set; } = "09:00";

    /// <summary>Format "HH:mm", e.g. "17:00"</summary>
    [Required, RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Use HH:mm format.")]
    public string WorkEndTime { get; set; } = "17:00";

    [Range(10, 120)]
    public int SlotDurationMinutes { get; set; } = 30;

    /// <summary>DayOfWeek values: 0 = Sunday ... 6 = Saturday</summary>
    [Required, MinLength(1, ErrorMessage = "Pick at least one working day.")]
    public List<int> WorkingDays { get; set; } = new() { 1, 2, 3, 4, 5 };
}

public class CreateDoctorRequest : DoctorProfileRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;
}

public class UpdateDoctorRequest : DoctorProfileRequest
{
    public bool IsActive { get; set; } = true;
}

public record TimeSlotDto(DateTime StartTime, DateTime EndTime, bool IsAvailable);
