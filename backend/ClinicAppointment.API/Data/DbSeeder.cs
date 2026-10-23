using ClinicAppointment.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicAppointment.API.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        // Real databases use migrations; the in-memory provider used by integration tests can't.
        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }

        if (await db.Users.AnyAsync())
        {
            return;
        }

        logger.LogInformation("Seeding initial data...");

        var specializations = new List<Specialization>
        {
            new() { Name = "General Medicine", Description = "Routine check-ups, common illnesses and referrals." },
            new() { Name = "Cardiology", Description = "Heart and blood vessel conditions." },
            new() { Name = "Dermatology", Description = "Skin, hair and nail conditions." },
            new() { Name = "Pediatrics", Description = "Medical care for infants, children and teens." },
            new() { Name = "Orthopedics", Description = "Bones, joints, muscles and sports injuries." }
        };
        db.Specializations.AddRange(specializations);

        db.Users.Add(new User
        {
            FullName = "Clinic Administrator",
            Email = "admin@clinic.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Role = Roles.Admin
        });

        var doctors = new[]
        {
            new Doctor
            {
                User = new User
                {
                    FullName = "Dr. Priya Sharma",
                    Email = "doctor@clinic.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Doctor@123"),
                    Role = Roles.Doctor,
                    PhoneNumber = "9876500001"
                },
                Specialization = specializations[0],
                Bio = "Family physician focused on preventive care.",
                YearsOfExperience = 8,
                ConsultationFee = 500,
                WorkStartTime = new TimeOnly(9, 0),
                WorkEndTime = new TimeOnly(13, 0),
                SlotDurationMinutes = 30,
                WorkingDays = "1,2,3,4,5"
            },
            new Doctor
            {
                User = new User
                {
                    FullName = "Dr. Arjun Mehta",
                    Email = "cardio@clinic.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Doctor@123"),
                    Role = Roles.Doctor,
                    PhoneNumber = "9876500002"
                },
                Specialization = specializations[1],
                Bio = "Interventional cardiologist with a focus on heart failure.",
                YearsOfExperience = 15,
                ConsultationFee = 1200,
                WorkStartTime = new TimeOnly(14, 0),
                WorkEndTime = new TimeOnly(18, 0),
                SlotDurationMinutes = 20,
                WorkingDays = "1,3,5,6"
            }
        };
        db.Doctors.AddRange(doctors);

        var patient = new Patient
        {
            User = new User
            {
                FullName = "Rahul Verma",
                Email = "patient@clinic.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Patient@123"),
                Role = Roles.Patient,
                PhoneNumber = "9876500100"
            },
            DateOfBirth = new DateOnly(1995, 6, 15),
            Gender = "Male"
        };
        db.Patients.Add(patient);

        // A past visit with a review, so doctor ratings show up in the demo
        var lastWeek = DateTime.Today.AddDays(-7).AddHours(12).AddMinutes(30);
        db.Appointments.Add(new Appointment
        {
            Doctor = doctors[0],
            Patient = patient,
            StartTime = lastWeek,
            EndTime = lastWeek.AddMinutes(doctors[0].SlotDurationMinutes),
            Reason = "Seasonal allergies",
            Notes = "Prescribed antihistamines. Follow up if symptoms persist.",
            Status = AppointmentStatus.Completed,
            Review = new Review
            {
                Doctor = doctors[0],
                Patient = patient,
                Rating = 5,
                Comment = "Listened carefully and explained everything clearly."
            }
        });

        await db.SaveChangesAsync();
        logger.LogInformation("Seeding complete.");
    }
}
