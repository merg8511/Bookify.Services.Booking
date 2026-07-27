using Dapper;
using Npgsql;
using NpgsqlTypes;
using System.Data;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Dapper;

internal sealed class DateOnlyTypeHandler
    : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(
        IDbDataParameter parameter,
        DateOnly value)
    {
        if (parameter is NpgsqlParameter npgsqlParameter)
        {
            npgsqlParameter.NpgsqlDbType =
                NpgsqlDbType.Date;

            npgsqlParameter.Value =
                value;

            return;
        }

        parameter.DbType =
            DbType.Date;

        parameter.Value =
            value.ToDateTime(
                TimeOnly.MinValue);
    }

    public override DateOnly Parse(
        object value)
    {
        return value switch
        {
            DateOnly dateOnly =>
                dateOnly,

            DateTime dateTime =>
                DateOnly.FromDateTime(
                    dateTime),

            _ => throw new DataException(
                $"Cannot convert database value " +
                $"of type '{value.GetType().FullName}' " +
                $"to {nameof(DateOnly)}.")
        };
    }
}
