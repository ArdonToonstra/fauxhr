using FauxHR.Core.Interfaces;
using FauxHR.Core.Services;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;

namespace FauxHR.App.Services;

public class FhirService : IFhirService
{
    private readonly AppState _appState;
    private FhirClient _client;

    public FhirService(AppState appState)
    {
        _appState = appState;
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
        // In Blazor WASM, we must provide a handler to avoid FhirClient trying to set 
        // AutomaticDecompression, which throws PlatformNotSupportedException.
        var handler = new HttpClientHandler();

        _client = new FhirClient(_appState.CurrentServerUrl, 
            new FhirClientSettings
            {
                PreferredFormat = ResourceFormat.Json,
                VerifyFhirVersion = true
            },
            handler);
    }

    public async Task<Patient?> GetPatientAsync(string id)
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
}
