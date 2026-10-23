using ClinicAppointment.API.Data;
using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicAppointment.API.Services;

public interface IAdminService
{
    Task<DashboardStatsDto> GetStatsAsync();
    Task<IReadOnlyList<PatientDto>> GetPatientsAsync(string? search);
}

public class AdminService : IAdminService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _clock;

    public AdminService(AppDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<DashboardStatsDto> GetStatsAsync()
    {
        var now = _clock.GetLocalNow().DateTime;
        var today = now.Date;
        var tomorrow = today.AddDays(1);

        return new DashboardStatsDto(
            TotalDoctors: await _db.Doctors.CountAsync(d => d.IsActive),
            TotalPatients: await _db.Patients.CountAsync(),
            TotalAppointments: await _db.Appointments.CountAsync(),
            TodayAppointments: await _db.Appointments.CountAsync(a =>
                a.StartTime >= today && a.StartTime < tomorrow && a.Status != AppointmentStatus.Cancelled),
            UpcomingAppointments: await _db.Appointments.CountAsync(a =>
                a.StartTime > now && a.Status == AppointmentStatus.Scheduled),
            CompletedAppointments: await _db.Appointments.CountAsync(a => a.Status == AppointmentStatus.Completed),
            CancelledAppointments: await _db.Appointments.CountAsync(a => a.Status == AppointmentStatus.Cancelled));
    }

    public async Task<IReadOnlyList<PatientDto>> GetPatientsAsync(string? search)
    {
        var query = _db.Patients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => p.User.FullName.Contains(term) || p.User.Email.Contains(term));
        }

        return await query
            .OrderBy(p => p.User.FullName)
            .Select(p => new PatientDto(
                p.Id,
                p.User.FullName,
                p.User.Email,
                p.User.PhoneNumber,
                p.DateOfBirth,
                p.Gender,
                p.Appointments.Count))
            .ToListAsync();
    }
}
