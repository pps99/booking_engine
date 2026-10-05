using Rezerv.BookingEngine.Application;
using Rezerv.BookingEngine.Domain;
using Xunit;

namespace Rezerv.BookingEngine.UnitTests;

public sealed class BookingPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 10, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidatePackage_AcceptsEligiblePackage()
    {
        var package = Package(customerId: 1, businessId: 2, credits: 1, expiry: Now.AddDays(1));

        var result = BookingPolicy.ValidatePackage(package, 1, 2, Now);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidatePackage_RejectsInsufficientCredits(int credits)
    {
        var package = Package(1, 2, credits, Now.AddDays(1));

        var result = BookingPolicy.ValidatePackage(package, 1, 2, Now);

        Assert.Equal("The package has no remaining credits.", result);
    }

    [Fact]
    public void ValidatePackage_RejectsExpiryAtCurrentInstant()
    {
        var result = BookingPolicy.ValidatePackage(Package(1, 2, 1, Now), 1, 2, Now);

        Assert.Equal("The package has expired.", result);
    }

    [Fact]
    public void ValidatePackage_RejectsWrongBusiness()
    {
        var result = BookingPolicy.ValidatePackage(Package(1, 2, 1, Now.AddDays(1)), 1, 3, Now);

        Assert.Equal("The package belongs to a different business.", result);
    }

    [Theory]
    [InlineData(8, 9, 9, 10, false)]
    [InlineData(8, 10, 9, 11, true)]
    [InlineData(9, 11, 8, 10, true)]
    [InlineData(8, 12, 9, 10, true)]
    public void Overlaps_UsesHalfOpenIntervals(
        int firstStart, int firstEnd, int secondStart, int secondEnd, bool expected)
    {
        var day = Now.Date;

        var result = BookingPolicy.Overlaps(
            day.AddHours(firstStart), day.AddHours(firstEnd),
            day.AddHours(secondStart), day.AddHours(secondEnd));

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(4, false)]
    [InlineData(3, false)]
    public void ShouldRefund_RequiresMoreThanFourFullHours(int hoursBefore, bool expected)
    {
        var classStart = Now.AddHours(10);

        var result = BookingPolicy.ShouldRefund(classStart.AddHours(-hoursBefore), classStart);

        Assert.Equal(expected, result);
    }

    private static CustomerPackage Package(
        int customerId, int businessId, int credits, DateTimeOffset expiry) => new()
        {
            CustomerId = customerId,
            BusinessId = businessId,
            RemainingCredits = credits,
            ExpiresAtUtc = expiry
        };
}
