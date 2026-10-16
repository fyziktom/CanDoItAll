# 02 · Current state, provenance and audit coverage

## Baselines and scope

Evidence date: 28 September 2026. Repository: `fyziktom/CanDoItAll`. The reviewed development snapshot is `7db3543ab437376baeca55089cb331fbe1b30483`; selected component-decoupling guidance is at `e55a780b36e75db39a05b7400408d8267d0f32d5`. Compare metadata places the latter three commits ahead with the development commit as its merge base. This does not establish that all changed source files were reviewed.

[The ledger](20-source-ledger.md) records exact file paths, commit and blob identities where available, read extent and evidence type. Code observations, repository guidance, historical records and proposals are distinguished. The JSON [source registry](../registers/sources.json) is the refresh input.

## Verified selected findings

| ID | Observation | Consequence | Evidence |
|---|---|---|---|
| F01 | Project editor has expected lifetime but no content stamp; null lifetime skips its check | A stale complete editor can overwrite newer content in the same lifetime | SRC-03 |
| F02 | Missing phases/options are deleted and replacement matching uses repeated list searches | Whole-snapshot tools are semantically broad; large collections also warrant cost profiling | SRC-03 |
| F03 | Agent project save uses the same owner under separate source authority and lease | Fix the shared owner and the tool's original-read contract, not just HTTP | SRC-06, SRC-08 |
| F04 | GetAsync builds the editor using separate project/phase/option queries | A new token must describe a coherent returned snapshot | SRC-03 |
| F05 | Source save commits and explicitly disposes its scope before another search transaction | Preserve this release; close content ordering/durability, not an imaginary held-save-lock bug | SRC-03 |
| F06 | SerializableMutationScope uses 64 process stripes on its relational path | Unrelated keys can serialize locally; more checked keys can increase interference | SRC-04 |
| F07 | PostgreSQL advisory scope keys are acquired using one awaited statement each | Check fan-out can add sequential roundtrips | SRC-04 |
| F08 | Authorized Projects source path has its own transaction and source-first sequence | A single generic replacement scope can regress authority ordering or deadlock behavior | SRC-06 |
| F09 | Task details already carry domain expected/proposed state and assignment revision | Preserve the stronger existing domain checks | SRC-09 |
| F10 | UI seam guidance already defines host draft ownership and submitted/live reconciliation | New editor behavior must extend rather than duplicate that policy | SRC-02 |
| F11 | Bulk lifetime and enlisted reads already exist | Avoid new per-row admission calls and a second cross-owner query framework | SRC-10, SRC-15 |
| F12 | Project hierarchy and agent structure reads can load global/full surfaces before narrowing | These are unsuitable as cheap mutation precondition queries | SRC-03, SRC-08 |
| F13 | Runtime creation receipts/reservations are not durable retry identities | Safe retry needs an explicit owner commit-result design | SRC-15 |
| F14 | Collaboration extraction declares no new concurrency/idempotency protocol | Rendering decoupling does not close owner-level concurrency | SRC-13 |
| F15 | Transaction participation shares a physical connection/transaction with lifetime guards | Reuse explicit enlistment, but do not parallelize commands over that connection | SRC-05 |
| F16 | Shared GUID stamping touches modified tracked entities only | A child-only mutation must explicitly participate in root revision rules | SRC-07 |

These are design findings and derived risks, not claims of incidents observed in production. In particular F06/F07 do not prove a measured bottleneck; F04 does not establish that a mixed snapshot was reproduced; F05 does not prove a specific search incident.

## Existing guarantees to preserve

Project lifetime binds canonical profile, project ID and incarnation, with retirements and reservations retained across deletion and history transfer. Source authority is acquired before an owner transaction and rechecked before commit. Import creates new target lifetimes and retained imported history does not become live execution authority. The complete schema context remains for migration/transfer/health rather than ordinary module writes. These rules are documented in SRC-15 and selected authority code in SRC-06.

Other owners already have important protection: task edit-state validation is inspected directly (SRC-09); Simple Chats, storage operation uncertainty, process prepared launches and tool journals are described by the module map and historical coverage (SRC-11/SRC-14). The latter are **reuse candidates requiring a current code read**, not this package's new verification of every implementation.

## Coverage debt: required before implementation

The full Projects update/delete/create/import/maintenance caller graph, exact HTTP endpoint files, complete tool request schema, ProjectsDbContext stamping sites, complete migration model, all renderer clone/mapping paths and owner-specific CI tests are not exhaustively mapped here. Nor are all writes in CRM, Prompts, Resources, Memory, Plugins, Security, Scheduler, Collaboration or process internals.

P00 must close this gap for the chosen pilot. The [mutation inventory](../registers/mutation_inventory.json) includes unknowns explicitly. A logical owner row is not a claim that all its methods are listed. Default-branch search was used only to discover some paths; baseline claims come from ref-specific reads.

## Corrections and qualifications to the earlier discussion

1. Project Save explicitly disposes its source mutation scope before postcommit work. It must not be blamed for holding those same process locks through its search callback. Some hierarchy methods keep their `await using` lifetime through postcommit activity; profile their actual branch and lifetime separately.
2. Not every Projects writer goes through the 64-stripe relational path: the authorized PostgreSQL source branch is different.
3. A same-transaction revision read alone does not establish that a source remains unchanged until publication. Required exclusion or serialization semantics must be designed for that particular invariant.
4. A fresh read is not proof of agent reconsideration. Runtime can bind inputs and a proposal, but cannot prove a model understood them.
5. Existing GUID, numeric, expected-field and storage-content tokens need compatible semantics, not a mandatory universal encoding.

## Evidence refresh rules

An unchanged file blob preserves a local observation only. Changed callers, composition, schemas, provider behavior or test fixtures can invalidate its architectural conclusion. A move with the identical blob is a relocation candidate, not automatic proof of ownership. A modified blob requires rereading. Missing files require symbol/owner discovery. Tests referenced by historical names require current discovery counts and execution before any passing status is entered.

The package's own validation checks JSON references, links and integrity. They do not verify the application. Every later review records the resolved current branch, actual working-tree state, sibling revisions, exact source scope and anything left unverified.
