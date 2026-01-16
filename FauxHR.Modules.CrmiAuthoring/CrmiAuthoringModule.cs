using FauxHR.Core.Interfaces;
using FauxHR.Core.Models;

namespace FauxHR.Modules.CrmiAuthoring;

/// <summary>
/// CRMI (Canonical Resource Management Infrastructure) Authoring Module.
/// Implements authoring capabilities for definitional FHIR resources following
/// the CRMI Implementation Guide: https://build.fhir.org/ig/HL7/crmi-ig/
/// </summary>
public class CrmiAuthoringModule : IIGModule
{
    public string Title => "CRMI Authoring";
    public string Description => "Canonical Resource Management Infrastructure - Author and manage definitional FHIR resources";
    public string IconClass => "bi bi-file-earmark-code";
    public string EntryPath => "crmi/overview";

    public IEnumerable<NavigationItem> GetNavigationItems()
    {
        yield return new NavigationItem
        {
            Title = "Overview",
            Url = "crmi/overview",
            IconClass = "bi bi-grid-3x3-gap"
        };
        yield return new NavigationItem
        {
            Title = "ActivityDefinition",
            Url = "crmi/activity-definition",
            IconClass = "bi bi-activity"
        };
        yield return new NavigationItem
        {
            Title = "ChargeItemDefinition",
            Url = "crmi/charge-item-definition",
            IconClass = "bi bi-currency-euro"
        };
        yield return new NavigationItem
        {
            Title = "Terminology",
            Url = "crmi/terminology",
            IconClass = "bi bi-translate"
        };
        yield return new NavigationItem
        {
            Title = "Settings",
            Url = "crmi/settings",
            IconClass = "bi bi-gear"
        };
    }
}
