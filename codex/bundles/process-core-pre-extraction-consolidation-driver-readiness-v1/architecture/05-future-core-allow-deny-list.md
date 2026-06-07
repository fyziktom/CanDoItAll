# Future Core Allow/Deny List

## Allowed Future Core Candidates

These candidates are allowed only for a later proposal and only when tests prove they have no application, infrastructure, compatibility, driver, or UI dependencies:

| Area | Allowed symbols or families | Reason |
| --- | --- | --- |
| Route eligibility | `ProcessDispatchRouteEligibility` | Deterministic status predicates. |
| Route stage descriptors | `ProcessDispatchRoutePipeline` stage order data | Immutable route ordering. |
| Route snapshots | Source-payload-free route DTOs | Read-model candidate after adapter removal is complete. |
| Finalizer intent | Finalizer intent records only | Intent data without finalizer execution. |
| Pre-execution facts | Database requirement decisions and materialization fingerprints/request builders | Pure decision and fingerprint logic. |
| Subprocess rules | Lifecycle mapping and artifact source matching | Deterministic rule behavior. |
| Artifact snapshots and matchers | `ProcessArtifactExpectationSnapshot`, matcher, resolver, text/path/provider-native rules | Pure snapshot matching and validation facts. |

## Denied From Future Core

These dependencies must stay in the application/module layer unless a later architecture decision explicitly changes the boundary:

| Denied dependency | Reason |
| --- | --- |
| EF contexts and queries | Persistence side effects and data access belong to infrastructure/application services. |
| Claim lifecycle, leases, heartbeats, and lost-claim handling | Runtime coordination and concurrency control. |
| Transition execution and `TransitionStepWithClaimAsync` | Writes process state and depends on claims. |
| AgentFramework execution, provider repair, retry, and no-progress journaling | Runtime behavior with provider and journal side effects. |
| Storage drivers, workspace file IO, content readers, and projection writes | Infrastructure side effects. |
| Route model adapters and dispatcher nested aliases | Compatibility boundary, not a future Core contract. |
| Finalizer application and finalizer adapter conversion | Applies behavior and legacy dispatcher compatibility. |
| Driver registries, driver packs, helper-driver runtime APIs, and DI hooks | Out of scope for this bundle. |
| UI, Razor, CSS, JS, TS, images, screenshots, and mobile proof | Out of scope for runtime/service refactor. |

## Guard Expectations

The active architecture guard must fail if this bundle creates `CanDoItAll.Processes.Core`, `CanDoItAll.Modules.Processes.Core`, production process-driver APIs, UI/media proof, or a docs map that contains production interface or DI registration examples.
