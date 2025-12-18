# fauxhr
FauxHR is a modular, client-side Personal Health Record (PHR) built with Blazor WASM and the Firely .NET SDK. Its purpose is to act as a "living" testbed for FHIR Implementation Guides (IGs), starting with the IKNL Advance Care Planning (ACP) IG.

## 🚀 How to Run
Prerequisites: [.NET 8 SDK](https://dotnet.microsoft.com/download)

1.  Open a terminal in the solution root.
2.  Run the application project:
    ```bash
    dotnet run --project FauxHR.App
    ```
    *Alternatively, use `dotnet watch --project FauxHR.App` for hot-reload.*
3.  Open your browser to the URL shown in the terminal (usually `https://localhost:7284`).

## 🏗️ Project Structure
- **FauxHR.App**: The Blazor WebAssembly application.
- **FauxHR.Core**: Shared logic and interfaces.
- **FauxHR.Modules.ExitStrategy**: IKNL ACP Implementation Guide logic.
- **FauxHR.MockData**: Scenario generators.
