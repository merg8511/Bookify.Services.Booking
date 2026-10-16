using System.Collections.Concurrent;
using System.Diagnostics;
using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Infrastructure.Payments.Stripe;
using Stripe;

namespace Bookify.Services.Booking.Integration.Tests.Observability;

public sealed class StripeGatewayActivityTests
{
    [Theory]
    [InlineData("create", "stripe.payment_intent.create")]
    [InlineData("get", "stripe.payment_intent.get")]
    [InlineData("cancel", "stripe.payment_intent.cancel")]
    public async Task Gateway_ShouldEmitClientSpanUnderActiveParent(
        string operation,
        string expectedSpanName)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var activities = new ConcurrentQueue<Activity>();

        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Bookify.Services.Booking",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => activities.Enqueue(activity)
        };
        ActivitySource.AddActivityListener(listener);

        using var parent = new Activity("bookify.test.parent")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();

        var gateway = new StripePaymentGateway(new StubPaymentIntentService());

        switch (operation)
        {
            case "create":
                {
                    Result<Money> amount = Money.Create(12.34m, "USD");
                    Assert.True(amount.IsSuccess);
                    Result<CreatePaymentAttemptResponse> result =
                        await gateway.CreatePaymentAttemptAsync(
                            new CreatePaymentAttemptRequest(
                                Guid.NewGuid(), amount.Value, "private-idempotency-value"), token);
                    Assert.True(result.IsSuccess);
                    break;
                }
            case "get":
                {
                    Result<PaymentGatewayResponse> result =
                        await gateway.GetPaymentStatusAsync("pi_bookify_test", token);
                    Assert.True(result.IsSuccess);
                    break;
                }
            case "cancel":
                {
                    Result<PaymentGatewayResponse> result =
                        await gateway.CancelPaymentAsync("pi_bookify_test", token);
                    Assert.True(result.IsSuccess);
                    break;
                }
            default:
                throw new InvalidOperationException(operation);
        }

        Activity child = Assert.Single(
            activities,
            activity =>
                activity.OperationName == expectedSpanName &&
                activity.TraceId == parent.TraceId &&
                activity.ParentSpanId == parent.SpanId);

        Assert.Equal(ActivityKind.Client, child.Kind);
        Assert.Equal(parent.TraceId, child.TraceId);
        Assert.Equal(parent.SpanId, child.ParentSpanId);
        Assert.Equal("stripe", child.GetTagItem("peer.service"));
        Assert.Equal(operation, child.GetTagItem("bookify.payment.operation"));

        string tags = string.Join(";", child.TagObjects.Select(x => $"{x.Key}:{x.Value}"));
        Assert.DoesNotContain("private-idempotency-value", tags, StringComparison.Ordinal);
        Assert.DoesNotContain("pi_test_secret_sensitive", tags, StringComparison.Ordinal);
        Assert.DoesNotContain("sk_test_", tags, StringComparison.Ordinal);
    }

    private sealed class StubPaymentIntentService : PaymentIntentService
    {
        public override Task<PaymentIntent> CreateAsync(
            PaymentIntentCreateOptions options,
            RequestOptions requestOptions = null!,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PaymentIntent
            {
                Id = "pi_bookify_test",
                Status = "requires_payment_method",
                ClientSecret = "pi_test_secret_sensitive"
            });
        }

        public override Task<PaymentIntent> GetAsync(
            string id,
            PaymentIntentGetOptions options = null!,
            RequestOptions requestOptions = null!,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PaymentIntent
            {
                Id = id,
                Status = "requires_payment_method"
            });
        }

        public override Task<PaymentIntent> CancelAsync(
            string id,
            PaymentIntentCancelOptions options = null!,
            RequestOptions requestOptions = null!,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PaymentIntent
            {
                Id = id,
                Status = "canceled"
            });
        }
    }
}
