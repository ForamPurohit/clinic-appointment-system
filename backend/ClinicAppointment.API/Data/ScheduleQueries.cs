using Microsoft.EntityFrameworkCore;

namespace ClinicAppointment.API.Data;

/// <summary>Schedule checks shared by slot listing, booking and rescheduling.</summary>
public static class ScheduleQueries
{
    public static Task<bool> IsDoctorOnLeaveAsync(this AppDbContext db, int doctorId, DateOnly date) =>
        db.DoctorLeaves.AnyAsync(l => l.DoctorId == doctorId && l.StartDate <= date && l.EndDate >= date);
}
