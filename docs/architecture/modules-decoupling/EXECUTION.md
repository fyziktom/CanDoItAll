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

Signed checkpoints: authorization `49ef77a3dcc4bc5b4d39126e7770e4706553098d`, Collaboration `d96ef27796cf7648cb42cfd42c2b832c0b432180`, Memory `f9908080ebe38f342ab4d1799fc7cef8481e3ce8`, catalog owners/migration authority `c0950854f44c674aafd474c64532fe08fe2801b3`, History/SimpleChats coordination `52c477f51591018762b3d0a12c68657f1379a733`, and Security/Providers/Search/Storage `0cb2d519edd549afcf79a61c0db758871fba74db`, all verified with the expected identity. The frozen Collaboration stable gate passed 10827 cases with no failures or skips; seven runtime-expanded theories explain the 55 additional rows over discovery. That gate predates subsequent owners and is not final-refactor proof.

This checkpoint adds owner-atomic Simple Chat definition creation receipts, a canonical complete-model migration and receipt-preserving transfer. Applied-source product build, 32 unit and 56 integration cases passed. Static enforcement passed after reviewing four generated metadata findings. Downgrade refuses to remove retained receipts; replay preserves the original identity and intervening human edits. Complete implementation and journeys A-H remain open.

Next integrate the Projects/Workbench assignment, deletion owner and durable revocation-claim cutover, then project lifetime admission and transfer. The combined candidate passed 51 unit, 68 integration and 25 component cases. Durable HR tool admission, Workflow/Structure delivery and Storage reconciliation continue in separate ignored drafts. The receipt prerequisite is now applied, but the HR adapter is not registered until its durable runtime admission/recovery path is complete. Continue automatically after each checkpoint.

Resume by reading this file, checking branch/HEAD/dirty files, and reconciling committed work and tested revisions with `COVERAGE.md` and `VALIDATION.md`.

## Decisions and blockers

