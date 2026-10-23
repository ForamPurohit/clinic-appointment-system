using ClinicAppointment.API.Data;
using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Exceptions;
using ClinicAppointment.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicAppointment.API.Services;

public interface IReviewService
{
    Task<ReviewDto> CreateAsync(int patientUserId, int appointmentId, CreateReviewRequest request);
    Task<DoctorReviewsDto> GetForDoctorAsync(int doctorId, int take = 20);
}

public class ReviewService : IReviewService
{
    /// <summary>Patients can review a visit for this many days afterwards.</summary>
    public const int ReviewWindowDays = 30;

    private readonly AppDbContext _db;
    private readonly TimeProvider _clock;

    public ReviewService(AppDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ReviewDto> CreateAsync(int patientUserId, int appointmentId, CreateReviewRequest request)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Include(a => a.Review)
            .FirstOrDefaultAsync(a => a.Id == appointmentId)
            ?? throw new NotFoundException("Appointment not found.");

        if (appointment.Patient.UserId != patientUserId)
        {
            throw new ForbiddenException("You can only review your own visits.");
        }

        if (appointment.Status != AppointmentStatus.Completed)
        {
            throw new BadRequestException("You can review a visit once the doctor has marked it completed.");
        }

        if (appointment.Review is not null)
        {
            throw new ConflictException("You've already reviewed this visit.");
        }

        if (request.Rating is < 1 or > 5)
        {
            throw new BadRequestException("Rating must be between 1 and 5 stars.");
        }

        var now = _clock.GetLocalNow().DateTime;
        if (appointment.StartTime.AddDays(ReviewWindowDays) < now)
        {
            throw new BadRequestException($"Reviews can be left up to {ReviewWindowDays} days after a visit.");
        }

        var review = new Review
        {
            AppointmentId = appointment.Id,
            DoctorId = appointment.DoctorId,
            PatientId = appointment.PatientId,
            Rating = request.Rating,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim()
        };

        _db.Reviews.Add(review);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("You've already reviewed this visit.");
        }

        return ToDto(review, appointment.Patient.User.FullName);
    }

    public async Task<DoctorReviewsDto> GetForDoctorAsync(int doctorId, int take = 20)
    {
        if (!await _db.Doctors.AnyAsync(d => d.Id == doctorId))
        {
            throw new NotFoundException("Doctor not found.");
        }

        var reviews = _db.Reviews.AsNoTracking().Where(r => r.DoctorId == doctorId);
        var count = await reviews.CountAsync();
        double? average = count == 0 ? null : Math.Round(await reviews.AverageAsync(r => (double)r.Rating), 1);

        var latest = await reviews
            .OrderByDescending(r => r.CreatedAt)
            .Take(Math.Clamp(take, 1, 50))
            .Select(r => new { Review = r, r.Patient.User.FullName })
            .ToListAsync();

        return new DoctorReviewsDto(average, count, latest.Select(x => ToDto(x.Review, x.FullName)).ToList());
    }

    /// <summary>Shows "Rahul V." rather than the full name, to protect patient privacy.</summary>
    public static string PublicName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "Patient",
            1 => parts[0],
            _ => $"{parts[0]} {char.ToUpperInvariant(parts[^1][0])}."
        };
    }

    private static ReviewDto ToDto(Review r, string patientFullName) =>
        new(r.Id, r.AppointmentId, r.DoctorId, PublicName(patientFullName), r.Rating, r.Comment, r.CreatedAt);
}
