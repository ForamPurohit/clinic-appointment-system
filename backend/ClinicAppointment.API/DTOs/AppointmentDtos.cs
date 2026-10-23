using System.ComponentModel.DataAnnotations;

namespace ClinicAppointment.API.DTOs;

public record AppointmentDto(
    int Id,
    int DoctorId,
    string DoctorName,
    string SpecializationName,
    int PatientId,
    string PatientName,
    string? PatientPhone,
    DateTime StartTime,
    DateTime EndTime,
    string Reason,
    string? Notes,
    string Status,
    DateTime CreatedAt,
    int RescheduleCount,
    int? Rating);

public class CreateAppointmentRequest
{
    [Range(1, int.MaxValue)]
    public int DoctorId { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    [Required, StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}

public class UpdateAppointmentStatusRequest
{
    /// <summary>Completed, Cancelled or NoShow</summary>
    [Required]
    public string Status { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Notes { get; set; }
}

public class AppointmentFilter
{
    public string? Status { get; set; }
    public int? DoctorId { get; set; }
    public DateOnly? Date { get; set; }
}

public class RescheduleAppointmentRequest
{
    [Required]
    public DateTime NewStartTime { get; set; }
}
