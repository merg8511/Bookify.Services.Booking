using System.Text.Json.Serialization;

namespace Bookify.Services.Booking.Api.Endpoints.Properties.SetStatus;

public sealed record SetPropertyStatusRequest([property: JsonRequired] bool IsActive);
