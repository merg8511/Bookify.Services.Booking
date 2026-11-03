using Bookify.Services.Booking.Api.Endpoints;
using Bookify.Services.Booking.Api.Idempotency;
using Bookify.Services.Booking.Api.Security;
using Bookify.Services.Booking.Application.Common.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using System.Text.Json.Nodes;

namespace Bookify.Services.Booking.Api.OpenApi;

internal sealed class BookifyContractOperationTransformer :
    IOpenApiOperationTransformer
{
    private const string IdempotencyHeaderName = "Idempotency-Key";
    private const string StripeSignatureHeaderName = "Stripe-Signature";
    private const string RetryAfterHeaderName = "Retry-After";
    private const int MaximumIdempotencyKeyLength = 255;

    public async Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        DocumentSecurity(operation, context, metadata);

        if (metadata.OfType<IdempotencyRequiredMetadata>().Any())
        {
            DocumentIdempotency(operation);
        }

        if (operation.OperationId == EndpointNames.Bookings.Create)
        {
            DocumentGuestCredentialResponse(operation);
        }

        if (operation.OperationId == EndpointNames.Payments.StripeWebhook)
        {
            DocumentStripeWebhookBody(operation);
        }

        DocumentPagination(operation);

        DocumentProblemResponses(operation);

        if (metadata.OfType<EnableRateLimitingAttribute>().Any())
        {
            await DocumentRateLimitingAsync(operation, context, cancellationToken);
        }
    }

    private static void DocumentSecurity(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        IList<object> metadata)
    {
        EndpointAccessCategory? category = metadata
            .OfType<EndpointAccessMetadata>()
            .LastOrDefault()?
            .Category;

        OpenApiDocument document = context.Document ??
            throw new InvalidOperationException("OpenAPI document context is unavailable");

        switch (category)
        {
            case EndpointAccessCategory.Public:
                return;

            case EndpointAccessCategory.ProviderWebhook:
                AddSecurityRequirement(
                    operation,
                    document,
                    BookifySecurityDocumentTransformer.StripeScheme);
                return;

            case EndpointAccessCategory.OwnerOrAdmin:
                AddSecurityRequirement(
                    operation,
                    document,
                    BookifySecurityDocumentTransformer.BearerScheme);
                return;

            case EndpointAccessCategory.CustomerOrGuest:
                AddSecurityRequirement(
                    operation,
                    document,
                    BookifySecurityDocumentTransformer.BearerScheme);

                AddSecurityRequirement(
                    operation,
                    document,
                    BookifySecurityDocumentTransformer.GuestScheme);
                return;
        }

        bool requiresAuthorization = metadata.OfType<IAuthorizeData>().Any();
        bool allowsAnonymous = metadata.OfType<IAllowAnonymous>().Any();

        if (requiresAuthorization && !allowsAnonymous)
        {
            AddSecurityRequirement(
                operation,
                document,
                BookifySecurityDocumentTransformer.BearerScheme);
        }
    }

    private static void AddSecurityRequirement(
        OpenApiOperation operation,
        OpenApiDocument document,
        string schemeName)
    {
        operation.Security ??= [];

        operation.Security.Add(
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(schemeName, document)] = []
            });
    }

    private static void DocumentIdempotency(OpenApiOperation operation)
    {
        operation.Parameters ??= [];

        if (operation.Parameters.Any(parameter => parameter.In == ParameterLocation.Header &&
            string.Equals(parameter.Name, IdempotencyHeaderName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        operation.Parameters.Add(
            new OpenApiParameter
            {
                Name = IdempotencyHeaderName,
                In = ParameterLocation.Header,
                Required = true,
                Description = "Required for this operation. Reuse the same key only when retrying the same logical " +
                              "request with an identical payload. A different logical operation requires a new key. " +
                              "Maximum length: 255 characters",
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    MinLength = 1,
                    MaxLength = MaximumIdempotencyKeyLength
                }
            });
    }

    private static void DocumentGuestCredentialResponse(OpenApiOperation operation)
    {
        OpenApiResponses? responses = operation.Responses;

        if (responses is null ||
            !responses.TryGetValue("201", out var response) ||
            response is not OpenApiResponse created)
        {
            return;
        }

        created.Headers ??= new Dictionary<string, IOpenApiHeader>();

        created.Headers[BookingGuestAccessHeader.Name] = new OpenApiHeader
        {
            Required = false,
            Description = "Returned only for a newly created anonymous booking. Contains the opaque " +
                          "guest access credential. Capture it immediately and store it securely. It is not included in " +
                          "the JSON body and is not reissued on an idempotency replay.",
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String
            }
        };
    }

    private static void DocumentStripeWebhookBody(OpenApiOperation operation)
    {
        operation.RequestBody ??= new OpenApiRequestBody
        {
            Required = true,
            Description = "Original, unmodified Stripe webhook JSON payload. The server validates Stripe-Signature " +
                          "against the raw request body. Do not re-serialize or modify a signed payload.",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object
                    }
                }
            }
        };
    }

    private static void DocumentPagination(OpenApiOperation operation)
    {
        if (operation.OperationId != EndpointNames.Properties.List &&
            operation.OperationId != EndpointNames.AdminBookings.List)
        {
            return;
        }

        if (operation.Parameters is null)
        {
            return;
        }

        foreach (var parameter in operation.Parameters)
        {
            if (parameter is not OpenApiParameter concrete ||
                concrete.In != ParameterLocation.Query)
            {
                continue;
            }

            if (string.Equals(concrete.Name, "pageNumber", StringComparison.OrdinalIgnoreCase))
            {
                concrete.Description = $"One-based page number. Defaults to {PaginationDefaults.DefaultPageNumber} " +
                    $"when omitted.";

                if (concrete.Schema is OpenApiSchema schema)
                {
                    schema.Minimum = "1";
                    schema.Default = JsonValue.Create(PaginationDefaults.DefaultPageNumber);
                }
            }
            else if (string.Equals(concrete.Name, "pageSize", StringComparison.OrdinalIgnoreCase))
            {
                concrete.Description = $"Number of items per page. Defaults to {PaginationDefaults.DefaultPageSize}. " +
                    $"Minimum: 1. Maximum: {PaginationDefaults.MaximumPageSize}.";

                if (concrete.Schema is OpenApiSchema schema)
                {
                    schema.Minimum = "1";
                    schema.Maximum = PaginationDefaults.MaximumPageSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    schema.Default = JsonValue.Create(PaginationDefaults.DefaultPageSize);
                }
            }
        }
    }

    private static void DocumentProblemResponses(OpenApiOperation operation)
    {
        OpenApiResponses? responses = operation.Responses;

        if (responses is null)
        {
            return;
        }

        foreach (var entry in responses)
        {
            if (entry.Value is not OpenApiResponse response ||
                response.Content is null ||
                !response.Content.ContainsKey("application/problem+json"))
            {
                continue;
            }

            string meaning = entry.Key switch
            {
                "400" => "Invalid request or validation error.",
                "404" => "Requested resource was not found.",
                "409" => "Conflict with the current resource state or request consistency rules.",
                "429" => "Request rate limit exceeded.",
                "500" => "Unexpected server error.",
                "504" => "An operation exceeded an upstream or infrastructure timeout.",
                _ => "Request processing error."
            };

            response.Description = $"{meaning} The response uses RFC 9457 Problem Details with standard fields such as " +
                "type, title, status, detail and instance. Bookify error responses may include the extensions code, traceId " +
                "and correlationId where applicable. Do not assume all extensions are present in every error.";
        }
    }

    private static async Task DocumentRateLimitingAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        operation.Responses ??= new OpenApiResponses();

        if (operation.Responses.ContainsKey("429"))
        {
            return;
        }

        var problemSchema = await context.GetOrCreateSchemaAsync(typeof(ProblemDetails), null, cancellationToken);

        operation.Responses["429"] = new OpenApiResponse
        {
            Description =
                    "Too many requests. The configured rate limit was exceeded. Returns application/problem+json " +
                    "with code RateLimit.Exceeded. Retry-After may be present when the limiter can calculate it.",

            Content =
                    new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/problem+json"] = new OpenApiMediaType
                        {
                            Schema = problemSchema
                        }
                    },

            Headers =
                    new Dictionary<string, IOpenApiHeader>
                    {
                        [RetryAfterHeaderName] = new OpenApiHeader
                        {
                            Required = false,
                            Description = "Optional number of seconds to wait before retrying.",
                            Schema = new OpenApiSchema
                            {
                                Type = JsonSchemaType.String
                            }
                        }
                    }
        };
    }
}
