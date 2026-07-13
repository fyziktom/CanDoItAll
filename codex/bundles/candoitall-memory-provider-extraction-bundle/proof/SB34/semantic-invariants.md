# SB34 Semantic Invariants

## SB34-I01 Current Docs Must Describe The Generic Provider Runtime

- The current-state docs must say that the base app owns a generic Memory Provider runtime and that native Cognitive Memory is optional and service-owned.
- Historical P0/P1 native docs must be marked as history where they could be mistaken for current base-host behavior.
- Proof: `repo://docs/cognitive-memory/README.md`, `repo://docs/cognitive-memory/current-state/stage-assessment.md`, `repo://docs/cognitive-memory/current-state/implementation-map.md`, `repo://docs/cognitive-memory/operations/api.md`, and `repo://docs/cognitive-memory/roadmap/roadmap.md`.

## SB34-I02 Provider Setup Must Preserve Zero-Provider Semantics

- Provider setup docs must distinguish driver registration from provider profiles.
- Zero-provider startup must remain documented as a valid operating mode.
- The docs must not imply native Cognitive Memory, Qdrant, OpenAI, HTTP, MCP, or mock providers are automatic fallbacks.
- Proof: `repo://docs/cognitive-memory/operations/provider-setup.md`, `bundle://proof/SB34/transcripts/passing-generic-memory-tests.txt`, `bundle://proof/SB34/transcripts/passing-maf-memory-tests.txt`, and `bundle://proof/SB34/transcripts/audit-base-generic-native-boundary.txt`.

## SB34-I03 Provider Authoring Must Keep Runtime Boundaries Typed

- Provider authoring must require strongly typed profiles, manifests, driver interfaces, Source Gateway snapshots, shared operation handling, ledgers, explicit errors, and focused tests.
- Provider authors must not bypass generic memory through host EF entities, native implementation types, direct Qdrant access, hidden mocks, or duplicated dispatch code.
- Proof: `repo://docs/cognitive-memory/operations/provider-authoring.md`, `bundle://proof/SB34/transcripts/audit-maf-memory-native-boundary.txt`, and `bundle://proof/SB34/transcripts/audit-source-snapshot-contract-family.txt`.

## SB34-I04 Retained Legacy Native References Must Be Owned

- Remaining `CognitiveMemory` references must be classified as retained legacy/native module coverage, legacy main DB export/retirement, native service code, or historical docs.
- Any base composition, generic memory, generic memory UI, or generic MAF memory dependency on native Cognitive Memory remains a release blocker.
- Proof: `bundle://proof/SB34/transcripts/audit-retained-cognitive-memory-references.txt`, `repo://docs/cognitive-memory/operations/release-notes-memory-provider-extraction.md`, and `bundle://proof/SB34/transcripts/audit-base-generic-native-boundary.txt`.

## SB34-I05 Live Re-entry Findings Must Close

- Current MAF tool provider, workflow executor, and context contributor seams must be the actual integration paths.
- The existing `MemorySourceSnapshot*` contract family must remain the active snapshot boundary.
- The native repo must be scaffolded and validated independently.
- Zero-provider behavior must be tested and documented.
- Proof: `bundle://proof/SB34/transcripts/live-reentry-closure-audit.txt`, `bundle://proof/SB34/transcripts/passing-maf-memory-tests.txt`, `bundle://proof/SB34/transcripts/native-solution-build.txt`, and `bundle://proof/SB34/transcripts/native-service-tests.txt`.

## SB34-I06 Release Gate Must Be Reproducible

- The final release gate must include focused generic runtime tests, MAF tests, component tests, browser tests, integration tests, native build/tests, main solution build, source audits, file hashes, and completed-stage validation.
- Existing NuGet metadata/advisory warnings are non-blocking only when commands exit successfully.
- Proof: `bundle://proof/SB34/manifest.md`, `bundle://proof/SB34/transcripts/main-solution-build.txt`, and `bundle://proof/SB34/transcripts/completed-stage-validation.txt`.

## Raw Note Closure

- Original memory separation prompt: solved. The base host now has a generic Memory Provider runtime, native Cognitive Memory is optional/service-owned, base startup supports zero providers, MAF uses generic memory paths, and legacy main DB data has an export/retirement path.
- Current bundle/subbundle request: solved. SB34 completed final cleanup docs, release notes, audits, final build/test/native validation, role reviews, proof manifest, semantic invariants, execution report, and completed-stage validation.
- Re-entry alignment: solved or explicitly deferred. Current MAF seams, source snapshot contracts, native repo scaffold/status, and zero-provider behavior are closed; retained legacy native suite migration is deferred with owner/risk/follow-up.

## Shallow-Pass Trap

A shallow SB34 pass would:

- update a top-level README but leave stale P0/P1 native-Qdrant docs as current guidance;
- claim zero-provider support while provider setup docs imply hidden native/mock fallback;
- remove or ignore retained legacy native references without classifying them;
- skip native repo validation;
- skip the completed-stage validator;
- rely on DTO-only notes instead of build/test/audit transcripts;
- leave source snapshot contract drift untested.

