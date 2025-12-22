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
        var customHandler = new CustomHeaderHandler(_appState);

        _client = new FhirClient(_appState.CurrentServerUrl, 
            new FhirClientSettings
            {
                PreferredFormat = ResourceFormat.Json,
                VerifyFhirVersion = true
            },
            customHandler);
    }

    // Custom Handler to inject headers - creates fresh HttpClient for each request
    private class CustomHeaderHandler : HttpMessageHandler
    {
        private readonly AppState _state;

        public CustomHeaderHandler(AppState state)
        {
            _state = state;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Clone the request to avoid "already sent" error
            var clonedRequest = await CloneHttpRequestMessageAsync(request);
            
            // Add custom headers only when in conformance testing mode
            if (_state.ConformanceTestingMode)
            {
                foreach (var header in _state.CustomHeaders)
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
            
            // Create a new HttpClient for each request (Blazor WASM compatible)
            // This uses the browser's fetch API under the hood
            using var httpClient = new HttpClient();
            return await httpClient.SendAsync(clonedRequest, cancellationToken);
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
}
