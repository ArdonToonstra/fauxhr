# Seed Data

FHIR transaction bundles for populating test servers. Bundles are listed in
`index.json` and grouped by module in the FauxHR Settings page.

## Adding a bundle

1. Drop your FHIR transaction bundle JSON file in the appropriate subfolder:
   - `acp/` — Exit Strategy (ACP)
   - `ctm/` — CTM (CDS Hooks)
   - `crmi/` — CRMI Authoring

2. Add an entry to `index.json`:

```json
{
  "bundles": [
    {
      "id": "acp-hendrik-hartman",
      "name": "Hendrik Hartman",
      "description": "Patient with full ACP document set",
      "module": "acp",
      "moduleName": "Exit Strategy (ACP)",
      "file": "seed-data/acp/hendrik-hartman.json"
    }
  ]
}
```

The `file` path is relative to the app root (i.e. relative to `wwwroot/`).
The bundle must be a valid FHIR Bundle with `"type": "transaction"`.
