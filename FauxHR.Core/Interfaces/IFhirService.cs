using Hl7.Fhir.Model;

namespace FauxHR.Core.Interfaces;

/// <summary>
/// Result of a FHIR operation that may succeed or fail with an OperationOutcome.
/// </summary>
/// <typeparam name="T">The expected resource type on success.</typeparam>
public class FhirOperationResult<T> where T : Resource
{
    public bool IsSuccess { get; init; }
    public T? Resource { get; init; }
    public OperationOutcome? Outcome { get; init; }
    public string? ErrorMessage { get; init; }

    public static FhirOperationResult<T> Success(T resource) => new()
    {
        IsSuccess = true,
        Resource = resource
    };

    public static FhirOperationResult<T> Failure(OperationOutcome? outcome, string? errorMessage = null) => new()
    {
        IsSuccess = false,
        Outcome = outcome,
        ErrorMessage = errorMessage ?? outcome?.Issue?.FirstOrDefault()?.Diagnostics ?? "Unknown error"
    };
}

public interface IFhirService
{
    Task<Patient?> GetPatientByIdAsync(string id);
    Task<Patient?> SearchPatientByIdentifierAsync(string system, string value);
    Task<Bundle> SearchPatientsAsync(string name);
    Task<Bundle> SearchResourceAsync(string resourceType, string queryString);
    Task<Hl7.Fhir.Model.Resource?> GetAsync(string path);
    Task<Bundle> SearchPractitionersAsync(string? name = null);
    Task<Practitioner?> GetPractitionerByIdAsync(string id);
    Task<Practitioner?> LoadDefaultPractitionerAsync();
    Task<Bundle> SearchRelatedPersonsAsync(string? name = null);
    Task<Bundle> TransactionAsync(Bundle bundle);
    
    // CRUD operations for canonical/definitional resources
    /// <summary>
    /// Creates a new resource on the FHIR server.
    /// </summary>
    Task<FhirOperationResult<T>> CreateAsync<T>(T resource) where T : Resource;
    
    /// <summary>
    /// Updates an existing resource on the FHIR server.
    /// </summary>
    Task<FhirOperationResult<T>> UpdateAsync<T>(T resource) where T : Resource;
    
    /// <summary>
    /// Deletes a resource from the FHIR server.
    /// </summary>
    Task<FhirOperationResult<Resource>> DeleteAsync(string resourceType, string id);
    
    /// <summary>
    /// Reads a specific resource by type and ID.
    /// </summary>
    Task<FhirOperationResult<T>> ReadAsync<T>(string id) where T : Resource, new();
}
