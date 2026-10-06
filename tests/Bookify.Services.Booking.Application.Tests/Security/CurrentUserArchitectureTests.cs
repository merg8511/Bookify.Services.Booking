using Bookify.Services.Booking.Application.Abstractions.Security;

namespace Bookify.Services.Booking.Application.Tests.Security;

public sealed class CurrentUserArchitectureTests
{
    [Fact]
    public void Application_ShouldNotReferenceAspNetCoreHttp()
    {
        // Arrange
        var referencedAssemblies = typeof(ICurrentUser).Assembly.GetReferencedAssemblies();

        // Assert
        Assert.DoesNotContain(referencedAssemblies,
            assembly => assembly.Name?.StartsWith("Microsoft.AspNetCore.Http", StringComparison.Ordinal) == true);
    }
}
