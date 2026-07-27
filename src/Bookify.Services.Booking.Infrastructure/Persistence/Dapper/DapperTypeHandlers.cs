using Dapper;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Dapper;

public static class DapperTypeHandlers
{
    private static int _registered;

    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
    }
}
