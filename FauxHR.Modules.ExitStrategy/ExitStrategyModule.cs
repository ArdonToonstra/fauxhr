using FauxHR.Core.Interfaces;
using FauxHR.Core.Models;

namespace FauxHR.Modules.ExitStrategy;

public class ExitStrategyModule : IIGModule
{
    public string Title => "Exit Strategy (ACP)";
    public string Description => "Advance Care Planning Implementation Guide (IKNL)";
    public string IconClass => "bi bi-clipboard-pulse";
    public string EntryPath => "acp/overview";

    public IEnumerable<NavigationItem> GetNavigationItems()
    {
        yield return new NavigationItem 
        { 
            Title = "Overview", 
            Url = "acp/overview",
            IconClass = "bi bi-journal-medical"
        };
        yield return new NavigationItem 
        { 
            Title = "Directives", 
            Url = "acp/directives",
            IconClass = "bi bi-clipboard-check"
        };
        yield return new NavigationItem 
        { 
            Title = "Goals", 
            Url = "acp/goals",
            IconClass = "bi bi-flag"
        };
    }
}
