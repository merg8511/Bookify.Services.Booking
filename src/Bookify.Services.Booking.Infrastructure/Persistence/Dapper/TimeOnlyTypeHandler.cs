using System.Data;
using Dapper;
using Npgsql;
using NpgsqlTypes;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Dapper;

internal sealed class TimeOnlyTypeHandler
    : SqlMapper.TypeHandler<TimeOnly>
{
    public override void SetValue(
        IDbDataParameter parameter,
        TimeOnly value)
    {
        if (parameter is NpgsqlParameter npgsqlParameter)
        {
            npgsqlParameter.NpgsqlDbType = NpgsqlDbType.Time;
            npgsqlParameter.Value = value;
            return;
        }

        parameter.DbType = DbType.Time;
        parameter.Value = value.ToTimeSpan();
    }

    public override TimeOnly Parse(object value)
    {
        return value switch
        {
            TimeOnly timeOnly => timeOnly,

            TimeSpan timeSpan
                when timeSpan >= TimeSpan.Zero &&
                     timeSpan < TimeSpan.FromDays(1)
                => TimeOnly.FromTimeSpan(timeSpan),

            DateTime dateTime
                => TimeOnly.FromDateTime(dateTime),

            _ => throw new DataException(
                $"Cannot convert database value of type " +
                $"'{value.GetType().FullName}' to {nameof(TimeOnly)}.")
        };
    }
}
