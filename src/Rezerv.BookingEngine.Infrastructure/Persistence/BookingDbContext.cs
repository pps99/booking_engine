using Microsoft.EntityFrameworkCore;
using Rezerv.BookingEngine.Domain;

namespace Rezerv.BookingEngine.Infrastructure.Persistence;

public sealed class BookingDbContext(DbContextOptions<BookingDbContext> options)
    : DbContext(options)
{
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<PackageDefinition> PackageDefinitions => Set<PackageDefinition>();
    public DbSet<CustomerPackage> CustomerPackages => Set<CustomerPackage>();
    public DbSet<TimetableSchedule> TimetableSchedules => Set<TimetableSchedule>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Business>(entity =>
        {
            entity.ToTable("businesses");
            entity.Property(x => x.Name).HasMaxLength(150);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.Property(x => x.Name).HasMaxLength(150);
            entity.Property(x => x.Email).HasMaxLength(254);
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<PackageDefinition>(entity =>
        {
            entity.ToTable("package_definitions");
            entity.Property(x => x.Name).HasMaxLength(150);
            entity.Property(x => x.Price).HasPrecision(10, 2);
            entity.HasIndex(x => new { x.BusinessId, x.IsActive });
        });

        modelBuilder.Entity<CustomerPackage>(entity =>
        {
            entity.ToTable("customer_packages");
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasIndex(x => new { x.CustomerId, x.BusinessId, x.ExpiresAtUtc });
        });

        modelBuilder.Entity<TimetableSchedule>(entity =>
        {
            entity.ToTable("timetable_schedules");
            entity.Property(x => x.ClassName).HasMaxLength(150);
            entity.Property(x => x.InstructorName).HasMaxLength(150);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasIndex(x => new { x.BusinessId, x.StartTimeUtc });
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.ToTable("bookings");
            entity.HasIndex(x => new { x.ScheduleId, x.Status });
            entity.HasIndex(x => new { x.CustomerId, x.Status });
            entity.HasIndex(x => x.PromotedFromWaitlistEntryId).IsUnique();
        });

        modelBuilder.Entity<WaitlistEntry>(entity =>
        {
            entity.ToTable("waitlist_entries");
            entity.Property(x => x.ResolutionReason).HasMaxLength(300);
            entity.HasIndex(x => new { x.ScheduleId, x.Status, x.JoinedAtUtc });
            entity.HasIndex(x => new { x.CustomerId, x.ScheduleId, x.Status });
        });
    }
}

