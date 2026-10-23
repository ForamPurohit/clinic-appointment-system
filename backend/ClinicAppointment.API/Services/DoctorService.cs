using ClinicAppointment.API.Data;
using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Exceptions;
using ClinicAppointment.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicAppointment.API.Services;

public interface IDoctorService
{
    Task<IReadOnlyList<DoctorDto>> GetAllAsync(int? specializationId, bool includeInactive);
    Task<DoctorDto> GetByIdAsync(int id);
    Task<DoctorDto> GetByUserIdAsync(int userId);
    Task<DoctorDto> CreateAsync(CreateDoctorRequest request);
    Task<DoctorDto> UpdateAsync(int id, UpdateDoctorRequest request);
    Task<IReadOnlyList<TimeSlotDto>> GetSlotsAsync(int doctorId, DateOnly date);
}

public class DoctorService : IDoctorService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _clock;

    public DoctorService(AppDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    private IQueryable<Doctor> Query() =>
        _db.Doctors.Include(d => d.User).Include(d => d.Specialization);

    public async Task<IReadOnlyList<DoctorDto>> GetAllAsync(int? specializationId, bool includeInactive)
    {
        var query = Query().AsNoTracking();

        if (!includeInactive) query = query.Where(d => d.IsActive);
        if (specializationId.HasValue) query = query.Where(d => d.SpecializationId == specializationId);

        var doctors = await query.OrderBy(d => d.User.FullName).ToListAsync();
        var ratings = await RatingsAsync(doctors.Select(d => d.Id).ToList());
        return doctors.Select(d => ToDto(d, ratings)).ToList();
    }

    public async Task<DoctorDto> GetByIdAsync(int id)
    {
        var doctor = await Query().AsNoTracking().FirstOrDefaultAsync(d => d.Id == id)
                     ?? throw new NotFoundException("Doctor not found.");
        return ToDto(doctor, await RatingsAsync(new List<int> { doctor.Id }));
    }

    public async Task<DoctorDto> GetByUserIdAsync(int userId)
    {
        var doctor = await Query().AsNoTracking().FirstOrDefaultAsync(d => d.UserId == userId)
                     ?? throw new NotFoundException("Doctor profile not found.");
        return ToDto(doctor, await RatingsAsync(new List<int> { doctor.Id }));
    }

    public async Task<DoctorDto> CreateAsync(CreateDoctorRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        await EnsureSpecializationExists(request.SpecializationId);

        var doctor = new Doctor
        {
            User = new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = Roles.Doctor
            }
        };
        ApplyProfile(doctor, request);

        _db.Doctors.Add(doctor);
        await _db.SaveChangesAsync();

        return await GetByIdAsync(doctor.Id);
    }

    public async Task<DoctorDto> UpdateAsync(int id, UpdateDoctorRequest request)
    {
        var doctor = await Query().FirstOrDefaultAsync(d => d.Id == id)
                     ?? throw new NotFoundException("Doctor not found.");

        await EnsureSpecializationExists(request.SpecializationId);

        ApplyProfile(doctor, request);
        doctor.IsActive = request.IsActive;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<IReadOnlyList<TimeSlotDto>> GetSlotsAsync(int doctorId, DateOnly date)
    {
        var doctor = await _db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.Id == doctorId && d.IsActive)
                     ?? throw new NotFoundException("Doctor not found.");

        if (!doctor.GetWorkingDays().Contains((int)date.DayOfWeek) || await _db.IsDoctorOnLeaveAsync(doctorId, date))
        {
            return Array.Empty<TimeSlotDto>();
        }

        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);

        var booked = await _db.Appointments
            .Where(a => a.DoctorId == doctorId
                        && a.Status != AppointmentStatus.Cancelled
                        && a.StartTime >= dayStart && a.StartTime < dayEnd)
            .Select(a => a.StartTime)
            .ToListAsync();

        var bookedSet = booked.ToHashSet();
        var now = _clock.GetLocalNow().DateTime;
        var duration = TimeSpan.FromMinutes(doctor.SlotDurationMinutes);
        var end = doctor.WorkEndTime.ToTimeSpan();

        var slots = new List<TimeSlotDto>();
        for (var t = doctor.WorkStartTime.ToTimeSpan(); t + duration <= end; t += duration)
        {
            var start = dayStart + t;
            slots.Add(new TimeSlotDto(start, start + duration, start > now && !bookedSet.Contains(start)));
        }

        return slots;
    }

    private async Task EnsureSpecializationExists(int specializationId)
    {
        if (!await _db.Specializations.AnyAsync(s => s.Id == specializationId))
        {
            throw new BadRequestException("Specialization not found.");
        }
    }

    private static void ApplyProfile(Doctor doctor, DoctorProfileRequest request)
    {
        var start = TimeOnly.Parse(request.WorkStartTime);
        var end = TimeOnly.Parse(request.WorkEndTime);

        if (end <= start)
        {
            throw new BadRequestException("Work end time must be after the start time.");
        }

        if ((end - start).TotalMinutes < request.SlotDurationMinutes)
        {
            throw new BadRequestException("Working hours must fit at least one appointment slot.");
        }

        var days = request.WorkingDays.Distinct().OrderBy(d => d).ToList();
        if (days.Any(d => d < 0 || d > 6))
        {
            throw new BadRequestException("Working days must be between 0 (Sunday) and 6 (Saturday).");
        }

        doctor.User.FullName = request.FullName.Trim();
        doctor.User.PhoneNumber = request.PhoneNumber;
        doctor.SpecializationId = request.SpecializationId;
        doctor.Bio = request.Bio;
        doctor.YearsOfExperience = request.YearsOfExperience;
        doctor.ConsultationFee = request.ConsultationFee;
        doctor.WorkStartTime = start;
        doctor.WorkEndTime = end;
        doctor.SlotDurationMinutes = request.SlotDurationMinutes;
        doctor.WorkingDays = string.Join(',', days);
    }

    private async Task<Dictionary<int, (double Average, int Count)>> RatingsAsync(List<int> doctorIds)
    {
        var rows = await _db.Reviews
            .Where(r => doctorIds.Contains(r.DoctorId))
            .GroupBy(r => r.DoctorId)
            .Select(g => new { DoctorId = g.Key, Average = g.Average(r => (double)r.Rating), Count = g.Count() })
            .ToListAsync();

        return rows.ToDictionary(r => r.DoctorId, r => (Math.Round(r.Average, 1), r.Count));
    }

    internal static DoctorDto ToDto(Doctor d, IReadOnlyDictionary<int, (double Average, int Count)> ratings)
    {
        var hasRating = ratings.TryGetValue(d.Id, out var rating);
        return ToDto(d, hasRating ? rating.Average : null, hasRating ? rating.Count : 0);
    }

    internal static DoctorDto ToDto(Doctor d, double? averageRating, int reviewCount) => new(
        d.Id,
        d.UserId,
        d.User.FullName,
        d.User.Email,
        d.User.PhoneNumber,
        d.SpecializationId,
        d.Specialization.Name,
        d.Bio,
        d.YearsOfExperience,
        d.ConsultationFee,
        d.WorkStartTime.ToString("HH:mm"),
        d.WorkEndTime.ToString("HH:mm"),
        d.SlotDurationMinutes,
        d.GetWorkingDays(),
        d.IsActive,
        averageRating,
        reviewCount);
}
