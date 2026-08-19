# A01 evidence report

## Decision state

- Implementation: complete for A01 scope.
- Evidence: complete and frozen for review.
- Gate: C1a independently reviewed and GO.
- Downstream: A02 is the only eligible next subbundle.

## Design result

A01 introduces a strict taxonomy instead of a broad platform service:

- A01 places pure `LogicalPath`, portable template expansion, physical syntax
  classification, and the opaque external-alias codec in SharedKernel; these additions
  perform no filesystem I/O or host probing.
- Infrastructure owns host validation, filesystem resolution, protected root binding,
  and the scoped registry implementation.
- `CanDoItAll.Infrastructure.Abstractions` owns only the narrow resolver/factory port and
  protected binding record. It has no dependencies.
- MAF Core/Models and Processes Application consume the port without referencing the
  Infrastructure implementation.
- Hosting/composition creates scoped registries and trusted execution metadata carries
  protected bindings. Agent-facing aliases, events, packages, prompts, and public
  projections omit physical roots and protected tokens.
- The standalone Hosting composition scopes the registry and every workspace service
  that consumes it. Its workflow executors use the same scoped lifetime already used by
  the product module; strict DI validation and a two-scope isolation test prove that an
  alias bound in one scope is unbound in another.

The versioned alias is `external-target/v1/<opaque-root-id>/<encoded-segments>`.
Root ids disclose no physical path; child segments are reversible percent encoding and
use structural comparison (root id canonical-insensitive, child segments ordinal).
Legacy drive aliases remain a read/migrate boundary, not a new writer format.

## Final commands and results

| Host | Command/scope | Result | Evidence |
|---|---|---:|---|
| Windows | Contract classes plus Hosting scope/isolation | 356/356 | `A01-windows-contract-post-review-final.trx` |
| Linux Docker | Same contract classes plus Hosting scope/isolation | 356/356 | `A01-linux-contract-post-review-final.trx` |
| Linux Docker | Extended A01-owned portability/regression matrix | 537/537 | `A01-linux-owned-post-review-final.trx` |
| Windows | `FullyQualifiedName~Path|~Workspace|~Storage` | 912/912 | `A01-windows-path-workspace-storage-post-review-final-2.trx` |
| Linux Docker | Same broad characterization | 898/912 | `A01-linux-path-workspace-storage-post-review-final.trx` |
| Windows Components | Focused HR alias authorization/migration/composition | 9/9 | `A01-windows-components-hr-post-review-final.trx` |
| Windows | Hosting/adapter lifetime and source-boundary regressions | 18/18 | `A01-windows-hosting-lifetime-post-review-final.trx` |
| Windows | `dotnet build CanDoItAll.Web.csproj -c Release --no-restore` | 0 warnings/errors | `A01-windows-web-build-post-review-final.log` |
| Linux Docker | Same Release build | 0 warnings/errors | `A01-linux-web-build-post-review-final.log` |
| Architecture | scoped CodeAnalytics + deterministic full project graph | 0 blocking diagnostics; 0 project cycles | snapshot `snap-20260809031028-a2e9718e`; `A01-project-reference-graph-final.json` |
| Scan | lexical scan + deterministic routing + semantic audit | 25,644 findings reviewed; 0 unclassified | `post-scan-reviewed-post-review-final*`; `inventories/01-execution-portability-scan-review.md` |

The final TRX files are under `artifacts/unix-portability/A01/test-results` and build,
scan, graph, redaction, and changed-file evidence is under
`artifacts/unix-portability/A01`.

## Linux broad failure classification

The 14 broad failures are stable, named, and outside A01's path-contract ownership:

1. Two `FloatingAgentContextBaselineCharacterizationTests` construct source paths with
   Windows separators. They are source-inspection fixtures assigned to later sweep work.
2. `ManagerStatusResponseFactoryTests` and four `WorkspaceRuntimeProcessToolsTests`
   assert Windows Tailwind/watch/process command shapes. They belong to B03/B01.
3. Three `ProjectStructureRuntimeLauncherTests` and one
   `ProjectStructureRuntimeLauncherPathResolverTests` use Windows runtime expectations.
   They belong to B02.
4. One `DotNetSolutionSetupRuntimeExecutorTests` case reaches the known runtime-owned
   setup portability failure assigned to B01/B06.
5. `ProfileTestSupportTests` and `StorageCatalogServiceTests` expect the obsolete root
   `docker-compose.yml`; the repository uses `compose.yaml`. This is a test-harness
   environment defect, not an A01 contract regression.

The 537-test Linux A01-owned matrix excludes only these assigned surfaces and is fully
green. No policy was weakened, test skipped, or allowlist broadened to obtain that result.

## Migration, rollback, and failure behavior

- Existing legacy aliases migrate only at registry-aware write boundaries and persist
  the generated protected binding. End-to-end HR edit/write/reload coverage proves the
  production path.
- Malformed/conflicting persisted binding authority throws explicit diagnostics; it is
  never silently filtered.
- An unbound versioned alias fails explicitly and requires trusted rebind/migration.
- Public package export, execution events, prompts, and source-ingestion metadata retain
  the opaque alias but remove protected binding authority.
- Rollback retains the legacy reader. New versioned aliases cannot be resolved by old
  code, so rollback requires restoring the pre-change store or migrating affected
  records; there is deliberately no insecure physical-path fallback.

## Changed surface

The complete list is captured in `A01-changed-files-final.txt`. The main groups are:

- SharedKernel logical/portable/alias codecs.
- Infrastructure physical syntax policy, control-plane/storage roots, storage browse
  keys, and external root registry.
- MAF workspace path resolution, runtime guard, policy, execution metadata, and scoped
  registry composition.
- Standalone Hosting scoped authority/workspace/executor composition and cross-scope
  isolation coverage.
- Process/Workbench/HR physical write boundaries and trusted binding propagation.
- Development configuration and documentation.
- Focused Windows/Linux/golden tests and bundle evidence.

No sibling Components/FileTools repository change or direct project-reference switch is
needed for A01. That explicitly authorized topology remains deferred until B00, after
core Gate C4, as required by the bundle dependency graph.

## Residual risk

- Actual macOS execution is unavailable locally. The explicit macOS golden matrix and
  actual Linux POSIX execution reduce semantic risk; actual macOS remains mandatory
  before C4.
- The analyzer retains existing intra-project Infrastructure module and Core type
  cycles. They are not project-reference cycles and are not introduced by A01.
- The 14 Linux broad failures remain mandatory work in their assigned subbundles/harness
  repair; they are not treated as green or ignored.
