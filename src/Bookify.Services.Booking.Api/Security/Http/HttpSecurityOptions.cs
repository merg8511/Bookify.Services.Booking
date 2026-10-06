namespace Bookify.Services.Booking.Api.Security.Http;

internal sealed class HttpSecurityOptions
{
    public const string SectionName = "HttpSecurity";
    public string[] TrustedFrontendOrigins { get; set; } = [];
    public ForwardedHeadersSecurityOptions ForwardedHeaders { get; set; } = new();
}

internal sealed class ForwardedHeadersSecurityOptions
{
    public int ForwardLimit { get; set; } = 1;
    public string[] KnownProxies { get; set; } = [];
    public string[] KnownNetworks { get; set; } = [];
}
