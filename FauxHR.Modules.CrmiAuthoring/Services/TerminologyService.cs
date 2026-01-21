using FauxHR.Core.Interfaces;
using Hl7.Fhir.Model;

namespace FauxHR.Modules.CrmiAuthoring.Services;

/// <summary>
/// Service for terminology operations (ValueSet, CodeSystem).
/// Provides search, retrieve, and expansion capabilities.
/// </summary>
public class TerminologyService
{
    private readonly IFhirService _fhirService;
    
    // Simple cache for expanded ValueSets to improve UI performance
    private readonly Dictionary<string, ValueSet> _expansionCache = new();

    public TerminologyService(IFhirService fhirService)
    {
        _fhirService = fhirService;
    }

    #region ValueSet Operations

    public async Task<Bundle> SearchValueSetsAsync(string? name = null, PublicationStatus? status = null)
    {
        var queryParts = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(name))
            queryParts.Add($"name:contains={Uri.EscapeDataString(name)}");
        
        if (status.HasValue && status != PublicationStatus.Unknown)
            queryParts.Add($"status={status.Value.ToString().ToLowerInvariant()}");
        
        queryParts.Add("_sort=-_lastUpdated");
        queryParts.Add("_count=50");

        var queryString = string.Join("&", queryParts);
        return await _fhirService.SearchResourceAsync("ValueSet", queryString);
    }

    public async Task<FhirOperationResult<ValueSet>> GetValueSetAsync(string id)
    {
        return await _fhirService.ReadAsync<ValueSet>(id);
    }

    /// <summary>
    /// Expands a ValueSet, using cache when available.
    /// </summary>
    public async Task<ValueSet?> ExpandValueSetAsync(string urlOrId, bool useCache = true)
    {
        // Check cache first
        if (useCache && _expansionCache.TryGetValue(urlOrId, out var cached))
        {
            return cached;
        }

        try
        {
            // Try to call $expand operation
            var resource = await _fhirService.GetAsync($"ValueSet/{Uri.EscapeDataString(urlOrId)}/$expand");
            
            if (resource is ValueSet expanded)
            {
                if (useCache)
                {
                    _expansionCache[urlOrId] = expanded;
                }
                return expanded;
            }

            // If $expand not supported, try to get the ValueSet directly
            var result = await GetValueSetAsync(urlOrId);
            if (result.IsSuccess && result.Resource != null)
            {
                if (useCache)
                {
                    _expansionCache[urlOrId] = result.Resource;
                }
                return result.Resource;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets concepts from an expanded ValueSet for use in dropdowns.
    /// </summary>
    public async Task<IEnumerable<(string Code, string Display)>> GetValueSetConceptsAsync(string urlOrId)
    {
        var expanded = await ExpandValueSetAsync(urlOrId);
        
        if (expanded?.Expansion?.Contains != null)
        {
            return expanded.Expansion.Contains
                .Where(c => !string.IsNullOrEmpty(c.Code))
                .Select(c => (c.Code!, c.Display ?? c.Code!));
        }

        // Fallback to compose if no expansion
        if (expanded?.Compose?.Include != null)
        {
            return expanded.Compose.Include
                .SelectMany(inc => inc.Concept ?? Enumerable.Empty<ValueSet.ConceptReferenceComponent>())
                .Where(c => !string.IsNullOrEmpty(c.Code))
                .Select(c => (c.Code!, c.Display ?? c.Code!));
        }

        return Enumerable.Empty<(string, string)>();
    }

    /// <summary>
    /// Clears the expansion cache.
    /// </summary>
    public void ClearCache()
    {
        _expansionCache.Clear();
    }

    #endregion

    #region CodeSystem Operations

    public async Task<Bundle> SearchCodeSystemsAsync(string? name = null, PublicationStatus? status = null)
    {
        var queryParts = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(name))
            queryParts.Add($"name:contains={Uri.EscapeDataString(name)}");
        
        if (status.HasValue && status != PublicationStatus.Unknown)
            queryParts.Add($"status={status.Value.ToString().ToLowerInvariant()}");
        
        queryParts.Add("_sort=-_lastUpdated");
        queryParts.Add("_count=50");

        var queryString = string.Join("&", queryParts);
        return await _fhirService.SearchResourceAsync("CodeSystem", queryString);
    }

    public async Task<FhirOperationResult<CodeSystem>> GetCodeSystemAsync(string id)
    {
        return await _fhirService.ReadAsync<CodeSystem>(id);
    }

    /// <summary>
    /// Gets concepts from a CodeSystem for display.
    /// </summary>
    public async Task<IEnumerable<(string Code, string Display, string? Definition)>> GetCodeSystemConceptsAsync(string id)
    {
        var result = await GetCodeSystemAsync(id);
        
        if (result.IsSuccess && result.Resource?.Concept != null)
        {
            return FlattenConcepts(result.Resource.Concept);
        }

        return Enumerable.Empty<(string, string, string?)>();
    }

    /// <summary>
    /// Recursively flattens nested CodeSystem concepts.
    /// </summary>
    private static IEnumerable<(string Code, string Display, string? Definition)> FlattenConcepts(
        IEnumerable<CodeSystem.ConceptDefinitionComponent> concepts, 
        string prefix = "")
    {
        foreach (var concept in concepts)
        {
            var displayPrefix = string.IsNullOrEmpty(prefix) ? "" : prefix + " > ";
            yield return (concept.Code!, displayPrefix + (concept.Display ?? concept.Code), concept.Definition);

            if (concept.Concept != null)
            {
                foreach (var child in FlattenConcepts(concept.Concept, displayPrefix + (concept.Display ?? concept.Code)))
                {
                    yield return child;
                }
            }
        }
    }

    #endregion
}
