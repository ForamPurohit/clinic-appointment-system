using ClinicAppointment.API.Data;
using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Models;
using ClinicAppointment.API.Services;
using ClinicAppointment.API.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace ClinicAppointment.Tests.Helpers;

/// <summary>
/// A fresh in-memory clinic for each test.
///
/// Seeded data:
///   Specializations: General Medicine (2 doctors), Cardiology (none)
///   Doctor          Mon-Fri, 09:00-12:00, 30-minute slots, active
///   InactiveDoctor  same hours, deactivated
///   Patient, OtherPatient, Admin
///
/// "Now" is fixed at Monday 19 Oct 2026, 08:00 (UTC is used as local time).
/// Every user's password is <see cref="Password"/>.
/// </summary>
public sealed class TestData : IDisposable
{
    public static readonly DateTime Monday = new(2026, 10, 19);
    public const string Password = "Secret@123";

    // Low work factor keeps hashing fast in tests
    private static readonly string PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4);

    private readonly DbContextOptions<AppDbContext> _options;

    public AppDbContext Db { get; }
    public FakeTimeProvider Clock { get; }

    public Specialization GeneralMedicine { get; }
    public Specialization Cardiology { get; }
    public Doctor Doctor { get; }
    public Doctor InactiveDoctor { get; }
    public Patient Patient { get; }
    public Patient OtherPatient { get; }
    public User Admin { get; }

    public TestData()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"clinic-tests-{Guid.NewGuid()}")
            .Options;
        Db = new AppDbContext(_options);

        Clock = new FakeTimeProvider(new DateTimeOffset(Monday.AddHours(8), TimeSpan.Zero));
        Clock.SetLocalTimeZone(TimeZoneInfo.Utc);

        GeneralMedicine = new Specialization { Name = "General Medicine", Description = "Check-ups" };
        Cardiology = new Specialization { Name = "Cardiology", Description = "Heart" };

        Doctor = NewDoctor("Dr. Active", "dr@test.com", GeneralMedicine);
        InactiveDoctor = NewDoctor("Dr. Retired", "retired@test.com", GeneralMedicine);
        InactiveDoctor.IsActive = false;

        Patient = NewPatient("Pat One", "p1@test.com");
        OtherPatient = NewPatient("Pat Two", "p2@test.com");

        Admin = new User { FullName = "Admin", Email = "admin@test.com", Role = Roles.Admin, PasswordHash = PasswordHash };

        Db.AddRange(GeneralMedicine, Cardiology, Doctor, InactiveDoctor, Patient, OtherPatient, Admin);
        Db.SaveChanges();
    }

    // ---------- services wired to this clinic ----------

    /// <summary>Events raised by services created through this fixture.</summary>
    public RecordingEvents Events { get; } = new();

    public AppointmentService Appointments() => new(Db, Clock, Events);
    public LeaveService Leaves() => new(Db, Clock, Events);
    public ReviewService Reviews() => new(Db, Clock);
    public DoctorService Doctors() => new(Db, Clock);
    public SpecializationService Specializations() => new(Db);
    public AdminService AdminService() => new(Db, Clock);

    public static JwtSettings Jwt => new()
    {
        Key = "unit-test-signing-key-that-is-definitely-long-enough-123",
        Issuer = "test-issuer",
        Audience = "test-audience",
        ExpiryMinutes = 60
    };

    public TokenService Tokens() => new(Options.Create(Jwt));
    public AuthService Auth() => new(Db, Tokens());

    /// <summary>A separate context, to prove changes were really saved.</summary>
    public AppDbContext NewContext() => new(_options);

    /// <summary>Moves "now" forward. FakeTimeProvider can't go backwards.</summary>
    public void SetNow(DateTime local) => Clock.SetUtcNow(new DateTimeOffset(local, TimeSpan.Zero));

    // ---------- builders ----------

    /// <summary>Inserts an appointment directly, skipping booking rules (useful for past visits).</summary>
    public async Task<Appointment> AddAppointmentAsync(
        Doctor doctor, Patient patient, DateTime start,
        AppointmentStatus status = AppointmentStatus.Scheduled, string reason = "Check-up")
    {
        var appointment = new Appointment
        {
            DoctorId = doctor.Id,
            PatientId = patient.Id,
            StartTime = start,
            EndTime = start.AddMinutes(doctor.SlotDurationMinutes),
            Reason = reason,
            Status = status
        };
        Db.Appointments.Add(appointment);
        await Db.SaveChangesAsync();
        return appointment;
    }

    public async Task<DoctorLeave> AddLeaveAsync(Doctor doctor, DateTime firstDay, DateTime lastDay, string? reason = "Holiday")
    {
        var leave = new DoctorLeave
        {
            DoctorId = doctor.Id,
            StartDate = DateOnly.FromDateTime(firstDay),
            EndDate = DateOnly.FromDateTime(lastDay),
            Reason = reason
        };
        Db.DoctorLeaves.Add(leave);
        await Db.SaveChangesAsync();
        return leave;
    }

    public async Task<Review> AddReviewAsync(Appointment appointment, int rating, string? comment = null)
    {
        var review = new Review
        {
            AppointmentId = appointment.Id,
            DoctorId = appointment.DoctorId,
            PatientId = appointment.PatientId,
            Rating = rating,
            Comment = comment
        };
        Db.Reviews.Add(review);
        await Db.SaveChangesAsync();
        return review;
    }

    public async Task<Doctor> AddDoctorAsync(string name, string email, Specialization specialization)
    {
        var doctor = NewDoctor(name, email, specialization);
        Db.Doctors.Add(doctor);
        await Db.SaveChangesAsync();
        return doctor;
    }

    public CreateAppointmentRequest Booking(DateTime start, Doctor? doctor = null) => new()
    {
        DoctorId = (doctor ?? Doctor).Id,
        StartTime = start,
        Reason = "Routine check-up"
    };

    public static CreateDoctorRequest NewDoctorRequest(int specializationId) => new()
    {
        FullName = "Dr. New Hire",
        Email = "New.Hire@Clinic.com",
        Password = "Doctor@123",
        PhoneNumber = "9999999999",
        SpecializationId = specializationId,
        Bio = "Joined this year",
        YearsOfExperience = 3,
        ConsultationFee = 700,
        WorkStartTime = "10:00",
        WorkEndTime = "14:00",
        SlotDurationMinutes = 20,
        WorkingDays = new List<int> { 1, 3, 5 }
    };

    private static Doctor NewDoctor(string name, string email, Specialization specialization) => new()
    {
        User = new User { FullName = name, Email = email, Role = Roles.Doctor, PasswordHash = PasswordHash },
        Specialization = specialization,
        WorkStartTime = new TimeOnly(9, 0),
        WorkEndTime = new TimeOnly(12, 0),
        SlotDurationMinutes = 30,
        WorkingDays = "1,2,3,4,5",
        ConsultationFee = 500,
        YearsOfExperience = 5
    };

    private static Patient NewPatient(string name, string email) => new()
    {
        User = new User { FullName = name, Email = email, Role = Roles.Patient, PasswordHash = PasswordHash },
        DateOfBirth = new DateOnly(1990, 1, 1)
    };

    public void Dispose() => Db.Dispose();
}
