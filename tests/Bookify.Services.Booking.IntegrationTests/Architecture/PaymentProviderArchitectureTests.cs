using Bookify.Services.Booking.Application.Abstractions.Payments;
using Bookify.Services.Booking.Domain.Payments;

namespace Bookify.Services.Booking.IntegrationTests.Architecture;

public sealed class PaymentProviderArchitectureTests
{
    [Fact]
    public void Domain_ShouldNotReferenceStripe()
    {
        // ARRANGE
        System.Reflection.Assembly domainAssembly =
            typeof(Payment)
                .Assembly;

        // ACT
        string[] referencedAssemblies =
            domainAssembly
                .GetReferencedAssemblies()
                .Select(
                    assembly =>
                        assembly.Name
                        ?? string.Empty)
                .ToArray();

        // ASSERT
        Assert.DoesNotContain(
            referencedAssemblies,
            assemblyName =>
                assemblyName.StartsWith(
                    "Stripe",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Application_ShouldNotReferenceStripe()
    {
        // ARRANGE
        System.Reflection.Assembly
            applicationAssembly =
                typeof(IPaymentGateway)
                    .Assembly;

        // ACT
        string[] referencedAssemblies =
            applicationAssembly
                .GetReferencedAssemblies()
                .Select(
                    assembly =>
                        assembly.Name
                        ?? string.Empty)
                .ToArray();

        // ASSERT
        Assert.DoesNotContain(
            referencedAssemblies,
            assemblyName =>
                assemblyName.StartsWith(
                    "Stripe",
                    StringComparison.OrdinalIgnoreCase));
    }
}
