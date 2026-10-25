# Inherited Agent planning journal and provider input

Status: behavioral checkpoint passed with an explicit test-only repair bridge. This
implements the inherited Agent owner and MAF input path; Process/Workflow producer,
business publication and manager continuation are still gated.

## Implemented boundary

Journal schema 7 and decision schema 2 retain the original profile, namespace, Agent,
background source, bounded parent binding, material identity/digest/capture time, parent
policy and exact unsettled work allocation. Existing interactive schema 6 and decision
schema 1 retain their authority rules and exact legacy bytes. The golden v1 input was
captured from the prior Release binary before this implementation.

The child uses one RunSnapshot for its lifetime, including when the parent supplied a
StepSnapshot. Its finite work comes from the grant; local replans are zero. Parent policy
and capture time remain original. Explicit and automatic child refresh refuse; a new
parent generation must issue a new child occurrence. No current settings are fabricated.

The new-only background factory performs exact execution lookup before invoking the
origin binder. The live owner has a distinct inherited admission and basis-read path.
It reuses atomic journal/work/segment transitions and refuses interactive capture or
start on the inherited journal. Current parent read/dispatch checks guard new work and
provider entry; actual responses and settlement can still be retained after revocation.
MafPlanningStepRuntime selects the instruction by admitted origin and sends the retained
material through its real SDK/admission boundary. No chat is manufactured.

## Discovery and execution

All filters use the named classes with FullyQualifiedName~ and explicit | combinations,
Release, --no-build --no-restore and UseLocalCanDoItAllLibraries=true. The retained proof
contains complete arguments, expected/discovered counts and every individual result.

| Lane | Cases |
| --- | ---: |
| AgentInheritedPlanningJournalTests | 33 |
| AgentPlanningDecisionJournalTests, ProjectTaskPlanAgentCodecTests, AgentToolJournalSchemaFiveTests | 70 |
| AgentInheritedPlanningOwnerTests | 18 |
| AgentPlanningLiveOwnerTests, AgentPlanningStepAdmissionTests, FileAgentPlanningDecisionTests, AgentPlanningSavedActionTests | 45 |
| AgentToolBackgroundJournalIntegrationTests, including its reservation partial | 35 |
| MafPlanningStepRuntimeTests | 13 |
| ProjectTaskPlanAdmissionTests, ProjectTaskPlanExecutionEntryTests | 25 |
| MafInheritedPlanningRuntimeTests | 3 |
| Total distinct cases | 242 |

The initial candidate passed 238 and failed four. One assertion expected dispatch denial
where the changed parent identity correctly failed the earlier receipt-read check.
Three new MAF cases stopped before transport because the synthetic provider fixture
inherited an unsupported medium thinking override. The repair changes only the new
fixture and owner-test files: it clears that synthetic override, as the existing
interactive fixture does, and asserts the precise read-denial exception. It does not
weaken production capability or authorization checks.

All 21 consumers of the changed fixture were rediscovered and repeated: 21 passed,
zero failed/skipped. The other 221 passing cases remain supported by the exact unchanged
production/source bridge. Thus 242 distinct behaviors have passing evidence; the initial
242-case command remains recorded as failed, not relabeled as a green rerun.

Direct Release builds passed for Models, Core, Workbench and MAF, plus Unit and
Integration assemblies. Integration was rebuilt for the test-only repair. Existing
unrelated build warnings remain. Full-source portability scanned untracked new files
and passed without a baseline write: 15,639 findings unchanged. Validation-tool tests,
rendering, documentation and diff checks pass.

## Architecture review

Before: snap-20261019234231-926561d8. Final:
snap-20261020000039-926561d8. The four-project Agent scope has 636 findings and 189
existing collector diagnostics. There are no project cycles and the five prior type
cycles are unchanged. Direct builds supply compilation evidence.

No project references, runtime partial files, provider dispatch loops or service-locator
paths were added. Models owns immutable provenance and strict encoding; the small Core
factory owns new-only inherited admission. The existing live owner reuses the same work,
basis and response transitions. MAF owns provider-input selection. Workbench rejects the
new origin at its existing interactive target boundary.

Three new structural findings were reviewed: member counts on the immutable origin and
private wire DTO, and the existing planning owner reaching 361 lines. Its added methods
are explicit origin entry points around shared work/basis operations; origin creation
and validation stay in separate cohesive types. This is a pass for this bounded change,
with the existing large journal/owner concentration still relevant to future changes.

## Frozen proof and resources

The original candidate retained 9,519 source and 3,889 runtime inputs:
source DF851A01C46898CC7B9993C543F4518A02DB34677A2457DDF6ED1AFD127A11A9,
runtime B07E34978C7ED128EF691BF718A16F465E69E38C8B20B60110E55C298DC0D639.
The repair retains the same counts:
source 9E82B95BA5A350F51A84B7496C0767D421C4998D8822DF036649CF74FDEAFFC5,
runtime 924FB6190FDAB884115333ADBAACB7B4B05033490FC07D40BED375DB8EE1C4DE.
Both were verified unchanged before freeze release.

- [Original proof](../proof/2026-10-19-planning-authority/c4-inherited-agent-journal.json):
  AEFDF18176B193ED768CB14850C3CC4EBB98E4D5863F8F371A0D6D124079538B.
- [Repair proof and production bridge](../proof/2026-10-19-planning-authority/c4-inherited-agent-journal-repair.json):
  4ED44C46D5597E50B9682A6DD8813ECD59F4D2CC7E5B63C2648A7E94B1A9E750.

Observers covered every test command for 450.60 and 54.18 seconds. Neither run left an
observed process or database-name delta. Respectively 25 and 14 unattributed TEMP entries
were preserved. Both roots were owned before creation, used standard Release outputs and
reused the same isolated PostgreSQL fixture. No new server or resource family was created.
Historical cleanup denials remain unchanged; the shared fixture is needed for further work.

## Remaining end-to-end work

Connect canonical Process child/material lookup to actual execution admission, including
stable occurrence retry and original runtime contract. Implement the Workbench inherited
participant and parent-fenced business effect, actual manager continuation and mapped
Workflow producer before enabling settings/capability negotiation. Grant fairness,
journal/allocation capacity, remaining mutation owners, external effects and final broad
validation remain open. The historical Stable result (16,182 passes, 17 failures,
2,444 unexecuted) remains non-green. SPA remains on codex/concurrent-mutations.
