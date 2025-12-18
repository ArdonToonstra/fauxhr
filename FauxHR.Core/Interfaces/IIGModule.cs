using FauxHR.Core.Models;

namespace FauxHR.Core.Interfaces;

public interface IIGModule
{
    string Title { get; }
    string Description { get; }
    string IconClass { get; }
    string EntryPath { get; }
    IEnumerable<NavigationItem> GetNavigationItems();
}
