using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rezerv.BookingEngine.Application;
using Rezerv.BookingEngine.Infrastructure.Persistence;
using Rezerv.BookingEngine.Infrastructure.Redis;
using Rezerv.BookingEngine.Infrastructure.Services;

namespace Rezerv.BookingEngine.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MySql")
            ?? throw new InvalidOperationException("ConnectionStrings:MySql is required.");

        services.AddDbContext<BookingDbContext>(options =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0))));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.AddSingleton<IRedisClient, RespRedisClient>();
        services.AddSingleton<IDistributedLockProvider, RedisDistributedLockProvider>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IBookingEngine, BookingEngineService>();
        services.AddHostedService<WaitlistExpiryService>();
        return services;
    }
}
