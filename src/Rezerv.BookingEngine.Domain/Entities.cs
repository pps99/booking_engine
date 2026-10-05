namespace Rezerv.BookingEngine.Domain;

public sealed class Business
{
    public int Id { get; set; }
    public required string Name { get; set; }
}

public sealed class Customer
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
}

public sealed class PackageDefinition
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public Business Business { get; set; } = null!;
    public required string Name { get; set; }
    public int TotalCredits { get; set; }
    public int ValidityDays { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class CustomerPackage
{
    public long Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int PackageDefinitionId { get; set; }
    public PackageDefinition PackageDefinition { get; set; } = null!;
    public int BusinessId { get; set; }
    public Business Business { get; set; } = null!;
    public int TotalCredits { get; set; }
    public int RemainingCredits { get; set; }
    public DateTimeOffset PurchasedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public uint Version { get; set; }
}

public sealed class TimetableSchedule
{
    public long Id { get; set; }
    public int BusinessId { get; set; }
    public Business Business { get; set; } = null!;
    public required string ClassName { get; set; }
    public required string InstructorName { get; set; }
    public DateTimeOffset StartTimeUtc { get; set; }
    public DateTimeOffset EndTimeUtc { get; set; }
    public int Capacity { get; set; }
    public uint Version { get; set; }
    public ICollection<Booking> Bookings { get; set; } = [];
    public ICollection<WaitlistEntry> WaitlistEntries { get; set; } = [];
}

public sealed class Booking
{
    public long Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public long ScheduleId { get; set; }
    public TimetableSchedule Schedule { get; set; } = null!;
    public long CustomerPackageId { get; set; }
    public CustomerPackage CustomerPackage { get; set; } = null!;
    public BookingStatus Status { get; set; }
    public DateTimeOffset BookedAtUtc { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public bool CreditRefunded { get; set; }
    public long? PromotedFromWaitlistEntryId { get; set; }
}

public sealed class WaitlistEntry
{
    public long Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public long ScheduleId { get; set; }
    public TimetableSchedule Schedule { get; set; } = null!;
    public long PreferredCustomerPackageId { get; set; }
    public CustomerPackage PreferredCustomerPackage { get; set; } = null!;
    public WaitlistStatus Status { get; set; }
    public DateTimeOffset JoinedAtUtc { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }
    public long? PromotedBookingId { get; set; }
    public string? ResolutionReason { get; set; }
}

public enum BookingStatus
{
    Confirmed = 1,
    Cancelled = 2
}

public enum WaitlistStatus
{
    Active = 1,
    Promoted = 2,
    Expired = 3,
    Cancelled = 4
}
