namespace ClinicAppointment.API.DTOs;

public record DashboardStatsDto(
    int TotalDoctors,
    int TotalPatients,
    int TotalAppointments,
    int TodayAppointments,
    int UpcomingAppointments,
    int CompletedAppointments,
    int CancelledAppointments);

public record PatientDto(
    int Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    DateOnly? DateOfBirth,
    string? Gender,
    int AppointmentCount);
