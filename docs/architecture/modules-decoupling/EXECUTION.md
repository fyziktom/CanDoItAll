# Module Decoupling Execution

## Active authorization

Execution mode: **LIGHTWEIGHT_INCREMENTAL_E2E**.

The operator's attached task, accepted on 2026-09-10, authorizes the complete existing-product module-boundary refactor in coherent, verified slices. This replaces the Foundation's procedural requirement for a separately approved implementation request for each slice, including the older restrictions in `START_HERE_FOR_ASTRA.md` and chapter 12. Legacy bundle/subbundle generation is waived. No new bundle framework, manifests, or per-phase proof packets are required.

The architectural reference remains [CanDoItAll Architecture Foundation](../../../codex/bundles/CanDoItAll_Architecture_Foundation/). Its ownership, product preservation, security, compatibility, and evidence requirements remain binding, together with repository engineering rules, truthful validation, portability checks, and platform permissions. Keep discoveries and justified refinements in these task records without rewriting the reference snapshot.

Work only on **modules-decoupling**. No merge, rebase, squash, history rewrite, pull, other-branch implementation, pull request, or auto-merge is authorized. Keep signed checkpoints local; pushing requires separate authorization and must target this branch. Preserve the accepted Agents UI checkpoint and existing product capabilities. Sibling repositories are read-only source dependencies. Preserve user data, secrets, live hosts, and unrelated work.

All task artifacts and reports are in English. Preserve product localization and compatible identifiers. Maintain only this record, `COVERAGE.md`, and `VALIDATION.md` as task coordination records; update maintained product documentation when implementation changes its claims.

## Starting state

| Item | Observed state |
| --- | --- |
| Repository | Current repository root (`fyziktom/CanDoItAll`) |
| Starting branch | `modules-decoupling` |
| Starting HEAD | `3da508996e46e68d811f5ffb6926df8b876878cf` |
| Working tree before task | Clean; no staged or unstaged user changes |
| Instructions inspected | Root `AGENTS.md`, `.github/copilot-instructions.md`, current testing guidance and CI workflow, operator task |
| Initial implementation status | Production code unchanged; focused baseline discovery in progress |
| Initial validation status | Signing reuse, isolated Release product build, documentation and portability-static PASS; focused runtime baseline pending |

## Current checkpoint and next action

Mode is durable. Two disposable signing operations from separate shell invocations passed with the expected identity; the second reused the agent without pinentry. Complete and verify the initial signed documentation checkpoint, finish the isolated build/test baseline, then implement the first owner boundary. Do not treat this planning checkpoint as completion.

Resume by reading this file, checking branch/HEAD/dirty files, and reconciling committed work and tested revisions with `COVERAGE.md` and `VALIDATION.md`.

## Decisions and blockers

- The mode note is task-scoped; it does not change application dependencies, global Codex instructions, or shared skills.
- Git uses `C:\Program Files\Git\usr\bin\gpg.exe` (GnuPG 2.4.9), matching `gpgconf`/agent, and home `%USERPROFILE%\.gnupg` (resolved and checked through that installation's `cygpath`). Expected signing fingerprint: `96E836FAA8854EE98ABC10903C206549E1D7EAD6`. No agent configuration file exists; retain defaults of 600 seconds idle / 7200 seconds absolute. No machine setting was changed, and no agent restart/kill is authorized for convenience. Further expiry can require trusted pinentry again.
- Existing application PID 1836 serves `http://localhost:5032` from the main checkout's Release output. It predates this task and is not owned by the current DotNetWatch backend. Preserve it. Its active PostgreSQL override profile is `e5df9ad6-33db-c697-4a06-78a74976013c`; no pending profile activation was observed. Baseline default Release copying encounters its locked DLLs; use isolated build output/fixtures rather than stop the host blindly.
- All inspected sibling worktrees were clean. Components matches CI's source commit; FileTools is clean at a newer detached commit than CI's pin. Keep the actual source graph stable (revisions in `VALIDATION.md`).
- Source-grounded findings: runtime `AppDbContext` scans registered mappings globally; separate Simple Chats, Memory and provider-history stores do not yet imply bounded EF models. Processes has its own context. Preserve complete design-time migrations and immutable per-host database profile binding.
- Existing HR CRM party/affiliation operations already call the owner. HR has 14 tool descriptors and Scheduler has three workflow-only descriptors, with managed identity and InteractiveChat guards. HR Simple Chat definition administration is absent and is required new integration.
- Structure workflow status reads write projections, and result detection uses a project-wide new-node set difference. Structure replay currently uses process-local locks and metadata keys without an atomic durable receipt/fingerprint. Process launch UI links after admission using mutable page scope. These are required investigation/repair areas, not runtime test results.
- Projects deletion/participation contracts expose `AppDbContext`. Replacing those seams must preserve their shared transaction and cleanup outcomes. Plugin and Scheduler bootstrap still issue schema DDL after migrations; CRM bootstrap is lookup seeding and must be retained.

## Ordered milestones

| Order | Outcome | State |
| --- | --- | --- |
| 0 | Signed execution checkpoint, current owner/caller map, isolated build/test/provider baseline | In progress |
| 1 | Bounded runtime persistence through a small complete owner operation; explicit model membership, unchanged complete schema/migrations and stamping; extend to remaining owners according to dependencies | Not started; Collaboration is the smallest confirmed candidate |
| 2 | Agents/Providers technical/pricing authority and CRM projections/enrichment; Work Management assignments; owner contracts replacing foreign EF and hidden writers | Not started |
| 3 | Projects/Structure/Resources/Storage lifecycle, shared transaction coordination, imports/exports, single migration authority and restart | Not started |
| 4 | Durable Structure contributions and execution origin/result recovery for Workflow/Process; pure queries, separate projection delivery, trusted scope and replay receipts | Not started |
| 5 | Required HR Simple Chat definition adapter with owner-atomic create receipt; preserved curators/Scheduler and permitted direct CRM/Resource automation | Not started |
| 6 | Remaining module/runtime contract reconciliation, cumulative architect/QA review, final frozen product/stable/browser/live/PostgreSQL/static gates and journeys A-H | Not started |

This order is adaptive: finish one coherent production path and its proof before expanding. Future estimates in the Foundation are not automatic implementation scope. The complete required scope remains open until coverage and mandatory journeys are closed.

## Imminent slice: Collaboration runtime ownership

`CollaborationService` currently depends on the global context although all four Collaboration records and their callers are owner-local. Introduce one explicit `CollaborationDbContext`, apply the same four mapping configurations, and register its per-operation factory against the immutable `ICanonicalRuntimeDatabase.Profile`. Route all service reads/writes through it and remove the unused public context-accepting command overloads. Keep notifications, activity mirroring, routes, IDs, and stored data shapes unchanged.

Share only the stateless GUID stamping algorithm with `AppDbContext`; do not create a generic foreign repository or new context inheritance hierarchy. Existing complete migration composition continues to apply the same configurations. No schema migration or data copy is intended. The transfer-residue participant remains an explicit maintenance read under the current complete-schema target lock until the coordinated transfer slice replaces that shared contract; it is not a business writer.

Acceptance: actual owner model excludes every foreign entity, mappings match the complete model, synchronous/asynchronous saves retain GUID stamping, canonical legacy records survive restart/readback, profile factories stay isolated, and existing Collaboration integration plus shell-badge behavior remains. Do not add optimistic-concurrency enforcement that the current Collaboration mappings do not have as an incidental refactor. Shared stamping/composition changes are a named broad-gate invalidation trigger, with focused proof first.
