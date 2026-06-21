# Post-Bundle Process Data Performance Hardening Semantic Invariants

## Scope

This is a post-bundle hardening addendum for the current Process implementation. It does not reopen SB20-SB29; it records a targeted EF Core, projection, and .NET performance repair pass requested after those bundle phases were closed.

## Invariants

1. Runtime event global ordering is owned by the persistence model's generated `GlobalSequence` key, not by application-side `MAX(GlobalSequence) + 1` allocation.
2. Runtime event root ordering remains explicit per root run and is allocated once per root per append batch before persistence.
3. Project-structure process projection reads only the process-link and process-run fields it needs; it does not hydrate full process plan payloads for surface nodes.
4. Launch-variable lookup narrows candidates by canonical JSON key/value snippets and still performs an in-memory exact key/value verification before returning assignments.
5. Launch-variable serialization and lookup share the same canonical key trim, value trim, ordinal ordering, and ordinal comparer behavior.
6. Blazor reload code awaits completed tasks directly instead of reading `Task.Result`.
7. Static regexes in the dotnet solution setup launch-variable contributor use generated regex methods instead of runtime compiled regex allocation.

## Production Behavior Artifact Matrix

| Behavior | Producer | Consumer | Lifecycle | Negative or Guard Proof |
| --- | --- | --- | --- | --- |
| Runtime event global sequence | `ProcessRuntimeEventEntityConfiguration` generated key mapping | `EfProcessRuntimeEventStore.ReadAfterGlobalSequenceAsync`, projection replay workers | Event rows are inserted with database/EF-generated global sequence values and then replayed in sequence order | `Runtime_event_store_assigns_contiguous_sequences_within_append_batch` verifies generated sequence ordering for batch appends |
| Runtime event root sequence | `EfProcessRuntimeUnitOfWork` and `EfProcessRuntimeEventStore` append paths | `ReadByRootRunAsync` and root-scoped projections | Root sequence starts after the current root max and increments in memory for additional events in the same append batch | `Runtime_event_store_assigns_contiguous_sequences_within_append_batch` verifies root sequence continuity |
| Assignment launch-variable lookup | `EfProcessRuntimeStepAssignmentStore.SerializeLaunchVariables` | Subprocess reuse lookup and process dispatch metadata lookup | Assignment variables are normalized to canonical JSON, prefiltered by key/value snippets, deserialized, and exact-matched in memory | `Runtime_step_assignment_store_finds_launch_variables_by_key_value_pairs` verifies a decoy row with the same value under another key is rejected |
| Project-structure process projection | `ProjectStructureProcessProjectionContributor` | Workbench project-structure surface | User-authored process links, runtime state summaries, plan ids, and definition ids are projected without tracking or full plan payload hydration | Focused integration class ran 25/26 passing; remaining failure is a prompt-template assertion unrelated to projection data shape |

## Residual Risk

Root-local sequence allocation still uses a read of the current root max and a unique constraint on `(RootRunId, RootSequence)`. Parallel writers for the same root run should either be serialized at the runtime command layer or repaired with a root-scoped database allocator if real concurrent same-root writes become expected.
