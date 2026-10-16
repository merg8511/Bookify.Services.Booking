namespace Bookify.Services.Booking.Infrastructure.Persistence;

internal sealed class PostgreSqlTimeoutOptions
{
    public const string SectionName = "PostgreSql";
    public int ConnectionTimeoutSeconds { get; set; } = 10;
    public int CommandTimeoutSeconds { get; set; } = 20;
}
