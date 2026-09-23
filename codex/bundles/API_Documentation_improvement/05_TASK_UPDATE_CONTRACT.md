# Task-update documentation worksheet

## Verified baseline and scope

At the reviewed development commit, the HTTP handler already binds `ProjectStructureTaskUpdateAgentInput`, not the old component-oriented request. This is a documentation/export repair; do not redo the completed task-runtime work. Source evidence: P05, P06, P07, P08 and P18. The attachment provides historical context only. [E01]

**Operation:** `PUT /api/project-structure/projects/{projectId}/tasks/{taskId}`  
**Body CLR type:** `ProjectStructureTaskUpdateAgentInput`  
**Nested schedule types:** `ProjectStructureTaskScheduleAgentChange`, `ProjectStructureTaskDateAgentChange`  
**Application target:** `ProjectStructureTaskDetailsService.UpdateAsync`, through `ToRequest()` and the captured mutation owner.

The type's name contains AgentInput, but this HTTP operation is not an agent runtime invocation. Document the transport distinction rather than renaming the public type or wire contract in this run.

## Description-ready facts

| Member | Verified meaning and writing requirement |
|---|---|
| route `projectId` | The project being edited. The supplied admission must name this project and remain valid at the owner boundary. |
| route `taskId` | The canonical task's string node key; not a task title and not universally a GUID. |
| `taskId` in body | Must equal the route value using ordinal equality. A mismatch is `TaskRouteMismatch` / HTTP 400. |
| `currentTitle` | The previously read title used by the edit request. It and proposedTitle cannot be blank. |
| `proposedTitle` | The desired title; send the unchanged current value when not renaming. |
| `currentProgressPercent` | Previously read progress: -1 means untracked; otherwise 0 through 100. |
| `proposedProgressPercent` | Requested tracked progress, 0 through 100. Do not advertise -1 here. |
| `currentEstimate` | The owner-read effort/cost estimate used as a precondition. |
| `proposedEstimate` | The desired estimate, validated/normalized by the same owner policy. |
| `scheduleChange` | Optional schedule-change object passed to the validated domain schedule constructor; null means no schedule-change object is supplied. |
| `assigneeChanged` | Explicitly requests the assignment-edit path rather than inferring a change from proposedAssignee alone. Verify preserve/clear behavior at the application owner before finalizing all examples. |
| `proposedAssignee` | When a non-null direct assignment is requested, only Person/Agent kinds are valid, ResourceId must be nonempty, and VersionId must be absent. Do not conflate it with process/workflow resource attachment. |
| `currentExecution` | Previously read task execution-state snapshot. Required by owner validation; not a guess derived solely from progress. |
| `proposedExecution` | Requested execution-state snapshot, including actual timestamps; transition and timestamp rules are checked by the owner. |
| `currentCostBasis` | Explicit `[property: JsonRequired]` nullable member. Presence is required even when the value is null. It must represent the owner-read expected-cost basis. |
| `currentDirectAssignmentRevision` | Nonnegative owner-read direct-assignment revision, not the number of assignments. |
| `expectedProjectAdmission` | A nullable C# property with a required HTTP use-case meaning. Obtain it from the structure read and return it unchanged; omission or the wrong ProjectId causes HTTP 409 `ProjectLifetimeRefreshRequired`. The agent tool instead supplies its captured admission. |

Do not translate the table into global Required/Range annotations that change the reader. Verify each member's actual HTTP binding, requiredness and schema metadata. The agent function schema may require fields differently from the HTTP serializer; a shared CLR type is not proof of identical transport rules.

## Nested schedule facts

`gesture` uses the existing GanttScheduleGesture contract. `affectedTasks` names every affected task and its previous/proposed interval. `criticalTaskIds` is a nullable list with a null-to-empty conversion in ToRequest. Each affected-task record has string taskId, previousStart, previousEnd, proposedStart, proposedEnd and isCritical (constructor default false).

Before publishing the final enum documentation, inspect the actual Gantt owner/JSON handling and list the accepted wire tokens. Do not use a casing assumption derived from unrelated Simple Chat converters. Preserve timezone offsets and validate scheduling rules; do not turn an instant into a date-only input.

The source integration test uses SetInterval and exact returned task start/end values. That is a useful positive reference, not proof that every gesture and malformed body has been tested. [P07]

## Nested estimate facts

P08 defines:

- ExpectedEffortHours is a nullable hours quantity. A present value must be greater than zero and within the policy's supported duration range.
- ExpectedEffortUnit is Hours or ManDays. It controls input/display conversion; it does not change the unit of ExpectedEffortHours.
- The default conversion factor is 8 hours per man-day. Policy overloads can receive a positive explicit factor.
- ExpectedCostAmount is nullable; a present value is nonnegative and no greater than 1,000,000,000,000,000 under the inspected policy.
- When cost is absent, normalization clears ExpectedCostCurrencyCode. When cost is present, the code must normalize to three ASCII letters. This is a syntax check, not an ISO registry membership check.

Do not describe nullable expected effort/cost as zero. Preserve decimal precision and distinguish cost from cost rate and recognized sales.

## HTTP workflow that the final descriptions/examples must teach

1. Work against a synthetic project on an authorized test host. Discover its actual authentication policy and acquire authority normally.
2. Read current structure using `POST /api/project-structure/projects/{projectId}/structure/read`. Use the documented HTTP-eligible source and request the required metadata. Do not invent `GET .../tasks/{taskId}` if no such route is mapped.
3. Select the canonical task by the returned node ID. Preserve the response's expectedProjectAdmission, task title/progress/interval and exact edit-state data. Where edit state is embedded in metadataJson, the final API description must explain the JSON-string shape and extraction path, not tell a non-.NET client to call an internal C# helper.
4. Construct current/proposed pairs. Unchanged fields retain their read values. Change only the intended values. Include currentCostBasis explicitly, including null.
5. Send the PUT with equal path/body task IDs and the current admission.
6. Interpret the actual success or error body. Do not deserialize every API failure as ProblemDetails or every success as internal Result<T>.
7. Read back the affected task. For a conflict, reread and deliberately reconcile; do not silently replace preconditions and replay an obsolete edit.

Any lease prerequisite must be documented from the actual update/owner path. The request DTO does not contain a LeaseToken field; do not fabricate one by copying the generic node-create example.

## Required examples and regressions

Generate examples from controlled seeded state and validate literal JSON over HTTP with the production serializer/handler. The test source P07/P18 is a starting point, not the final exhaustive proof.

| Case | Expected proof |
|---|---|
| Read/modify/write with unchanged non-target fields | Exact body is accepted and only intended change appears in canonical readback |
| Valid reschedule | Plain IDs bind; previous intervals are those read; proposed interval persists |
| Missing canonical task | Typed existing not-found behavior, not a serializer metadata exception |
| Route/body mismatch | Existing 400 TaskRouteMismatch |
| Omitted currentCostBasis versus explicit null | Presence rule is tested; a permissible explicit-null case can proceed |
| Missing or wrong-project admission | Existing 409 ProjectLifetimeRefreshRequired, no retargeted write |
| Current -1 versus proposed -1 | Documented validator distinction is respected |
| Stale current state/direct-assignment revision | Owner conflict handling and no lost concurrent update |
| Assignee unchanged, replace and clear | Exact application semantics established and described; no speculative null/clear rule |
| Invalid nested gesture/ID/interval/estimate/execution | Correct binding/owner rejection and truthful error schema |

The final SharedInfo migration note must name the previous broken wrapper shape, the new plain-ID shape, affected route/tool, source version and the read-first requirements. Do not claim old clients remain compatible without testing their actual payloads.
