using FauxHR.Core.Interfaces;
using FauxHR.Core.Services;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using System.Text.Json;
using Hl7.Fhir.Rest;
using Task = System.Threading.Tasks.Task;

namespace FauxHR.Modules.ExitStrategy.Services;

public class AcpQuery
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string QueryString { get; set; } = string.Empty;
    public QueryStatus Status { get; set; } = QueryStatus.Pending;
    public Bundle? Result { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum QueryStatus
{
    Pending,
    Running,
    Success,
    Error
}

public class AcpDataService
{
    private readonly IFhirService _fhirService;
    private readonly AppState _appState;
    private readonly Blazored.LocalStorage.ILocalStorageService _localStorage;

    public AcpDataService(IFhirService fhirService, AppState appState, Blazored.LocalStorage.ILocalStorageService localStorage)
    {
        _fhirService = fhirService;
        _appState = appState;
        _localStorage = localStorage;
    }

    public List<AcpQuery> GetQueries(string patientId)
    {
        return new List<AcpQuery>
        {
            new() 
            { 
                Title = "ACP Procedures and Encounters", 
                ResourceType = "Procedure", 
                QueryString = $"patient=Patient/{patientId}&code=http://snomed.info/sct|713603004&_include=Procedure:encounter" 
            },
            new() 
            { 
                Title = "Treatment Directives", 
                ResourceType = "Consent", 
                QueryString = $"patient=Patient/{patientId}&scope=http://terminology.hl7.org/CodeSystem/consentscope|treatment&category=http://snomed.info/sct|129125009&_include=Consent:actor" 
            },
            new() 
            { 
                Title = "Advance Directives", 
                ResourceType = "Consent", 
                QueryString = $"patient=Patient/{patientId}&scope=http://terminology.hl7.org/CodeSystem/consentscope|adr&category=http://terminology.hl7.org/CodeSystem/consentcategorycodes|acd&_include=Consent:actor" 
            },
            new() 
            { 
                Title = "Medical Policy Goals", 
                ResourceType = "Goal", 
                QueryString = $"patient=Patient/{patientId}&description=http://snomed.info/sct|385987000,1351964001,713148004" 
            },
            new() 
            { 
                Title = "Specific Care Observations", 
                ResourceType = "Observation", 
                QueryString = $"patient=Patient/{patientId}&code=http://snomed.info/sct|153851000146100,395091006,340171000146104,247751003" 
            },
            new() 
            { 
                Title = "ICD Medical Devices", 
                ResourceType = "DeviceUseStatement", 
                QueryString = $"patient=Patient/{patientId}&device.type:in=https://api.iknl.nl/docs/pzp/r4/ValueSet/ACP-MedicalDeviceProductType-ICD&_include=DeviceUseStatement:device" 
            },
            new() 
            { 
                Title = "Communications", 
                ResourceType = "Communication", 
                QueryString = $"patient=Patient/{patientId}&reason-code=http://snomed.info/sct|713603004" 
            },
            new() 
            { 
                Title = "ACP Forms", 
                ResourceType = "QuestionnaireResponse", 
                QueryString = $"subject=Patient/{patientId}&questionnaire=https://api.iknl.nl/docs/pzp/r4/Questionnaire/ACP-zib2020" 
            }
        };
    }

    public async Task<string?> FindPatientIdByIdentifierAsync(string serverUrl, string system, string value)
    {
        try
        {
            // Use existing service if querying the current server (optimization & consistency)
            if (serverUrl == _appState.CurrentServerUrl)
            {
                var patient = await _fhirService.SearchPatientByIdentifierAsync(system, value);
                return patient?.Id;
            }

            // Use explicit FhirClient for dynamic server URLs
            // Critical: Check for browser platform or just always pass handler to be safe in Blazor WASM
            using var handler = new HttpClientHandler(); 
            var settings = new Hl7.Fhir.Rest.FhirClientSettings 
            { 
                VerifyFhirVersion = false,
                PreferredFormat = ResourceFormat.Json 
            };
            var client = new Hl7.Fhir.Rest.FhirClient(serverUrl, settings, handler); // Handler avoids PlatformNotSupportedException
            
            var query = new[] { $"identifier={system}|{value}" };
            
            var bundle = await client.SearchAsync<Patient>(query);
            
            if (bundle?.Entry != null && bundle.Entry.Count >= 1 && bundle.Entry[0].Resource is Patient p)
            {
                return p.Id;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error finding patient on {serverUrl}: {ex.Message}");
            return null;
        }
    }

    public async Task ExecuteQueryAsync(AcpQuery query, string? serverUrl = null, string? serverLabel = null)
    {
        if (query.Status == QueryStatus.Running) return;

        query.Status = QueryStatus.Running;
        
        try
        {
            Bundle? bundle;
            string effectiveServerUrl = serverUrl ?? _appState.CurrentServerUrl;

            // Normalize URLs for comparison (trim trailing slashes)
            bool isCurrentServer = string.IsNullOrEmpty(serverUrl) || 
                                   serverUrl.TrimEnd('/') == _appState.CurrentServerUrl.TrimEnd('/');

            if (!isCurrentServer && !string.IsNullOrEmpty(serverUrl))
            {
                // Dynamic server query
                using var handler = new HttpClientHandler();
                var settings = new Hl7.Fhir.Rest.FhirClientSettings 
                { 
                    VerifyFhirVersion = false,
                    PreferredFormat = ResourceFormat.Json
                };
                var client = new Hl7.Fhir.Rest.FhirClient(serverUrl, settings, handler);

                // Use GetAsync to handle the pre-formatted query string exactly as defined
                var resource = await client.GetAsync($"{query.ResourceType}?{query.QueryString}");
                
                if (resource is OperationOutcome outcome)
                {
                     bundle = new Bundle
                     {
                         Type = Bundle.BundleType.Searchset,
                         Total = 1,
                         Entry = new List<Bundle.EntryComponent> { new Bundle.EntryComponent { Resource = outcome } }
                     };
                }
                else
                {
                    bundle = resource as Bundle ?? new Bundle();
                }
            }
            else
            {
                // Default service (already handled correctly)
                bundle = await _fhirService.SearchResourceAsync(query.ResourceType, query.QueryString);
            }
            
            query.Result = bundle;
            
            // Check if the bundle itself is null or if it contains OperationOutcome
            bool hasError = false;
            OperationOutcome? errorOutcome = null;
            
            // Case 1: The entire response is a single OperationOutcome (not wrapped in a Bundle)
            if (bundle.Entry == null || !bundle.Entry.Any())
            {
                // Check if this is actually an OperationOutcome response
                if (bundle.Type == Bundle.BundleType.Searchset && bundle.Total == 0)
                {
                    // Empty result set - this is normal, not an error
                    hasError = false;
                }
            }
            else
            {
                // Case 2: OperationOutcome is in the Bundle entries
                foreach (var entry in bundle.Entry)
                {
                    if (entry.Resource is OperationOutcome outcome)
                    {
                        hasError = true;
                        errorOutcome = outcome;
                        // Extract error message from OperationOutcome if available
                        var issue = outcome.Issue.FirstOrDefault();
                        query.ErrorMessage = issue != null ? $"{issue.Severity}: {issue.Diagnostics}" : "Server returned an error (OperationOutcome)";
                        break;
                    }
                }
            }
            
            query.Status = hasError ? QueryStatus.Error : QueryStatus.Success;
            
            // Save to LocalStorage
            if (bundle.Entry != null)
            {
                foreach (var entry in bundle.Entry)
                {
                    if (entry.Resource != null)
                    {
                        var res = entry.Resource;
                        
                        // Update Meta.Source
                        if (res.Meta == null) res.Meta = new Meta();
                        res.Meta.Source = effectiveServerUrl;

                        // Skip saving OperationOutcome from a "Success" query unless it's the specific error we found earlier?
                        // Actually the user wants to see it. If hasError is true, the UI handles it via 'ShowErrorDetails'.
                        // But we should save valid resources found.
                        // If the resource is OperationOutcome, saving it might be useful for persistent logs, 
                        // but `Overview.razor` tiles typically show clinical resources.
                        // Let's save everything.
                        
                        // Generate Source-Scoped Key
                        string storageKey = GetStorageKey(effectiveServerUrl, res);
                        
                        // Deduplication Logic: Check LastUpdated
                        bool shouldSave = true;
                        
                        if (await _localStorage.ContainKeyAsync(storageKey))
                        {
                            try 
                            {
                                var existingJson = await _localStorage.GetItemAsStringAsync(storageKey);
                                
                                // Lightweight check using System.Text.Json
                                if (!string.IsNullOrEmpty(existingJson))
                                {
                                    using var doc = JsonDocument.Parse(existingJson);
                                    if (doc.RootElement.TryGetProperty("meta", out var meta) && 
                                        meta.TryGetProperty("lastUpdated", out var lastUpdatedProp) &&
                                        lastUpdatedProp.TryGetDateTime(out var existingLastUpdated))
                                    {
                                        if (res.Meta?.LastUpdated != null && res.Meta.LastUpdated <= existingLastUpdated)
                                        {
                                            shouldSave = false; // Existing is newer or same
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // If parsing fails, default to overwriting (safer to have latest pulled data)
                                Console.WriteLine($"Error checking existing resource {storageKey}: {ex.Message}");
                            }
                        }
                        
                        if (shouldSave)
                        {
                            // Serialize with pretty print
                            var serializer = new FhirJsonSerializer();
                            var rawJson = serializer.SerializeToString(res);
                            
                            try
                            {
                                var jsonDocument = System.Text.Json.JsonDocument.Parse(rawJson);
                                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                                var json = System.Text.Json.JsonSerializer.Serialize(jsonDocument, options);
                                await _localStorage.SetItemAsStringAsync(storageKey, json);
                            }
                            catch
                            {
                                await _localStorage.SetItemAsStringAsync(storageKey, rawJson);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            query.ErrorMessage = ex.Message;
            query.Status = QueryStatus.Error;
        }
    }

    private string GetStorageKey(string serverUrl, Resource res)
    {
        // Create a slug for the server to keep keys readable but unique per source
        // E.g. https://server.fire.ly -> server_fire_ly
        // http://hapi.fhir.org/baseR4 -> hapi_fhir_org_baseR4
        var uri = new Uri(serverUrl);
        var hostSlug = uri.Host.Replace(".", "_");
        var pathSlug = uri.AbsolutePath.Trim('/').Replace("/", "_");
        var serverSlug = $"{hostSlug}_{(string.IsNullOrEmpty(pathSlug) ? "" : pathSlug)}".TrimEnd('_');
        
        // Ensure ID presence
        var resourceId = !string.IsNullOrEmpty(res.Id) ? res.Id : Guid.NewGuid().ToString();
        
        return $"{serverSlug}_{res.TypeName}_{resourceId}"; 
    }

    public async Task<AcpQuery> ResolveReferencesAsync(List<AcpQuery> completedQueries)
    {
        var resolveQuery = new AcpQuery
        {
            Title = "Fetching referenced resources",
            ResourceType = "RelatedPerson, PractitionerRole, Practitioner, ect.",
            QueryString = "",
            Status = QueryStatus.Running
        };

        try
        {
            var existingKeys = await _localStorage.KeysAsync();
            var fetchedResources = new List<Hl7.Fhir.Model.Resource>();
            var allEntries = new List<Bundle.EntryComponent>();
            
            // Collect initial references from completed queries
            var referencedUrls = new HashSet<string>();
            foreach (var query in completedQueries)
            {
                if (query.Result?.Entry == null) continue;
                
                foreach (var entry in query.Result.Entry)
                {
                    if (entry.Resource == null) continue;
                    ExtractReferences(entry.Resource, referencedUrls);
                }
            }

            // Iteratively resolve references (handles second-level and deeper)
            var processedUrls = new HashSet<string>();
            int iteration = 0;
            int maxIterations = _appState.ReferenceResolutionDepth;
            
            while (referencedUrls.Any() && iteration < maxIterations)
            {
                iteration++;
                var urlsToProcess = referencedUrls.Except(processedUrls).ToList();
                
                foreach (var refUrl in urlsToProcess)
                {
                    processedUrls.Add(refUrl);
                    
                    try
                    {
                        var parts = refUrl.Split('/');
                        if (parts.Length < 2) continue;
                        
                        var resourceType = parts[^2];
                        var resourceId = parts[^1];
                        
                        // Check if we already have this resource
                        var keyPattern = $"{resourceType}-{resourceId}";
                        if (existingKeys.Any(k => k.StartsWith(keyPattern)))
                        {
                            continue;
                        }
                        
                        // Fetch the resource
                        var resource = await _fhirService.GetAsync($"{resourceType}/{resourceId}");
                        if (resource != null)
                        {
                            // Update Meta.Source
                            if (resource.Meta == null) resource.Meta = new Meta();
                            resource.Meta.Source = _appState.CurrentServerUrl;

                            // Save to LocalStorage
                            var key = $"{resource.TypeName}-{resource.Id}-{DateTime.Now:yyyyMMdd}";
                            var serializer = new FhirJsonSerializer();
                            var rawJson = serializer.SerializeToString(resource);
                            
                            // Pretty-print
                            try
                            {
                                var jsonDocument = JsonDocument.Parse(rawJson);
                                var options = new JsonSerializerOptions { WriteIndented = true };
                                var json = JsonSerializer.Serialize(jsonDocument, options);
                                await _localStorage.SetItemAsStringAsync(key, json);
                            }
                            catch
                            {
                                await _localStorage.SetItemAsStringAsync(key, rawJson);
                            }
                            
                            allEntries.Add(new Bundle.EntryComponent { Resource = resource });
                            fetchedResources.Add(resource);
                            
                            // Extract references from this newly fetched resource
                            ExtractReferences(resource, referencedUrls);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error fetching reference {refUrl}: {ex.Message}");
                    }
                }
            }

            resolveQuery.Result = new Bundle
            {
                Total = fetchedResources.Count,
                Entry = allEntries
            };
            resolveQuery.Status = QueryStatus.Success;
        }
        catch (Exception ex)
        {
            resolveQuery.ErrorMessage = ex.Message;
            resolveQuery.Status = QueryStatus.Error;
        }

        return resolveQuery;
    }

    private void ExtractReferences(Hl7.Fhir.Model.Resource resource, HashSet<string> references)
    {
        if (resource is Procedure procedure)
        {
            AddReference(procedure.Encounter?.Reference, references);
            foreach (var performer in procedure.Performer ?? new List<Procedure.PerformerComponent>())
            {
                AddReference(performer.Actor?.Reference, references);
            }
        }
        else if (resource is Encounter encounter)
        {
            foreach (var participant in encounter.Participant ?? new List<Encounter.ParticipantComponent>())
            {
                AddReference(participant.Individual?.Reference, references);
            }
            AddReference(encounter.Subject?.Reference, references);
        }
        else if (resource is Consent consent)
        {
            foreach (var provision in consent.Provision?.Provision ?? new List<Consent.provisionComponent>())
            {
                foreach (var actor in provision.Actor ?? new List<Consent.provisionActorComponent>())
                {
                    AddReference(actor.Reference?.Reference, references);
                }
            }
        }
        else if (resource is Observation observation)
        {
            foreach (var performer in observation.Performer ?? new List<ResourceReference>())
            {
                AddReference(performer.Reference, references);
            }
        }
        else if (resource is PractitionerRole practitionerRole)
        {
            AddReference(practitionerRole.Practitioner?.Reference, references);
            AddReference(practitionerRole.Organization?.Reference, references);
        }
    }

    private void AddReference(string? reference, HashSet<string> references)
    {
        if (string.IsNullOrEmpty(reference)) return;
        
        // Handle both relative and absolute references
        string normalizedRef = reference;
        
        // If it's an absolute URL pointing to the current server, make it relative
        if (reference.StartsWith(_appState.CurrentServerUrl))
        {
            normalizedRef = reference.Substring(_appState.CurrentServerUrl.Length).TrimStart('/');
        }
        else if (reference.StartsWith("http://") || reference.StartsWith("https://"))
        {
            // It's an absolute URL to a different server - skip it
            return;
        }
        
        if (!string.IsNullOrEmpty(normalizedRef))
        {
            references.Add(normalizedRef);
        }
    }
}
