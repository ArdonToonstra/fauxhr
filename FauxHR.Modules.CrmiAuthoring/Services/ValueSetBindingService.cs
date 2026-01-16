using Blazored.LocalStorage;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace FauxHR.Modules.CrmiAuthoring.Services;

/// <summary>
/// Service for managing ValueSet bindings to FHIR element paths.
/// Allows configuring which ValueSets should provide dropdown options for specific elements.
/// </summary>
public class ValueSetBindingService
{
    private readonly ILocalStorageService _localStorage;
    private readonly TerminologyService _terminologyService;
    
    private const string BINDINGS_KEY = "ValueSetBindings";
    private Dictionary<string, ValueSetBindingConfig> _bindings = new();
    private bool _isInitialized = false;

    public ValueSetBindingService(ILocalStorageService localStorage, TerminologyService terminologyService)
    {
        _localStorage = localStorage;
        _terminologyService = terminologyService;
    }

    /// <summary>
    /// Initialize the service by loading saved bindings from storage.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            var stored = await _localStorage.GetItemAsync<Dictionary<string, ValueSetBindingConfig>>(BINDINGS_KEY);
            if (stored != null)
            {
                _bindings = stored;
            }
        }
        catch
        {
            _bindings = new Dictionary<string, ValueSetBindingConfig>();
        }

        _isInitialized = true;
    }

    /// <summary>
    /// Get the binding configuration for a specific element path.
    /// </summary>
    public ValueSetBindingConfig? GetBinding(string elementPath)
    {
        return _bindings.TryGetValue(elementPath, out var binding) ? binding : null;
    }

    /// <summary>
    /// Set a ValueSet binding for a specific element path.
    /// </summary>
    public async Task SetBindingAsync(string elementPath, string valueSetUrl, string? displayName = null)
    {
        _bindings[elementPath] = new ValueSetBindingConfig
        {
            ElementPath = elementPath,
            ValueSetUrl = valueSetUrl,
            DisplayName = displayName ?? valueSetUrl
        };

        await SaveBindingsAsync();
    }

    /// <summary>
    /// Remove a binding for a specific element path.
    /// </summary>
    public async Task RemoveBindingAsync(string elementPath)
    {
        if (_bindings.Remove(elementPath))
        {
            await SaveBindingsAsync();
        }
    }

    /// <summary>
    /// Get all configured bindings.
    /// </summary>
    public IReadOnlyDictionary<string, ValueSetBindingConfig> GetAllBindings()
    {
        return _bindings;
    }

    /// <summary>
    /// Get codes from a bound ValueSet for a specific element path.
    /// </summary>
    public async Task<List<BoundCode>> GetBoundCodesAsync(string elementPath)
    {
        var binding = GetBinding(elementPath);
        if (binding == null)
        {
            return new List<BoundCode>();
        }

        return await GetCodesFromValueSetAsync(binding.ValueSetUrl);
    }

    /// <summary>
    /// Get codes from a ValueSet or CodeSystem by URL.
    /// </summary>
    public async Task<List<BoundCode>> GetCodesFromValueSetAsync(string url)
    {
        var codes = new List<BoundCode>();

        try
        {
            // First try to expand the ValueSet (this handles both URL and ID)
            var expanded = await _terminologyService.ExpandValueSetAsync(url);
            
            if (expanded != null)
            {
                // Get codes from expansion
                if (expanded.Expansion?.Contains != null)
                {
                    foreach (var contains in expanded.Expansion.Contains)
                    {
                        codes.Add(new BoundCode
                        {
                            System = contains.System,
                            Code = contains.Code,
                            Display = contains.Display ?? contains.Code
                        });
                    }
                }
                
                // Fallback to compose if no expansion
                if (!codes.Any() && expanded.Compose?.Include != null)
                {
                    foreach (var include in expanded.Compose.Include)
                    {
                        var system = include.System;

                        // If concepts are explicitly listed
                        if (include.Concept != null)
                        {
                            foreach (var concept in include.Concept)
                            {
                                codes.Add(new BoundCode
                                {
                                    System = system,
                                    Code = concept.Code,
                                    Display = concept.Display ?? concept.Code
                                });
                            }
                        }
                        else if (!string.IsNullOrEmpty(system))
                        {
                            // Try to get codes from the referenced CodeSystem
                            var csResult = await _terminologyService.GetCodeSystemAsync(system);
                            if (csResult.IsSuccess && csResult.Resource?.Concept != null)
                            {
                                AddConceptsFromCodeSystem(codes, system, csResult.Resource.Concept);
                            }
                        }
                    }
                }

                if (codes.Any())
                {
                    return codes;
                }
            }

            // Try as a CodeSystem directly
            var csResult2 = await _terminologyService.GetCodeSystemAsync(url);
            if (csResult2.IsSuccess && csResult2.Resource?.Concept != null)
            {
                AddConceptsFromCodeSystem(codes, csResult2.Resource.Url ?? url, csResult2.Resource.Concept);
            }
        }
        catch
        {
            // Return empty list on error
        }

        return codes;
    }

    private void AddConceptsFromCodeSystem(List<BoundCode> codes, string system, List<CodeSystem.ConceptDefinitionComponent> concepts)
    {
        foreach (var concept in concepts)
        {
            codes.Add(new BoundCode
            {
                System = system,
                Code = concept.Code,
                Display = concept.Display ?? concept.Code
            });

            // Recursively add nested concepts
            if (concept.Concept != null)
            {
                AddConceptsFromCodeSystem(codes, system, concept.Concept);
            }
        }
    }

    private async Task SaveBindingsAsync()
    {
        await _localStorage.SetItemAsync(BINDINGS_KEY, _bindings);
    }
}

/// <summary>
/// Configuration for a ValueSet binding to a FHIR element.
/// </summary>
public class ValueSetBindingConfig
{
    public string ElementPath { get; set; } = "";
    public string ValueSetUrl { get; set; } = "";
    public string? DisplayName { get; set; }
}

/// <summary>
/// Represents a code from a bound ValueSet/CodeSystem.
/// </summary>
public class BoundCode
{
    public string? System { get; set; }
    public string? Code { get; set; }
    public string? Display { get; set; }

    public string DisplayText => !string.IsNullOrEmpty(Display) ? Display : Code ?? "";
    public string FullDisplay => !string.IsNullOrEmpty(Display) ? $"{Display} ({Code})" : Code ?? "";
}