The SB34 proof would fail that implementation because it includes source audits, retained-reference classification, final build/test/native transcripts, source snapshot definition audit, live re-entry closure, completed-stage validation, file hashes, and explicit release notes.

## Validator Invariant Contract

- Invariant ID: `SB34-RELEASE-GATE`
- Source raw note: Original memory separation request, current bundle/subbundle request, and 2026-07-05 re-entry alignment.
- Expected behavior: SB34 closes the generic memory provider extraction with current docs, release notes, retained-reference classification, final validation transcripts, file hashes, and completed-stage validation.
- Disallowed shallow implementation: README-only cleanup, hidden provider fallback language, stale native-Qdrant current guidance, unclassified retained native references, skipped native validation, skipped completed-stage validator, or source snapshot contract drift.
- Failing-first test: `bundle://proof/SB34/transcripts/failing-first-source-snapshot-contract-family-audit.txt` and `bundle://proof/SB34/transcripts/failing-second-source-snapshot-contract-family-audit.txt`.
- Passing test: `bundle://proof/SB34/transcripts/audit-source-snapshot-contract-family.txt`, `bundle://proof/SB34/transcripts/passing-generic-memory-tests.txt`, `bundle://proof/SB34/transcripts/passing-maf-memory-tests.txt`, and `bundle://proof/SB34/transcripts/passing-memory-playwright-tests.txt`.
- Changed source files: `repo://docs/cognitive-memory/README.md`, `repo://docs/cognitive-memory/current-state/stage-assessment.md`, `repo://docs/cognitive-memory/operations/provider-setup.md`, `repo://docs/cognitive-memory/operations/provider-authoring.md`, and `repo://docs/cognitive-memory/operations/release-notes-memory-provider-extraction.md`.
- Production assertions: SB34 adds no production runtime behavior; it validates and documents the already implemented generic provider runtime, optional native service path, Source Gateway boundary, MAF memory integration, UI, ledgers, and zero-provider behavior.
- Red-team negative case: A reviewer should reject a pass that cannot reproduce the focused generic, MAF, component, browser, integration, native, main build, boundary-audit, anti-stub, and completed-stage validation evidence.
- Downstream dependency check: No downstream subbundles remain; release readiness depends on this proof and the deferred-work table in `repo://docs/cognitive-memory/operations/release-notes-memory-provider-extraction.md`.

## Adversarial Negative Proof

- Failing-first source snapshot audit: the initial broad usage-level audit incorrectly treated legitimate generic-memory references to the shared snapshot contract as duplicate contracts.
- Failing-second source snapshot audit: the intermediate audit also treated generic source status enums as duplicate snapshot contracts.
- Passing audit: the final definition-level audit checks only `MemorySourceSnapshot*` type definitions and proves they remain in the primary MAF contract file.
- Proof: `bundle://proof/SB34/transcripts/failing-first-source-snapshot-contract-family-audit.txt`, `bundle://proof/SB34/transcripts/failing-second-source-snapshot-contract-family-audit.txt`, and `bundle://proof/SB34/transcripts/audit-source-snapshot-contract-family.txt`.

## Semantic Positive Proof

- Generic runtime tests passed, including zero-provider, provider selection, handlers, ledgers, workers, HTTP/MCP/native-remote adapters, Source Gateway, deterministic mock explicit registration, and SB33 e2e observability.
- MAF memory tests passed, including tool provider, workflow executor, context contributor, shared policy/result shaping, native-free audit, and no-provider behavior.
- Component and Playwright tests passed for the memory route zero-provider, provider profiles, query/feedback/manual ingestion/operations/events, and provider UI surfaces.
- Native solution build/tests passed separately.
- Main solution build passed.
- Source audits passed for base/generic/native boundaries and retained-reference classification.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| SB34 release-gate validation | `bundle://proof/SB34/transcripts/passing-generic-memory-tests.txt` | `bundle://proof/SB34/transcripts/passing-maf-memory-tests.txt` | `bundle://proof/SB34/transcripts/main-solution-build.txt` | `bundle://proof/SB34/transcripts/failing-first-source-snapshot-contract-family-audit.txt` |
| SB34 operator guidance | `repo://docs/cognitive-memory/operations/provider-setup.md` | `repo://docs/cognitive-memory/operations/provider-authoring.md` | `repo://docs/cognitive-memory/operations/release-notes-memory-provider-extraction.md` | `bundle://proof/SB34/transcripts/audit-sb34-stub-xml-markers.txt` |
| SB34 retained-reference classification | `bundle://proof/SB34/transcripts/audit-retained-cognitive-memory-references.txt` | `repo://docs/cognitive-memory/operations/release-notes-memory-provider-extraction.md` | `bundle://proof/SB34/transcripts/live-reentry-closure-audit.txt` | `bundle://proof/SB34/transcripts/audit-base-generic-native-boundary.txt` |

## Downstream Dependency Check

- No downstream subbundles remain.
- Merge/release can rely on SB34 proof because it consolidates SB01-SB33 behavior and reruns the final release gate.
- Deferred work is non-blocking and owned in release notes: retained native-suite migration, native import from legacy export, transport-extension profile UX, and native service production runbooks.
