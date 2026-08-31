# SB02 architecture and boundary review

Decision: pass the bounded provider optimization for root's independent integration gate. Scope is the database snapshot adapter plus the provider-management validator/materializer boundary. No new project, dependency direction, public contract, DI registration or persistence schema is introduced.

`DatabaseProviderRuntimeProfileSnapshotLoader` remains the infrastructure adapter for EF reads and revision composition in the existing outer module. `SharedProviderRuntimeProfileMaterializer` remains the owner of shared-provider relationship, canonical publication and operational availability validation in ProviderManagement. Its public full-materialization method consumes the extracted internal validation result and retains its original model/tag/metadata construction. The revision adapter uses only validated shape presence and the unchanged composite token revision; local profiles still invoke the original persisted mapper.

The one internal `SharedProviderValidatedRuntimeShape` record communicates the existing profile/import/source references and validated publication/transport fields. It is not retained across calls and does not expose a new public runtime API. The already-dependent `CanDoItAll.Modules.AgentFramework` assembly receives one explicit `InternalsVisibleTo` friendship. This assembly-level permission is broader than a type-level permission in C#, so its use is intentionally limited to this validator/result collaboration; it creates no reverse project reference and should not become a general cross-module shortcut. Existing test friendships are unchanged.

The selected shared path joins imports to their source with an explicit single-import cardinality condition. The condition preserves rejection before source EF conversion when malformed duplicate rows are present. Typed entity materialization is deliberately retained, including conversion behavior. The set path retains the old three reads across all providers/imports/sources because filtering unrelated rows would change existing failure behavior. This is the smallest proven optimization: retain the existing validation and revision rules, omit effective model/catalog copies, and combine only the safe selected read boundary. No validator or available-state cache is introduced.

## Direct and analyzer evidence

- `source-binary-hashes.json` identifies the sole project-file change and unchanged downstream validation/mapping/context contracts.
- `source-equivalence.json` proves the composite revision and GUID writer source tail is unchanged from `3d5def561`, and all ten context files match baseline Git blobs.
- Scoped CodeAnalytics before: `snap-20260831135127-6a61c183`; after: `snap-20260831142651-6a61c183`. Both snapshots include ProviderManagement and the consuming Module, are healthy, and report zero diagnostics.
- Project-reference edges are unchanged. The scoped existing module/type cycle findings are identical before and after (two); no new project cycle is introduced. `architecture-comparison.json` records exact equality rather than asserting the pre-existing findings disappeared.
- One new informational `COMPLEXITY-002` finding reports nine source members on `SharedProviderValidatedRuntimeShape` (eight positional fields plus its constructor). This is one cohesive validation result with no added behavior, not a new aggregation service. It is reviewed and accepted; splitting it would add coupling/boilerplate without reducing a responsibility.
- The executable source-boundary and project-reference characterization tests passed in the selected Unit suite.

The allocator assertion is intentionally bounded and synchronous. The relational proof uses the concrete Npgsql adapter and isolated PostgreSQL rather than an InMemory approximation. Fixture-only duplicate-index manipulation is not a migration and is restored before fixture completion. No broad benchmark or concurrency/pipeline design change is hidden in this work.
