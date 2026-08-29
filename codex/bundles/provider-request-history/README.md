# Provider Request History And Shared Pricing

**Implementation complete and validated on 2026-08-29.** The bundle implements searchable,
authorized provider request history with shared-provider price evidence and managed-credential
attribution. It preserves canonical agent, simple-chat and workflow content instead of copying
tracked transcripts into the provider-history index.

## Outcome

- Provider editors expose a lazy **History** tab immediately after **Sharing**.
- Agents exposes a lazy global **Request history** tab over authorized providers.
- Both surfaces wait for explicit Search and use bounded, protected keyset paging.
- Settings exposes explicit Load/Apply controls for Light or Detailed capture, retention,
  bounded detail size, quota and cleanup batch.
- Shared relay, buffered, streamed, retry, batch, image, agent, simple-chat and workflow paths
  record typed attempt metadata and terminal outcomes.
- Managed credential identity is recorded separately from the caller subject; secrets are
  never stored or displayed.
- Price evidence retains provider-reported, calculated, configured-free and unavailable
  provenance. Generic gpt-5.6 uses the managed Sol tariff alias.
- Existing canonical histories are linked by stable provider/model/source identity. The
  history store keeps scalar metadata and bounded optional current-turn detail only.
- Public shared-provider catalog labels are invariant and do not disclose private upstream
  model identifiers; the server-only routing index retains exact routing identity.

## Architecture

Three neutral projects isolate the feature:

- [Abstractions](../../../src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Abstractions)
- [Application](../../../src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Application)
- [Persistence](../../../src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Persistence)

Production capture adapters remain beside their owning runtimes. Canonical transcript owners
publish typed metadata and lifecycle intents through outbox/journal boundaries. UI components
orchestrate application services and contain no pricing, caller-validation or retention logic.
The final graph has 107 source projects and 556 source project references with no cycle.

Normative records:

- [Target solution](architecture/01-target-solution.md)
- [Dependency direction](architecture/02-csharp-dependency-direction.md)
- [History lifecycle](architecture/05-history-data-lifecycle.md)
- [Search and security](architecture/09-search-security-contract.md)
- [Pricing and capture](architecture/10-pricing-and-capture-contract.md)
- [Execution report](reviews/01-execution-report.md)
- [Requirement traceability](traceability/01-requirement-traceability.md)

## Phase Status

| Phase | Outcome | Status |
|---|---|---|
| [SB01](subbundles/01-contracts-and-boundaries/README.md) | Contracts and boundaries | Passed |
| [SB02](subbundles/02-shared-pricing-evidence/README.md) | Shared pricing evidence | Passed |
| [SB03](subbundles/03-history-storage-and-lifecycle/README.md) | Storage and lifecycle | Passed |
| [SB04](subbundles/04-invocation-capture-and-attribution/README.md) | Capture and attribution | Passed |
| [SB05](subbundles/05-canonical-linking-and-backfill/README.md) | Canonical linking and backfill | Passed after hosted timeout repair |
| [SB06](subbundles/06-authorized-history-search/README.md) | Authorized bounded search | Passed |
| [SB07](subbundles/07-history-tabs-and-policy-ui/README.md) | History tabs and policy UI | Passed |
| [SB08](subbundles/08-runtime-and-performance-proof/README.md) | Runtime and performance proof | Passed |
| [SB09](subbundles/09-final-closure/README.md) | Final closure audit | Passed |

## Final Validation

- Solution build: 0 warnings, 0 errors.
- Unit: 7,185 passed.
- Components: 1,188 passed with serialized bUnit execution.
- Integration: 1,247 passed, 0 failed, 1 unrelated opt-in live Ollama test not executed.
- Scale: 2 passed, including a one-million-row PostgreSQL search fixture.
- Configured publisher/client Docker acceptance: 1 passed.
- Standard app http://127.0.0.1:5032/agents: HTTP 200 and both lazy history views inspected.
- Docker publisher 5210 and client 5212: HTTP 200; the two-key attribution, shared price,
  matching history identifiers and cleanup scenario passed.
- Static gates: git diff --check clean apart from line-ending notices; no source graph cycle;
  no blocking async/performance anti-pattern introduced in the reviewed paths.

See [SB08 validation](proof/SB08/validation.md), [browser/runtime review](proof/SB08/browser-review.md),
[performance measurements](proof/SB08/performance/provider-history-scale-measurements.json) and
[SB09 closure](proof/SB09/validation.md).

## Validation Summary

- Bundle preparation status: Completed.
- Execution status: Completed.
- Subbundle gate review: Completed; SB01-SB09 passed.
- Final closure gate: Passed on 2026-08-29.
- Browser validation analytics: Passed on standard 5032 and shared publisher/client 5210/5212.
## Deferred Scope

Exact-person IDM/EGCP matching, cross-instance history federation, billing reconciliation,
historic repricing without original evidence, full-text body search/export, exact wire replay,
prompt content-addressing, sibling RAG instrumentation and mobile redesign remain outside this
bundle. Detailed mode can retain bounded current-turn prompt/response text and therefore has
an intentional storage/privacy cost controlled by policy.
