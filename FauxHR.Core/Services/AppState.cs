using Hl7.Fhir.Model;

namespace FauxHR.Core.Services;

public class AppState
{
    // Default to the public Firely Sandbox or a localhost placeholder
    public string CurrentServerUrl { get; private set; } = "https://server.fire.ly"; 
    
    public Patient? CurrentPatient { get; private set; }

    public event Action? OnChange;

    public void SetServerUrl(string url)
    {
        if (CurrentServerUrl != url)
        {
            CurrentServerUrl = url;
            NotifyStateChanged();
        }
    }

    public void SetPatient(Patient? patient)
    {
        if (CurrentPatient?.Id != patient?.Id)
        {
            CurrentPatient = patient;
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
