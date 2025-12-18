# 🚀 Project Plan: FauxHR (Mock PHR & IG Quality Control)

FauxHR is a modular, client-side Personal Health Record (PHR) built with Blazor WASM and the Firely .NET SDK. Its purpose is to act as a "living" testbed for FHIR Implementation Guides (IGs), starting with the IKNL Advance Care Planning (ACP) IG.

## 🏗️ 1. Core Architecture Goals

*   **Modular by Design**: Each IG is treated as a separate "Module" (e.g., ExitStrategy for ACP).
*   **FHIR Native**: Use the Firely .NET SDK for all resource handling, parsing, and client logic.
*   **Persistence Agnostic**: Built as a FHIR Facade. Initially supports Public FHIR Servers, later supporting an openEHR mapping layer.
*   **Quality Control**: By implementing the UI for an IG, we identify gaps, ambiguities, or impracticalities in the IG profiles.

## 📅 2. Implementation Roadmap

### Phase 1: The "FauxHR" Shell (Foundation)
- [ ] **Project Scaffolding**: Create a Blazor WebAssembly solution with a shared library for core FHIR services.
- [ ] **Service Injection**: Set up a global FhirClient context that can point to different environments (Public Firely Sandbox vs. Localhost).
- [ ] **Global Patient Context**: Build a "Patient Picker" component. Once a patient is selected, their ID is available to all IG modules.
- [ ] **Modular Navigation**: Implement a dynamic sidebar that loads navigation links from registered IG modules.

### Phase 2: Module "ExitStrategy" (IKNL ACP IG)
- [ ] **Data Discovery Service**: Build a service that executes the multiple REST calls required by the IKNL IG:
    - `GET [base]/Consent?patient=[id]`
    - `GET [base]/Goal?patient=[id]`
    - `GET [base]/Observation?patient=[id]&category=advance-care-planning`
- [ ] **Mapping & UI Components**:
    - Create a `ConsentCard.razor` to display treatment directives.
    - Create a `GoalTimeline.razor` to visualize medical policy goals.
    - Implement Dutch translation logic for ZIB-coded elements (SNOMED/LOINC).
- [ ] **IG "Stress Test"**: Attempt to render every `MustSupport` element in the IG. Document any elements that are difficult to represent in a user-friendly UI.

### Phase 3: The Mock Data Engine (Scenario Builder)
- [ ] **Resource Factory**: Use the Firely SDK to build "Perfect" resources.
    - *Example*: A C# class that generates a Consent resource with all required IKNL extensions and profiles.
- [ ] **Scenario Runner**: A UI feature where a user can click "Generate ACP Scenario" to automatically POST a set of valid resources to the current FHIR server for testing.

### Phase 4: Integration of openEHR (The Evolution)
- [ ] **Abstract Repository**: Refactor data fetching to an `IClinicalStore` interface.
- [ ] **openEHR Provider**: Create an implementation that translates AQL (Archetype Query Language) results from an EHRbase instance into the FHIR POCOs used by the ExitStrategy module.

## 🛠️ 3. Proposed Technical Stack

| Layer | Technology | Framework |
| :--- | :--- | :--- |
| **Blazor WebAssembly** | .NET 8/9 | |
| **FHIR Logic** | Firely .NET SDK | (Hl7.Fhir.R4 / R5) | We start with R4  dotnet add package Hl7.Fhir.R4 --version 6.0.1
| **Styling** | MudBlazor or FluentUI for Blazor | (Clean, medical feel) |
| **Auth** | Mock Auth initially | OIDC (Auth0) later |
| **Deployment** | Azure Static Web Apps | GitHub Pages |

## 📁 4. Project Structure (Antigravity Workspace)

```text
FauxHR/
├── FauxHR.App                 # Main Blazor WASM Project
│   ├── Pages/                 # Dashboard & Settings
│   └── AppState.cs            # Global Patient/Server state
├── FauxHR.Core                # Shared Logic
│   ├── Interfaces/            # IIGModule, IFhirService
│   └── Extensions/            # FhirExtensions (ZIB helpers)
├── FauxHR.Modules.ExitStrategy# IKNL ACP Specific Logic
│   ├── Components/            # IG-specific Razor components
│   └── Services/              # AcpDataService.cs
└── FauxHR.MockData            # Scenario Generators
    └── Generators/            # IknlAcpGenerator.cs
```

## 💡 5. Future "Funny" Use-Case Names (Sub-Modules)

*   **ExitStrategy**: Advance Care Planning (Current).
*   **SweetSpot**: Diabetes/Glucose tracking IG.
*   **BeatIt**: Cardiovascular/Hypertension IG.
*   **InnerPeace**: Mental Health / Questionnaire-based IG.