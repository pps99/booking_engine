using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Rezerv.BookingEngine.Application;
using Rezerv.BookingEngine.Domain;
using Rezerv.BookingEngine.Infrastructure.Persistence;
using Rezerv.BookingEngine.Infrastructure.Redis;

namespace Rezerv.BookingEngine.Infrastructure.Services;

public sealed class BookingEngineService(
    BookingDbContext dbContext,
    IDistributedLockProvider lockProvider,
    IRedisClient redis,
    TimeProvider timeProvider) : IBookingEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly BookingDbContext _db = dbContext;

    public async Task<IReadOnlyList<PackageDefinitionResponse>> GetPackagesAsync(
        int? businessId, CancellationToken cancellationToken)
    {
        var query = _db.PackageDefinitions.AsNoTracking().Where(x => x.IsActive);
        if (businessId.HasValue)
        {
            query = query.Where(x => x.BusinessId == businessId.Value);
        }

        return await query
            .OrderBy(x => x.Business.Name)
            .ThenBy(x => x.Name)
            .Select(x => new PackageDefinitionResponse(
                x.Id, x.BusinessId, x.Business.Name, x.Name,
                x.TotalCredits, x.ValidityDays, x.Price))
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerPackageResponse> PurchasePackageAsync(
        PurchasePackageRequest request, CancellationToken cancellationToken)
    {
        var customerExists = await _db.Customers
            .AnyAsync(x => x.Id == request.CustomerId, cancellationToken);
        if (!customerExists)
        {
            throw NotFound("customer_not_found", "Customer was not found.");
        }

        var definition = await _db.PackageDefinitions
            .SingleOrDefaultAsync(x => x.Id == request.PackageDefinitionId && x.IsActive, cancellationToken)
            ?? throw NotFound("package_not_found", "Package definition was not found.");

        var now = timeProvider.GetUtcNow();
        var package = new CustomerPackage
        {
            CustomerId = request.CustomerId,
            PackageDefinitionId = definition.Id,
            BusinessId = definition.BusinessId,
            TotalCredits = definition.TotalCredits,
            RemainingCredits = definition.TotalCredits,
            PurchasedAtUtc = now,
            ExpiresAtUtc = now.AddDays(definition.ValidityDays),
            Version = 1
        };

        _db.CustomerPackages.Add(package);
        await _db.SaveChangesAsync(cancellationToken);

        return new CustomerPackageResponse(
            package.Id, package.CustomerId, package.BusinessId, definition.Name,
            package.TotalCredits, package.RemainingCredits, package.ExpiresAtUtc);
    }

    public async Task<IReadOnlyList<TimetableResponse>> GetTimetableAsync(
        int? businessId, DateOnly? date, CancellationToken cancellationToken)
    {
        var version = await TryGetCacheVersionAsync(cancellationToken);
        var cacheKey = $"rezerv:timetable:v{version}:business:{businessId?.ToString() ?? "all"}:date:{date?.ToString("yyyy-MM-dd") ?? "all"}";

        try
        {
            var cached = await redis.GetStringAsync(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return JsonSerializer.Deserialize<List<TimetableResponse>>(cached, JsonOptions) ?? [];
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Timetable reads degrade to MySQL; booking correctness never depends on this cache.
        }

        var query = _db.TimetableSchedules.AsNoTracking();
        if (businessId.HasValue)
        {
            query = query.Where(x => x.BusinessId == businessId.Value);
        }

        if (date.HasValue)
        {
            var from = new DateTimeOffset(date.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var until = from.AddDays(1);
            query = query.Where(x => x.StartTimeUtc >= from && x.StartTimeUtc < until);
        }

        var result = await query
            .OrderBy(x => x.StartTimeUtc)
            .Select(x => new TimetableResponse(
                x.Id,
                x.ClassName,
                x.InstructorName,
                x.StartTimeUtc,
                x.EndTimeUtc,
                x.Bookings.Count(b => b.Status == BookingStatus.Confirmed),
                Math.Max(0, x.Capacity - x.Bookings.Count(b => b.Status == BookingStatus.Confirmed)),
                x.Capacity,
                x.BusinessId,
                x.Business.Name,
                x.Bookings.Count(b => b.Status == BookingStatus.Confirmed) >= x.Capacity))
            .ToListAsync(cancellationToken);

        try
        {
            await redis.SetStringAsync(
                cacheKey, JsonSerializer.Serialize(result, JsonOptions),
                TimeSpan.FromMinutes(2), cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Cache availability must not make the read endpoint unavailable.
        }

        return result;
    }

    public async Task<BookingResponse> BookAsync(
        BookClassRequest request, CancellationToken cancellationToken)
    {
        await using var scheduleLock = await lockProvider.AcquireAsync(
            $"schedule:{request.ScheduleId}", cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        try
        {
            var schedule = await GetScheduleAsync(request.ScheduleId, cancellationToken);
            EnsureScheduleCanBeBooked(schedule);
            var package = await GetPackageAsync(request.CustomerPackageId, cancellationToken);
            EnsurePackageCanBook(package, request.CustomerId, schedule.BusinessId);
            await EnsureNoExistingOrOverlappingBookingAsync(request.CustomerId, schedule, cancellationToken);

            var attendance = await _db.Bookings.CountAsync(
                x => x.ScheduleId == schedule.Id && x.Status == BookingStatus.Confirmed,
                cancellationToken);

            if (attendance >= schedule.Capacity)
            {
                if (!request.JoinWaitlistIfFull)
                {
                    throw Conflict("schedule_full", "The schedule is full. Join the waitlist instead.");
                }

                var waitlist = await AddWaitlistEntryAsync(
                    request.CustomerId, schedule, package, cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new BookingResponse(
                    "waitlisted", null, waitlist.Id, schedule.Id, package.RemainingCredits,
                    "The schedule is full; the customer joined the waitlist without a credit deduction.");
            }

            package.RemainingCredits--;
            package.Version++;
            schedule.Version++;
            var booking = new Booking
            {
                CustomerId = request.CustomerId,
                ScheduleId = schedule.Id,
                CustomerPackageId = package.Id,
                Status = BookingStatus.Confirmed,
                BookedAtUtc = timeProvider.GetUtcNow()
            };
            _db.Bookings.Add(booking);

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await InvalidateTimetableAsync(cancellationToken);

            return new BookingResponse(
                "booked", booking.Id, null, schedule.Id, package.RemainingCredits,
                "Booking confirmed and one package credit deducted.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new BookingEngineException(
                "concurrent_update", "The booking state changed. Please retry.", 409);
        }
        catch (MySqlException exception) when (exception.Number is 1205 or 1213)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new BookingEngineException(
                "transaction_conflict", "The booking conflicted with another request. Please retry.", 409);
        }
    }

    public async Task<BookingResponse> JoinWaitlistAsync(
        JoinWaitlistRequest request, CancellationToken cancellationToken)
    {
        await using var scheduleLock = await lockProvider.AcquireAsync(
            $"schedule:{request.ScheduleId}", cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var schedule = await GetScheduleAsync(request.ScheduleId, cancellationToken);
        EnsureScheduleCanBeBooked(schedule);
        var package = await GetPackageAsync(request.CustomerPackageId, cancellationToken);
        EnsurePackageCanBook(package, request.CustomerId, schedule.BusinessId);
        await EnsureNoExistingOrOverlappingBookingAsync(request.CustomerId, schedule, cancellationToken);

        var attendance = await _db.Bookings.CountAsync(
            x => x.ScheduleId == schedule.Id && x.Status == BookingStatus.Confirmed,
            cancellationToken);
        if (attendance < schedule.Capacity)
        {
            throw Conflict("schedule_not_full", "The schedule has space; book it directly.");
        }

        var entry = await AddWaitlistEntryAsync(
            request.CustomerId, schedule, package, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new BookingResponse(
            "waitlisted", null, entry.Id, schedule.Id, package.RemainingCredits,
            "Added to the FIFO waitlist. No credit is reserved or deducted.");
    }

    public async Task<CancellationResponse> CancelAsync(
        CancelBookingRequest request, CancellationToken cancellationToken)
    {
        var scheduleId = await _db.Bookings.AsNoTracking()
            .Where(x => x.Id == request.BookingId && x.CustomerId == request.CustomerId)
            .Select(x => (long?)x.ScheduleId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("booking_not_found", "Booking was not found for this customer.");

        await using var scheduleLock = await lockProvider.AcquireAsync(
            $"schedule:{scheduleId}", cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        try
        {
            var booking = await _db.Bookings
                .Include(x => x.Schedule)
                .Include(x => x.CustomerPackage)
                .SingleAsync(x => x.Id == request.BookingId && x.CustomerId == request.CustomerId,
                    cancellationToken);

            if (booking.Status != BookingStatus.Confirmed)
            {
                throw Conflict("booking_not_active", "Only a confirmed booking can be cancelled.");
            }

            var now = timeProvider.GetUtcNow();
            if (now >= booking.Schedule.StartTimeUtc)
            {
                throw Conflict("class_started", "A class that has started cannot be cancelled.");
            }

            var refund = BookingPolicy.ShouldRefund(now, booking.Schedule.StartTimeUtc);
            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAtUtc = now;
            booking.CreditRefunded = refund;
            booking.Schedule.Version++;
            if (refund)
            {
                booking.CustomerPackage.RemainingCredits++;
                booking.CustomerPackage.Version++;
            }

            var promotedBookingId = await PromoteFirstEligibleWaitlistEntryAsync(
                booking.Schedule, now, cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await InvalidateTimetableAsync(cancellationToken);

            return new CancellationResponse(
                booking.Id,
                refund,
                promotedBookingId,
                refund
                    ? "Booking cancelled and one credit refunded."
                    : "Booking cancelled within four hours of class start; no credit was refunded.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new BookingEngineException(
                "concurrent_update", "The booking state changed. Please retry.", 409);
        }
        catch (MySqlException exception) when (exception.Number is 1205 or 1213)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new BookingEngineException(
                "transaction_conflict", "The cancellation conflicted with another request. Please retry.", 409);
        }
    }

    private async Task<long?> PromoteFirstEligibleWaitlistEntryAsync(
        TimetableSchedule schedule, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var entries = await _db.WaitlistEntries
            .Include(x => x.PreferredCustomerPackage)
            .Where(x => x.ScheduleId == schedule.Id && x.Status == WaitlistStatus.Active)
            .OrderBy(x => x.JoinedAtUtc)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var entry in entries)
        {
            var reason = BookingPolicy.ValidatePackage(
                entry.PreferredCustomerPackage, entry.CustomerId, schedule.BusinessId, now);

            if (reason is null)
            {
                var overlaps = await HasOverlappingBookingAsync(entry.CustomerId, schedule, cancellationToken);
                if (overlaps)
                {
                    reason = "Customer now has an overlapping confirmed booking.";
                }
            }

            if (reason is not null)
            {
                entry.Status = WaitlistStatus.Expired;
                entry.ResolvedAtUtc = now;
                entry.ResolutionReason = reason;
                continue;
            }

            entry.PreferredCustomerPackage.RemainingCredits--;
            entry.PreferredCustomerPackage.Version++;
            schedule.Version++;
            var promoted = new Booking
            {
                CustomerId = entry.CustomerId,
                ScheduleId = schedule.Id,
                CustomerPackageId = entry.PreferredCustomerPackageId,
                Status = BookingStatus.Confirmed,
                BookedAtUtc = now,
                PromotedFromWaitlistEntryId = entry.Id
            };
            _db.Bookings.Add(promoted);
            entry.Status = WaitlistStatus.Promoted;
            entry.ResolvedAtUtc = now;
            entry.ResolutionReason = "Promoted from the FIFO waitlist.";

            await _db.SaveChangesAsync(cancellationToken);
            entry.PromotedBookingId = promoted.Id;
            return promoted.Id;
        }

        return null;
    }

    private async Task<WaitlistEntry> AddWaitlistEntryAsync(
        int customerId,
        TimetableSchedule schedule,
        CustomerPackage package,
        CancellationToken cancellationToken)
    {
        var exists = await _db.WaitlistEntries.AnyAsync(
            x => x.CustomerId == customerId &&
                 x.ScheduleId == schedule.Id &&
                 x.Status == WaitlistStatus.Active,
            cancellationToken);
        if (exists)
        {
            throw Conflict("already_waitlisted", "The customer is already on this waitlist.");
        }

        var entry = new WaitlistEntry
        {
            CustomerId = customerId,
            ScheduleId = schedule.Id,
            PreferredCustomerPackageId = package.Id,
            Status = WaitlistStatus.Active,
            JoinedAtUtc = timeProvider.GetUtcNow()
        };
        _db.WaitlistEntries.Add(entry);
        return entry;
    }

    private async Task<TimetableSchedule> GetScheduleAsync(
        long scheduleId, CancellationToken cancellationToken) =>
        await _db.TimetableSchedules.SingleOrDefaultAsync(x => x.Id == scheduleId, cancellationToken)
        ?? throw NotFound("schedule_not_found", "Timetable schedule was not found.");

    private async Task<CustomerPackage> GetPackageAsync(
        long packageId, CancellationToken cancellationToken) =>
        await _db.CustomerPackages.SingleOrDefaultAsync(x => x.Id == packageId, cancellationToken)
        ?? throw NotFound("customer_package_not_found", "Purchased customer package was not found.");

    private void EnsureScheduleCanBeBooked(TimetableSchedule schedule)
    {
        if (schedule.StartTimeUtc <= timeProvider.GetUtcNow())
        {
            throw Conflict("class_started", "A class that has started cannot be booked or waitlisted.");
        }
    }

    private void EnsurePackageCanBook(CustomerPackage package, int customerId, int businessId)
    {
        var error = BookingPolicy.ValidatePackage(
            package, customerId, businessId, timeProvider.GetUtcNow());
        if (error is not null)
        {
            throw new BookingEngineException("invalid_package", error);
        }
    }

    private async Task EnsureNoExistingOrOverlappingBookingAsync(
        int customerId, TimetableSchedule schedule, CancellationToken cancellationToken)
    {
        var sameSchedule = await _db.Bookings.AnyAsync(
            x => x.CustomerId == customerId &&
                 x.ScheduleId == schedule.Id &&
                 x.Status == BookingStatus.Confirmed,
            cancellationToken);
        if (sameSchedule)
        {
            throw Conflict("already_booked", "The customer already booked this schedule.");
        }

        if (await HasOverlappingBookingAsync(customerId, schedule, cancellationToken))
        {
            throw Conflict("overlapping_booking", "The customer has an overlapping confirmed booking.");
        }
    }

    private Task<bool> HasOverlappingBookingAsync(
        int customerId, TimetableSchedule schedule, CancellationToken cancellationToken) =>
        _db.Bookings.AnyAsync(
            x => x.CustomerId == customerId &&
                 x.Status == BookingStatus.Confirmed &&
                 x.Schedule.StartTimeUtc < schedule.EndTimeUtc &&
                 schedule.StartTimeUtc < x.Schedule.EndTimeUtc,
            cancellationToken);

    private async Task<long> TryGetCacheVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var value = await redis.GetStringAsync("rezerv:timetable:version", cancellationToken);
            return long.TryParse(value, out var version) ? version : 0;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
    }

    private async Task InvalidateTimetableAsync(CancellationToken cancellationToken)
    {
        try
        {
            _ = await redis.IncrementAsync("rezerv:timetable:version", cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The two-minute TTL bounds stale reads if invalidation is temporarily unavailable.
        }
    }

    private static BookingEngineException NotFound(string code, string message) => new(code, message, 404);
    private static BookingEngineException Conflict(string code, string message) => new(code, message, 409);
}
