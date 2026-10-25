# Inherited planning through the registered workspace

Status: all 185 selected cases pass after a production activity-transition repair.
The real workspace entry and common lifecycle are implemented. Concrete Process/Workflow
producers, Workbench inherited publication and manager continuation remain open.

## Behavior and boundaries

AgentFramework Core exposes IAgentInheritedPlanningWorkspaceService as an in-process
integration port. The current-profile facade resolves the same scoped service instance.
Exact child admission precedes provider entry; new-only preparation creates the original
binding, while a retained initial run uses its saved input, source, model and scope.
Current provider settings and the registered participant must match the saved runtime
contract. Retained waiting, active, terminal or uncertain runs are read without taking
the provider dispatch lease or creating new work.

The inherited coordinator acquires ownership before entering the existing completion
handler. Its one-use capability stays live through credentials, provider execution,
history, usage/failure persistence and cleanup. A competing startup cannot enter the
handler that would write another caller's failure. Detail reads require current source
and participant authorization without manufacturing an interactive chat or review.

The shared loop still owns both origins' provider iteration and usage accumulation.
The new cohesive workspace executor uses narrow typed preparation/completion delegates;
the existing internal startup record moved out of the large execution partial. No new
runtime partial, project reference, provider dependency, history scan or second index was
added. Existing generic approval remains closed for inherited execution.

CodeAnalytics snapshots snap-20261020015959-aab4b286 and
snap-20261020022908-aab4b286 cover five projects: 2,089 to 2,094 types and 62 to 63 service
registrations. No new cycles appear; zero project cycles and five existing type cycles
remain. Two informational DTO-size findings replace the nested startup finding, yielding
679 to 680 findings. The additional informational DI diagnostic concerns the scoped alias;
the actual registration test verifies instance identity. Existing large-file warnings are
retained and reviewed; the facade changes route into the extracted owner. Scoped loader
diagnostics increase from 215 to 216; successful actual builds establish compilation.

## Validation and repair

| Selection | Cases |
| --- | ---: |
| Shared/inherited coordination, MAF contract/input, exact admission and owner regressions |99|
| Existing ProjectTaskPlanExecutionEntryTests |11|
| AgentInheritedPlanningWorkspaceTests |12|
| AgentInheritedPlanningLifecycleTests |3|
| MafRuntimeArchitectureServicesTests |60|
| Total |185|

The initial candidate passed 182/185. Three retained-result paths tried to complete an
activity still in Accepted. The repair reports PersistingResult before completing only
that caller's activity. It leaves the original execution unchanged. The competing-read
test also has a 20-second deadline. All 185 cases repeat because production changed;
no unchanged-production bridge is claimed.

The registered workspace tests use real preparation/credentials and the MAF SDK with an
intercepted HTTP transport. They cover both parent basis modes, Project history scope,
saved-input retries, concurrent provider work, retained waiting/failed records, current
read revocation, parent rejection, runtime/provider/Agent-option changes, and preserved
usage with unknown work after failure. Lifecycle tests prove one-use capability lifetime,
duplicate rejection before failure-writing code, and lease cleanup without invented
accounting. Product-source authority is deliberately not claimed from the test source.

Core, Modules.AgentFramework, Integration and Unit rebuild in standard Release, with
shared outputs serialized. Exact discovery confirms 125 Integration and 60 Unit cases.
The full portability-static procedure passes, including untracked source: 15,640 findings,
no baseline change, final enforcement without --write-baseline. The rendering register
records only the execution-service DI alias's changed source hash and reviewed rationale.
Scanner tests, rendering checks, documentation and diff checks pass.

## Evidence and resources

Initial source: 9,555 files, SHA256
B9F16A14D0DD9E2BE33BBF16D5940795E47D3F034A0A2F34B377E5F164983D36.
Initial runtime: 3,680 files, SHA256
92CCC2F133DF803191CEC0489E1E6329BF4F081FF85E23EDE5AD782723488279.
[Initial proof](../proof/2026-10-19-planning-authority/c4-inherited-workspace-lifecycle.json)
SHA256 D15646A56F7EF3EBC5F5BDB49B4606B454E5130DFA25DDB6D73E48D5039ECD59.

Final source: 9,555 files, SHA256
B88B86A0CA1087F7ACC64F110A6A9454D5DED985363DB07ABBA1390EED7DB031.
Final runtime: 3,680 files, SHA256
C064EEBAC4963478DC3236F55CC45BFB55E7D87AD65AF8D7A5097853F3093CD9.
[Passing proof](../proof/2026-10-19-planning-authority/c4-inherited-workspace-lifecycle-final.json)
SHA256 7C256FBF9A839015A310C9D0282A464A3AF2CFF00EE89B9A7CABA231B1B1DB62.

Both candidate roots record ownership before creation and reuse PostgreSQL 18 on 53619,
standard outputs and existing task-plan/tool-admission families. Observers run for
391.0883235 and 393.2450934 seconds and cover every complete test command. Exact source
and runtime verification passes before releasing either freeze. Reconciliation finds no
surviving observed processes or database-name delta. Eight initial and nine final
unattributed TEMP entries are preserved. The shared fixture remains needed for subsequent
lanes; ordinary port 5032 and installed tooling are preserved.

Historical cleanup denials and the failing historical broad Stable gate remain open.
This checkpoint does not close R5/R6 or the E2E bundle. Continue the
[Process integration plan](../plan/P07-process-planning-integration.md).
