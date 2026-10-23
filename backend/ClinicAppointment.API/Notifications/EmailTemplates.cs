using System.Globalization;
using System.Net;
using ClinicAppointment.API.DTOs;

namespace ClinicAppointment.API.Notifications;

/// <summary>Builds the HTML emails sent to patients. All user-entered text is HTML-encoded.</summary>
public static class EmailTemplates
{
    public static (string Subject, string Html) Booked(AppointmentDto a, string patientName, string clientUrl) => (
        $"Appointment confirmed: {When(a.StartTime)}",
        Layout(patientName,
            "Your appointment is confirmed.",
            Details(a),
            "Please arrive 10 minutes early. If your plans change, you can reschedule or cancel online.",
            ("View my appointments", $"{clientUrl}/patient/appointments")));

    public static (string Subject, string Html) Rescheduled(AppointmentDto a, DateTime oldStart, string patientName, string clientUrl) => (
        $"Appointment moved to {When(a.StartTime)}",
        Layout(patientName,
            $"Your appointment has been moved from <s>{When(oldStart)}</s> to the time below.",
            Details(a),
            "Reply to this email if you didn't make this change.",
            ("View my appointments", $"{clientUrl}/patient/appointments")));

    public static (string Subject, string Html) Cancelled(AppointmentDto a, string patientName, string clientUrl) => (
        $"Appointment cancelled: {When(a.StartTime)}",
        Layout(patientName,
            "This appointment has been cancelled.",
            Details(a),
            "You can book a new time whenever suits you.",
            ("Book a new appointment", $"{clientUrl}/patient/book")));

    public static (string Subject, string Html) Reminder(AppointmentDto a, string patientName, string clientUrl) => (
        $"Reminder: appointment {When(a.StartTime)}",
        Layout(patientName,
            "This is a reminder about your upcoming appointment.",
            Details(a),
            "Can't make it? Please reschedule or cancel so another patient can have the time.",
            ("Manage my appointment", $"{clientUrl}/patient/appointments")));

    public static (string Subject, string Html) ReviewRequest(AppointmentDto a, string patientName, string clientUrl) => (
        $"How was your visit with {a.DoctorName}?",
        Layout(patientName,
            $"Thanks for visiting {Encode(a.DoctorName)}. Your feedback helps other patients choose a doctor.",
            Details(a),
            "It takes less than a minute.",
            ("Rate your visit", $"{clientUrl}/patient/appointments?tab=past")));

    public static string When(DateTime start) => start.ToString("ddd d MMM yyyy, h:mm tt", CultureInfo.InvariantCulture);

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string Details(AppointmentDto a) => $"""
        <table style="border-collapse:collapse;margin:16px 0;font-size:15px">
          <tr><td style="padding:4px 16px 4px 0;color:#4d625d">When</td><td style="padding:4px 0"><strong>{When(a.StartTime)}</strong></td></tr>
          <tr><td style="padding:4px 16px 4px 0;color:#4d625d">Doctor</td><td style="padding:4px 0">{Encode(a.DoctorName)}, {Encode(a.SpecializationName)}</td></tr>
          <tr><td style="padding:4px 16px 4px 0;color:#4d625d">Reason</td><td style="padding:4px 0">{Encode(a.Reason)}</td></tr>
        </table>
        """;

    private static string Layout(string name, string intro, string details, string note, (string Label, string Url) button) => $"""
        <div style="font-family:Arial,Helvetica,sans-serif;color:#15302a;max-width:560px;margin:0 auto;padding:24px">
          <p style="font-size:18px;font-weight:bold;color:#1d5c4f;margin:0 0 24px">Lakeview Clinic</p>
          <p>Hello {Encode(name)},</p>
          <p>{intro}</p>
          {details}
          <p>{note}</p>
          <p style="margin:28px 0">
            <a href="{button.Url}" style="background:#1d5c4f;color:#ffffff;padding:12px 20px;border-radius:6px;text-decoration:none;font-weight:bold">{button.Label}</a>
          </p>
          <p style="color:#4d625d;font-size:13px">You're receiving this because you have an appointment at Lakeview Clinic.</p>
        </div>
        """;
}
