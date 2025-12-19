using Blazored.LocalStorage;
using FauxHR.Core.Services;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Task = System.Threading.Tasks.Task;

namespace FauxHR.App.Services;

public class PractitionerContextService
{
    private readonly AppState _appState;
    private readonly ILocalStorageService _localStorage;
    private readonly FhirJsonDeserializer _parser = new();
    private readonly FhirJsonSerializer _serializer = new();
    
    private const string PRACTITIONER_KEY = "CurrentPractitioner";
    private const string PRACTITIONER_ROLE_KEY = "CurrentPractitionerRole";

    public PractitionerContextService(AppState appState, ILocalStorageService localStorage)
    {
        _appState = appState;
        _localStorage = localStorage;
    }

    public async Task InitializeDefaultPractitionerAsync()
    {
        // Try to load from local storage first
        var storedPractitioner = await LoadFromStorageAsync();
        
        if (storedPractitioner.practitioner != null)
        {
            _appState.SetPractitioner(storedPractitioner.practitioner, storedPractitioner.role);
        }
        else
        {
            // Load default practitioner
            var defaultPractitioner = GetDefaultPractitioner();
            var defaultRole = GetDefaultPractitionerRole();
            
            await SaveToStorageAsync(defaultPractitioner, defaultRole);
            _appState.SetPractitioner(defaultPractitioner, defaultRole);
        }
    }

    public async Task SetPractitionerAsync(Practitioner practitioner, PractitionerRole? role = null)
    {
        await SaveToStorageAsync(practitioner, role);
        _appState.SetPractitioner(practitioner, role);
    }

    private async Task<(Practitioner? practitioner, PractitionerRole? role)> LoadFromStorageAsync()
    {
        try
        {
            var practitionerJson = await _localStorage.GetItemAsStringAsync(PRACTITIONER_KEY);
            var roleJson = await _localStorage.GetItemAsStringAsync(PRACTITIONER_ROLE_KEY);

            Practitioner? practitioner = null;
            PractitionerRole? role = null;

            if (!string.IsNullOrEmpty(practitionerJson))
            {
                practitioner = _parser.Deserialize<Practitioner>(practitionerJson);
            }

            if (!string.IsNullOrEmpty(roleJson))
            {
                role = _parser.Deserialize<PractitionerRole>(roleJson);
            }

            return (practitioner, role);
        }
        catch
        {
            return (null, null);
        }
    }

    private async Task SaveToStorageAsync(Practitioner practitioner, PractitionerRole? role)
    {
        var practitionerJson = _serializer.SerializeToString(practitioner);
        await _localStorage.SetItemAsStringAsync(PRACTITIONER_KEY, practitionerJson);

        if (role != null)
        {
            var roleJson = _serializer.SerializeToString(role);
            await _localStorage.SetItemAsStringAsync(PRACTITIONER_ROLE_KEY, roleJson);
        }
    }

    private Practitioner GetDefaultPractitioner()
    {
        var json = @"{
  ""resourceType"": ""Practitioner"",
  ""id"": ""nl-core-TreatmentDirective2-01-Practitioner-01"",
  ""meta"": {
    ""profile"": [
      ""http://nictiz.nl/fhir/StructureDefinition/nl-core-HealthProfessional-Practitioner""
    ]
  },
  ""identifier"": [
    {
      ""system"": ""http://fhir.nl/fhir/NamingSystem/big"",
      ""value"": ""21870932 2.16.528.1.1007.5.1""
    }
  ],
  ""name"": [
    {
      ""use"": ""official"",
      ""text"": ""J.H.R. Peters"",
      ""family"": ""Peters"",
      ""given"": [
        ""J."",
        ""H."",
        ""R.""
      ],
      ""prefix"": [
        ""Dr.""
      ]
    }
  ],
  ""telecom"": [
    {
      ""system"": ""phone"",
      ""value"": ""+3715828282"",
      ""use"": ""work""
    },
    {
      ""system"": ""email"",
      ""value"": ""j.peters@hospital.nl"",
      ""use"": ""work""
    }
  ],
  ""address"": [
    {
      ""line"": [
        ""Simon Smitweg 1""
      ],
      ""city"": ""Leiderdorp"",
      ""postalCode"": ""2353 GA"",
      ""country"": ""Nederland""
    }
  ],
  ""gender"": ""female""
}";
        return _parser.Deserialize<Practitioner>(json);
    }

    private PractitionerRole GetDefaultPractitionerRole()
    {
        var json = @"{
  ""resourceType"": ""PractitionerRole"",
  ""id"": ""nl-core-TreatmentDirective2-01-PractitionerRole-01"",
  ""meta"": {
    ""profile"": [
      ""http://nictiz.nl/fhir/StructureDefinition/nl-core-HealthProfessional-PractitionerRole""
    ]
  },
  ""practitioner"": {
    ""reference"": ""Practitioner/nl-core-TreatmentDirective2-01-Practitioner-01"",
    ""type"": ""Practitioner"",
    ""display"": ""Healthcare professional (person), J.H.R. Peters""
  },
  ""specialty"": [
    {
      ""coding"": [
        {
          ""system"": ""http://fhir.nl/fhir/NamingSystem/uzi-rolcode"",
          ""version"": ""2020-04-01T00:00:00"",
          ""code"": ""01.000"",
          ""display"": ""Arts""
        }
      ]
    }
  ],
  ""telecom"": [
    {
      ""system"": ""phone"",
      ""value"": ""+3715828282"",
      ""use"": ""work""
    },
    {
      ""system"": ""email"",
      ""value"": ""j.peters@hospital.nl"",
      ""use"": ""work""
    }
  ]
}";
        return _parser.Deserialize<PractitionerRole>(json);
    }
}
