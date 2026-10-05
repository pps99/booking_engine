namespace Rezerv.BookingEngine.Application;

public sealed record PackageDefinitionResponse(
    int Id, int BusinessId, string BusinessName, string Name,
    int TotalCredits, int ValidityDays, decimal Price);

public sealed record PurchasePackageRequest(int CustomerId, int PackageDefinitionId);

public sealed record CustomerPackageResponse(
    long Id, int CustomerId, int BusinessId, string PackageName,
    int TotalCredits, int RemainingCredits, DateTimeOffset ExpiresAtUtc);

public sealed record TimetableResponse(
    long ScheduleId, string ClassName, string InstructorName,
    DateTimeOffset StartTime, DateTimeOffset EndTime,
    int AttendanceCount, int AvailableSlots, int Capacity,
    int BusinessId, string BusinessName, bool IsFull);

public sealed record BookClassRequest(
    int CustomerId, long ScheduleId, long CustomerPackageId,
    bool JoinWaitlistIfFull = false);

public sealed record JoinWaitlistRequest(
    int CustomerId, long ScheduleId, long CustomerPackageId);

public sealed record CancelBookingRequest(int CustomerId, long BookingId);

public sealed record BookingResponse(
    string Status, long? BookingId, long? WaitlistEntryId,
    long ScheduleId, int RemainingCredits, string Message);

public sealed record CancellationResponse(
    long BookingId, bool CreditRefunded, long? PromotedBookingId,
    string Message);

public interface IBookingEngine
{
    Task<IReadOnlyList<PackageDefinitionResponse>> GetPackagesAsync(
        int? businessId, CancellationToken cancellationToken);

    Task<CustomerPackageResponse> PurchasePackageAsync(
        PurchasePackageRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<TimetableResponse>> GetTimetableAsync(
        int? businessId, DateOnly? date, CancellationToken cancellationToken);

    Task<BookingResponse> BookAsync(
        BookClassRequest request, CancellationToken cancellationToken);

    Task<BookingResponse> JoinWaitlistAsync(
        JoinWaitlistRequest request, CancellationToken cancellationToken);

    Task<CancellationResponse> CancelAsync(
        CancelBookingRequest request, CancellationToken cancellationToken);
}

