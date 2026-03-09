using System.Net;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient("FhirProxy")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        // Automatically decompress gzip/deflate/brotli so the proxy forwards clean plaintext
        AutomaticDecompression = DecompressionMethods.All
    });

var app = builder.Build();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

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
