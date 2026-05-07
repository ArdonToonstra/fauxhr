namespace FauxHR.Modules.CdsHooks.Models;

/// <summary>
/// Represents a CDS Hooks request for the patient-view hook.
/// See: https://cds-hooks.hl7.org/2.0/#calling-a-cds-service
/// </summary>
public class CdsHooksRequest
{
    public string Hook { get; set; } = "patient-view";
    public string HookInstance { get; set; } = Guid.NewGuid().ToString();
    public CdsContext Context { get; set; } = new();
    public string? FhirServer { get; set; }
    public Dictionary<string, object?> Prefetch { get; set; } = new();
}

public class CdsContext
{
    public string UserId { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public string? EncounterId { get; set; }
}

/// <summary>
/// Represents the CDS Hooks response containing cards.
/// </summary>
public class CdsHooksResponse
{
    public List<CdsCard> Cards { get; set; } = new();
}

public class CdsCard
{
    public string Summary { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string Indicator { get; set; } = "info"; // info | warning | critical
    public CdsSource? Source { get; set; }
    public List<CdsLink>? Links { get; set; }
}

public class CdsSource
{
    public string Label { get; set; } = string.Empty;
    public string? Url { get; set; }
}

public class CdsLink
{
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Type { get; set; } = "absolute"; // absolute | smart
}
