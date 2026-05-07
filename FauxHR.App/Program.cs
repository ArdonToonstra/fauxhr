using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FauxHR.App;
using Blazored.LocalStorage;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });



// Core Services
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<FauxHR.Core.Services.AppState>();
builder.Services.AddScoped<FauxHR.App.Services.PractitionerContextService>();
builder.Services.AddScoped<FauxHR.Core.Interfaces.IFhirService, FauxHR.App.Services.FhirService>();

// Modules
builder.Services.AddScoped<FauxHR.Core.Interfaces.IIGModule, FauxHR.Modules.ExitStrategy.ExitStrategyModule>();
builder.Services.AddScoped<FauxHR.Modules.ExitStrategy.Services.AcpDataService>();
builder.Services.AddScoped<FauxHR.Modules.ExitStrategy.Services.AcpIntegratedDataLoader>();

// CRMI Authoring Module
builder.Services.AddScoped<FauxHR.Core.Interfaces.IIGModule, FauxHR.Modules.CrmiAuthoring.CrmiAuthoringModule>();
builder.Services.AddSingleton(new FauxHR.Modules.CrmiAuthoring.Services.CanonicalUrlSettings 
{ 
    BaseUrl = "https://example.org/fhir" // Configure your organization's base URL
});
builder.Services.AddScoped<FauxHR.Modules.CrmiAuthoring.Services.CrmiArtifactService>();
builder.Services.AddScoped<FauxHR.Modules.CrmiAuthoring.Services.TerminologyService>();
builder.Services.AddScoped<FauxHR.Modules.CrmiAuthoring.Services.ValueSetBindingService>();

// CDS Hooks Module (CTM Pattern A)
builder.Services.AddScoped<FauxHR.Core.Interfaces.IIGModule, FauxHR.Modules.CdsHooks.CdsHooksModule>();
builder.Services.AddScoped<FauxHR.Modules.CdsHooks.Services.CdsHooksPrefetchService>();

var host = builder.Build();

var scope = host.Services.CreateScope();
var appState = scope.ServiceProvider.GetRequiredService<FauxHR.Core.Services.AppState>();

// Detect whether the FauxHR.App.Server proxy is available.
// A 200 from /fhir-proxy/ping means the server is running alongside this WASM app.
// On GitHub Pages there is no server, so this silently fails and direct mode is used.
try
{
    var httpClient = scope.ServiceProvider.GetRequiredService<HttpClient>();
    var probe = await httpClient.GetAsync("fhir-proxy/ping");
    appState.SetServerProxyAvailable(probe.IsSuccessStatusCode);
}
catch
{
    appState.SetServerProxyAvailable(false);
}

// Initialize practitioner context on startup
var practitionerService = scope.ServiceProvider.GetRequiredService<FauxHR.App.Services.PractitionerContextService>();
await practitionerService.InitializeDefaultPractitionerAsync();

await host.RunAsync();
