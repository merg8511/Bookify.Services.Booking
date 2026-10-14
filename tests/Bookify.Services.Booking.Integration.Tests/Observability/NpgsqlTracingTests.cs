using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;

namespace Bookify.Services.Booking.Integration.Tests.Observability;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class NpgsqlTracingTests
{
    [Fact]
    public async Task ParameterizedQuery_ShouldNotExposeParameterValueInSpanTags()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        const string secretValue = "very-private-observability-value";
        var finished = new ConcurrentQueue<Activity>();

        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Npgsql",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => finished.Enqueue(activity)
        };
        ActivitySource.AddActivityListener(listener);

        using IServiceScope scope = _factory.Services.CreateScope();
        IDbConnectionFactory factory =
            scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        await using DbConnection connection = await factory.OpenConnectionAsync(token);
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = "SELECT @secret";
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = "secret";
        parameter.Value = secretValue;
        command.Parameters.Add(parameter);

        Assert.Equal(secretValue, Convert.ToString(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture));

        Activity? span = finished.FirstOrDefault(a =>
            a.Source.Name == "Npgsql" &&
            a.GetTagItem("db.query.text")?.ToString() == "SELECT @secret");
        Assert.NotNull(span);
        string serialized = string.Join(";", span.TagObjects.Select(tag =>
            $"{tag.Key}={tag.Value}"));
        Assert.DoesNotContain(secretValue, serialized, StringComparison.Ordinal);
    }

    private readonly BookingApiFactory _factory;

    public NpgsqlTracingTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DapperConnection_ShouldEmitNpgsqlClientActivityUnderParent()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var finished = new ConcurrentQueue<Activity>();

        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Npgsql",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => finished.Enqueue(activity)
        };
        ActivitySource.AddActivityListener(listener);

        using var parent = new Activity("bookify.test.database")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();

        using IServiceScope scope = _factory.Services.CreateScope();
        IDbConnectionFactory connectionFactory =
            scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory.OpenConnectionAsync(token);

        await using DbCommand command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        object? result = await command.ExecuteScalarAsync(token);

        Assert.Equal(1, Convert.ToInt32(result, CultureInfo.InvariantCulture));

        Activity? dbActivity = finished.FirstOrDefault(activity =>
            activity.Source.Name == "Npgsql" &&
            activity.Kind == ActivityKind.Client &&
            activity.ParentSpanId == parent.SpanId &&
            string.Equals(activity.GetTagItem("db.query.text")?.ToString(), "SELECT 1",
                StringComparison.Ordinal));

        Assert.NotNull(dbActivity);
        Assert.Equal(parent.TraceId, dbActivity.TraceId);
        Assert.NotEqual(parent.SpanId, dbActivity.SpanId);
        Assert.Equal(ActivityIdFormat.W3C, dbActivity.IdFormat);
    }
}
