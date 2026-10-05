using Rezerv.BookingEngine.Domain;

namespace Rezerv.BookingEngine.Application;

public static class BookingPolicy
{
    public static string? ValidatePackage(
        CustomerPackage package,
        int customerId,
        int businessId,
        DateTimeOffset now)
    {
        if (package.CustomerId != customerId)
        {
            return "The package does not belong to this customer.";
        }

        if (package.BusinessId != businessId)
        {
            return "The package belongs to a different business.";
        }

        if (package.ExpiresAtUtc <= now)
        {
            return "The package has expired.";
        }

        if (package.RemainingCredits < 1)
        {
            return "The package has no remaining credits.";
        }

        return null;
    }

    public static bool Overlaps(
        DateTimeOffset firstStart,
        DateTimeOffset firstEnd,
        DateTimeOffset secondStart,
        DateTimeOffset secondEnd) =>
        firstStart < secondEnd && secondStart < firstEnd;

    public static bool ShouldRefund(
        DateTimeOffset cancelledAtUtc,
        DateTimeOffset classStartUtc) =>
        classStartUtc - cancelledAtUtc > TimeSpan.FromHours(4);
}

