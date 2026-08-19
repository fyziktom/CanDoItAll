# Source Artifacts

| Authority | Portable reference | Use in this bundle |
| --- | --- | --- |
| Raw request | `bundle://inputs/00-original-request.md` | Scope, backend-only boundary, and prepare-only instruction |
| WIP predecessor | `repo://codex/bundles/Simple-Llm-Chats-Hardening-Sse` | Historical implementation/proof claims, specifications, invalidation rules, and stale SB13 blocker |
| Initial backend predecessor | `repo://codex/bundles/Simple-Llm-Chats-Backend-Api` | Original product model, transport contract, and implementation baseline |
| Current API documentation | `repo://docs/llm-chats-api.md` | Public behavior and current contradictions to repair |
| Current testing policy | `repo://docs/testing.md` | Authoritative lane solutions, discovery discipline, and final stable gate |
| Current CI graph | `repo://.github/workflows/ci.yml` | Pinned sibling source and Windows/Linux/macOS closure evidence |
| Core product module | `repo://src/Modules/CanDoItAll.Modules.LlmChats` | Domain, application services, dispatcher, recovery, event pipeline, and options |
| Persistence module | `repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence` | EF/PostgreSQL, runtime adapters, event repository, transfer, and profile fencing |
| Web transport | `repo://src/App/CanDoItAll.Web/Api` | Routes, DTOs, mapping, authorization metadata, Problem Details, and SSE projection |
| Provider runtime | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime` | Shared streaming adapter and unsafe logging path |
| Provider drivers | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Providers/Drivers` | Raw provider error creation boundary |
| Composition | `repo://src/App/CanDoItAll.Composition` | Hosted dispatcher and scoped/singleton lifetime ownership |
| Migrations | `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql` | Schema, snapshot, and pending-model authority |
| Unit tests | `repo://tests/Unit/CanDoItAll.Tests.Unit` | Deterministic application, concurrency, options, and adapter proof |
| Integration tests | `repo://tests/Integration/CanDoItAll.Tests.Integration` | Real Web host, PostgreSQL, migration, transfer, and SSE proof |
| Test entry points | `repo://tests/Solutions` | Current Unit, Integration, and Stable `.slnx` lanes |

## Repository Baseline

- Analyzed Git commit: `a8e3f87e9ac917357c13fae56ab5eb1f0659521d` on branch `simple-chats`.
- The product/test worktree was clean before creation of this bundle.
- The WIP candidate was `dea90cfd...`; nine later commits include source-graph, DI/runtime-lease, SSE, and test-topology changes that invalidate its downstream proof.
- CodeAnalytics snapshot `snap-20260815201127-356b279c` found nine scoped product projects, zero dependency cycles, zero diagnostics, and no open questions. It is analysis evidence, not a reusable final proof artifact.

## Source Precedence

1. Current tracked product source, tests, `docs/testing.md`, and CI at the execution commit.
2. This successor bundle's locked requirements and decisions.
3. WIP specifications when they do not contradict current source or this successor.
4. Historical predecessor status/proof only as supporting evidence; it cannot close a successor gate.
