using ClinicAppointment.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicAppointment.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Specialization> Specializations => Set<Specialization>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<DoctorLeave> DoctorLeaves => Set<DoctorLeave>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.FullName).HasMaxLength(100).IsRequired();
            e.Property(u => u.Email).HasMaxLength(150).IsRequired();
            e.Property(u => u.Role).HasMaxLength(20).IsRequired();
            e.Property(u => u.PhoneNumber).HasMaxLength(20);
        });

        modelBuilder.Entity<Specialization>(e =>
        {
            e.HasIndex(s => s.Name).IsUnique();
            e.Property(s => s.Name).HasMaxLength(100).IsRequired();
            e.Property(s => s.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<Doctor>(e =>
        {
            e.HasOne(d => d.User)
             .WithOne(u => u.Doctor)
             .HasForeignKey<Doctor>(d => d.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(d => d.Specialization)
             .WithMany(s => s.Doctors)
             .HasForeignKey(d => d.SpecializationId)
             .OnDelete(DeleteBehavior.Restrict);

            e.Property(d => d.ConsultationFee).HasPrecision(10, 2);
            e.Property(d => d.Bio).HasMaxLength(1000);
            e.Property(d => d.WorkingDays).HasMaxLength(20);
        });

        modelBuilder.Entity<Patient>(e =>
        {
            e.HasOne(p => p.User)
             .WithOne(u => u.Patient)
             .HasForeignKey<Patient>(p => p.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.Property(p => p.Gender).HasMaxLength(20);
            e.Property(p => p.Address).HasMaxLength(250);
        });

        modelBuilder.Entity<Appointment>(e =>
        {
            e.HasOne(a => a.Doctor)
             .WithMany(d => d.Appointments)
             .HasForeignKey(a => a.DoctorId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Patient)
             .WithMany(p => p.Appointments)
             .HasForeignKey(a => a.PatientId)
             .OnDelete(DeleteBehavior.Restrict);

            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.Reason).HasMaxLength(500).IsRequired();
            e.Property(a => a.Notes).HasMaxLength(1000);

            // Database-level guard against double booking:
            // a doctor can only have one active (non-cancelled) appointment per start time.
            e.HasIndex(a => new { a.DoctorId, a.StartTime })
             .IsUnique()
             .HasFilter("[Status] <> 'Cancelled'");

            // Used by the reminder job to find appointments still needing a reminder
            e.HasIndex(a => new { a.Status, a.StartTime });
        });

        modelBuilder.Entity<DoctorLeave>(e =>
        {
            e.HasOne(l => l.Doctor)
             .WithMany(d => d.Leaves)
             .HasForeignKey(l => l.DoctorId)
             .OnDelete(DeleteBehavior.Cascade);

            e.Property(l => l.Reason).HasMaxLength(200);
            e.HasIndex(l => new { l.DoctorId, l.StartDate, l.EndDate });
        });

        modelBuilder.Entity<Review>(e =>
        {
            e.HasOne(r => r.Appointment)
             .WithOne(a => a.Review)
             .HasForeignKey<Review>(r => r.AppointmentId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(r => r.Doctor)
             .WithMany(d => d.Reviews)
             .HasForeignKey(r => r.DoctorId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.Patient)
             .WithMany()
             .HasForeignKey(r => r.PatientId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(r => r.AppointmentId).IsUnique();
            e.Property(r => r.Comment).HasMaxLength(1000);
            e.ToTable(t => t.HasCheckConstraint("CK_Review_Rating", "[Rating] BETWEEN 1 AND 5"));
        });
    }
}
