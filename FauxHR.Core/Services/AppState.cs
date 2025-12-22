using Hl7.Fhir.Model;

namespace FauxHR.Core.Services;

public class AppState
{
    // Default to the public Firely Sandbox or a localhost placeholder
    public string CurrentServerUrl { get; private set; } = "https://server.fire.ly"; 
    
    public Patient? CurrentPatient { get; private set; }
    public Practitioner? CurrentPractitioner { get; private set; }
    public PractitionerRole? CurrentPractitionerRole { get; private set; }
    
    // Reference Resolution Settings
    public bool EnableReferenceResolution { get; private set; } = true;
    public int ReferenceResolutionDepth { get; private set; } = 3;

    public List<FhirServerConfig> AvailableServers { get; private set; } = new()
    {
        new FhirServerConfig { Url = "https://server.fire.ly", Label = "Firely Server" },
        new FhirServerConfig { Url = "http://hapi.fhir.org/baseR4", Label = "HAPI Server" }
    };

    public event Action? OnChange;

    public void SetServerUrl(string url)
    {
        if (CurrentServerUrl != url)
        {
            CurrentServerUrl = url;
            NotifyStateChanged();
        }
    }
    
    public void AddServer(string url, string label)
    {
        if (!AvailableServers.Any(s => s.Url == url))
        {
            AvailableServers.Add(new FhirServerConfig { Url = url, Label = label });
            NotifyStateChanged();
        }
    }
    
    public void RemoveServer(string url)
    {
        var server = AvailableServers.FirstOrDefault(s => s.Url == url);
        if (server != null)
        {
            AvailableServers.Remove(server);
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
    
    public void SetPractitioner(Practitioner? practitioner, PractitionerRole? role = null)
    {
        if (CurrentPractitioner?.Id != practitioner?.Id)
        {
            CurrentPractitioner = practitioner;
            CurrentPractitionerRole = role;
            NotifyStateChanged();
        }
    }
    
    public void SetReferenceResolutionSettings(bool enabled, int depth)
    {
        EnableReferenceResolution = enabled;
        ReferenceResolutionDepth = Math.Max(1, Math.Min(5, depth)); // Clamp between 1-5
        NotifyStateChanged();
    }
    
    public bool ShowDebugInfo { get; private set; } = false;
    
    public void SetShowDebugInfo(bool show)
    {
        if (ShowDebugInfo != show)
        {
            ShowDebugInfo = show;
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}

public class FhirServerConfig
{
    public string Url { get; set; } = "";
    public string Label { get; set; } = "";
}
