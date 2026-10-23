using System.ComponentModel.DataAnnotations;

namespace ClinicAppointment.API.DTOs;

public record LeaveDto(int Id, int DoctorId, DateOnly StartDate, DateOnly EndDate, string? Reason);

public class CreateLeaveRequest
{
    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }

    [StringLength(200)]
    public string? Reason { get; set; }
}

public record ReviewDto(int Id, int AppointmentId, int DoctorId, string PatientName, int Rating, string? Comment, DateTime CreatedAt);

public class CreateReviewRequest
{
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
    public int Rating { get; set; }

    [StringLength(1000)]
    public string? Comment { get; set; }
}

public record DoctorReviewsDto(double? AverageRating, int ReviewCount, IReadOnlyList<ReviewDto> Reviews);
