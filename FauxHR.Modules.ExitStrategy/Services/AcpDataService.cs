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
                Title = "Procedures", 
                Description = "ACP procedures", 
                ResourceType = "Procedure", 
                QueryString = $"patient=Patient/{patientId}&code=http://snomed.info/sct|713603004&_include=Procedure:encounter" 
            },
            new() 
            { 
                Title = "Consent (Treatment)", 
                Description = "Treatment Directives", 
                ResourceType = "Consent", 
                QueryString = $"patient=Patient/{patientId}&scope=http://terminology.hl7.org/CodeSystem/consentscope|treatment&category=http://snomed.info/sct|129125009&_include=Consent:actor" 
            },
            new() 
            { 
                Title = "Consent (Advance)", 
                Description = "Advance Directives", 
                ResourceType = "Consent", 
                QueryString = $"patient=Patient/{patientId}&scope=http://terminology.hl7.org/CodeSystem/consentscope|adr&category=http://terminology.hl7.org/CodeSystem/consentcategorycodes|acd&_include=Consent:actor" 
            },
            new() 
            { 
                Title = "Goals", 
                Description = "Medical Policy Goals", 
                ResourceType = "Goal", 
                QueryString = $"patient=Patient/{patientId}&description=http://snomed.info/sct|385987000,1351964001,713148004" 
            },
            new() 
            { 
                Title = "Observations", 
                Description = "Wishes and Plans", 
                ResourceType = "Observation", 
                QueryString = $"patient=Patient/{patientId}&code=http://snomed.info/sct|153851000146100,395091006,340171000146104,247751003" 
            },
            new() 
            { 
                Title = "DeviceUseStatement and Devices", 
                Description = "ICD Devices", 
                ResourceType = "DeviceUseStatement", 
                QueryString = $"patient=Patient/{patientId}&device.type:in=https://api.iknl.nl/docs/pzp/r4/ValueSet/ACP-MedicalDeviceProductType-ICD&_include=DeviceUseStatement:device" 
            },
            new() 
            { 
                Title = "Communication", 
                Description = "ACP Events", 
                ResourceType = "Communication", 
                QueryString = $"patient=Patient/{patientId}&reason-code=http://snomed.info/sct|713603004" 
            },
            new() 
            { 
                Title = "QuestionnaireResponse", 
                Description = "ACP Form", 
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
            query.Status = QueryStatus.Success;

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
                        res.Meta.Source = _appState.CurrentServerUrl;



                        // Generate Filename-like Key: [ResourceType]-[ID]-[YYYYMMDD]
                        var key = $"{res.TypeName}-{res.Id}-{DateTime.Now:yyyyMMdd}";
                        
                        // Serialize (using FHIR Serializer)
                        var serializer = new FhirJsonSerializer();
                        var json = serializer.SerializeToString(res);
                        
                        // Save
                        await _localStorage.SetItemAsStringAsync(key, json);
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
