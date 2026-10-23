using System.ComponentModel.DataAnnotations;

namespace ClinicAppointment.API.DTOs;

public record SpecializationDto(int Id, string Name, string? Description, int DoctorCount);

public class SpecializationRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }
}
