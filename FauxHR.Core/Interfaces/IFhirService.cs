using Hl7.Fhir.Model;

namespace FauxHR.Core.Interfaces;

public interface IFhirService
{
    Task<Patient?> GetPatientAsync(string id);
    Task<Bundle> SearchPatientsAsync(string? name = null);
    Task<Patient?> SearchPatientByIdentifierAsync(string system, string value);
}
