# C# architecture execution gate

Status: Pass for bounded implementation

## Current-state inventory

CodeAnalytics snapshot `snap-20260813011301-2e2ad9ad` loaded `CanDoItAll.Processes.Persistence`, `CanDoItAll.AgentFramework.Core`, and `CanDoItAll.Manager` with 774 types, 6,145 members, zero diagnostics, and no blocking load errors.

| Responsibility | Current owner | Evidence and risk | Current tests |
|---|---|---|---|
| Persisted process-plan serialization, hash version resolution, and migration state | `ProcessInstancePlanPersistenceMapper` | Static type; 432-line file; timestamp and recursive property-name detection are mixed into mapping | `ProcessPersistenceStoreTests`, `ProcessPlanMigrationIntegrationTests` |
| Process start, ownership attachment, identity establishment, and session cleanup | `LocalWorkspaceProcessHost` plus platform ownership-start types | Parameterless host; 996-line file and 31 source members; failed `Attach` escapes before the later cleanup path | `LocalWorkspaceProcessHostTests` |
| Manager process-registry serialization and recovery authorization | `FileManagerOwnedProcessRegistry` and `ManagerProcessCoordinator` | Registry has two construction dependencies; coordinator has four. The 840-line file lacks a schema-1 DTO and boundary validation | `ManagerProcessOwnershipTests` |

CodeAnalytics reports one pre-existing type cycle between `AgentReferenceDataCache` and its nested entry type. It is unrelated to these responsibilities and no touched code may add or alter a project/type cycle.

## Boundary ownership

- F01 remains in `CanDoItAll.Processes.Persistence`. A small top-level payload-shape classifier owns deterministic JSON-shape classification; the mapper continues to own deserialization, hash verification, and read results.
- F02 remains in `CanDoItAll.AgentFramework.Core`. `LocalWorkspaceProcessHost` owns the start transaction. Platform ownership-start types own aborting their partially established native boundary. A typed delegate is the narrow test seam; a single-implementation factory interface is not justified.
- F03 remains in `CanDoItAll.Manager`. The file registry owns schema adaptation and durable rewrite. Dedicated top-level legacy DTOs model schema 1; the coordinator receives only current safe records.
- No contract or project moves are required. No composition-root registration changes are planned.

## Dependency direction

The existing direction remains:

`CanDoItAll.Manager -> CanDoItAll.AgentFramework.Core`

`CanDoItAll.Processes.Persistence -> Processes abstractions/builder and EF infrastructure`

No `.csproj` edit or new project reference is allowed in F01-F03. The existing scoped snapshot is the before-state dependency proof; a refreshed snapshot and direct project-file review are required before architecture closure.

## Pattern decisions

### Structured classifier

- Force: runtime and migration tests need one deterministic payload-shape authority.
- Decision: cohesive top-level classifier in the persistence project.
- Rejected: growing the mapper's recursive string/property scan; it keeps classification implicit and untestable as a separate policy.

### Transactional start with delegate injection

- Force: all failures after `Process.Start` must converge on one total cleanup path and tests must deterministically fail attachment.
- Decision: keep orchestration in the host and inject the ownership-start preparation delegate through an internal constructor.
- Rejected: a new public factory interface with one trivial implementation.

### Explicit legacy DTO adapter

- Force: schema-1 JSON has a different shape and cannot be trusted as the current authorization model.
- Decision: deserialize schema 1 into dedicated DTOs and map to current fail-closed records before use.
- Rejected: nullable current contracts or permissive current-model deserialization, because both weaken the authorization boundary.

## Testability contract

- F01: direct positive V1/V2 cases, partial/conflicting negative cases, exact V1/V2 hash assertions, and real PostgreSQL upgrade/restart/idempotency proof.
- F02: injected `Attach` failure records the started PID, verifies `WorkspaceProcessStartException`, observes no surviving process, and proves abort/disposal; existing normal ownership tests remain green.
- F03: real schema-1 JSON without `Boundary` becomes `OwnershipUnverified`, is rewritten as the current schema, never increments fake-host termination count, and malformed current boundaries are rejected.
- C1: one clean package-mode Release build plus the named unit/integration slices proves composition remains intact.

## Partial-class policy

No new production partial class is permitted. Existing EF migration partials are generated-code conventions and may only be added through the repository migration workflow. New helper and DTO types must be top-level, not nested architecture boundaries.

## Closure checkpoint

Refresh CodeAnalytics after F03, confirm unchanged project references and no new cycle, inspect all changed C# files, verify independent negative tests, and run the C# architecture review gate before F04.

## Closure result

Status: Pass

- Refreshed snapshot: `snap-20260813015258-2fca2beb` (`CanDoItAll.Processes.Persistence`, `CanDoItAll.AgentFramework.Core`, `CanDoItAll.Manager`).
- Snapshot result: 3 projects, 783 types, 6,198 members, no blocking load errors. Four informational `DI0001` diagnostics describe pre-existing factory registrations in Manager `Program.cs` and do not identify a source error.
- Dependency direction remains `CanDoItAll.Manager -> CanDoItAll.AgentFramework.Core`; no `.csproj`, `.props`, or `.targets` file changed.
- The only cycle remains the pre-existing type-level relationship between `AgentReferenceDataCache` and its nested `AgentReferenceDataCacheEntry`; no touched type participates.
- F01 retains classification in persistence, F02 retains start ownership in core, and F03 retains schema adaptation plus durable rewrite in Manager.
- F03 current records use an explicit polymorphic termination-authority contract. Schema 1 alone uses nullable legacy DTO fields; missing legacy authority maps to `UnavailableManagerProcessTerminationAuthority`, never to a permissive current identity.
- No new partial production class, composition-root registration, project reference, fallback mechanism, or single-implementation interface was introduced.
- Independent negative tests cover ambiguous V2 payloads, post-start attachment failure with a live child, missing legacy boundary with termination count zero, and five malformed current boundary combinations.

The C# architecture review gate passes for C1 and F04 progression.
