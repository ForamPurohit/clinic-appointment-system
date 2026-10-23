using ClinicAppointment.API.Data;
using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Exceptions;
using ClinicAppointment.API.Models;
using ClinicAppointment.API.Notifications;
using Microsoft.EntityFrameworkCore;

namespace ClinicAppointment.API.Services;

public interface IAppointmentService
{
    Task<AppointmentDto> BookAsync(int patientUserId, CreateAppointmentRequest request);
    Task<AppointmentDto> RescheduleAsync(int id, int userId, string role, RescheduleAppointmentRequest request);
    Task<IReadOnlyList<AppointmentDto>> GetForPatientAsync(int patientUserId);
    Task<IReadOnlyList<AppointmentDto>> GetForDoctorAsync(int doctorUserId, DateOnly? date);
    Task<IReadOnlyList<AppointmentDto>> GetAllAsync(AppointmentFilter filter);
    Task<AppointmentDto> GetByIdAsync(int id, int userId, string role);
    Task<AppointmentDto> CancelAsync(int id, int userId, string role);
    Task<AppointmentDto> UpdateStatusAsync(int id, int userId, string role, UpdateAppointmentStatusRequest request);
}

public class AppointmentService : IAppointmentService
{
    /// <summary>Patients may move an appointment this many times; admins aren't limited.</summary>
    public const int MaxPatientReschedules = 2;

    private const string SlotTakenMessage = "This time slot was just booked. Pick another slot.";

    private readonly AppDbContext _db;
    private readonly TimeProvider _clock;
    private readonly IAppointmentEvents _events;

    public AppointmentService(AppDbContext db, TimeProvider clock, IAppointmentEvents events)
    {
        _db = db;
        _clock = clock;
        _events = events;
    }

    private DateTime Now => _clock.GetLocalNow().DateTime;

