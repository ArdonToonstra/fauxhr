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

## 📄 LForms Integration
The application uses [LHC-Forms](https://lhncbc.github.io/lforms/) to render FHIR Questionnaires.
- **Assets**: Scripts and CSS are served locally from `FauxHR.Modules.ExitStrategy/wwwroot`.
- **Questionnaire Definition**: Rendering a `QuestionnaireResponse` requires the original `Questionnaire` definition. This is currently embedded as a resource in `FauxHR.Modules.ExitStrategy`.
- **Usage**: The helper `wwwroot/js/lforms-helper.js` handles the merging of the Response data into the Questionnaire definition before rendering.

## 🧪 Testing with Nictiz Conformancelab

The application supports testing against external FHIR servers like the Nictiz Conformancelab. Due to browser security restrictions (CORS and SSL certificate validation), you need to run Chrome with special flags for testing:

### Windows:
```powershell
& "C:\Program Files\Google\Chrome\Application\chrome.exe" --user-data-dir="C:\ChromeDevSession" --disable-web-security --disable-gpu --ignore-certificate-errors
```

### What these flags do:
- `--user-data-dir="C:\ChromeDevSession"` - Creates an isolated Chrome profile (doesn't affect your normal browsing)
- `--disable-web-security` - Disables CORS restrictions
- `--ignore-certificate-errors` - Bypasses SSL certificate validation for PKIoverheid certificates

⚠️ **Security Warning**: Only use this Chrome instance for local development testing. Never browse other websites with these flags enabled.