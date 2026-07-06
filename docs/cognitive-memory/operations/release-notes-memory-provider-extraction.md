# Memory Provider Extraction Release Notes

## Release Decision

The memory provider extraction is ready for merge after SB34 closure when the final build, tests, dependency audits, native repo build/tests, and bundle completed-stage validation pass.

This release changes Cognitive Memory from a base-host native module assumption into an optional provider behind the generic Memory Provider runtime.

## Operator-Visible Changes

- The base app can start with zero configured memory providers.
- `/memory` is the generic provider management and operations surface.
- Memory calls from UI, MAF tools, workflows, context contribution, and Source Gateway ingestion use shared generic runtime paths.
- Missing provider, disabled provider, capability mismatch, missing driver, timeout, and provider errors are observable as typed diagnostics and ledger records.
- HTTP and native-remote provider drivers are opt-in.
- The deterministic mock provider is explicit test/development configuration only.
- MCP memory provider support is package-level and requires host composition to register the driver before MCP profiles can dispatch.
- Native Cognitive Memory now belongs to `C:\repositories\CanDoItAll.CognitiveMemory` as an optional service/provider path.
- Historical main database `CognitiveMemory_*` tables are retained read-only with an export service and no destructive drop migration.

## Default Startup

No provider is enabled by default:

```json
{
  "Memory": {
    "Providers": {
      "DeterministicMock": {
        "Enabled": false
      },
      "Http": {
        "Enabled": false
      },
      "NativeRemote": {
        "Enabled": false
      }
    }
  }
}
```

Zero-provider startup is a supported production state. Provider management UI remains available, and provider-backed operations fail predictably with typed diagnostics rather than falling back to native Cognitive Memory, Qdrant, OpenAI, or a mock provider.

## Provider Setup

See [provider setup](provider-setup.md).

The short version:

1. Enable the driver in composition/configuration.
2. Create or import an enabled `MemoryProviderProfile`.
3. Declare only capabilities supported by that driver/provider.
4. Verify provider health and run a context query from `/memory`.
5. Inspect operation and feedback ledgers for typed status and diagnostics.

## Native Service Setup

Native Cognitive Memory setup is separate from base app setup:

```powershell
cd C:\repositories\CanDoItAll.CognitiveMemory
dotnet build .\CanDoItAll.CognitiveMemory.slnx --no-restore --verbosity:minimal
dotnet test .\tests\CanDoItAll.CognitiveMemory.Tests\CanDoItAll.CognitiveMemory.Tests.csproj --logger "console;verbosity=minimal"
```

After the native service is running, enable `Memory:Providers:NativeRemote:Enabled`, import a `NativeRemote` profile with `native.cognitiveMemory.remote.serviceBaseUrl`, and verify health through the generic profile UI.

Native service DB migrations, Qdrant projection configuration, advanced native memory features, native workers, and native UI packaging are service-owned concerns.

## Migration Notes

The main app does not drop legacy native memory tables. SB31 added:

- read-only legacy main DB export contracts;
- PostgreSQL legacy data reader;
- export service tests;
- a no-op retirement migration that removes native tables from the main EF model without destructive SQL.

Use [legacy main DB retirement](legacy-main-db-retirement.md) before deleting historical data. Import into the native service is intentionally not claimed by this release because a native import contract is not implemented yet.

The old main-repo Cognitive Memory module remains only as retained legacy/native regression coverage until a follow-up native-suite migration deletes or moves it. It is not part of base startup proof.

## Rollback

Rollback is configuration-first:

1. Disable the provider profile.
2. Disable the corresponding driver flag.
3. Restart the host if driver registration changed.
4. Confirm `/memory` renders zero-provider or disabled-provider state.

No rollback step requires restoring native Cognitive Memory as a base dependency. Generic memory ledgers can remain in place. Legacy main DB native tables remain read-only unless an operator explicitly archives or drops them outside this release.

## Release Validation

Run the commands in [validation and testing](validation-and-testing.md). The final gate includes:

- full generic memory tests;
- MAF memory unit tests;
- generic memory component tests;
- memory provider Playwright tests when UI behavior is touched;
- database runtime switching integration test;
- native service build and tests;
- main solution build;
- source audits for base-host/native coupling;
- bundle completed-stage validation.

## Known Deferred Work

| Deferred item | Owner | Risk | Follow-up |
| --- | --- | --- | --- |
| Move/delete retained legacy main-repo native module and native regression tests. | Memory/native maintainers | Audits can be misread if retained legacy tests are treated as base-host dependencies. | Native-suite migration bundle. |
| Native import from legacy main DB export. | Native service maintainers | Historical data can be exported but not imported into native service by this release. | Native import contract bundle. |
| Dedicated profile import/editor fields for HTTP, MCP, and native-remote transport extensions. | Generic memory UI maintainers | Operators may need seeded/imported profiles for transport-specific extension keys. | Provider profile UX hardening bundle. |
| Production hosting runbook for native service. | Platform/operator owners | Native provider rollout needs environment-specific secrets, DB, service health, and deployment policy. | Native service operations bundle. |

## Compatibility

Existing legacy Cognitive Memory HTTP API files are retired from the base host surface as part of extraction. Historical API docs remain under `docs/cognitive-memory` for native-provider history and should not be treated as generic provider API contracts.

New provider integrations must target the generic protocol contracts in `src/Memory` and the native service protocol surface when using native Cognitive Memory remotely.
