using FauxHR.Core.Interfaces;
using FauxHR.Core.Services;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
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
                Title = "ACP related procedures and encounters", 
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
                Title = "Events", 
                ResourceType = "Communication", 
                QueryString = $"patient=Patient/{patientId}&reason-code=http://snomed.info/sct|713603004" 
            },
            new() 
            { 
                Title = "QuestionnaireResponse", 
                ResourceType = "QuestionnaireResponse", 
                QueryString = $"subject=Patient/{patientId}&questionnaire=https://api.iknl.nl/docs/pzp/r4/Questionnaire/ACP-zib2020" 
            }
        };
    }

    public async Task ExecuteQueryAsync(AcpQuery query)
    {
        if (query.Status == QueryStatus.Running) return;

        query.Status = QueryStatus.Running;
        // Ideally trigger UI update here via callback if passed, but simpler to rely on caller re-rendering.
        
        try
        {
            var bundle = await _fhirService.SearchResourceAsync(query.ResourceType, query.QueryString);
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

            // Save to LocalStorage (even errors, so user can inspect them)
            if (bundle.Entry != null)
            {
                foreach (var entry in bundle.Entry)
                {
                    if (entry.Resource != null)
                    {
                        var res = entry.Resource;
                        
                        // Update Meta.Source
                        if (res.Meta == null) res.Meta = new Meta();
                        res.Meta.Source = _appState.CurrentServerUrl;

                        // Generate Filename-like Key: [ResourceType]-[ID]-[YYYYMMDD]
                        // Ensure ID is set (generate one if missing, e.g., for OperationOutcome)
                        var resourceId = !string.IsNullOrEmpty(res.Id) ? res.Id : Guid.NewGuid().ToString();
                        var key = $"{res.TypeName}-{resourceId}-{DateTime.Now:yyyyMMdd}";
                        
                        // Serialize with pretty print
                        var serializer = new FhirJsonSerializer();
                        var rawJson = serializer.SerializeToString(res);
                        
                        // Pretty-print the JSON using System.Text.Json
                        try
                        {
                            var jsonDocument = System.Text.Json.JsonDocument.Parse(rawJson);
                            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                            var json = System.Text.Json.JsonSerializer.Serialize(jsonDocument, options);
                            
                            // Save
                            await _localStorage.SetItemAsStringAsync(key, json);
                        }
                        catch
                        {
                            // If formatting fails, save raw JSON
                            await _localStorage.SetItemAsStringAsync(key, rawJson);
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
}
