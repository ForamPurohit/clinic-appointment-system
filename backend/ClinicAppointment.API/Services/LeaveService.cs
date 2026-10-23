using ClinicAppointment.API.Data;
using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Exceptions;
using ClinicAppointment.API.Models;
using ClinicAppointment.API.Notifications;
using Microsoft.EntityFrameworkCore;

namespace ClinicAppointment.API.Services;

public interface ILeaveService
{
    Task<IReadOnlyList<LeaveDto>> GetUpcomingAsync(int doctorId);
    Task<IReadOnlyList<LeaveDto>> GetMineAsync(int doctorUserId);
    Task<LeaveDto> AddAsync(int doctorUserId, CreateLeaveRequest request);
    Task DeleteAsync(int doctorUserId, int leaveId);
}

public class LeaveService : ILeaveService
{
    public const int MaxLeaveDays = 60;

    private readonly AppDbContext _db;
    private readonly TimeProvider _clock;
    private readonly IAppointmentEvents _events;

    public LeaveService(AppDbContext db, TimeProvider clock, IAppointmentEvents events)
    {
        _db = db;
        _clock = clock;
        _events = events;
    }

    private DateOnly Today => DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);

    /// <summary>Current and future leave for one doctor. Public, so the booking page can grey out days.</summary>
    public async Task<IReadOnlyList<LeaveDto>> GetUpcomingAsync(int doctorId)
    {
        var today = Today;
        return await _db.DoctorLeaves.AsNoTracking()
            .Where(l => l.DoctorId == doctorId && l.EndDate >= today)
            .OrderBy(l => l.StartDate)
            .Select(l => new LeaveDto(l.Id, l.DoctorId, l.StartDate, l.EndDate, l.Reason))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<LeaveDto>> GetMineAsync(int doctorUserId) =>
        await GetUpcomingAsync(await DoctorIdAsync(doctorUserId));

    public async Task<LeaveDto> AddAsync(int doctorUserId, CreateLeaveRequest request)
    {
        var doctorId = await DoctorIdAsync(doctorUserId);

        if (request.EndDate < request.StartDate)
        {
            throw new BadRequestException("The last day of leave can't be before the first day.");
        }

        if (request.StartDate < Today)
        {
            throw new BadRequestException("Leave can't start in the past.");
        }

        if (request.EndDate.DayNumber - request.StartDate.DayNumber + 1 > MaxLeaveDays)
        {
            throw new BadRequestException($"A single leave period can be at most {MaxLeaveDays} days.");
        }

        var overlaps = await _db.DoctorLeaves.AnyAsync(l =>
            l.DoctorId == doctorId && l.StartDate <= request.EndDate && l.EndDate >= request.StartDate);
        if (overlaps)
        {
            throw new ConflictException("This overlaps leave you've already added.");
        }

        var from = request.StartDate.ToDateTime(TimeOnly.MinValue);
        var to = request.EndDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var booked = await _db.Appointments.CountAsync(a =>
            a.DoctorId == doctorId
            && a.Status == AppointmentStatus.Scheduled
            && a.StartTime >= from && a.StartTime < to);
        if (booked > 0)
        {
            throw new ConflictException(
                $"You have {booked} scheduled {(booked == 1 ? "appointment" : "appointments")} in this period. " +
                "Ask the clinic admin to move or cancel them first.");
        }

        var leave = new DoctorLeave
        {
            DoctorId = doctorId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim()
        };
        _db.DoctorLeaves.Add(leave);
        await _db.SaveChangesAsync();

        await _events.LeaveChangedAsync(doctorId, leave.StartDate, leave.EndDate);
        return new LeaveDto(leave.Id, leave.DoctorId, leave.StartDate, leave.EndDate, leave.Reason);
    }

    public async Task DeleteAsync(int doctorUserId, int leaveId)
    {
        var doctorId = await DoctorIdAsync(doctorUserId);
        var leave = await _db.DoctorLeaves.FirstOrDefaultAsync(l => l.Id == leaveId && l.DoctorId == doctorId)
                    ?? throw new NotFoundException("Leave not found.");

        _db.DoctorLeaves.Remove(leave);
        await _db.SaveChangesAsync();

        await _events.LeaveChangedAsync(doctorId, leave.StartDate, leave.EndDate);
    }

    private async Task<int> DoctorIdAsync(int doctorUserId) =>
        await _db.Doctors.Where(d => d.UserId == doctorUserId).Select(d => (int?)d.Id).FirstOrDefaultAsync()
        ?? throw new NotFoundException("Doctor profile not found.");
}
