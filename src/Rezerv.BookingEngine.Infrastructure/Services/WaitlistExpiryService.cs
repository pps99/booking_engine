using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rezerv.BookingEngine.Domain;
using Rezerv.BookingEngine.Infrastructure.Persistence;

namespace Rezerv.BookingEngine.Infrastructure.Services;

public sealed class WaitlistExpiryService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<WaitlistExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await ExpireEndedWaitlistsAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Failed to expire ended waitlists.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ExpireEndedWaitlistsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var now = timeProvider.GetUtcNow();
        var entries = await db.WaitlistEntries
            .Where(x => x.Status == WaitlistStatus.Active && x.Schedule.EndTimeUtc <= now)
            .ToListAsync(cancellationToken);

        foreach (var entry in entries)
        {
            entry.Status = WaitlistStatus.Expired;
            entry.ResolvedAtUtc = now;
            entry.ResolutionReason = "Class ended before a slot became available; no credit was deducted.";
        }

        if (entries.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Expired {Count} ended waitlist entries.", entries.Count);
        }
    }
}

