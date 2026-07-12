# Memory Provider Extraction Release Notes

## Release Decision

The memory provider extraction was reopened for architecture repair on 2026-07-12. SB35-SB39 repair provider selection, agent modes, transport truthfulness, and external-repository isolation. SB40 remains the release gate until final builds, live-process/browser proof, architecture review, and completed-stage bundle validation pass.

This release changes Cognitive Memory from a base-host native module assumption into an optional provider behind the generic Memory Provider runtime.

## Operator-Visible Changes

- The base app can start with zero configured memory providers.
- `/memory` is the generic provider management and operations surface.
- Memory calls from UI, MAF tools, workflows, context contribution, and Source Gateway ingestion use shared generic runtime paths.
- Missing provider, disabled provider, capability mismatch, missing driver, timeout, and provider errors are observable as typed diagnostics and ledger records.
- Each agent can bind ordered zero/many provider aliases and choose automatic context or explicit leading `/mem:<alias>` directives.
- Automatic context is synchronous and deterministic; explicit tool queries may be asynchronous only when operation status is implemented.
- HTTP and native-remote provider drivers are opt-in.
- The deterministic mock provider is explicit test/development configuration only.
- MCP remote-HTTP provider support is configuration-gated and supports context query plus optional operation status. Unsupported ingestion, feedback, and event tool mappings are rejected.
- Native Cognitive Memory now belongs to `C:\repositories\CanDoItAll.CognitiveMemory` as an optional service/provider path.
- The external solution owns its Protocol v1 wire DTOs and builds/tests without the main checkout; compatibility is verified through JSON fixtures and live consumer conformance.
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
      },
      "Mcp": {
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
5. Bind the profile to an agent with a stable alias and choose `Automatic` or `ExplicitDirective` mode.
6. Inspect operation ledgers for typed status and diagnostics.

## Native Service Setup

Native Cognitive Memory setup is separate from base app setup:

```powershell
cd C:\repositories\CanDoItAll.CognitiveMemory
dotnet build .\CanDoItAll.CognitiveMemory.slnx --no-restore --verbosity:minimal
dotnet test .\tests\CanDoItAll.CognitiveMemory.Tests\CanDoItAll.CognitiveMemory.Tests.csproj --logger "console;verbosity=minimal"
```

After the native service is running, enable `Memory:Providers:NativeRemote:Enabled`, configure a `NativeRemote` profile with `native.cognitiveMemory.remote.serviceBaseUrl` plus an environment-variable credential reference, and verify a project-scoped query through the generic profile UI.

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
| Production hosting runbook for native service. | Platform/operator owners | Native provider rollout needs environment-specific secrets, DB, service health, and deployment policy. | Native service operations bundle. |
| Distributed claim/lease for generic memory background workers. | Generic memory persistence maintainers | Multiple active worker replicas can process the same due row. | Run one active memory-worker host until an atomic lease/claim contract is implemented. |

## Compatibility

Existing legacy Cognitive Memory HTTP API files are retired from the base host surface as part of extraction. Historical API docs remain under `docs/cognitive-memory` for native-provider history and should not be treated as generic provider API contracts.

New provider integrations must target the generic protocol contracts in `src/Memory` and the native service protocol surface when using native Cognitive Memory remotely.
