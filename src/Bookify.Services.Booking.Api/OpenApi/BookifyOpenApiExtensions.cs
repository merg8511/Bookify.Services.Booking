namespace Bookify.Services.Booking.Api.OpenApi;

internal static class BookifyOpenApiExtensions
{
    public static IServiceCollection AddBookifyOpenApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOpenApi(options =>
        {
            options.AddOperationTransformer<BookifyContractOperationTransformer>();
            options.AddDocumentTransformer<BookifySecurityDocumentTransformer>();
        });

        return services;
    }
}
