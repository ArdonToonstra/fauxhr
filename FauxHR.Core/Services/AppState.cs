using Hl7.Fhir.Model;

namespace FauxHR.Core.Services;

public class AppState
{
    // Default to the public Firely Sandbox or a localhost placeholder
    public string CurrentServerUrl { get; private set; } = "https://server.fire.ly"; 
    
    public Patient? CurrentPatient { get; private set; }
    
    // Reference Resolution Settings
    public bool EnableReferenceResolution { get; private set; } = true;
    public int ReferenceResolutionDepth { get; private set; } = 3;

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
    
    public void SetReferenceResolutionSettings(bool enabled, int depth)
    {
        EnableReferenceResolution = enabled;
        ReferenceResolutionDepth = Math.Max(1, Math.Min(5, depth)); // Clamp between 1-5
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
