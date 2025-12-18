using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FauxHR.App;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Core Services
builder.Services.AddScoped<FauxHR.Core.Services.AppState>();
builder.Services.AddScoped<FauxHR.Core.Interfaces.IFhirService, FauxHR.App.Services.FhirService>();

// Modules
builder.Services.AddScoped<FauxHR.Core.Interfaces.IIGModule, FauxHR.Modules.ExitStrategy.ExitStrategyModule>();

await builder.Build().RunAsync();
