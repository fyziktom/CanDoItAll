# Cognitive Memory Stage Assessment

## Decision

The current stage is **memory provider extraction architecture repair, final validation gate**.

The base app now has a provider-agnostic memory runtime and UI that works with zero providers. Native Cognitive Memory is optional and service-owned. Qdrant, native memory, SemanticCompletion, OpenAI, and deterministic mock providers are not base memory fallbacks.

## Stage Matrix

| Area | Current stage | Evidence | Residual risk / next hardening |
| --- | --- | --- | --- |
| Base startup | Release candidate | `HostCompositionDependencyRemovalTests`, SB30 proof, SB33 source audits. | Keep composition guard tests in every memory/provider change. |
| Generic protocol and provider registry | Release candidate | SB01-SB05 proof, full generic memory tests, provider selection tests. | External provider profiles need more operator UX over time. |
| Runtime ledgers and workers | Release candidate for a single active worker replica | Async final results are durable and per-item failures are isolated. | Multi-replica deployments still require an atomic claim/lease before background workers may run concurrently. |
| Source Gateway | Release candidate | SB04, SB11-SB14, and SB33 source request proof. | New source modules must register adapters and preserve redaction/provenance. |
| Agent Framework integration | Release candidate | Dedicated `CanDoItAll.AgentFramework.Memory` project with mode, directive, multi-provider, tool, workflow, and model-message tests. | New operations must remain hidden until their transport, authorization, persistence, and worker paths are complete. |
| Generic UI | Release candidate | SB20-SB23 and SB33 Playwright/component proof. | Final native-provider-specific advanced UI migration can improve operator ergonomics. |
| Native service | Release candidate for optional provider path | Native solution build/tests and native protocol proof in SB24-SB29 and SB33. | Native deployment packaging and production hosting profile remain operator/platform work. |
| Historical main DB data | Release candidate for retention/export | SB31 export service, no-op retirement migration, and docs. | Native import is intentionally not claimed until a native import contract exists. |
| Legacy main-repo native module | Retained legacy coverage | Still referenced only by retained legacy/native tests, not base composition. | Follow-up native-suite migration should move/delete retained legacy tests and module files. |
| Documentation and release gate | Release candidate | Provider setup, authoring, validation, release notes, and SB34 release-gate proof. | Keep docs and release-gate transcripts current as provider drivers evolve. |

## What Is Actually Done

- The generic memory projects exist under `src/Memory`.
- The generic `/memory` module is registered by base composition.
- Base `appsettings.json` disables deterministic mock, HTTP, native-remote, and MCP memory drivers by default.
- Agent memory settings support ordered zero/many provider bindings, automatic mode, and explicit `/mem:<alias>` mode.
- Provider operation dispatch is centralized in the shared memory operation handler.
- Source ingestion uses the provider-neutral `CanDoItAll.Memory.SourceGateway.Abstractions` contract family.
- The native repo builds and tests independently.
- Legacy main DB native memory tables are retired from the main EF model without destructive drops.

## What Is Not Yet True

- The old main-repo native module is not deleted. It is retained as legacy/native regression coverage until a follow-up native-suite migration moves or removes it.
- MCP memory provider registration is configuration-gated and disabled by default.
- Native import from the legacy main DB export is not implemented. The export is the compatibility contract.
- Production deployment of the native service and provider-specific advanced native UI remains an operator/platform concern.

## Senior Risks

- **Retained legacy module risk:** the old native module can confuse dependency audits. Treat only base composition/generic memory/MAF audits as startup coupling proof.
- **Provider configuration risk:** enabling a driver without a provider profile does not create a provider. Operators must create or import profiles.
- **Native service operations risk:** the native service is optional and tested, but production hosting, secret management, and rollout policy need environment-specific runbooks.
- **Worker concurrency risk:** background worker processing is safe per item but does not yet own a distributed database lease. Run one active memory worker replica until that lease exists.
- **Legacy data risk:** historical tables stay read-only in the main DB. Import into the native service needs a separate native import contract.

## Validation Evidence

The release-candidate proof is captured under `codex/bundles/candoitall-memory-provider-extraction-bundle/proof`:

- SB30: base host decoupling.
- SB31: legacy main DB export/retirement.
- SB32: test-suite rebalance.
- SB33: end-to-end regression and observability proof.
- SB34: final cleanup, docs, and release gate.

Use [validation and testing](../operations/validation-and-testing.md) for current commands.
