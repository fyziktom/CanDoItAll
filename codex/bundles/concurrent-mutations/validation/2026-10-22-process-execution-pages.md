# Process execution page qualification

Status: Behavioral correctness passes; worker activation remains blocked by the measured
metadata cost. This is a checkpoint in P16, not completion of concurrent mutations.
Prerequisites are signed as `13de74201` and `8cd7ebab8`; SPA remains on its separate branch.

## Behavior and ownership

The request to prevent concurrent actors from losing or falsely completing work is owned
here by a Core read contract and a Persistence selector. The existing workspace store
holds publication exclusion and delegates selection. The Agent module resolves the same
scoped store. No project reference, new partial class, second canonical journal or worker
activation was added. Selected run records are read afresh; their original owner remains
responsible for authority, dispatch leases, effects and terminalization.

Pages take 1–64 candidate records in GUID key order. The selector checks canonical and
projection version/revision/count agreement and unique nonempty identities. Legacy rows
are conservative candidates; future projections, missing indexes and pending publication
produce explicit non-exhausted outcomes. It does not rebuild indexes or recover journals.
Invalid input, corrupt metadata and missing/corrupt selected records cannot become a
successful empty page. The content version must agree between indexes; it is not the
storage layout version or the version of an empty workspace template.

## Focused proof

The two exact owning filters are
`FullyQualifiedName~CanDoItAll.Tests.Integration.Runtime.FileSandboxWorkspaceProcessPageIntegrationTests.`
(18 cases) and
`FullyQualifiedName~CanDoItAll.Tests.Integration.Runtime.FileSandboxWorkspaceProcessQueryIntegrationTests.`
(12 cases). Build-backed discovery confirms 30 and the final run passes all 30. Real store
publication creates the selected records and the ordinary history fixtures. Restarted
pages preserve key order, isolate workspaces, read only selected records, observe same-stamp
record changes, and refuse prepared publications until their original owner recovers them.

The shallow-pass trap is applying `Take` after loading every journal or treating an
incomplete index as empty. Physical-read observations and the failure cases reject both.
No fixture-specific production branch, stub or `NotImplemented` path was introduced.

The first 18-case attempt passed 3 and failed 15 because the prototype incorrectly compared
the canonical content version with `SandboxWorkspaceDocument.Empty.Version`. The production
check was corrected; all 30 cases were rediscovered and rerun. All 48 executions, including
the 15 historical failures, remain in the compact proof. No old pass substitutes for the
final candidate. Core, Persistence, Agent module, Web and Integration built in standard
Release; after the correction, the four affected downstream outputs were refreshed again.
Source pins and 14 owning/copied DLL/PDB identities match before and after execution.

Commands and exact results are retained in
[the compact proof](../proof/2026-10-22-process-execution-pages.json). The command shape is
`dotnet build <owning.csproj> --configuration Release --no-restore --no-dependencies -p:UseLocalCanDoItAllLibraries=true /m:1`,
followed by build-backed filtered discovery and filtered `dotnet test --no-build --no-restore`
in the same configuration/mode. Builds ran in dependency order against already qualified
unchanged dependencies. The final integrated Stable gate remains scheduled after the
producer/settings changes; it is not rerun for this additive, unused read port alone.

## Cost finding and progression

| Synthetic unrelated history | Metadata bytes | First / repeated page, ms | Allocated bytes, first / repeated | Selected journal reads |
| --- | ---: | ---: | ---: | ---: |
| 20,000 | 16,424,265 | 134.77 / 137.62 | 9,196,432 / 9,204,264 | 2 / 2 |
| 100,000 | 82,104,264 | 1263.90 / 1002.50 | 43,898,272 / 43,733,440 | 2 / 2 |

These diagnostics use real selected records and synthetic metadata for unrelated history.
Filesystem cache was uncontrolled; two samples do not establish p95, cold-cache latency,
concurrent publication, sustained throughput or fairness. The journal-read bound is proven;
history-independent I/O and latency are not. Repeating an 82 MB metadata parse while holding
workspace exclusion is unsuitable for the recovery worker. Activation remains blocked
pending an owner-maintained compact index and cost/contention qualification. No timestamp
cache, hidden fallback or arbitrary history cap is accepted as a substitute.

## Architecture, static checks and resources

The scoped CodeAnalytics comparison is `snap-20261022125636-bf90ea00` to
`snap-20261022142721-198deb42`: 140→145 types, 1484→1504 members, no new cycle or selected
reader finding. Both report the same ten NuGet-advisory diagnostics as blocking and the
same existing HistoricalReadPass containment/back-reference cycle. This is a limited
comparison, not a clean whole-repository certificate. Direct builds pass. Existing store
growth is one guard/delegation method; selection behavior lives in its small owner.

C# architecture gate: Pass with follow-up. Dependency direction, responsibility and
testability are supported for the read contract. Worker activation cannot proceed until
the measured metadata bottleneck and actual stage-owner integration qualify.

The DI registration's rendering-register hash and rationale were explicitly reviewed:
the new scoped read port changes no renderer, assets or editor lifetime. Final portability
passes with 15669 reviewed findings unchanged and no baseline write; the new untracked
source was included in the scan. Rendering (1212 inputs), documentation (434 files),
whitespace and portability/redaction/rendering tool tests (6/4/7) also pass.

Both candidates use the existing `tool-admission` fixture family. All three execution
commands are observer-covered and both reconcile with zero new process, database-name or
temporary-path residue. No host or container was created. Historical cleanup denials stay
recorded in the resource ledger and were not retried. Keep the reviewed proof and shared
fixture for the remaining stage, Agent owner, fairness and HTTP integration.
