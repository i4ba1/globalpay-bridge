# Running GlobalPay Bridge in Visual Studio

Targeted at **Visual Studio 2026** (18.x) and Visual Studio 2022 **17.14+**, both of which open the
`.slnx` solution natively. Verified on this machine with Visual Studio Community 2026 (18.9.12128).

---

## 1. What runs where

| Project | Runs in Visual Studio | Notes |
|---------|----------------------|-------|
| `GlobalPay.Api` | Yes | The debug target. Listens on `http://localhost:5080` |
| `GlobalPay.ProviderSimulator` | Yes | Listens on `http://localhost:5090` |
| `GlobalPay.Functions` | **Only with the Azure development workload installed** | Otherwise run it in Docker (see §6) |
| Domain / Application / Infrastructure / Contracts | Library projects | Debugged indirectly by stepping into the API |
| `tests/*` | Yes (Test Explorer) | 115 tests |

On this machine the Azure workload is **not** installed and neither is Azure Functions Core Tools. That
is fine: keep Functions in Docker and debug the API in Visual Studio. That is also the fastest loop.

---

## 2. Open the solution

1. Start Visual Studio.
2. **File → Open → Project/Solution…** and select `GlobalPayBridge.slnx` at the repository root.

On first open, Visual Studio may show a prompt based on `.vsconfig` listing components this project
expects. Accept it to install anything missing (the file only lists components that exist in the VS
catalog, so the prompt is safe to accept or dismiss).

All ten projects load. If your Visual Studio is older than 17.14 it will not open `.slnx`; see §10.

---

## 3. One-time setup

Open **View → Terminal** in Visual Studio (or use an external shell at the repository root) and run:

```powershell
# Local tools (dotnet-ef)
dotnet tool restore

# Infrastructure: SQL Server, Redis, Azurite, Keycloak, provider simulator, Functions
.\scripts\start-local-dependencies.ps1
```

`start-local-dependencies.ps1` starts the Docker services and waits for SQL Server to report healthy
(first run takes ~40 s while the container initialises). Add `-WithoutFunctions` if you intend to run
Functions from Visual Studio, or `-Stop` to stop the dependency set later.

Apply the database schema once:

```powershell
dotnet dotnet-ef database update `
  --project src/GlobalPay.Infrastructure `
  --startup-project src/GlobalPay.Infrastructure `
  --connection "Server=localhost,11433;Database=GlobalPayBridge;User Id=sa;Password=GlobalPay_Local_2026!;TrustServerCertificate=True;Encrypt=False"
