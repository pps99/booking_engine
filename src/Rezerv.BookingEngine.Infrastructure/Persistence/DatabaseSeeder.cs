using Microsoft.EntityFrameworkCore;
using Rezerv.BookingEngine.Domain;

namespace Rezerv.BookingEngine.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(
        BookingDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken = default)
    {
        if (await db.Businesses.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var baseDay = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).AddDays(3);

        var businesses = new[]
        {
            new Business { Id = 1, Name = "Rezerv Fitness" },
            new Business { Id = 2, Name = "Balance Yoga Studio" }
        };
        var customers = Enumerable.Range(1, 10)
            .Select(id => new Customer
            {
                Id = id,
                Name = $"Demo Customer {id}",
                Email = $"customer{id}@example.com"
            })
            .ToArray();
        var definitions = new[]
        {
            new PackageDefinition
            {
                Id = 1, BusinessId = 1, Name = "Fitness 10 Credits",
                TotalCredits = 10, ValidityDays = 90, Price = 99m
            },
            new PackageDefinition
            {
                Id = 2, BusinessId = 1, Name = "Fitness 5 Credits",
                TotalCredits = 5, ValidityDays = 30, Price = 55m
            },
            new PackageDefinition
            {
                Id = 3, BusinessId = 2, Name = "Yoga 10 Credits",
                TotalCredits = 10, ValidityDays = 60, Price = 120m
            }
        };
        var packages = Enumerable.Range(1, 10)
            .Select(id => new CustomerPackage
            {
                Id = id,
                CustomerId = id,
                PackageDefinitionId = id == 10 ? 3 : 1,
                BusinessId = id == 10 ? 2 : 1,
                TotalCredits = 10,
                RemainingCredits = id == 9 ? 0 : 10,
                PurchasedAtUtc = now.AddDays(-10),
                ExpiresAtUtc = id == 10 ? now.AddDays(-1) : now.AddDays(80),
                Version = 1
            })
            .ToArray();
        var schedules = new[]
        {
            Schedule(1, 1, "Morning Yoga", "John Doe", baseDay.AddHours(9), 60, 15),
            Schedule(2, 1, "HIIT Express", "Amelia Tan", baseDay.AddHours(10), 45, 8),
            Schedule(3, 1, "Full Spin Class", "Marcus Lee", baseDay.AddHours(12), 60, 2),
            Schedule(4, 1, "Pilates", "Sarah Lim", baseDay.AddHours(14), 60, 10),
            Schedule(5, 1, "Strength Basics", "John Doe", baseDay.AddHours(16), 60, 6),
            Schedule(6, 2, "Vinyasa Flow", "Nina Chen", baseDay.AddDays(1).AddHours(9), 75, 12),
            Schedule(7, 2, "Yin Yoga", "Nina Chen", baseDay.AddDays(1).AddHours(11), 60, 10),
            Schedule(8, 2, "Meditation", "Oliver Wong", baseDay.AddDays(1).AddHours(13), 45, 20),
            Schedule(9, 2, "Power Yoga", "Maya Singh", baseDay.AddDays(2).AddHours(10), 60, 8),
            Schedule(10, 2, "Mobility", "Maya Singh", baseDay.AddDays(2).AddHours(12), 45, 10)
        };
        var bookings = new[]
        {
            new Booking
            {
                Id = 1, CustomerId = 2, ScheduleId = 3, CustomerPackageId = 2,
                Status = BookingStatus.Confirmed, BookedAtUtc = now.AddDays(-1)
            },
            new Booking
            {
                Id = 2, CustomerId = 3, ScheduleId = 3, CustomerPackageId = 3,
                Status = BookingStatus.Confirmed, BookedAtUtc = now.AddHours(-20)
            }
        };
        packages[1].RemainingCredits--;
        packages[2].RemainingCredits--;
        var waitlist = new[]
        {
            new WaitlistEntry
            {
                Id = 1, CustomerId = 4, ScheduleId = 3, PreferredCustomerPackageId = 4,
                Status = WaitlistStatus.Active, JoinedAtUtc = now.AddHours(-4)
            },
            new WaitlistEntry
            {
                Id = 2, CustomerId = 5, ScheduleId = 3, PreferredCustomerPackageId = 5,
                Status = WaitlistStatus.Active, JoinedAtUtc = now.AddHours(-3)
            }
        };

        db.AddRange(businesses);
        db.AddRange(customers);
        db.AddRange(definitions);
        db.AddRange(packages);
        db.AddRange(schedules);
        db.AddRange(bookings);
        db.AddRange(waitlist);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static TimetableSchedule Schedule(
        long id, int businessId, string name, string instructor,
        DateTimeOffset start, int durationMinutes, int capacity) => new()
        {
            Id = id,
            BusinessId = businessId,
            ClassName = name,
            InstructorName = instructor,
            StartTimeUtc = start,
            EndTimeUtc = start.AddMinutes(durationMinutes),
            Capacity = capacity,
            Version = 1
        };
}

