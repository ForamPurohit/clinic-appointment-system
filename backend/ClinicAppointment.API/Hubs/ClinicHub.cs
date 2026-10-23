using System.Security.Claims;
using ClinicAppointment.API.Data;
using ClinicAppointment.API.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ClinicAppointment.API.Hubs;

/// <summary>
/// Real-time channel for the Angular app.
///
/// Client to server:
///   WatchDoctor(doctorId) / UnwatchDoctor(doctorId)  - follow a doctor's slot availability
/// Server to client:
///   SlotsChanged { doctorId, date }                  - to everyone watching that doctor
///   ScheduleChanged { change, appointment }          - to the doctor whose schedule changed
///
/// Doctors who connect with their JWT are added to their own schedule group automatically.
/// </summary>
public class ClinicHub : Hub
{
    public const string SlotsChanged = "SlotsChanged";
    public const string ScheduleChanged = "ScheduleChanged";

    private readonly AppDbContext _db;

    public ClinicHub(AppDbContext db)
    {
        _db = db;
    }

    public static string SlotsGroup(int doctorId) => $"slots-{doctorId}";
    public static string ScheduleGroup(int doctorId) => $"schedule-{doctorId}";

    public Task WatchDoctor(int doctorId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, SlotsGroup(doctorId));

    public Task UnwatchDoctor(int doctorId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, SlotsGroup(doctorId));

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user?.IsInRole(Roles.Doctor) == true &&
            int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            var doctorId = await _db.Doctors
                .Where(d => d.UserId == userId)
                .Select(d => (int?)d.Id)
                .FirstOrDefaultAsync();

            if (doctorId.HasValue)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, ScheduleGroup(doctorId.Value));
            }
        }

        await base.OnConnectedAsync();
    }
}
