# Requirement Traceability

This map is a preparation artifact. H01–H16 are planned invariant groups in
[validation strategy](../plan/02-validation-strategy.md), not passing tests. Each phase
README names exact existing homes and proposed behavioral cases.

## Raw Input To Design And Execution

| Raw note | Requirement | Design owner | Execution owner / proof | Current product status |
|---|---|---|---|---|
| N001 shared use Unpriced | R001 | [Pricing contract](../architecture/10-pricing-and-capture-contract.md) | SB02/SB04/SB08; H02/H05/H06; buffered/SSE price and explicit unknown. | Not solved; planned. |
| N002 client/API key attribution, IDM later | R002/R013 | [Caller and access](../architecture/09-search-security-contract.md) | SB01/SB04/SB06/SB08; H07/H12; same subject/two keys, legacy/forged/denied. | Not solved; EGCP person mapping intentionally deferred. |
| N003 provider tab beside Sharing | R003 | [UI/form design](../architecture/08-ui-search-analysis.md) | SB07/SB08; H14/H15; scoped tab, Search/Enter zeroSave. | Not solved; planned. |
| N004 explicit request/range/filter, no initial load | R004 | [Query contract](../architecture/09-search-security-contract.md) | SB06/SB07/SB08; H11/H14–H16; spy/network/SQL proof, draft versus applied. | Not solved; planned. |
| N005 Agents all-provider tab | R005 | [Target solution](../architecture/01-target-solution.md) | SB06/SB07/SB08; H11/H12/H14/H15; one pipeline/current instance/profile. | Not solved; planned. |
| N006 existing histories must not double | R006 | [Source lifecycle](../architecture/05-history-data-lifecycle.md) | SB03–SB06/SB08; H03/H09/H10; actual source producers, one attempt/multiple owners. | Not solved; planned. |
| N007 provider/model match plus untracked capture | R007 | [Actual capture matrix](../architecture/10-pricing-and-capture-contract.md) | SB01/SB04/SB05/SB08; H01/H05–H10; distinct attempts and all identified paths. | Not solved; planned. |
| N008 general history retention settings | R008 | [Retention](../architecture/05-history-data-lifecycle.md) | SB03/SB05/SB07/SB08; H03/H04/H10/H13; owner lifetime, quotas, explicit Apply. | Not solved; planned. |
| N009 Light versus Detailed | R009 | [Detail and policy](../architecture/09-search-security-contract.md) | SB03/SB04/SB06/SB07; H03/H12–H15; no Light snippets, protected bounded opt-in. | Not solved; planned. |
| N010 conversation duplication risk | R010 | [Current-turn ownership](../architecture/03-csharp-pattern-selection-records.md) | SB03–SB05/SB08; H05/H09/H10/H13/H16; canonical link, once-per-operation input. | Not solved; planned. |
| N011 reuse prepared parts, avoid bad architecture | R011/R014 | [Inventory](../architecture/00-csharp-current-state-inventory.md), [dependencies](../architecture/02-csharp-dependency-direction.md) | SB01–SB09; H01–H16; graph/size/constructor, durability and measured bounds. | Not solved as product change; analysis/design supplied. |
| N012 named skills, detailed design, bundle only now | R012 | [Current analysis](../analysis/01-current-state.md), [performance scan](../architecture/07-history-performance-analysis.md) | Preparation gate and SB09 audit; source inventories, independent review, canonical validator, docs-only diff. | Preparation solved; [validation passed](../reviews/02-preparation-validation.md). No implementation performed. |

## Cross-Cutting Requirements

| Requirement | Owner phases | Mandatory evidence |
|---|---|---|
| R013 authorization | SB01/SB06/SB07/SB08 | H07/H12/H14/H15: verified caller, independent grants, owner access, before-publish fence and denied-content UI. |
| R014 durable lifecycle/migration | SB03–SB06/SB08 | H03–H06/H09/H10/H16: same-context EF, file first-create/update/delete journal, crash/replay, profile/retention and additive migration. |

## Phase To Input Reverse Map

| Phase | Inputs and responsibility |
|---|---|
| SB01 | N002, N006–N012: identity/ownership/dependency and actual-path contract. |
| SB02 | N001, N007, N011: shared price evidence, arithmetic and compatibility. |
| SB03 | N006, N008–N011: storage, detail bounds, policy, retention/profile primitives. |
| SB04 | N001, N002, N006, N007, N009–N011: actual invocation capture and caller. |
| SB05 | N006–N011: canonical reuse, first/later source commits, backfill/delete/late owner. |
| SB06 | N002–N009, N011: bounded query, content/policy operations and authorization. |
| SB07 | N001–N012: two lazy surfaces, visible provenance/caller/detail and separate Settings editor. |
| SB08 | N001–N011: actual composed runtime, UI, lifecycle and measured performance proof. |
| SB09 | N001–N012: final semantic/evidence audit; no extra implementation. |

## Closure Rule

Do not mark a raw note Solved from this map or an old related bundle. Each requires
actual shipped behavior, production source path, discovered/executed test, adversarial
case and applicable runtime/SQL/UI evidence. N012 alone closes during this preparation
after validation. Future authorized execution must keep that original scope distinction.

Exact test names may be consolidated when phase READMEs and H rows describe the same
invariant: implement one well-placed case per distinct behavior, update both references,
and prove actual discovery. This is not permission to drop a negative case or replace it
with a configuration assertion. Existing test names remain source-verified anchors.

## Explicit Deferred Scope

Exact-person IDM/EGCP mapping, global remote-log federation/cost deduplication, full-text
body search, export, exact-wire replay, prompt content-addressing, sibling RAG coverage,
mobile/shared component redesign and historic repricing without original evidence. These
are acknowledged limits, not product defects claimed fixed by this bundle.
