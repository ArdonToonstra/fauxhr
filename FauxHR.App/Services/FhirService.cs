using FauxHR.Core.Interfaces;
using FauxHR.Core.Services;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;

using FauxHR.App.Services;
using Hl7.Fhir.Serialization;
namespace FauxHR.App.Services;

public class FhirService : IFhirService
{
    private readonly AppState _appState;
    private readonly HttpClient _httpClient;
    private FhirClient _client = default!;

    public FhirService(AppState appState, HttpClient httpClient)
    {
        _appState = appState;
        _httpClient = httpClient;
        InitializeClient(); // Initializes _client
        
        // Re-initialize client if server URL changes
        _appState.OnChange += () => 
        {
            if (_client != null && (_client.Settings.PreferredFormat != ResourceFormat.Json || _client.Endpoint.OriginalString != _appState.CurrentServerUrl))
            {
                InitializeClient();
            }
        };
    }

    private void InitializeClient()
    {
        // In Blazor WASM, always provide a custom handler to prevent FhirClient from
        // creating its own handler which would try to set AutomaticDecompression (not supported)
        var customHandler = new CustomHeaderHandler(_appState, _httpClient);

        _client = new FhirClient(_appState.CurrentServerUrl,
            new FhirClientSettings
            {
                PreferredFormat = ResourceFormat.Json,
                VerifyFhirVersion = false, // Skip metadata check — authenticated servers often require auth on /metadata too
                SerializationEngine = FhirSerializationEngineFactory.Recoverable(ModelInfo.ModelInspector)
            },
            customHandler);
    }

    // Custom Handler to inject headers - creates fresh HttpClient for each request
    private class CustomHeaderHandler : HttpMessageHandler
    {
        private readonly AppState _state;
        private readonly Uri _appBaseUri;

        public CustomHeaderHandler(AppState state, HttpClient httpClient)
        {
            _state = state;
            _appBaseUri = httpClient.BaseAddress ?? new Uri("http://localhost/");
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Clone the request to avoid "already sent" error
            var clonedRequest = await CloneHttpRequestMessageAsync(request);

            // Route through local server proxy when the target server has CORS issues.
            // The server project (FauxHR.App.Server) exposes /fhir-proxy/{path} and
            // forwards the request server-to-server, bypassing browser CORS enforcement.
            if (_state.UseServerProxy && clonedRequest.RequestUri != null)
            {
                var serverBase = new Uri(_state.CurrentServerUrl.TrimEnd('/') + "/");
                var relativePath = serverBase.MakeRelativeUri(clonedRequest.RequestUri).ToString();
                clonedRequest.RequestUri = new Uri(_appBaseUri, $"fhir-proxy/{relativePath}");
                clonedRequest.Headers.TryAddWithoutValidation("X-Fhir-Server", _state.CurrentServerUrl);
            }

            // Inject per-server custom headers when conformance testing mode is on
            if (_state.ConformanceTestingMode)
            {
                foreach (var header in _state.CurrentServerHeaders)
                {
                    if (!string.IsNullOrWhiteSpace(header.Key))
                    {
                        if (clonedRequest.Headers.Contains(header.Key))
                        {
                            clonedRequest.Headers.Remove(header.Key);
                        }
                        clonedRequest.Headers.Add(header.Key, header.Value);
                    }
                }
            }

            // Capture request details for debug logging.
            // When proxied, reconstruct the actual upstream URL for clarity.
            var logUrl = clonedRequest.RequestUri?.ToString() ?? "";
            if (_state.UseServerProxy && logUrl.Contains("/fhir-proxy/"))
            {
                var proxyPath = logUrl[(logUrl.IndexOf("/fhir-proxy/") + "/fhir-proxy/".Length)..];
                logUrl = $"{_state.CurrentServerUrl.TrimEnd('/')}/{proxyPath}";
            }
            var logMethod = clonedRequest.Method.ToString();
            var logHeaders = new Dictionary<string, string>();
            foreach (var h in clonedRequest.Headers)
            {
                if (h.Key.Equals("X-Fhir-Server", StringComparison.OrdinalIgnoreCase)) continue;
                logHeaders[h.Key] = string.Join(", ", h.Value);
            }

            // Create a new HttpClient for each request (Blazor WASM compatible)
            // This uses the browser's fetch API under the hood
            using var httpClient = new HttpClient();
            var response = await httpClient.SendAsync(clonedRequest, cancellationToken);

            // Log request/response for debug display
            try
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _state.SetLastRequestLog(new FhirRequestLog
                {
                    Method = logMethod,
                    Url = logUrl,
                    RequestHeaders = logHeaders,
                    StatusCode = (int)response.StatusCode,
                    ResponseBody = responseBody.Length > 2000 ? responseBody[..2000] + "\n... (truncated)" : responseBody,
                    Timestamp = DateTime.Now
                });
            }
            catch { /* don't let logging break the request */ }

