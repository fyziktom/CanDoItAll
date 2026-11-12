# 04 · Target architecture and dependency direction

## Preserve the owner model

SRC-11 describes a modular monolith with authoritative owner contexts and typed integration contracts. This plan strengthens mutation semantics inside that architecture. HTTP, an agent tool and an in-process page are adapters over the same owner operation; none gets a special weaker write path.

Do not route every Blazor action through loopback HTTP for consistency. Conversely, preserve existing Process/Project Structure control-plane routes where they are already the product contract (SRC-16). "No mandatory HTTP rewrite" does not authorize bypassing their established governance boundary.

### Dependency sketch

```text
HTTP endpoint       first-party tool adapter       routed UI host/session
     |                        |                            |
     +------------------------+----------------------------+
                              |
                  owner application operation
               /              |                        owner contracts   owner DbContext    typed owner ports
                              |                 |
                  persistence mechanics    composition adapter
                              |                 |
                 canonical PostgreSQL    another fact owner

feature renderer ---> feature contracts / presentation types
       ^                         (no DbContext, no MAF, no Web)
       |
production host OR backend-free scenario host
```

The arrows represent allowed dependencies, not a requirement to add a new interface or assembly at every arrow. Existing concrete owner services remain legitimate application boundaries. A new port is justified by ownership, substitution or a real integration seam, not by a quota.

## Responsibility allocation

| Layer | Owns | Must not own |
|---|---|---|
| Shared data-only vocabulary | Optional common outcome category, operation correlation primitives | Universal entity mutation, reflection-based change sets, module lookup |
| Fact owner's contracts | Commands, expected stamp, accepted result, safe conflict detail, batch read requests | DbContext, physical transaction, host session, arbitrary dynamic properties |
| Fact owner's application service | Domain validation, command semantics, outcome at commit, atomic boundary | Rendering, caller-specific bypass, unbounded global discovery |
| Persistence infrastructure | Enlistment, physical connection, transaction mechanics, explicit gates and diagnostics | Which project fields invalidate planning or whether an agent may override a human |
| Integration adapter | Translates an intentional cross-owner dependency using owned ports | Writing another module's tables or exporting its persistence model |
| Agent/runtime adapter | Binds invocation, source authority, proposal and basis to the owner's command | Inventing missing expected state or interpreting conflict as permission to overwrite |
| UI host/session | Draft, submission, request generations, read-back, conflict workflow | Durable truth, database tokens obtained after a stale submission |
| Renderer | Controls, events, presentation, field-level conflict display | Persistence, server-side authorization, circuit-wide mutable editing state |
| Composition | Registers intentional adapters and canonical persistence binding | Business rules or second fallback masters |
| Maintenance authority | Canonical schema, controlled restore/import/upgrade | Public general-purpose bypass for ordinary writers |

## Shared abstractions: deliberately limited

Initially reuse the existing concurrency-token utility and owner outcome families. An owner may expose a typed `ProjectEditStamp` without creating a global `IMutableEntity<T>` hierarchy. If two owners later need identical transport-independent conflict categories, extract only that vocabulary. Preserve module-specific validation and accepted-result details.

The new Projects operation ledger, if required for the pilot retry contract, belongs to Projects persistence and the canonical complete schema model. Shared storage helpers can be used, but there is no central "MutationDbContext" that writes everybody's aggregates. Existing tool journals link to the Projects operation ID and accepted receipt; they do not become a second authority for whether the Projects transaction committed.

A cross-owner validation port returns bounded typed witnesses and participates explicitly in an existing transaction where strong publication semantics require it. The host composes concrete adapters. Do not introduce an `IServiceProvider`-backed registry of arbitrary entity types as a dependency shortcut.

## Contracts during component extraction

Changing the assembly that contains an editor model does not change who owns the data. A renderer can consume an opaque edit stamp or a presentation-level conflict state, but never an EF concurrency exception or database transaction handle. If the current Projects contracts assembly needs the shared editor DTO after extraction, move the data-only contract while retaining the intended namespace/serialization compatibility. Verify evaluated references rather than guessing from folder names.

Do not make Projects UI depend on `CanDoItAll.Modules.CrmHr` just to reuse `CrmHrDraftReconciler`. Reuse the semantic algorithm first. Extract a genuinely feature-neutral utility only if multiple concrete owners justify it and the public signature remains free of domain types. A local implementation can be preferable to a generic framework with many policy callbacks.

## Integration reach

For the Projects pilot, the perimeter includes Projects, Workbench's agent adapter, the actual HTTP boundary, UI hosts/renderers or future extracted contracts, search and canonical migrations/transfer. It does not require importing the whole Process runtime into Projects. Existing source authority ports continue to express those relationships.

For later agent basis checks, start with Projects plus Workbench only. Do not immediately couple every mutation to CRM rates, Memory, Plugins, ProviderManagement and Workflow. Add a dependency only when the particular intention is materially based on that fact; determine whether it is immutable, current-at-admission or current-at-publication.

## Architectural rejection tests

Reject a new AppComponents → concrete-module edge; renderer → persistence/MAF/Web dependency; owner → another owner's DbContext; ordinary injection of the complete AppDbContext; hidden service-provider routing; concurrent commands using the enlisted shared connection; static mutable per-user drafts; or a parallel outcome service that can disagree with the authoritative owner.

The current module and UI guidance already identifies these boundaries (SRC-02, SRC-11, SRC-16). P00 discovers their actual compiled/evaluated test guards and extends those guards only for the new seam, rather than writing brittle assertions about file counts or naming patterns.