- The mode note is task-scoped; it does not change application dependencies, global Codex instructions, or shared skills.
- Git uses `C:\Program Files\Git\usr\bin\gpg.exe` (GnuPG 2.4.9), matching `gpgconf`/agent, and home `%USERPROFILE%\.gnupg` (resolved and checked through that installation's `cygpath`). Expected signing fingerprint: `96E836FAA8854EE98ABC10903C206549E1D7EAD6`. Initial policy had no configuration file and used defaults of 600 seconds idle / 7200 seconds absolute. It expired before the first real commit. After explaining the change, apply the operator-authorized finite task target: 14400 idle / 28800 absolute seconds. `gpgconf --check-options` and `--reload gpg-agent` succeeded; no agent was killed/restarted. Trusted pinentry then unlocked the first signed checkpoint.
- **Restore at closure:** the task created `%USERPROFILE%\.gnupg\gpg-agent.conf` from an absent file. SHA-256 is `89E963D5696BE60087BFA127381DECACB857FB470CC5A2DCAF7A03252B6DDC5E`. It contains only the task comment and `default-cache-ttl 14400` / `max-cache-ttl 28800`. Remove only if the hash still matches, then reload the same agent; preserve any concurrent user edits and report them. Cache expiry/hardware policy can still require another unlock. Do not leave the temporary policy unreported.
- Existing application PID 1836 serves `http://localhost:5032` from the main checkout's Release output. It predates this task and is not owned by the current DotNetWatch backend. Preserve it. Its active PostgreSQL override profile is `e5df9ad6-33db-c697-4a06-78a74976013c`; no pending profile activation was observed. Baseline default Release copying encounters its locked DLLs; use isolated build output/fixtures rather than stop the host blindly.
- All inspected sibling worktrees were clean. Components matches CI's source commit; FileTools is clean at a newer detached commit than CI's pin. Keep the actual source graph stable (revisions in `VALIDATION.md`).
- Source-grounded findings: runtime `AppDbContext` scans registered mappings globally; separate Simple Chats, Memory and provider-history stores do not yet imply bounded EF models. Processes has its own context. Preserve complete design-time migrations and immutable per-host database profile binding.
- Existing HR CRM party/affiliation operations already call the owner. HR has 14 tool descriptors and Scheduler has three workflow-only descriptors, with managed identity and InteractiveChat guards. HR Simple Chat definition administration is absent and is required new integration.
- Structure workflow status reads write projections, and result detection uses a project-wide new-node set difference. Structure replay currently uses process-local locks and metadata keys without an atomic durable receipt/fingerprint. Process launch UI links after admission using mutable page scope. These are required investigation/repair areas, not runtime test results.
- Projects deletion/participation contracts expose `AppDbContext`. Replacing those seams must preserve their shared transaction and cleanup outcomes. Plugin and Scheduler bootstrap no longer issue separate schema DDL after migrations; CRM lookup seeding is retained.

## Ordered milestones

| Order | Outcome | State |
| --- | --- | --- |
| 0 | Signed execution checkpoint, current owner/caller map, isolated build/test/provider baseline | In progress |
| 1 | Bounded runtime persistence through a small complete owner operation; explicit model membership, unchanged complete schema/migrations and stamping; extend to remaining owners according to dependencies | Collaboration, Memory, TestLab, Resources, Plugins, History, SimpleChats, Security, Providers, Search and Storage contexts implemented with focused proof; Collaboration intermediate broad gate passed; remaining owners and foreign access remain open |
| 2 | Agents/Providers technical/pricing authority and CRM projections/enrichment; Work Management assignments; owner contracts replacing foreign EF and hidden writers | Not started |
| 3 | Projects/Structure/Resources/Storage lifecycle, shared transaction coordination, imports/exports, single migration authority and restart | Explicit connection/transaction coordination and History/SimpleChats maintenance cutover implemented. Canonical migration authority and focused fresh/populated bootstrap proof passed; Projects lifecycle remains in preparation |
| 4 | Durable Structure contributions and execution origin/result recovery for Workflow/Process; pure queries, separate projection delivery, trusted scope and replay receipts | Not started |
| 5 | Required HR Simple Chat definition adapter with owner-atomic create receipt; preserved curators/Scheduler and permitted direct CRM/Resource automation | Owner receipt, canonical migration and transfer implemented with focused proof; HR adapter and durable runtime admission/recovery remain in preparation |
| 6 | Remaining module/runtime contract reconciliation, cumulative architect/QA review, final frozen product/stable/browser/live/PostgreSQL/static gates and journeys A-H | Not started |

This order is adaptive: finish one coherent production path and its proof before expanding. Future estimates in the Foundation are not automatic implementation scope. The complete required scope remains open until coverage and mandatory journeys are closed.

## Implemented ownership decisions

`CollaborationService` uses the explicit four-entity `CollaborationDbContext` and a per-operation factory bound to immutable `ICanonicalRuntimeDatabase.Profile`. Its unused public context-accepting command overloads are removed. Notifications, activity mirroring, routes, IDs and stored shapes are preserved.

Share only the stateless GUID stamping algorithm with `AppDbContext`; do not create a generic foreign repository or new context inheritance hierarchy. Existing complete migration composition continues to apply the same configurations. No schema migration or data copy is intended. The transfer-residue participant remains an explicit maintenance read under the current complete-schema target lock until the coordinated transfer slice replaces that shared contract; it is not a business writer.

Acceptance: actual owner model excludes every foreign entity, mappings match the complete model, synchronous/asynchronous saves retain GUID stamping, canonical legacy records survive restart/readback, profile factories stay isolated, and existing Collaboration integration plus shell-badge behavior remains. Do not add optimistic-concurrency enforcement that the current Collaboration mappings do not have as an incidental refactor. Shared stamping/composition changes are a named broad-gate invalidation trigger, with focused proof first.

Memory now follows the same boundary with its seven existing mappings. All Memory ledger/retention/lease stores use `MemoryDbContext`; generic module registration no longer mutates the global model registry. Application composition supplies the pooled owner factory; standalone consumers register that typed factory explicitly. GUID profile stamping remains distinct from worker lease ownership tokens, and generic workers remain disabled by default. PostgreSQL mapping/readback/restart/retention/profile and lease proof plus the existing API/UI paths passed. The two lease providers share a process; this does not claim a simultaneous distributed race.

TestLab uses its four existing aggregate mappings, Resources its single metadata mapping, and Plugins its six installation/grant/connection/OAuth/log mappings. Canonical pooled factories preserve profile binding. Resources composes project references through a data contract instead of querying Projects entities; existing catalog limits and missing-project behavior remain explicit. Workbench foreign reads and transfer maintenance are separate unfinished cutovers.

Migration cleanup removes the Plugin/Scheduler post-migration DDL while retaining CRM lookup seeding and canonical provider bootstrap. The complete design-time factory fails explicitly when its composition catalog cannot load; partial reflection discovery no longer silently omits mappings. Applied PostgreSQL proof covers exact fresh physical mappings, repeated bootstrap, and populated baseline upgrade/restart with historical extra indexes/defaults preserved. This removes opportunistic repair of manually corrupted, already-baselined schemas; it does not claim to repair such corruption. Static baseline review removes only the two allowances belonging to the deleted initializers.

History has an explicit eleven-entity model; SimpleChats has its nine existing entities. Public History staging contracts accept owner data, without a caller's context. Infrastructure coordination requires the actual active owner connection/transaction, checks canonical physical profile identity, creates fresh enlisted contexts, and rejects retired or mismatched scopes. Independent factories do not switch behavior based on ambient state. Simple Chats transcript/product writes retain their same scoped owner and append-only evidence rules; callbacks run after the owning commit. GUID stamping remains separate from numeric Simple Chats versions. The explicit InMemory test path checks store identity and makes no atomicity claim.

Shared relay retention uses an explicit read-only integration SQL join so both owners' expiry predicates and deadline ordering apply before the batch limit. History outbox, workflow usage/resume, file/agent history and chat maintenance participate in the original owner transaction. The transfer adapter uses an explicit target-profile History session; the global target-transfer orchestration is still an unfinished maintenance boundary.

Security maps its two existing entities without introducing token stamping. Provider Management maps its six entities and retains the complete schema's source-to-secret FK while omitting Security entities from the runtime model. Security existence reads use a bulk owner query for ordinary reads and explicit enlistment during mutations. Secret-reference policies no longer accept a caller context. Catalog reads use a fixed number of calls; mutation callbacks run after the transaction coordination frame is released. Technical catalog/CRM projection and remaining bootstrap/maintenance readers are subsequent boundaries.

Search owns its one index entity; Storage owns its catalog and routing entities. Normal services now use these contexts, preserving bootstrap/path migration and search behavior. Typed Storage planning facts carry host/path classification and parse only the referenced FTP configurations; they do not perform provider I/O. Structure reads Resource/TestLab projection facts through their owners, with explicit enlisted variants retaining the original mutation read set. Project deletion and Prompt search projection have not yet switched to the new staged operations.

Simple Chats now maps ten entities, including an immutable definition-create receipt keyed by producer, actor, history namespace and a server-owned intent. The same definition writer atomically reserves the intent, creates revision one and commits its receipt. Replays return that original identity without resolving the provider or overwriting later revisions; changed semantic input conflicts. Nested unit-of-work failure makes the transaction rollback-only even when its caller catches the error. No LLM call or approval occurs inside this transaction.

Migration `20260910163827_AddLlmChatDefinitionCreateReceipts` is generated from the canonical 149-entity model. The receipt-to-original-revision FK is deferred; the unique intent key remains immediate. The internal Simple Chats transfer document advances from version 8 to 9 and preserves receipt fingerprints and identities. Replacement refuses a target that already holds receipts. Downgrade is supported only while the receipt table is empty; once receipts exist, retain the schema or restore a reviewed pre-admission backup without replaying effects. The owner contract adds no tools, context or transcript access to ordinary Simple Chats.
