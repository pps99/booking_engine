using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Rezerv.BookingEngine.Infrastructure.Persistence;

public sealed class BookingDbContextFactory : IDesignTimeDbContextFactory<BookingDbContext>
{
    public BookingDbContext CreateDbContext(string[] args)
    {
        const string connectionString =
            "Server=localhost;Port=3306;Database=rezerv_booking;User=rezerv;Password=rezerv_dev_password";
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0)))
            .Options;
        return new BookingDbContext(options);
    }
}