            return response;
        }

        private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Version = request.Version
            };

            // Copy headers
            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            // Copy content if present
            if (request.Content != null)
            {
                var contentBytes = await request.Content.ReadAsByteArrayAsync();
                clone.Content = new ByteArrayContent(contentBytes);

                // Copy content headers
                foreach (var header in request.Content.Headers)
                {
                    clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            return clone;
        }
    }

    public async Task<Patient?> GetPatientByIdAsync(string id)
    {
        try 
        {
            return await _client.ReadAsync<Patient>($"Patient/{id}");
        }
        catch (FhirOperationException)
        {
            return null;
        }
    }

    public async Task<Bundle> SearchPatientsAsync(string? name = null)
    {
        var q = new SearchParams();
        if(!string.IsNullOrEmpty(name))
        {
            q.Add("name:contains", name);
        }
        
        // Return empty bundle if search fails or returns null to be safe, though SearchAsync usually returns a Bundle
        return await _client.SearchAsync<Patient>(q) ?? new Bundle();
    }

    public async Task<Patient?> SearchPatientByIdentifierAsync(string system, string value)
    {
        var q = new SearchParams();
        q.Add("identifier", $"{system}|{value}");
        
        var bundle = await _client.SearchAsync<Patient>(q);
        
        return bundle?.Entry.Select(e => e.Resource as Patient).FirstOrDefault(p => p != null);
    }

    public async Task<Bundle> SearchResourceAsync(string resourceType, string queryString)
    {
        try 
        {
            // Use GetAsync which returns a Resource (could be Bundle or OperationOutcome)
            var resource = await _client.GetAsync($"{resourceType}?{queryString}");
            
            // Check if the response is an OperationOutcome instead of a Bundle
            if (resource is OperationOutcome outcome)
            {
                // Wrap the OperationOutcome in a Bundle so it can be processed consistently
                var errorBundle = new Bundle
                {
                    Type = Bundle.BundleType.Searchset,
                    Total = 1,
                    Entry = new List<Bundle.EntryComponent>
                    {
                        new Bundle.EntryComponent { Resource = outcome }
                    }
                };
                return errorBundle;
            }
            
            return resource as Bundle ?? new Bundle();
        }
        catch (FhirOperationException ex)
        {
            if (ex.Outcome != null)
            {
                 return new Bundle
                {
                    Type = Bundle.BundleType.Searchset,
                    Total = 1,
                    Entry = new List<Bundle.EntryComponent>
                    {
                        new Bundle.EntryComponent { Resource = ex.Outcome }
                    }
                };
            }
            return new Bundle(); // Fallback if no outcome
        }
    }

    public async Task<Hl7.Fhir.Model.Resource?> GetAsync(string path)
    {
        try
        {
            return await _client.GetAsync(path);
        }
        catch (FhirOperationException ex)
        {
            return ex.Outcome; // Return the outcome on error so caller can inspect it
        }
    }

    public async Task<Bundle> SearchPractitionersAsync(string? name = null)
    {
        var q = new SearchParams();
        if(!string.IsNullOrEmpty(name))
        {
            q.Add("name:contains", name);
        }
        
        return await _client.SearchAsync<Practitioner>(q) ?? new Bundle();
    }

    public async Task<Practitioner?> GetPractitionerByIdAsync(string id)
    {
        try 
        {
            return await _client.ReadAsync<Practitioner>($"Practitioner/{id}");
        }
        catch (FhirOperationException)
        {
            return null;
        }
    }

    public async Task<Bundle> SearchRelatedPersonsAsync(string? name = null)
    {
        var q = new SearchParams();
        if(!string.IsNullOrEmpty(name))
        {
            q.Add("name:contains", name);
        }
        
        return await _client.SearchAsync<RelatedPerson>(q) ?? new Bundle();
    }

    public async Task<Practitioner?> LoadDefaultPractitionerAsync()
    {
        try
        {
            var json = await _httpClient.GetStringAsync("data/default-practitioner.json");
            var deserializer = new FhirJsonDeserializer();
            var practitioner = deserializer.Deserialize<Practitioner>(json);
            return practitioner;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading default practitioner: {ex.Message}");
            return null;
        }

    }

    public async Task<Bundle> TransactionAsync(Bundle bundle)
    {
        return await _client.TransactionAsync(bundle) ?? new Bundle();
    }

    public async Task<FhirOperationResult<T>> CreateAsync<T>(T resource) where T : Resource
    {
        try
        {
            var result = await _client.CreateAsync(resource);
            return result != null 
                ? FhirOperationResult<T>.Success(result) 
                : FhirOperationResult<T>.Failure(null, "Server returned null response");
        }
        catch (FhirOperationException ex)
        {
            return FhirOperationResult<T>.Failure(ex.Outcome, ex.Message);
        }
        catch (Exception ex)
        {
            return FhirOperationResult<T>.Failure(null, ex.Message);
        }
    }

    public async Task<FhirOperationResult<T>> UpdateAsync<T>(T resource) where T : Resource
    {
        try
        {
            var result = await _client.UpdateAsync(resource);
            return result != null 
                ? FhirOperationResult<T>.Success(result) 
                : FhirOperationResult<T>.Failure(null, "Server returned null response");
        }
        catch (FhirOperationException ex)
        {
            return FhirOperationResult<T>.Failure(ex.Outcome, ex.Message);
        }
        catch (Exception ex)
        {
            return FhirOperationResult<T>.Failure(null, ex.Message);
        }
    }

    public async Task<FhirOperationResult<Resource>> DeleteAsync(string resourceType, string id)
    {
        try
        {
            await _client.DeleteAsync($"{resourceType}/{id}");
            // Delete returns void on success, so we create a minimal success indicator
            return FhirOperationResult<Resource>.Success(new OperationOutcome
            {
                Issue = new List<OperationOutcome.IssueComponent>
                {
                    new() { Severity = OperationOutcome.IssueSeverity.Information, Code = OperationOutcome.IssueType.Deleted, Diagnostics = "Resource deleted successfully" }
                }
            });
        }
        catch (FhirOperationException ex)
        {
            return FhirOperationResult<Resource>.Failure(ex.Outcome, ex.Message);
        }
        catch (Exception ex)
        {
            return FhirOperationResult<Resource>.Failure(null, ex.Message);
        }
    }

    public async Task<FhirOperationResult<T>> ReadAsync<T>(string id) where T : Resource, new()
    {
        try
        {
            var result = await _client.ReadAsync<T>($"{typeof(T).Name}/{id}");
            return result != null 
                ? FhirOperationResult<T>.Success(result) 
                : FhirOperationResult<T>.Failure(null, "Resource not found");
        }
        catch (FhirOperationException ex)
        {
            return FhirOperationResult<T>.Failure(ex.Outcome, ex.Message);
        }
        catch (Exception ex)
        {
            return FhirOperationResult<T>.Failure(null, ex.Message);
        }
    }
}