```

---

## 4. Set the startup project

**Single project (normal case):** right-click `GlobalPay.Api` in Solution Explorer →
**Set as Startup Project**, then press **F5**.

**Multiple projects** (API + provider simulator together): right-click the solution →
**Configure Startup Projects…** → **Multiple startup projects** → set `GlobalPay.Api` and
`GlobalPay.ProviderSimulator` to **Start** → OK. Then **F5** launches both under the debugger.

> The provider simulator is optional in VS because `start-local-dependencies.ps1` already runs it in
> Docker on the same port. Run one or the other, not both.

### Launch profiles

`src/GlobalPay.Api/Properties/launchSettings.json` provides:

| Profile | URL | Environment |
|---------|-----|-------------|
| `http` | http://localhost:5080 | Development |
| `https` | https://localhost:7081 + http://localhost:5080 | Development |

Pick the profile from the dropdown next to the Run button. The ports match `docker-compose.yml`, so
configuration and tokens are interchangeable between the two ways of running.

---

## 5. Verify it is running

With the API running (F5), open a browser:

- http://localhost:5080/ — `{"service":"GlobalPay.Api","status":"healthy"}`
- **http://localhost:5080/swagger** — Swagger UI (interactive; recommended)
- http://localhost:5080/scalar/v1 — Scalar, an alternative rendering of the same document
- http://localhost:5080/openapi/v1.json — the generated OpenAPI document
- http://localhost:5080/health/ready — should be `Healthy` once SQL and Redis are reachable

### Authorize in Swagger UI

1. Get a token from Keycloak:

   ```powershell
   $token = (curl.exe -s -X POST `
     "http://localhost:8080/realms/globalpay/protocol/openid-connect/token" `
     -d "grant_type=password&client_id=globalpay-cli&username=admin.user&password=Password123!" `
     | ConvertFrom-Json).access_token

   $token | Set-Clipboard
   ```

2. In Swagger UI click **Authorize** (top right), paste the token into the **Value** field, click
   **Authorize**, then **Close**. Swashbuckle adds the `Bearer ` prefix, and the token is kept for the
   session across page reloads.

3. Expand any endpoint → **Try it out** → **Execute**. The response includes the status, the body, and
   the `X-Correlation-Id` response header.

The security scheme is declared in the OpenAPI document by
`src/GlobalPay.Api/OpenApi/BearerSecuritySchemeTransformer.cs`, so both Swagger UI and Scalar get the
**Authorize** control from one place.

Full token and flow commands are in
[§7 of the developer guide](developer-guide.md#7-end-to-end-walkthrough).

---

## 6. Running the Functions host

`GlobalPay.Functions` contains the blob import trigger, the outbox dispatcher, and the reconciliation
timer. Pick one:

**Option A — Docker (no extra install, recommended).** Leave Functions to Docker Compose; it uses the
same SQL/Redis/Azurite endpoints and is already running as part of
`.\scripts\start-local-dependencies.ps1`.

```powershell
docker compose logs -f functions          # watch imports and outbox dispatch
```

**Option B — F5 in Visual Studio.** Install the **Azure development** workload via the Visual Studio
Installer (it brings the Azure Functions tools and Core Tools). Then:

1. Copy the local settings template once:

   ```powershell
   Copy-Item src/GlobalPay.Functions/local.settings.sample.json src/GlobalPay.Functions/local.settings.json
   ```

2. Right-click `GlobalPay.Functions` → **Set as Startup Project** → **F5**.

Without the workload, Visual Studio still *builds* the project (it is a normal SDK project with the
isolated-worker SDK) but cannot launch it.

---

## 7. Debugging

- **Breakpoints work in handlers and the domain.** Set one in
  `CalculatePayrollRunCommandHandler.HandleAsync` or `PayrollCalculator.Calculate`, then drive the call
  from Swagger UI (`/swagger` → Try it out) or the walkthrough script.
- **Exception visibility.** `GlobalPayExceptionHandler` converts unhandled exceptions into problem
  details. Enable **Debug → Windows → Exception Settings → Common Language Runtime Exceptions** to break
  on throw while diagnosing.
- **Change app settings without editing tracked files:** right-click `GlobalPay.Api` →
  **Manage User Secrets** (the project already has a `UserSecretsId`). Use this for connection strings or
  a different OIDC authority.
- **See SQL traffic:** set the Serilog override in user secrets or `appsettings.Development.json`:

  ```json
  { "Serilog": { "MinimumLevel": { "Override": { "Microsoft.EntityFrameworkCore.Database.Command": "Information" } } } }
  ```
- **Inspect telemetry locally:** in Development the host writes OpenTelemetry traces/metrics to the
  console, which appears in the VS **Output** window under the project's debug output.
- **Hot Reload** works for endpoint and handler body edits; structural changes (new constructor
  dependencies, new endpoints) need a restart.

---

## 8. Tests in Visual Studio

Open **Test → Test Explorer**. All 115 tests are discovered across the three test projects.

**Run everything:** Test Explorer → **Run All**. Expect 84 unit + 24 integration + 7 architecture.

Test host parallelism is capped by `tests/globalpay.runsettings`, which is wired in through
`Directory.Build.props` (`RunSettingsFilePath`). Without that cap, Visual Studio can start three test
hosts at once and fail with `Failed to create CoreCLR, HRESULT: 0x8007000E`. The same limit is applied
on the command line by `-m:1`.

Useful Test Explorer actions:

- **Filter** by `FullyQualifiedName~PaymentsFlowTests` to run one class.
- **Run** / **Debug** a single test with a breakpoint inside the handler it exercises.
- **Analyze Code Coverage** on a run if the Enterprise edition (or the
  `Microsoft.CodeCoverage` collector) is available.

Note that `GlobalPay.IntegrationTests` starts the real API host (`ApiTestFactory`) in-process, so those
tests do not need SQL Server running; they assert authentication, authorization, validation, health, and
OpenAPI behaviour.

---

## 9. Entity Framework migrations from Visual Studio

Use the project's local tool from the VS terminal (this is the supported path — no extra package needed):

```powershell
dotnet dotnet-ef migrations add <Name> `
  --project src/GlobalPay.Infrastructure `
  --startup-project src/GlobalPay.Infrastructure `
  --output-dir Persistence/Migrations

dotnet dotnet-ef database update `
  --project src/GlobalPay.Infrastructure `
  --startup-project src/GlobalPay.Infrastructure
```

The design-time factory (`DesignTimeDbContextFactory`) lives in `GlobalPay.Infrastructure`, so the
Infrastructure project is both the target and the startup project.

Package Manager Console is an alternative, but it requires adding `Microsoft.EntityFrameworkCore.Tools`
to the project and selecting `GlobalPay.Infrastructure` as the Default project:

```powershell
Add-Migration <Name> -Project GlobalPay.Infrastructure -StartupProject GlobalPay.Infrastructure
Update-Database -Project GlobalPay.Infrastructure -StartupProject GlobalPay.Infrastructure
```

---

## 10. Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Solution will not open / "not supported" | Visual Studio older than 17.14 cannot read `.slnx` | Use VS 2022 17.14+ or VS 2026, or generate a classic solution: `dotnet new sln --name GlobalPayBridge-classic --format sln` then `dotnet sln GlobalPayBridge-classic.sln add (Get-ChildItem -Recurse -Filter *.csproj).FullName` |
| `GlobalPay.Functions` cannot start | Azure development workload / Functions tools not installed | Run Functions in Docker (§6 option A) or install the workload |
| API starts, then requests return 500 with a SQL error | SQL Server container not healthy yet, or migrations not applied | `docker compose ps` then apply migrations (§3) |
| Startup fails with `Blob storage is not configured` | Not running through `dotnet run`/launchSettings, or `ConnectionStrings:Blobs` cleared | Launch with the `http` profile; set `ConnectionStrings:Blobs` in user secrets |
| Startup fails with `Globalization Invariant Mode is not supported` | `InvariantGlobalization` was re-enabled | It must remain `false`; `Microsoft.Data.SqlClient` requires ICU (ADR-013) |
| Tests fail with `Failed to create CoreCLR, HRESULT: 0x8007000E` | Test hosts ran in parallel | Ensure `tests/globalpay.runsettings` is used (it is wired in) and avoid running projects in parallel |
| Build throws `OutOfMemoryException` in NuGet or analyzers | Accumulated MSBuild/compiler servers | Run `dotnet build-server shutdown`, then rebuild |
| A protected endpoint returns 500 `ICommandHandler… has not been registered` | Endpoint parameter missing `[FromServices]` | Annotate it; Autofac does not expose `IServiceProviderIsService` so minimal APIs cannot infer services |
| Request to `/api/v1/...` returns 401 | No bearer token, or Keycloak not running | Start Keycloak, obtain a token (§5) |
| `Login failed for user 'sa'` when applying migrations | A native SQL Server / Express instance owns port 1433, so host connections bypass the container | Use `Server=localhost,11433` — the Compose file publishes SQL Server on 11433 to avoid exactly this clash |
| `dotnet ef database update` fails on a fresh server | The database does not exist yet, and SQL refuses a login that names a missing database | Run `database/scripts/00-create-database.sql` once, then update (§3) |
| Port already in use (5080/5090/8080) | Dependency already running in Docker, or another process | `.\scripts\start-local-dependencies.ps1 -Stop`, or change the published port |
| Keycloak user login fails | Realm import only runs on an empty volume | `docker compose down -v` then re-run the start script |

Quick diagnostics:

```powershell
docker compose ps
docker compose logs -f functions
dotnet user-secrets list --project src/GlobalPay.Api
dotnet build-server shutdown
```

---

## 11. Recommended daily loop

```powershell
# once per session
.\scripts\start-local-dependencies.ps1

# then, in Visual Studio
#   - F5 GlobalPay.Api (or API + ProviderSimulator as multiple startup projects)
#   - breakpoints in handlers / domain
#   - Test Explorer -> Run All before committing

# at the end of the session
.\scripts\start-local-dependencies.ps1 -Stop
```

Related reading: [developer guide](developer-guide.md) · [README](../../README.md) ·
[ADRs](../adr/README.md) · [runbooks](../runbooks/incident-triage.md)
