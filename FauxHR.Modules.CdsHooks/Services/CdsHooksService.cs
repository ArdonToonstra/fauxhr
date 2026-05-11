using System.Text;
using System.Text.Json;
using FauxHR.Modules.CdsHooks.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace FauxHR.Modules.CdsHooks.Services;

public class CdsHooksService
{
    private const string CdsBaseUrl = "https://clinical-trial-matching.donit.be/cds-services/";

    private readonly HttpClient _http;
    private readonly FhirJsonSerializer _fhirSerializer = new();
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    public CdsHooksService(HttpClient http) => _http = http;

    /// <summary>
    /// POSTs the CDS Hooks request to the given service and returns the parsed response.
    /// Returns null if the service is unreachable or returns a non-success status.
    /// </summary>
    public async Task<CdsHooksResponse?> InvokeAsync(string serviceId, CdsHooksRequest request)
    {
        // Serialize each FHIR resource in the prefetch using the Firely SDK, then
        // embed the result as a raw JsonElement so System.Text.Json doesn't re-encode it.
        var prefetchElements = new Dictionary<string, JsonElement>();
        foreach (var (key, value) in request.Prefetch)
        {
            if (value is Resource resource)
            {
                var fhirJson = _fhirSerializer.SerializeToString(resource);
                using var doc = JsonDocument.Parse(fhirJson);
                prefetchElements[key] = doc.RootElement.Clone();
            }
        }

        var body = JsonSerializer.Serialize(new
        {
            hook = request.Hook,
            hookInstance = request.HookInstance,
            context = new { userId = request.Context.UserId, patientId = request.Context.PatientId },
            prefetch = prefetchElements
        });

        using var message = new HttpRequestMessage(HttpMethod.Post, CdsBaseUrl + serviceId)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        message.Headers.Accept.Clear();
        message.Headers.Accept.ParseAdd("application/json");

        var response = await _http.SendAsync(message);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<CdsHooksResponse>(json, ReadOptions);
    }
}
