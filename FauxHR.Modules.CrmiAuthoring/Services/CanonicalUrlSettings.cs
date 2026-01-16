namespace FauxHR.Modules.CrmiAuthoring.Services;

/// <summary>
/// Configuration for canonical URL generation.
/// </summary>
public class CanonicalUrlSettings
{
    /// <summary>
    /// Base URL for canonical identifiers (e.g., "https://your-org.example.org/fhir").
    /// </summary>
    public string BaseUrl { get; set; } = "https://example.org/fhir";
}
