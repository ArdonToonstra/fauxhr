# fauxhr
FauxHR is a modular, client-side Personal Health Record (PHR) built with Blazor WASM and the Firely .NET SDK. Its purpose is to act as a "living" testbed for FHIR Implementation Guides (IGs), starting with the IKNL Advance Care Planning (ACP) IG.

## How to Run

### Option 1: WASM only (no proxy)

Prerequisites: [.NET 8 SDK](https://dotnet.microsoft.com/download)

```bash
dotnet run --project FauxHR.App
```

This runs the standalone Blazor WASM app (usually at `https://localhost:7284`). Works for FHIR servers that allow CORS (e.g. Firely Server, HAPI).

### Option 2: With FHIR proxy (recommended)

Some FHIR servers (Nictiz Conformancelab, PZP Coalitie) have broken CORS headers. The server project includes a reverse proxy that bypasses this.

```bash
dotnet run --project FauxHR.App.Server
```

This serves the WASM app and proxy at `http://localhost:5090`. Servers marked with a "Proxy" badge in Settings will route through the proxy automatically.

### Option 3: Docker

```bash
docker compose up -d --build
```

The app is available at `http://localhost:8080`. See [Docker Deployment](#docker-deployment) for details.

## Project Structure
- **FauxHR.App** — Blazor WebAssembly application
- **FauxHR.App.Server** — ASP.NET Core host with FHIR reverse proxy
- **FauxHR.Core** — Shared logic, interfaces, and state management
- **FauxHR.Modules.ExitStrategy** — IKNL ACP Implementation Guide module
- **FauxHR.Modules.CrmiAuthoring** — CRMI Authoring UI module

## LForms Integration
The application uses [LHC-Forms](https://lhncbc.github.io/lforms/) to render FHIR Questionnaires.
- **Assets**: Scripts and CSS are served locally from `FauxHR.Modules.ExitStrategy/wwwroot`.
- **Questionnaire Definition**: Rendering a `QuestionnaireResponse` requires the original `Questionnaire` definition. This is currently embedded as a resource in `FauxHR.Modules.ExitStrategy`.
- **Usage**: The helper `wwwroot/js/lforms-helper.js` handles the merging of the Response data into the Questionnaire definition before rendering.

## FHIR Proxy

The `FauxHR.App.Server` project includes a reverse proxy at `/fhir-proxy/{path}` that forwards requests server-to-server, bypassing CORS restrictions in the browser.

**Security measures:**
- **Domain allowlist** — Only requests to known FHIR server hostnames are forwarded. Controlled via the `FHIR_PROXY_ALLOWLIST` environment variable (semicolon-separated hostnames). Default: `server.fire.ly;hapi.fhir.org;nictiz.proxy.interoplab.eu;pzp-coalitie.proxy.interoplab.eu`
- **Rate limiting** — 60 requests/minute per client IP
- **Request size limit** — 5 MB max request body
- **Scheme validation** — Only `http` and `https` URLs are accepted

To allow additional FHIR servers through the proxy, add their hostnames to `FHIR_PROXY_ALLOWLIST`.

## Docker Deployment

The included `Dockerfile` and `docker-compose.yml` are designed for self-hosting behind a reverse proxy like Cloudflare Tunnel.

### Build and run

```bash
docker compose up -d --build
```

### Configuration

Environment variables in `docker-compose.yml`:

| Variable | Default | Description |
|----------|---------|-------------|
| `FHIR_PROXY_ALLOWLIST` | `server.fire.ly;hapi.fhir.org;nictiz.proxy.interoplab.eu;pzp-coalitie.proxy.interoplab.eu` | Allowed upstream FHIR server hostnames |
| `ASPNETCORE_URLS` | `http://+:8080` | Listening URL (HTTP only, TLS is handled by Cloudflare) |

### Container security

- Non-root user (`appuser`)
- Read-only root filesystem
- `no-new-privileges` flag
- 256 MB memory limit

### Deploying with Cloudflare Tunnel

Point your tunnel at `http://localhost:8080`:

```yaml
# cloudflared config.yml
ingress:
  - hostname: fauxhr.donit.be
    service: http://localhost:8080
  - service: http_status:404
```

### ARM64 / Raspberry Pi

The standard .NET Docker images support `linux/arm64` natively. Build directly on the Pi:

```bash
git clone https://github.com/ArdonToonstra/fauxhr.git
cd fauxhr
docker compose up -d --build
```
