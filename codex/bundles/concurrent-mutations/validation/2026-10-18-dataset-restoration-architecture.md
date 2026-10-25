# C# Architecture Gate Result

Status: Pass with follow-up (previous-selection restoration foundation only).

## Findings

| Severity | Finding | Evidence | Required action |
| --- | --- | --- | --- |
| Resolved | Activation service and restoration lease referenced each other. | The lease now receives bounded observation and cache-invalidation delegates; snapshot `snap-20261019000956-83b97284` has no type cycle in the two-project scope. | Keep the lease independent of the broader activation service. |
| Informational | Existing-profile session and restoration lease trigger member-count heuristics. | The 41-line session owns explicit-profile/key construction and disposal; the lease owns exact file publication and coordination. Private fields contribute to the count. | No unrelated runtime behavior belongs in either type. |
| Existing | Infrastructure Persistence, ControlPlane and Storage form a module cycle. | Scoped dependency analysis reports one module cycle, no project-reference cycle and no new project files/references. | Existing architecture debt; this slice does not expand its ownership. |
| Existing | Workspace analysis is degraded by NuGet vulnerability advisories. | All 189 workspace error diagnostics are package advisories; affected direct Release builds pass. | Preserve this limitation; dependency maintenance remains separate from this gate. |

## Dependency direction

Infrastructure owns pure activation records, existing-file publication and SQL exclusion.
The restoration coordinator has no product-module dependency. Composition creates the
explicit-profile session, native finite pool registrations and PostgreSQL options. The
Web action parses arguments and emits closed safe outcomes before ordinary host
construction. It cannot run migrations, callbacks or background hosts. No project
reference changed. The reviewed global project graph remains the previously recorded
248-project, 996-edge acyclic graph; this scoped snapshot reports 1,249 dependency facts.

## Partial-class policy

No production partial class was added. Restoration integration tests have a separate
class and reuse an extracted activation fixture. The original activation scenarios use
the same fixture without moving production behavior into test helpers. The existing
lifecycle test group adds the child command chain to its existing recovery-command file.

## Testability proof

Pure record tests prove legacy reads, descriptor rejection and exact retry identity.
Existing-file tests prove missing targets are not recreated. Twelve direct PostgreSQL
restoration cases cover publication interruptions, exact SQL/plan mismatches, protocol
refusal, held file/SQL locks and a profile recheck after SQL release. A real child restores
the selection, repeats the same plan and then releases the original native deletion gate;
profile/key/schema/participant state stays unchanged. Invalid ordinary runtime settings
and an occupied web port prove the command uses its explicit offline profile without
starting the host. Four captured-source command regressions remain passing.

The first frozen candidate passed 43/43 unit and 25/26 integration cases. The one failed
fixture treated the now-installed protocol 2 as unsupported; it now derives an unsupported
version from the installed registration. The repair candidate passed 11/11 unit and
18/18 integration cases. Frozen identities, static checks and resource reconciliation
are recorded in [lifecycle validation](2026-10-18-lifecycle-integration.md).

## Closure decision

The restoration prerequisite may be used by the pending readiness/rotation work. This
does not close P03 or the overall concurrency bundle. Legacy installation, pending owner
inventories, rotation blocking, full runtime snapshot policies and their acceptance
proof remain required. Keep SPA on its existing review branch.
