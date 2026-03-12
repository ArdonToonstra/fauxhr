using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// --- Request Size Limits ---
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 5 * 1024 * 1024; // 5 MB
});

// --- SSL cert bypass (opt-in for dev/conformance servers with private CA certs) ---
var ignoreCertErrors = string.Equals(
    builder.Configuration["FHIR_PROXY_IGNORE_CERT_ERRORS"], "true",
    StringComparison.OrdinalIgnoreCase);

// --- SSRF Allowlist ---
var allowlistEnv = builder.Configuration["FHIR_PROXY_ALLOWLIST"]
    ?? "server.fire.ly;hapi.fhir.org;nictiz.proxy.interoplab.eu;pzp-coalitie.proxy.interoplab.eu";
var allowedHosts = new HashSet<string>(
    allowlistEnv.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
    StringComparer.OrdinalIgnoreCase);

// --- Rate Limiting ---
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Request.Headers["CF-Connecting-IP"].FirstOrDefault()
                          ?? ctx.Connection.RemoteIpAddress?.ToString()
                          ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddHttpClient("FhirProxy")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        // Allow bypassing private-CA certificates (e.g. interoplab.eu dev servers).
        // Only enabled when FHIR_PROXY_IGNORE_CERT_ERRORS=true is explicitly set.
        ServerCertificateCustomValidationCallback = ignoreCertErrors
            ? HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            : null
    });

var app = builder.Build();

app.UseRateLimiter();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

// Health-check endpoint so Blazor WASM can detect whether the proxy is available.
app.MapGet("/fhir-proxy/ping", () => Results.Ok());

// FHIR reverse proxy — bypasses broken CORS headers from upstream servers.
// Blazor WASM sends requests to /fhir-proxy/{path} (same origin, no CORS),
// and this endpoint forwards them server-to-server to the target FHIR server.
app.Map("/fhir-proxy/{**path}", async (HttpContext ctx, IHttpClientFactory httpClientFactory) =>
{
    var targetServer = ctx.Request.Headers["X-Fhir-Server"].FirstOrDefault();
    if (string.IsNullOrEmpty(targetServer))
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsync("X-Fhir-Server header is required");
        return;
    }

    // SSRF protection: validate URL scheme and check host against allowlist
    if (!Uri.TryCreate(targetServer, UriKind.Absolute, out var targetBase)
        || (targetBase.Scheme != "https" && targetBase.Scheme != "http"))
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsync("Invalid X-Fhir-Server URL");
        return;
    }

    if (!allowedHosts.Contains(targetBase.Host))
    {
        ctx.Response.StatusCode = 403;
        await ctx.Response.WriteAsync($"FHIR server host '{targetBase.Host}' is not in the allowlist");
        return;
    }

    var path = ctx.Request.RouteValues["path"]?.ToString() ?? "";
    var targetUri = new Uri($"{targetServer.TrimEnd('/')}/{path}{ctx.Request.QueryString}");

    using var client = httpClientFactory.CreateClient("FhirProxy");

    // Forward headers from the browser request, skipping hop-by-hop / proxy-specific ones
    var skipHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Host", "X-Fhir-Server", "Transfer-Encoding", "Connection" };

    foreach (var header in ctx.Request.Headers)
    {
        if (skipHeaders.Contains(header.Key)) continue;
        client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value.ToArray());
    }

    HttpResponseMessage upstream;
    try
    {
        if (HttpMethods.IsGet(ctx.Request.Method))
        {
            upstream = await client.GetAsync(targetUri);
        }
        else
        {
            var content = new StreamContent(ctx.Request.Body);
            if (ctx.Request.ContentType != null)
                content.Headers.TryAddWithoutValidation("Content-Type", ctx.Request.ContentType);
            upstream = await client.PostAsync(targetUri, content);
        }
    }
    catch (HttpRequestException ex)
    {
        ctx.Response.StatusCode = 502;
        var hint = ex.Message.Contains("certificate", StringComparison.OrdinalIgnoreCase)
                   || ex.InnerException?.Message.Contains("certificate", StringComparison.OrdinalIgnoreCase) == true
            ? " The upstream server may be using a certificate from a private/untrusted CA. Set FHIR_PROXY_IGNORE_CERT_ERRORS=true to bypass (dev environments only)."
            : "";
        await ctx.Response.WriteAsync($"Upstream FHIR server unreachable: {ex.Message}.{hint}");
        return;
    }
    catch (Exception ex)
    {
        ctx.Response.StatusCode = 502;
        await ctx.Response.WriteAsync($"Proxy error: {ex.GetType().Name}: {ex.Message}");
        return;
    }

    ctx.Response.StatusCode = (int)upstream.StatusCode;

    // Read the body fully first (HttpClient already decoded chunked/compressed content)
    var body = await upstream.Content.ReadAsByteArrayAsync();

    // Hop-by-hop headers must never be forwarded — they describe the transport layer between
    // two adjacent nodes, not the end-to-end message. Forwarding Transfer-Encoding: chunked
    // in particular causes ERR_INVALID_CHUNKED_ENCODING because the body is already decoded.
    var skipResponseHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Transfer-Encoding", "Connection", "Keep-Alive", "TE", "Trailers", "Upgrade",
        "Proxy-Authenticate", "Proxy-Authorization",
        "Content-Length",        // will be set explicitly below
        "Content-Encoding"       // body is already decoded by HttpClient
    };

    foreach (var header in upstream.Headers)
    {
        if (header.Key.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase)) continue;
        if (skipResponseHeaders.Contains(header.Key)) continue;
        ctx.Response.Headers.TryAdd(header.Key, header.Value.ToArray());
    }
    foreach (var header in upstream.Content.Headers)
    {
        if (skipResponseHeaders.Contains(header.Key)) continue;
        ctx.Response.Headers.TryAdd(header.Key, header.Value.ToArray());
    }

    ctx.Response.ContentLength = body.Length;
    await ctx.Response.Body.WriteAsync(body);
});

app.MapFallbackToFile("index.html");

app.Run();
