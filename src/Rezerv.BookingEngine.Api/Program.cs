using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Rezerv.BookingEngine.Api;
using Rezerv.BookingEngine.Infrastructure;
using Rezerv.BookingEngine.Infrastructure.Persistence;
using Rezerv.BookingEngine.Infrastructure.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Rezerv Booking Engine API",
        Version = "v1",
        Description = "Studio packages, timetables, bookings, cancellations and FIFO waitlists."
    });
});
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Rezerv API v1"));
app.MapControllers();
app.MapGet("/health", async (
    BookingDbContext db, IRedisClient redis, CancellationToken cancellationToken) =>
{
    var mysql = await db.Database.CanConnectAsync(cancellationToken);
    var redisAvailable = false;
    try
    {
        redisAvailable = await redis.PingAsync(cancellationToken);
    }
    catch
    {
        // Health response reports the dependency instead of throwing.
    }

    return mysql && redisAvailable
        ? Results.Ok(new { status = "healthy", mysql, redis = redisAvailable })
        : Results.Json(
            new { status = "unhealthy", mysql, redis = redisAvailable },
            statusCode: StatusCodes.Status503ServiceUnavailable);
});

await InitializeDatabaseAsync(app.Services);
await app.RunAsync();

static async Task InitializeDatabaseAsync(IServiceProvider services)
{
    await using var scope = services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
    await db.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(
        db, scope.ServiceProvider.GetRequiredService<TimeProvider>());
}

public partial class Program;
