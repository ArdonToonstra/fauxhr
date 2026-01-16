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
    private readonly FhirJsonDeserializer _deserializer = new();
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
        // Always load the default practitioner (updated J.H.R. Peters)
        var defaultPractitioner = GetDefaultPractitioner();
        var defaultRole = GetDefaultPractitionerRole();
        
        await SaveToStorageAsync(defaultPractitioner, defaultRole);
        _appState.SetPractitioner(defaultPractitioner, defaultRole);
        
        // Set the default organization context
        _appState.SetOrganization("Leiderdorp University Medical Center", "nl-core-organization-01");
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
                practitioner = _deserializer.Deserialize<Practitioner>(practitionerJson);
            }

            if (!string.IsNullOrEmpty(roleJson))
            {
                role = _deserializer.Deserialize<PractitionerRole>(roleJson);
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
  ""id"": ""nl-core-HealthProfessional-Practitioner-01"",
  ""meta"": {
    ""profile"": [
      ""http://nictiz.nl/fhir/StructureDefinition/nl-core-HealthProfessional-Practitioner""
    ]
  },
  ""text"": {
    ""status"": ""extensions"",
    ""div"": ""<div xmlns=\""http://www.w3.org/1999/xhtml\""><div>Id 21870932 (BIG), J.H.R. Peters</div><div><a href=\""tel:+3715828282\"">+3715828282</a> (Tel Werk), <a href=\""mailto:j.peters@hospital.nl\"">j.peters@hospital.nl</a> (E-mail Werk)</div><div>Simon Smitweg 1, 2353 GA Leiderdorp, Nederland (Werk)</div></div>""
  },
  ""identifier"": [
    {
      ""system"": ""http://fhir.nl/fhir/NamingSystem/big"",
      ""value"": ""21870932""
    }
  ],
  ""name"": [
    {
      ""use"": ""official"",
      ""text"": ""J.H.R. Peters"",
      ""family"": ""Peters"",
      ""_family"": {
        ""extension"": [
          {
            ""url"": ""http://hl7.org/fhir/StructureDefinition/humanname-own-name"",
            ""valueString"": ""Peters""
          }
        ]
      },
      ""given"": [
        ""J."",
        ""H."",
        ""R.""
      ],
      ""_given"": [
        {
          ""extension"": [
            {
              ""url"": ""http://hl7.org/fhir/StructureDefinition/iso21090-EN-qualifier"",
              ""valueCode"": ""IN""
            }
          ]
        },
        {
          ""extension"": [
            {
              ""url"": ""http://hl7.org/fhir/StructureDefinition/iso21090-EN-qualifier"",
              ""valueCode"": ""IN""
            }
          ]
        },
        {
          ""extension"": [
            {
              ""url"": ""http://hl7.org/fhir/StructureDefinition/iso21090-EN-qualifier"",
              ""valueCode"": ""IN""
            }
          ]
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
  ],
  ""address"": [
    {
      ""extension"": [
        {
          ""url"": ""http://nictiz.nl/fhir/StructureDefinition/ext-AddressInformation.AddressType"",
          ""valueCodeableConcept"": {
            ""coding"": [
              {
                ""system"": ""http://terminology.hl7.org/CodeSystem/v3-AddressUse"",
                ""code"": ""WP"",
                ""display"": ""Work Place""
              }
            ]
          }
        }
      ],
      ""use"": ""work"",
      ""line"": [
        ""Simon Smitweg 1""
      ],
      ""_line"": [
        {
          ""extension"": [
            {
              ""url"": ""http://hl7.org/fhir/StructureDefinition/iso21090-ADXP-streetName"",
              ""valueString"": ""Simon Smitweg""
            },
            {
              ""url"": ""http://hl7.org/fhir/StructureDefinition/iso21090-ADXP-houseNumber"",
              ""valueString"": ""1""
            }
          ]
        }
      ],
      ""city"": ""Leiderdorp"",
      ""postalCode"": ""2353 GA"",
      ""country"": ""Nederland"",
      ""_country"": {
        ""extension"": [
          {
            ""url"": ""http://nictiz.nl/fhir/StructureDefinition/ext-CodeSpecification"",
            ""valueCodeableConcept"": {
              ""coding"": [
                {
                  ""system"": ""urn:iso:std:iso:3166"",
                  ""version"": ""2020-10-26T00:00:00"",
                  ""code"": ""NL"",
                  ""display"": ""Nederland""
                }
              ]
            }
          }
        ]
      }
    }
  ]
}";
        return _deserializer.Deserialize<Practitioner>(json);
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
        return _deserializer.Deserialize<PractitionerRole>(json);
    }
}
