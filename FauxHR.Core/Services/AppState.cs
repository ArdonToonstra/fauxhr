using Hl7.Fhir.Model;

namespace FauxHR.Core.Services;

public class AppState
{
    // Default to the public Firely Sandbox or a localhost placeholder
    public string CurrentServerUrl { get; private set; } = "https://server.fire.ly";

    public Patient? CurrentPatient { get; private set; }
    public Practitioner? CurrentPractitioner { get; private set; }
    public PractitionerRole? CurrentPractitionerRole { get; private set; }

    // Organization context for the currently logged-in user
    public string? CurrentOrganizationName { get; private set; }
    public string? CurrentOrganizationId { get; private set; }

    // Reference Resolution Settings
    public bool EnableReferenceResolution { get; private set; } = true;
    public int ReferenceResolutionDepth { get; private set; } = 3;

    public List<FhirServerConfig> AvailableServers { get; private set; } = new()
    {
        new FhirServerConfig { Url = "https://server.fire.ly", Label = "Firely Server" },
        new FhirServerConfig { Url = "http://hapi.fhir.org/baseR4", Label = "HAPI Server" },
        new FhirServerConfig
        {
            Url = "https://nictiz.proxy.interoplab.eu/d/67c22c0aba87c4750a34a962/nictiz/r4/fhir",
            Label = "Conformancelab Nictiz",
            RequiresProxy = true,
            HasUntrustedCert = true,
            CustomHeaders = new() { new HeaderItem { Key = "Authorization", Value = "Basic TmljdGl6OlBhc3N3b3Jk" } } // TEST credentials only
        },
        new FhirServerConfig
        {
            Url = "https://pzp-coalitie.proxy.interoplab.eu/r4/fhir",
            Label = "PZP Coalitie (Interoplab)",
            RequiresProxy = true,
            HasUntrustedCert = true,
            CustomHeaders = new() { new HeaderItem { Key = "Authorization", Value = "Bearer 27e14882-0370-400b-a6d4-dee94c9fcf10" } } // TEST token only
        }
    };

    public FhirServerConfig? CurrentServerConfig =>
        AvailableServers.FirstOrDefault(s => s.Url.TrimEnd('/') == CurrentServerUrl.TrimEnd('/'));

    /// <summary>True when the server proxy (/fhir-proxy/) was detected as available on startup.</summary>
    public bool IsServerProxyAvailable { get; private set; } = false;

    public void SetServerProxyAvailable(bool available)
    {
        IsServerProxyAvailable = available;
        NotifyStateChanged();
    }

    /// <summary>True when the currently selected server requires proxying AND the proxy is running.
    /// Falls back to direct mode (no proxy) so GitHub Pages can still attempt requests.</summary>
    public bool UseServerProxy => IsServerProxyAvailable && (CurrentServerConfig?.RequiresProxy ?? false);

    /// <summary>Custom headers for the currently selected server.</summary>
    public List<HeaderItem> CurrentServerHeaders => CurrentServerConfig?.CustomHeaders ?? new();

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

    public void UpdateServerHeaders(string serverUrl, List<HeaderItem> headers)
    {
        var server = AvailableServers.FirstOrDefault(s => s.Url.TrimEnd('/') == serverUrl.TrimEnd('/'));
        if (server != null)
        {
            server.CustomHeaders = headers.Select(h => new HeaderItem { Key = h.Key, Value = h.Value }).ToList();
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

    public void SetOrganization(string? organizationName, string? organizationId = null)
    {
        if (CurrentOrganizationName != organizationName || CurrentOrganizationId != organizationId)
        {
            CurrentOrganizationName = organizationName;
            CurrentOrganizationId = organizationId;
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

    // Conformance Testing Settings — enabled by default so headers are always injected
    public bool ConformanceTestingMode { get; private set; } = true;

    public void SetConformanceTestingMode(bool enabled)
    {
        ConformanceTestingMode = enabled;
        NotifyStateChanged();
    }

    // Debug: last FHIR request/response log
    public FhirRequestLog? LastRequestLog { get; private set; }

    public void SetLastRequestLog(FhirRequestLog log)
    {
        LastRequestLog = log;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}

public class FhirServerConfig
{
    public string Url { get; set; } = "";
    public string Label { get; set; } = "";
    /// <summary>When true, requests are preferably routed through the local /fhir-proxy/ endpoint
    /// to bypass CORS restrictions and private-CA certificate issues. Falls back to direct mode
    /// when the proxy is not available (e.g. on GitHub Pages).</summary>
    public bool RequiresProxy { get; set; } = false;
    /// <summary>When true, the server uses a certificate from a private/internal CA that browsers
    /// do not trust by default. Users must manually accept the cert in the browser before
    /// direct requests will work.</summary>
    public bool HasUntrustedCert { get; set; } = false;
    /// <summary>Per-server custom HTTP headers (e.g. Authorization).</summary>
    public List<HeaderItem> CustomHeaders { get; set; } = new();
}

public class HeaderItem
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public class FhirRequestLog
{
    public string Method { get; set; } = "";
    public string Url { get; set; } = "";
    public Dictionary<string, string> RequestHeaders { get; set; } = new();
    public int StatusCode { get; set; }
    public string ResponseBody { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