    private IQueryable<Appointment> Query() =>
        _db.Appointments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Doctor).ThenInclude(d => d.Specialization)
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Include(a => a.Review);

    // ======================= Booking =======================

    public async Task<AppointmentDto> BookAsync(int patientUserId, CreateAppointmentRequest request)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.UserId == patientUserId)
                      ?? throw new ForbiddenException("Only patients can book appointments.");

        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.Id == request.DoctorId && d.IsActive)
                     ?? throw new NotFoundException("Doctor not found.");

        var start = TrimToMinute(request.StartTime);
        var end = start.AddMinutes(doctor.SlotDurationMinutes);

        await EnsureSlotIsBookableAsync(doctor, patient.Id, start, end, ignoreAppointmentId: null);

        var appointment = new Appointment
        {
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            StartTime = start,
            EndTime = end,
            Reason = request.Reason.Trim(),
            Status = AppointmentStatus.Scheduled
        };

        _db.Appointments.Add(appointment);
        await SaveBookingAsync();

        var dto = await LoadDtoAsync(appointment.Id);
        await _events.BookedAsync(dto);
        return dto;
    }

    // ======================= Rescheduling =======================

    public async Task<AppointmentDto> RescheduleAsync(int id, int userId, string role, RescheduleAppointmentRequest request)
    {
        if (role == Roles.Doctor)
        {
            throw new ForbiddenException("Doctors can't reschedule appointments. Ask the patient or clinic admin.");
        }

        var appointment = await Query().FirstOrDefaultAsync(a => a.Id == id)
                          ?? throw new NotFoundException("Appointment not found.");

        EnsureAccess(appointment, userId, role);

        if (appointment.Status != AppointmentStatus.Scheduled)
        {
            throw new BadRequestException($"Only scheduled appointments can be rescheduled. This one is {appointment.Status}.");
        }

        if (appointment.StartTime <= Now)
        {
            throw new BadRequestException("This appointment has already started, so it can't be moved.");
        }

        if (role == Roles.Patient && appointment.RescheduleCount >= MaxPatientReschedules)
        {
            throw new BadRequestException(
                $"This appointment has already been moved {MaxPatientReschedules} times. Cancel it and book a new one instead.");
        }

        var newStart = TrimToMinute(request.NewStartTime);
        if (newStart == appointment.StartTime)
        {
            throw new BadRequestException("Pick a different time from the current one.");
        }

        var doctor = appointment.Doctor;
        if (!doctor.IsActive)
        {
            throw new BadRequestException("This doctor is no longer taking appointments.");
        }

        var newEnd = newStart.AddMinutes(doctor.SlotDurationMinutes);
        await EnsureSlotIsBookableAsync(doctor, appointment.PatientId, newStart, newEnd, ignoreAppointmentId: appointment.Id);

        var oldStart = appointment.StartTime;
        appointment.StartTime = newStart;
        appointment.EndTime = newEnd;
        appointment.ReminderSentAt = null; // the new time deserves its own reminder
        if (role == Roles.Patient)
        {
            appointment.RescheduleCount++;
        }

        await SaveBookingAsync();

        var dto = ToDto(appointment);
        await _events.RescheduledAsync(dto, oldStart);
        return dto;
    }

    // ======================= Queries =======================

    public async Task<IReadOnlyList<AppointmentDto>> GetForPatientAsync(int patientUserId)
    {
        var list = await Query().AsNoTracking()
            .Where(a => a.Patient.UserId == patientUserId)
            .OrderByDescending(a => a.StartTime)
            .ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<AppointmentDto>> GetForDoctorAsync(int doctorUserId, DateOnly? date)
    {
        var query = ApplyDate(Query().AsNoTracking().Where(a => a.Doctor.UserId == doctorUserId), date);
        var list = await query.OrderBy(a => a.StartTime).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<AppointmentDto>> GetAllAsync(AppointmentFilter filter)
    {
        var query = Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var status = ParseStatus(filter.Status);
            query = query.Where(a => a.Status == status);
        }

        if (filter.DoctorId.HasValue)
        {
            query = query.Where(a => a.DoctorId == filter.DoctorId);
        }

        var list = await ApplyDate(query, filter.Date).OrderByDescending(a => a.StartTime).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<AppointmentDto> GetByIdAsync(int id, int userId, string role)
    {
        var appointment = await Query().AsNoTracking().FirstOrDefaultAsync(a => a.Id == id)
                          ?? throw new NotFoundException("Appointment not found.");

        EnsureAccess(appointment, userId, role);
        return ToDto(appointment);
    }

    // ======================= Cancelling and outcomes =======================

    public async Task<AppointmentDto> CancelAsync(int id, int userId, string role)
    {
        var appointment = await Query().FirstOrDefaultAsync(a => a.Id == id)
                          ?? throw new NotFoundException("Appointment not found.");

        EnsureAccess(appointment, userId, role);

        if (appointment.Status != AppointmentStatus.Scheduled)
        {
            throw new BadRequestException($"Only scheduled appointments can be cancelled. This one is {appointment.Status}.");
        }

        if (role == Roles.Patient && appointment.StartTime <= Now)
        {
            throw new BadRequestException("Past appointments can't be cancelled.");
        }

        appointment.Status = AppointmentStatus.Cancelled;
        await _db.SaveChangesAsync();

        var dto = ToDto(appointment);
        await _events.CancelledAsync(dto);
        return dto;
    }

    public async Task<AppointmentDto> UpdateStatusAsync(int id, int userId, string role, UpdateAppointmentStatusRequest request)
    {
        var appointment = await Query().FirstOrDefaultAsync(a => a.Id == id)
                          ?? throw new NotFoundException("Appointment not found.");

        if (role == Roles.Patient)
        {
            throw new ForbiddenException("Patients can't change appointment status.");
        }

        EnsureAccess(appointment, userId, role);

        var newStatus = ParseStatus(request.Status);
        if (newStatus == AppointmentStatus.Scheduled)
        {
            throw new BadRequestException("Use Completed, Cancelled or NoShow.");
        }

        if (appointment.Status != AppointmentStatus.Scheduled)
        {
            throw new BadRequestException($"This appointment is already {appointment.Status}.");
        }

        if ((newStatus is AppointmentStatus.Completed or AppointmentStatus.NoShow) && appointment.StartTime > Now)
        {
            throw new BadRequestException("An appointment can only be marked completed or no-show once it has started.");
        }

        appointment.Status = newStatus;
        if (request.Notes is not null)
        {
            appointment.Notes = request.Notes.Trim();
        }

        await _db.SaveChangesAsync();

        var dto = ToDto(appointment);
        await _events.StatusChangedAsync(dto);
        return dto;
    }

    // ======================= Helpers =======================

    /// <summary>Applies every booking rule. Used for new bookings and for reschedules.</summary>
    private async Task EnsureSlotIsBookableAsync(Doctor doctor, int patientId, DateTime start, DateTime end, int? ignoreAppointmentId)
    {
        if (start <= Now)
        {
            throw new BadRequestException("Pick a time in the future.");
        }

        if (!doctor.GetWorkingDays().Contains((int)start.DayOfWeek))
        {
            throw new BadRequestException("The doctor doesn't work on this day.");
        }

        var workStart = doctor.WorkStartTime.ToTimeSpan();
        var workEnd = doctor.WorkEndTime.ToTimeSpan();

        if (start.TimeOfDay < workStart || end.TimeOfDay > workEnd || end.Date != start.Date)
        {
            throw new BadRequestException("This time is outside the doctor's working hours.");
        }

        if ((start.TimeOfDay - workStart).TotalMinutes % doctor.SlotDurationMinutes != 0)
        {
            throw new BadRequestException("Pick one of the listed time slots.");
        }

        if (await _db.IsDoctorOnLeaveAsync(doctor.Id, DateOnly.FromDateTime(start)))
        {
            throw new BadRequestException("The doctor is on leave that day. Pick another date.");
        }

        var doctorBusy = await _db.Appointments.AnyAsync(a =>
            a.DoctorId == doctor.Id
            && a.StartTime == start
            && a.Status != AppointmentStatus.Cancelled
            && a.Id != ignoreAppointmentId);
        if (doctorBusy)
        {
            throw new ConflictException(SlotTakenMessage);
        }

        var patientBusy = await _db.Appointments.AnyAsync(a =>
            a.PatientId == patientId
            && a.Status == AppointmentStatus.Scheduled
            && a.StartTime < end && a.EndTime > start
            && a.Id != ignoreAppointmentId);
        if (patientBusy)
        {
            throw new ConflictException("You already have an appointment that overlaps this time.");
        }
    }

    private async Task SaveBookingAsync()
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The filtered unique index caught two requests for the same slot at the same moment
            throw new ConflictException(SlotTakenMessage);
        }
    }

    private static DateTime TrimToMinute(DateTime t) => new(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0);

    private static void EnsureAccess(Appointment a, int userId, string role)
    {
        var allowed = role switch
        {
            Roles.Admin => true,
            Roles.Doctor => a.Doctor.UserId == userId,
            Roles.Patient => a.Patient.UserId == userId,
            _ => false
        };

        if (!allowed)
        {
            throw new ForbiddenException("You don't have access to this appointment.");
        }
    }

    private static IQueryable<Appointment> ApplyDate(IQueryable<Appointment> query, DateOnly? date)
    {
        if (!date.HasValue) return query;

        var from = date.Value.ToDateTime(TimeOnly.MinValue);
        var to = from.AddDays(1);
        return query.Where(a => a.StartTime >= from && a.StartTime < to);
    }

    private static AppointmentStatus ParseStatus(string value) =>
        Enum.TryParse<AppointmentStatus>(value, ignoreCase: true, out var status) && Enum.IsDefined(status)
            ? status
            : throw new BadRequestException($"Unknown status '{value}'.");

    private async Task<AppointmentDto> LoadDtoAsync(int id) =>
        ToDto(await Query().AsNoTracking().FirstAsync(x => x.Id == id));

    internal static AppointmentDto ToDto(Appointment a) => new(
        a.Id,
        a.DoctorId,
        a.Doctor.User.FullName,
        a.Doctor.Specialization.Name,
        a.PatientId,
        a.Patient.User.FullName,
        a.Patient.User.PhoneNumber,
        a.StartTime,
        a.EndTime,
        a.Reason,
        a.Notes,
        a.Status.ToString(),
        a.CreatedAt,
        a.RescheduleCount,
        a.Review?.Rating);
}
