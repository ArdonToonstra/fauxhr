using Hl7.Fhir.Model;

namespace FauxHR.Core.Interfaces;

public interface IFhirService
{
    Task<Patient?> GetPatientByIdAsync(string id);
    Task<Patient?> SearchPatientByIdentifierAsync(string system, string value);
    Task<Bundle> SearchPatientsAsync(string name);
    Task<Bundle> SearchResourceAsync(string resourceType, string queryString);
    Task<Hl7.Fhir.Model.Resource?> GetAsync(string path);
}
