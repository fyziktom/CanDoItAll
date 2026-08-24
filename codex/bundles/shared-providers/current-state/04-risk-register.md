# Preparation risk register

| ID | Risk | Severity | Primary mitigation | Owning subbundle |
| --- | --- | --- | --- | --- |
| R-001 | Internal provider profile or secret metadata leaks through catalog | Critical | dedicated public projection, allowlist mapping, redaction tests | SB01, SB03 |
| R-002 | Shared provider becomes an open proxy | Critical | opaque routing IDs, no caller URL/header, adapter registry | SB04 |
| R-003 | Tool calls execute centrally or lose semantics | Critical | relay function-tool wire data; local runtime owns execution | SB04, SB06 |
| R-004 | New `ProviderKind.Shared` spreads runtime switches | High | connector-origin metadata projected to OpenAI-compatible runtime | SB06 |
| R-005 | Workspace/MAF/HTTP project cycle | Critical | abstractions lower layer, composition wiring outward | SB00, SB01 |
| R-006 | Source token duplicated in every imported profile | High | canonical source secret reference and derived runtime materialization | SB02, SB06 |
| R-007 | Remote disappearance deletes referenced profiles | High | import state machine, preserve local ID, no destructive outage sync | SB05 |
| R-008 | Same model name routes to wrong publication | Critical | publication-namespaced opaque model routing IDs | SB01, SB04 |
| R-009 | Temporary source outage silently falls back locally | Critical | explicit availability gate and error | SB06, SB07 |
| R-010 | Blind OpenAI proxy enables storage/built-in tools/data egress | Critical | supported-field policy and negative tests | SB04 |
| R-011 | Streaming buffers or ignores disconnect | High | `ResponseHeadersRead`, bounded parser, cancellation tests | SB04 |
| R-012 | Access context is treated as trusted identity | Critical | separate auth subject, opaque value, audit labeling | SB01, SB04 |
| R-013 | Access context leaks to upstream provider | High | explicit outbound-header allowlist and upstream capture test | SB04, SB07 |
| R-014 | Usage/cost is double-counted or fabricated | High | one invocation record/source; completeness semantics | SB04 |
| R-015 | Source URL causes SSRF/DNS-rebinding/TLS bypass | Critical | explicit URI/network policy, redirect handling, tests | SB05 |
| R-016 | EF changes break migration/runtime model | High | focused model/migration tests, one final broad gate | SB02, SB12 |
| R-017 | Backend is assumed working from in-process tests | Critical | real three-app Docker lane before UI | SB07 |
| R-018 | UI mutates remote-owned fields | High | read-only projection and service-side enforcement | SB08, SB09 |
| R-019 | OpenAPI claims more compatibility than implementation | High | operation/capability contract tests and skill wording | SB11 |
| R-020 | Repeated broad tests consume excessive credits | High | machine test budget and frozen checkpoints | all |
| R-021 | E2E proof requires paid/live provider | High | deterministic upstream fixture container | SB07, SB12 |
| R-022 | Final containers are cleaned up before manual test | Medium | final script omits teardown and writes handoff | SB12 |
