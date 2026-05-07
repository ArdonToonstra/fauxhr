using FauxHR.Core.Interfaces;
using FauxHR.Core.Models;

namespace FauxHR.Modules.CdsHooks;

public class CdsHooksModule : IIGModule
{
    public string Title => "CTM (CDS Hooks)";
    public string Description => "Clinical Trial Matching via CDS Hooks patient-view (USCDI+ CTM Pattern A)";
    public string IconClass => "bi bi-lightning-charge";
    public string EntryPath => "cds-hooks/patient-view";

    public IEnumerable<NavigationItem> GetNavigationItems()
    {
        yield return new NavigationItem
        {
            Title = "Patient View",
            Url = "cds-hooks/patient-view",
            IconClass = "bi bi-person-lines-fill"
        };
    }
}
