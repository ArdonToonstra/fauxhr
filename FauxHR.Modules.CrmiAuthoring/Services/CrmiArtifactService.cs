using FauxHR.Core.Interfaces;
using Hl7.Fhir.Model;

namespace FauxHR.Modules.CrmiAuthoring.Services;

/// <summary>
/// Service for managing CRMI artifacts (ActivityDefinition, ChargeItemDefinition).
/// Handles canonical URL generation, status transitions, and CRMI extension management.
/// </summary>
public class CrmiArtifactService
{
    private readonly IFhirService _fhirService;
    private readonly CanonicalUrlSettings _urlSettings;

    // CRMI Extension URLs
    public static class ExtensionUrls
    {
        public const string ArtifactUsage = "http://hl7.org/fhir/StructureDefinition/artifact-usage";
        public const string CopyrightLabel = "http://hl7.org/fhir/StructureDefinition/artifact-copyrightLabel";
        public const string ArtifactComment = "http://hl7.org/fhir/StructureDefinition/cqf-artifactComment";
        public const string PublicationDate = "http://hl7.org/fhir/StructureDefinition/cqf-publicationDate";
        public const string PublicationStatus = "http://hl7.org/fhir/StructureDefinition/cqf-publicationStatus";
        public const string KnowledgeCapability = "http://hl7.org/fhir/StructureDefinition/cqf-knowledgeCapability";
        public const string KnowledgeRepresentationLevel = "http://hl7.org/fhir/StructureDefinition/cqf-knowledgeRepresentationLevel";
    }

    public CrmiArtifactService(IFhirService fhirService, CanonicalUrlSettings urlSettings)
    {
        _fhirService = fhirService;
        _urlSettings = urlSettings;
    }

    #region Canonical URL Generation

    /// <summary>
    /// Generates a canonical URL for a resource based on its type and name.
    /// </summary>
    public string GenerateCanonicalUrl(string resourceType, string name)
    {
        var sanitizedName = SanitizeName(name);
        return $"{_urlSettings.BaseUrl.TrimEnd('/')}/{resourceType}/{sanitizedName}";
    }

    /// <summary>
    /// Sanitizes a name for use in a canonical URL (lowercase, alphanumeric with dashes).
    /// </summary>
    private static string SanitizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "unnamed";

        // Replace spaces and underscores with dashes, remove other special chars, lowercase
        var sanitized = name.ToLowerInvariant()
            .Replace(' ', '-')
            .Replace('_', '-');
        
        // Keep only alphanumeric and dashes
        return new string(sanitized.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
    }

    #endregion

    #region Status Transitions

    /// <summary>
    /// Checks if a status transition is valid according to CRMI lifecycle rules.
    /// </summary>
    public (bool IsValid, string? Message) ValidateStatusTransition(PublicationStatus current, PublicationStatus target)
    {
        // Same status is always valid
        if (current == target)
            return (true, null);

        return (current, target) switch
        {
            // Draft can transition to Active or Retired
            (PublicationStatus.Draft, PublicationStatus.Active) => (true, null),
            (PublicationStatus.Draft, PublicationStatus.Retired) => (true, null),
            
            // Active can only transition to Retired (going back to Draft requires new version)
            (PublicationStatus.Active, PublicationStatus.Retired) => (true, null),
            (PublicationStatus.Active, PublicationStatus.Draft) => (false, "An active artifact cannot transition back to draft. Create a new version instead."),
            
            // Retired cannot transition back (requires new version)
            (PublicationStatus.Retired, PublicationStatus.Draft) => (false, "A retired artifact cannot transition back to draft. Create a new version instead."),
            (PublicationStatus.Retired, PublicationStatus.Active) => (false, "A retired artifact cannot transition back to active. Create a new version instead."),
            
            // Unknown can go anywhere
            (PublicationStatus.Unknown, _) => (true, null),
            
            _ => (false, $"Unknown status transition from {current} to {target}")
        };
    }

    /// <summary>
    /// Returns valid target statuses from the current status.
    /// </summary>
    public IEnumerable<PublicationStatus> GetValidTransitions(PublicationStatus current)
    {
        return current switch
        {
            PublicationStatus.Draft => new[] { PublicationStatus.Draft, PublicationStatus.Active, PublicationStatus.Retired },
            PublicationStatus.Active => new[] { PublicationStatus.Active, PublicationStatus.Retired },
            PublicationStatus.Retired => new[] { PublicationStatus.Retired },
            PublicationStatus.Unknown => Enum.GetValues<PublicationStatus>(),
            _ => new[] { current }
        };
    }

    #endregion

    #region ActivityDefinition Operations

    public async Task<Bundle> SearchActivityDefinitionsAsync(string? title = null, PublicationStatus? status = null)
    {
        var queryParts = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(title))
            queryParts.Add($"title:contains={Uri.EscapeDataString(title)}");
        
        if (status.HasValue && status != PublicationStatus.Unknown)
            queryParts.Add($"status={status.Value.ToString().ToLowerInvariant()}");
        
        queryParts.Add("_sort=-_lastUpdated");
        queryParts.Add("_count=50");

        var queryString = string.Join("&", queryParts);
        return await _fhirService.SearchResourceAsync("ActivityDefinition", queryString);
    }

    public async Task<FhirOperationResult<ActivityDefinition>> GetActivityDefinitionAsync(string id)
    {
        return await _fhirService.ReadAsync<ActivityDefinition>(id);
    }

    public async Task<FhirOperationResult<ActivityDefinition>> CreateActivityDefinitionAsync(ActivityDefinition activityDefinition)
    {
        // Ensure date is set
        activityDefinition.DateElement ??= new FhirDateTime(DateTimeOffset.Now);
        
        // Set initial status if not set
        activityDefinition.Status ??= PublicationStatus.Draft;

        return await _fhirService.CreateAsync(activityDefinition);
    }

    public async Task<FhirOperationResult<ActivityDefinition>> UpdateActivityDefinitionAsync(ActivityDefinition activityDefinition)
    {
        // Update the date on modification
        activityDefinition.DateElement = new FhirDateTime(DateTimeOffset.Now);

        return await _fhirService.UpdateAsync(activityDefinition);
    }

    public async Task<FhirOperationResult<Resource>> DeleteActivityDefinitionAsync(string id)
    {
        return await _fhirService.DeleteAsync("ActivityDefinition", id);
    }

    #endregion

    #region ChargeItemDefinition Operations

    public async Task<Bundle> SearchChargeItemDefinitionsAsync(string? title = null, PublicationStatus? status = null)
    {
        var queryParts = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(title))
            queryParts.Add($"title:contains={Uri.EscapeDataString(title)}");
        
        if (status.HasValue && status != PublicationStatus.Unknown)
            queryParts.Add($"status={status.Value.ToString().ToLowerInvariant()}");
        
        queryParts.Add("_sort=-_lastUpdated");
        queryParts.Add("_count=50");

        var queryString = string.Join("&", queryParts);
        return await _fhirService.SearchResourceAsync("ChargeItemDefinition", queryString);
    }

    public async Task<FhirOperationResult<ChargeItemDefinition>> GetChargeItemDefinitionAsync(string id)
    {
        return await _fhirService.ReadAsync<ChargeItemDefinition>(id);
    }

    public async Task<FhirOperationResult<ChargeItemDefinition>> CreateChargeItemDefinitionAsync(ChargeItemDefinition chargeItemDefinition)
    {
        // Ensure date is set
        chargeItemDefinition.DateElement ??= new FhirDateTime(DateTimeOffset.Now);
        
        // Set initial status if not set
        chargeItemDefinition.Status ??= PublicationStatus.Draft;

        return await _fhirService.CreateAsync(chargeItemDefinition);
    }

    public async Task<FhirOperationResult<ChargeItemDefinition>> UpdateChargeItemDefinitionAsync(ChargeItemDefinition chargeItemDefinition)
    {
        // Update the date on modification
        chargeItemDefinition.DateElement = new FhirDateTime(DateTimeOffset.Now);

        return await _fhirService.UpdateAsync(chargeItemDefinition);
    }

    public async Task<FhirOperationResult<Resource>> DeleteChargeItemDefinitionAsync(string id)
    {
        return await _fhirService.DeleteAsync("ChargeItemDefinition", id);
    }

    #endregion

    #region Extension Helpers

    /// <summary>
    /// Gets or creates the artifact-usage extension value.
    /// </summary>
    public static string? GetUsageExtension(Resource resource)
    {
        var extensions = resource switch
        {
            ActivityDefinition ad => ad.Extension,
            ChargeItemDefinition cd => cd.Extension,
            _ => null
        };

        var ext = extensions?.FirstOrDefault(e => e.Url == ExtensionUrls.ArtifactUsage);
        return (ext?.Value as Markdown)?.Value;
    }

    /// <summary>
    /// Sets the artifact-usage extension value.
    /// </summary>
    public static void SetUsageExtension(Resource resource, string? usage)
    {
        var extensions = resource switch
        {
            ActivityDefinition ad => ad.Extension,
            ChargeItemDefinition cd => cd.Extension,
            _ => null
        };

        if (extensions == null) return;

        // Remove existing
        extensions.RemoveAll(e => e.Url == ExtensionUrls.ArtifactUsage);

        if (!string.IsNullOrWhiteSpace(usage))
        {
            extensions.Add(new Extension(ExtensionUrls.ArtifactUsage, new Markdown(usage)));
        }
    }

    /// <summary>
    /// Gets or creates the copyright-label extension value.
    /// </summary>
    public static string? GetCopyrightLabelExtension(Resource resource)
    {
        var extensions = resource switch
        {
            ActivityDefinition ad => ad.Extension,
            ChargeItemDefinition cd => cd.Extension,
            _ => null
        };

        var ext = extensions?.FirstOrDefault(e => e.Url == ExtensionUrls.CopyrightLabel);
        return (ext?.Value as FhirString)?.Value;
    }

    /// <summary>
    /// Sets the copyright-label extension value.
    /// </summary>
    public static void SetCopyrightLabelExtension(Resource resource, string? label)
    {
        var extensions = resource switch
        {
            ActivityDefinition ad => ad.Extension,
            ChargeItemDefinition cd => cd.Extension,
            _ => null
        };

        if (extensions == null) return;

        // Remove existing
        extensions.RemoveAll(e => e.Url == ExtensionUrls.CopyrightLabel);

        if (!string.IsNullOrWhiteSpace(label))
        {
            extensions.Add(new Extension(ExtensionUrls.CopyrightLabel, new FhirString(label)));
        }
    }

    #endregion
}
