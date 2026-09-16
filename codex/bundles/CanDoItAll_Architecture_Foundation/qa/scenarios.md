# Regression and hardening scenarios

These are acceptance specifications, not executed tests. Select current concrete test implementations per affected slice and record baseline/post-change evidence.

## QA-001

**Title:** Floating agent reads Project Structure

**Module ids:** MOD-AGENTS; MOD-STRUCTURE; BND-CONVERSATIONS

**Contract ids:** CON-003; CON-018; CON-044

**Level:** desktop UI + integration

**Priority:** P0

**Given:** Project A contains notes, a task, a file, and a source projection; the agent has valid authority.

**When:** Open the floating agent and invoke the actual Structure read tool for selected nodes.

**Then:** Correct scoped content and coverage; the window remains usable without a concrete page-instance dependency.

**Fault injection:** Remove the tool registration: explicit unavailable, not a fabricated result.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-002

**Title:** Floating agent over Gantt edits the same task

**Module ids:** MOD-AGENTS; MOD-STRUCTURE; BND-WORK; BND-CONVERSATIONS

**Contract ids:** CON-023; CON-025; CON-044

**Level:** desktop UI + PostgreSQL

**Priority:** P0

**Given:** A task appears in both graph and Gantt.

**When:** Change an allowed date through the agent, then open the task in the other view.

**Then:** Both surfaces show the same identity and owner revision; agent session/history is preserved.

**Fault injection:** Concurrent edit before commit produces conflict, not lost update.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-003

**Title:** Native note remains canonical inline content

**Module ids:** MOD-STRUCTURE; MOD-AGENTS

**Contract ids:** CON-019; CON-021

**Level:** integration + UI

**Priority:** P0

**Given:** A native simple note and a file node with descriptive Notes exist.

**When:** Create/edit a native note through the agent, reload, export, and import.

**Then:** Native content survives without forced conversion to a file; file-node notes do not overwrite bytes.

**Fault injection:** Equal text in both fields must retain its two distinct meanings.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-004

**Title:** Agent creates a canonical task

**Module ids:** MOD-AGENTS; MOD-STRUCTURE; BND-WORK

**Contract ids:** CON-021; CON-023; CON-024

**Level:** tool + PostgreSQL + UI

**Priority:** P0

**Given:** Agent has task-creation capability and a valid parent.

**When:** Create a task with a goal and an authorized resource attachment.

**Then:** Correct identity, kind policy, assignment, and Gantt/canvas display.

**Fault injection:** Generic node creation with task internals cannot bypass task validators.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-005

**Title:** View switch during invocation

**Module ids:** BND-CONVERSATIONS; MOD-AGENTS; MOD-STRUCTURE

**Contract ids:** CON-003; CON-044; CON-045

**Level:** desktop UI

**Priority:** P0

**Given:** A turn was admitted from Structure in project A.

**When:** Switch to Gantt or another A tab and continue the conversation.

**Then:** In-flight origin retains A and the original snapshot; a later turn receives explicit current context; no duplicate subscriptions.

**Fault injection:** Delay the old UI load until after the new load completes.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-006

**Title:** Project switch cannot retarget a tool write

**Module ids:** MOD-AGENTS; MOD-STRUCTURE; BND-CONVERSATIONS

**Contract ids:** CON-021; CON-044; CON-051

**Level:** UI + integration

**Priority:** P0

**Given:** A turn was admitted with a parent node in A.

**When:** Switch to B before file/task creation completes.

**Then:** The effect belongs to A if still authorized, or is blocked; nothing is created in B.

**Fault injection:** Replace current-selection state after admission.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-007

**Title:** Database-profile switch invalidates old context

**Module ids:** MOD-WORKSPACE; BND-COMPOSITION; MOD-AGENTS; MOD-STRUCTURE

**Contract ids:** CON-007; CON-018; CON-048

**Level:** UI + integration

**Priority:** P0

**Given:** D1 and D2 have colliding local IDs.

**When:** Switch profiles while a load/run is active.

**Then:** Old DTOs, secrets, preparation, and worker scope are not used in D2; the run remains bound to D1 or is blocked.

**Fault injection:** Deliver a late D1 notification/callback after the new generation.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-008

**Title:** Snapshot bounds, expiry, and coverage

**Module ids:** MOD-STRUCTURE; MOD-AGENTS; BND-API

**Contract ids:** CON-018; CON-044

**Level:** unit + integration

**Priority:** P0

**Given:** Snapshot reaches actual limits; baseline documentation mentions 512 nodes, 1024 links, and five minutes.

**When:** Request uncovered/expired data or request InvocationSnapshot through HTTP.

**Then:** Explicit stale/partial/rejected source result; no silent database fallback or false completeness.

**Fault injection:** Forged fingerprint, missing selection coverage, and clock advance.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-009

**Title:** Permission revocation after loading

**Module ids:** MOD-SECURITY; MOD-AGENTS; MOD-STRUCTURE; MOD-CRM

**Contract ids:** CON-010; CON-018; CON-051

**Level:** integration + tool

**Priority:** P0

**Given:** A snapshot exists before project/CRM access is revoked.

**When:** Attempt a new read, mutation, and approval continuation.

**Then:** Current policy prevents unauthorized access and mutation; cache lifetime does not defer revocation.

**Fault injection:** Leave the old cache valid without invalidation.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-010

**Title:** Forged producer or authority

**Module ids:** MOD-STRUCTURE; MOD-SECURITY; BND-WORKFLOWS

**Contract ids:** CON-021; CON-051

**Level:** security integration

**Priority:** P0

**Given:** Untrusted workflow/tool input supplies another RunId, ProjectId, or admin flag.

**When:** Submit it to the gateway/owner API.

**Then:** Resolver rejects mismatches; audit retains the actual actor without escalation.

**Fault injection:** A note asks the caller to skip validation.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-011

**Title:** Tool attachment is not an HTTP endpoint list

**Module ids:** MOD-AGENTS; MOD-PROCESSES; BND-API

**Contract ids:** CON-003; CON-028

**Level:** integration

**Priority:** P0

**Given:** Process HTTP API exists without a general Process tool provider.

**When:** Inspect attached tools and invoke a supported Structure process bridge.

**Then:** Only genuinely declared tools appear; the existing bridge works without an invented provider.

**Fault injection:** An unavailable tool request fails explicitly.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-012

**Title:** Serial tool ordering and approval barrier

**Module ids:** MOD-AGENTS; MOD-SECURITY; BND-WORKFLOWS

**Contract ids:** CON-003; CON-004

**Level:** runtime integration

**Priority:** P0

**Given:** Provider returns a dependent read/write sequence containing an approval.

**When:** Execute the sequence and delay approval.

**Then:** Ordering remains intact; later mutations do not cross the barrier; confirmed effects are not repeated.

**Fault injection:** Concurrent cancellation/approval or retry of the same request.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-013

**Title:** Tool outcome matches owner receipt

**Module ids:** MOD-AGENTS; MOD-STRUCTURE; BND-API

**Contract ids:** CON-019; CON-021; CON-054

**Level:** integration

**Priority:** P0

**Given:** Owner can validate and reject a mutation.

**When:** Return validation, conflict, and committed-with-warning outcomes.

**Then:** Tool/UI/API reports neither false success nor false rollback and returns safe correct identity/revision.

**Fault injection:** Exception after DB commit before response.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-014

**Title:** File read remains in scope

**Module ids:** BND-STORAGE; MOD-STRUCTURE; MOD-AGENTS; MOD-RESOURCES

**Contract ids:** CON-043; CON-052

**Level:** storage integration

**Priority:** P0

**Given:** A and B have similar filenames under different root bindings.

**When:** A's agent requests B, traversal, an absolute path, or a retargeted path.

**Then:** Unauthorized access is denied; allowed A files remain readable with correct content.

**Fault injection:** Retarget the root or change a symlink immediately before I/O.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-015

**Title:** Create text/JSON/Markdown/Mermaid/log assets

**Module ids:** MOD-STRUCTURE; BND-STORAGE; MOD-AGENTS

**Contract ids:** CON-021; CON-022; CON-043

**Level:** integration + UI

**Priority:** P0

**Given:** Valid storage configuration and no existing file.

**When:** Create each supported text kind and open it in the viewer.

**Then:** Actual UTF-8 bytes, matching extension/MIME, correct binding, and content-equivalent export.

**Fault injection:** Invalid JSON or a size-limit violation must not publish a completed asset.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-016

**Title:** Media upload and safe preview

**Module ids:** MOD-STRUCTURE; BND-STORAGE

**Contract ids:** CON-022; CON-043

**Level:** integration + UI

**Priority:** P1

**Given:** Images/SVG/video and an impermissible active payload are available.

**When:** Upload, preview, and read back.

**Then:** Safe MIME/content validation and usable real-file preview; rejected content is explicit.

**Fault injection:** Corrupt base64 or a forged extension.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-017

**Title:** Contribution retry returns one identity

**Module ids:** MOD-STRUCTURE; MOD-AGENTS; BND-WORKFLOWS

**Contract ids:** CON-021; CON-054

**Level:** PostgreSQL

**Priority:** P0

**Given:** Stable key/fingerprint and native-node intent.

**When:** Deliver the same request repeatedly, including after worker restart.

**Then:** One effect and the same receipt/NodeKey; no duplicate workflow output.

**Fault injection:** Lose the response after commit.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-018

**Title:** Contribution key with a different payload

**Module ids:** MOD-STRUCTURE; MOD-SECURITY

**Contract ids:** CON-021

**Level:** PostgreSQL

**Priority:** P0

**Given:** A contribution is committed.

**When:** Reuse its key for another target or content.

**Then:** Idempotency conflict without silently changing master data.

**Fault injection:** Rotate only a secret lease token: unchanged semantic intent must not become a different content fingerprint.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-019

**Title:** Concurrent contribution instances

**Module ids:** MOD-STRUCTURE; BND-COMPOSITION

**Contract ids:** CON-021

**Level:** PostgreSQL concurrency

**Priority:** P0

**Given:** Two dispatchers or independent services receive the same key.

**When:** Deliver the intent concurrently.

**Then:** Durable uniqueness/deduplication retains one effect; in-memory locking is not the only guard.

**Fault injection:** Kill the winning worker after commit but before acknowledgement.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-020

**Title:** Retry after the result node was deleted

**Module ids:** MOD-STRUCTURE; MOD-AGENTS

**Contract ids:** CON-021

**Level:** integration

**Priority:** P0

**Given:** A contribution created a note that the user later deleted.

**When:** Producer repeats ensure.

**Then:** Tombstone/suppressed/target-deleted result without automatic resurrection.

**Fault injection:** Producer attempts an unauthorized new key for the same suppressed managed intent.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-021

**Title:** Required versus optional contribution

**Module ids:** MOD-STRUCTURE; MOD-PROCESSES; BND-WORKFLOWS

**Contract ids:** CON-021; CON-031; CON-054

**Level:** integration

**Priority:** P0

**Given:** A run has a required output and a separate optional annotation.

**When:** Reject attachment policy or simulate outage.

**Then:** Required delivery remains pending/blocked; optional warning is visible; execution is not needlessly repeated.

**Fault injection:** Destination storage fails after inference has finished.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-022

**Title:** Projection rebuild does not create native objects

**Module ids:** MOD-STRUCTURE; MOD-PROJECTS; MOD-CRM

**Contract ids:** CON-018; CON-032

**Level:** integration

**Priority:** P0

**Given:** Native notes/tasks and projected entities have separate identities.

**When:** Repeat read, rebuild, and source refresh.

**Then:** Native data, layout, counts, and revisions do not change; no new contribution effect.

**Fault injection:** One source owner fails during rebuild.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-023

**Title:** Retry does not overwrite later human edits

**Module ids:** MOD-STRUCTURE; MOD-AGENTS

**Contract ids:** CON-019; CON-021

**Level:** PostgreSQL

**Priority:** P0

**Given:** An agent created a node and a human changed its name/body.

**When:** Replay the original create receipt and then a stale update.

**Then:** Create returns the original identity without overwrite; update requires the right revision.

**Fault injection:** Out-of-order delivery after editing.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-024

**Title:** Contribution quotas and typed schema

**Module ids:** MOD-STRUCTURE; MOD-SECURITY

**Contract ids:** CON-019; CON-020; CON-021

**Level:** unit + integration

**Priority:** P1

**Given:** Authorized caller exceeds a limit or supplies an unsupported kind/system field.

**When:** Submit a typed request.

**Then:** Bounded rejection without partial native writes; a smaller valid request still succeeds.

**Fault injection:** Excess metadata, graph fan-out, or a forged source reference.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-025

**Title:** Structural-relation owner and endpoint scope

**Module ids:** MOD-STRUCTURE; MOD-PROJECTS; BND-WORK

**Contract ids:** CON-017; CON-020; CON-025

**Level:** integration

**Priority:** P0

**Given:** Projected endpoints and a native note exist; relation kinds have different owners.

**When:** Create a structural link and try to use it to alter project hierarchy or task dependency.

**Then:** Structure owns the native link; foreign relation commands are delegated to their owner or rejected.

**Fault injection:** Cross-project endpoint without authorization to both scopes.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-026

**Title:** Parent changes during contribution

**Module ids:** MOD-STRUCTURE; BND-WORK

**Contract ids:** CON-021; CON-023

**Level:** PostgreSQL concurrency

**Priority:** P0

**Given:** Parent was validated before writing.

**When:** Another transaction deletes, reparents, or changes its kind.

**Then:** Mutation recheck, revision, or constraint prevents orphaning and invalid task parentage.

**Fault injection:** Barrier between read and commit.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-027

**Title:** Workflow launch and result return through Structure

**Module ids:** MOD-STRUCTURE; BND-WORKFLOWS; MOD-AGENTS; BND-STORAGE

**Contract ids:** CON-029; CON-030; CON-021; CON-055

**Level:** desktop UI + real integration

**Priority:** P0

**Given:** Versioned workflow with a minimal deterministic executor and asset/task output.

**When:** Launch from a node, observe the run, and open output.

**Then:** Same user journey, correct run/version identity, actual content, and intended parent.

**Fault injection:** A separately authorized live-provider smoke test is needed; a fake is not live proof.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-028

**Title:** Workflow results without an open page

**Module ids:** MOD-STRUCTURE; BND-WORKFLOWS; BND-COMPOSITION

**Contract ids:** CON-030; CON-021; CON-045; CON-055

**Level:** integration + UI reconnect

**Priority:** P0

**Given:** Workflow was admitted and the user leaves the page.

**When:** Finish the run and reopen the project.

**Then:** Authoritative status/output restoration does not depend on component GetStatus calls.

**Fault injection:** Drop the transient invalidation notification.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-029

**Title:** Repeated workflow start

**Module ids:** BND-WORKFLOWS; MOD-STRUCTURE

**Contract ids:** CON-030

**Level:** PostgreSQL/runtime

**Priority:** P0

**Given:** Same start OperationId and immutable input.

**When:** Double-click or retry after timeout.

**Then:** Same RunId/receipt without another paid inference.

**Fault injection:** Lose the launch response after admission.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-030

**Title:** Projection failure after workflow admission

**Module ids:** BND-WORKFLOWS; MOD-STRUCTURE

**Contract ids:** CON-030; CON-021; CON-055

**Level:** failure injection

**Priority:** P0

**Given:** Owner has confirmed RunId.

**When:** Fail ApplyStatus or output attachment.

**Then:** Preserve the accepted run and separate writeback failure; repair does not restart it.

**Fault injection:** Invalid-operation, serialization, or storage exception after admission.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-031

**Title:** Two workflow runs and late completion

**Module ids:** BND-WORKFLOWS; MOD-STRUCTURE

**Contract ids:** CON-030; CON-021; CON-055

**Level:** concurrency integration

**Priority:** P0

**Given:** A reference node starts R1 and R2 under allowed policy.

**When:** Complete R2 first, then R1.

**Then:** Both manifests retain lineage; late R1 cannot overwrite R2's selected/latest status.

**Fault injection:** Concurrent human node creation must not be attributed to R1 by set difference.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-032

**Title:** Late output after cancellation

**Module ids:** BND-WORKFLOWS; MOD-PROCESSES; MOD-STRUCTURE

**Contract ids:** CON-028; CON-030; CON-021; CON-055

**Level:** failure injection

**Priority:** P0

**Given:** Cancellation is accepted before an external executor's late response.

**When:** Deliver output after cancellation.

**Then:** Owner state machine decides outcome/attachment; Structure cannot independently declare success.

**Fault injection:** Race completion against cancellation acknowledgement.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-033

**Title:** Workflow approval/resume preserves authority

**Module ids:** BND-WORKFLOWS; MOD-SECURITY; MOD-AGENTS

**Contract ids:** CON-004; CON-030; CON-051

**Level:** runtime integration

**Priority:** P0

**Given:** Workflow awaits approval in its original valid scope.

**When:** Restart/reconnect and approve through an authorized actor.

**Then:** Correct run/checkpoint resumes with the exact payload; supported approval backends remain usable.

**Fault injection:** Revoked actor or modified payload is denied/expired, not bypassed.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-034

**Title:** Invalid workflow backend, version, or input

**Module ids:** BND-WORKFLOWS; MOD-STRUCTURE; MOD-SCHEDULER

**Contract ids:** CON-029; CON-030

**Level:** unit + integration

**Priority:** P1

**Given:** Inactive version, unsupported backend, or invalid input schema.

**When:** Request start.

**Then:** Rejection before admission with no accepted run; a valid baseline workflow still starts.

**Fault injection:** No implicit fallback to another definition/backend.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-035

**Title:** Structure workflow executor preserves supported operations

**Module ids:** BND-WORKFLOWS; MOD-STRUCTURE; BND-WORK

**Contract ids:** CON-018; CON-021; CON-023

**Level:** runtime integration

**Priority:** P0

**Given:** Definition uses ListProjects, ReadTree, ReadNode, CreateAsset, and CreateTaskNodes.

**When:** Execute the sequence with scoped input context.

**Then:** Every actually declared operation has a typed adapter and real owner read/effect.

**Fault injection:** Input AgentId/RunId cannot become trusted authority.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-036

**Title:** Process launch and writeback through Structure

**Module ids:** MOD-PROCESSES; MOD-STRUCTURE; MOD-AGENTS; BND-STORAGE

**Contract ids:** CON-027; CON-028; CON-031

**Level:** desktop UI + PostgreSQL/runtime

**Priority:** P0

**Given:** Small deterministic process step with artifact output.

**When:** Launch, observe, complete, and inspect output/history.

**Then:** Correct parent project/run/root, preserved user journey, and authoritative process state.

**Fault injection:** Separately prove the live agent adapter, not only a seeded fixture.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-037

**Title:** Subprocess lineage and scope

**Module ids:** MOD-PROCESSES; MOD-STRUCTURE; BND-WORKFLOWS

**Contract ids:** CON-027; CON-028; CON-051

**Level:** runtime integration

**Priority:** P0

**Given:** Parent runs in A under a bounded output root.

**When:** Create a subprocess/workflow child and publish a result.

**Then:** Child has parent/causation and its own RunId without broader scope; artifacts do not land in B.

**Fault injection:** Forged parent-root alias or RunId.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-038

**Title:** Process outcome differs from task acceptance

**Module ids:** MOD-PROCESSES; BND-WORK; MOD-STRUCTURE; MOD-TESTLAB

**Contract ids:** CON-028; CON-023; CON-036

**Level:** integration

**Priority:** P0

**Given:** Task has an acceptance gate and process has technical success/failure.

**When:** Complete execution without acceptance criteria or with negative test evidence.

**Then:** Task is not automatically accepted; UI distinguishes runtime success from pending acceptance.

**Fault injection:** A textual success event cannot bypass the owner's verdict.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-039

**Title:** Process recovery and confirmed effects

**Module ids:** MOD-PROCESSES; MOD-AGENTS; BND-WORKFLOWS

**Contract ids:** CON-003; CON-027; CON-028

**Level:** failure injection

**Priority:** P0

**Given:** A process has a completed step followed by an interrupted step.

**When:** Restart and invoke supported recovery.

**Then:** Completed effects are not repeated contrary to owner policy; original scope, receipts, and checkpoints remain.

**Fault injection:** Crash after remote effect before local completion requires reconciliation, not a false exactly-once guarantee.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-040

**Title:** Processes execute without the Agents UI assembly

**Module ids:** MOD-PROCESSES; MOD-AGENTS; BND-MAF

**Contract ids:** CON-001; CON-003

**Level:** architecture + integration

**Priority:** P0

**Given:** Headless host registers the real execution adapter.

**When:** Evaluate/build the graph and run a process agent step.

**Then:** Consumer depends on execution/catalog contracts, not Razor page types; behavior is preserved.

**Fault injection:** Missing implementation fails explicitly.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-041

**Title:** Headless direct runtime versus desktop-only actions

**Module ids:** MOD-STRUCTURE; BND-COMPOSITION; BND-STORAGE

**Contract ids:** CON-048; CON-052

**Level:** supported host integration

**Priority:** P1

**Given:** Headless and desktop hosts declare their actual capabilities.

**When:** Start a direct executable and invoke optional terminal/Explorer actions.

**Then:** Supported direct execution works without UI; unsupported desktop actions return typed unavailable without shell fallback/elevation.

**Fault injection:** Unknown runtime/shell or missing executable.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-042

**Title:** Shutdown preserves admitted durable operations

**Module ids:** BND-COMPOSITION; MOD-PROCESSES; BND-WORKFLOWS; BND-SIMPLECHATS

**Contract ids:** CON-003; CON-027; CON-030; CON-049

**Level:** restart integration

**Priority:** P0

**Given:** Admitted durable operations are running or waiting.

**When:** Perform graceful and forced shutdown/restart.

**Then:** Recovery/blocked status remains discoverable; leases for live children of waiting runs are not discarded.

**Fault injection:** Interrupt claim/acknowledgement and profile switching.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-043

**Title:** Deferred image generation has a truthful baseline

**Module ids:** MOD-STRUCTURE; MOD-PROVIDERS; BND-STORAGE

**Contract ids:** CON-021; CON-022

**Level:** characterization + failure injection

**Priority:** P1

**Given:** Inspected baseline uses an in-memory queue, not proven durability.

**When:** Test successful generation and restart after queued/running states.

**Then:** Record the baseline gap; a changed durable path proves receipts/recovery or explicitly stays within narrower scope.

**Fault injection:** Provider succeeded but storage or DB update fails.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-044

**Title:** Target node deleted before completion

**Module ids:** MOD-STRUCTURE; BND-WORKFLOWS; MOD-PROCESSES

**Contract ids:** CON-021; CON-030; CON-031; CON-054

**Level:** failure injection

**Priority:** P0

**Given:** Run has a frozen destination that the user deletes.

**When:** Deliver output.

**Then:** Runtime result remains; attachment is blocked/suppressed without current-parent fallback or resurrection.

**Fault injection:** Reattachment requires a new explicitly authorized intent.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-045

**Title:** Worker dispatch uses original run scope

**Module ids:** BND-COMPOSITION; MOD-STRUCTURE; MOD-PROCESSES

**Contract ids:** CON-021; CON-027; CON-048; CON-054

**Level:** integration

**Priority:** P0

**Given:** Two database generations/workspaces and queued work from the old run exist.

**When:** Dispatch after the global UI profile changes.

**Then:** Use the original durable scope or reject safely; never automatically select the current connection.

**Fault injection:** Old data incarnation with colliding IDs.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-046

**Title:** Deleting project blocks new contributions

**Module ids:** MOD-PROJECTS; MOD-STRUCTURE; MOD-PROCESSES

**Contract ids:** CON-016; CON-021

**Level:** PostgreSQL concurrency

**Priority:** P0

**Given:** Lifecycle owner moved the project to Deleting.

**When:** A late producer requests node creation or process start.

**Then:** Lifecycle fence prevents new effects from undermining cleanup.

**Fault injection:** Race contribution admission against lifecycle commit.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-047

**Title:** CRM and canvas edit one WorkAssignment

**Module ids:** MOD-CRM; BND-WORK; MOD-STRUCTURE

**Contract ids:** CON-010; CON-024

**Level:** UI + PostgreSQL

**Priority:** P0

**Given:** Same work appears in CRM workload and canvas.

**When:** Change assignee through CRM and then the task dialog.

**Then:** One assignment identity/revision and consistent Gantt/workload, not a second AssignedTo copy.

**Fault injection:** Old bridge writer is blocked or adapted after cutover.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-048

**Title:** Concurrent assignments and reservations

**Module ids:** MOD-CRM; BND-WORK

**Contract ids:** CON-013; CON-024

**Level:** PostgreSQL concurrency

**Priority:** P0

**Given:** Two planners share an expected revision under mandatory reservation policy.

**When:** Assign conflicting capacity concurrently.

**Then:** One valid commit or explicit conflict/pending; no confirmed task without its required reservation.

**Fault injection:** Roll back the second context after the first context saves.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-049

**Title:** Quote becomes stale before plan commit

**Module ids:** MOD-PROVIDERS; BND-WORK; MOD-CRM

**Contract ids:** CON-005; CON-026

**Level:** integration

**Priority:** P0

**Given:** Quote Q references agent configuration/tariff revision 1.

**When:** Change configuration/tariff to 2 before baseline commit.

**Then:** Explicit rejection/refresh or an explicitly still-valid pinned quote; never silent repricing.

**Fault injection:** Concurrent resource-mix and router change.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-050

**Title:** Unknown versus zero versus partial cost

**Module ids:** MOD-PROVIDERS; BND-WORK; MOD-CRM

**Contract ids:** CON-005; CON-009

**Level:** unit + integration

**Priority:** P0

**Given:** Free tariff, missing tariff, missing usage, and partial usage are distinct.

**When:** Calculate query, estimate, and summary.

**Then:** Zero means explicitly free; unknown/partial remain visible and are not treated as reliable zero.

**Fault injection:** Omit one usage/token category.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-051

**Title:** Historical price does not change

**Module ids:** MOD-PROVIDERS; MOD-PROCESSES; BND-SIMPLECHATS

**Contract ids:** CON-007; CON-009

**Level:** PostgreSQL

**Priority:** P0

**Given:** Invocation was priced with a pinned tariff.

**When:** Change/deactivate the profile and catalog price.

**Then:** Historical provenance, usage, and cost remain; new quotes may change.

**Fault injection:** Report rebuild must not reprice original evidence.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-052

**Title:** Currencies, units, and double counting

**Module ids:** MOD-PROVIDERS; BND-WORK; MOD-CRM; MOD-PROCESSES

**Contract ids:** CON-005; CON-009; CON-026

**Level:** unit + integration

**Priority:** P0

**Given:** Human hourly rates, token costs, currencies, parent/child runs, and relay usage coexist.

**When:** Build a cost summary.

**Then:** Preserve units/basis; no mixed-currency sum without explicit FX; no duplicate invocation counting.

**Fault injection:** Same evidence ID arrives through two feeds.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-053

**Title:** Shared-provider publication/import revocation

**Module ids:** MOD-PROVIDERS; MOD-AGENTS; BND-API

**Contract ids:** CON-006; CON-007; CON-008

**Level:** transport + integration

**Priority:** P0

**Given:** Published model subset and imported origin identity exist.

**When:** Synchronize/invoke, then remove model/publication.

**Then:** Routing identity remains; disallowed model is rejected/unavailable; no secret copied into import.

**Fault injection:** Old catalog cache and upstream timeout.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-054

**Title:** Provider snapshot and secrets

**Module ids:** MOD-PROVIDERS; MOD-SECURITY; MOD-AGENTS

**Contract ids:** CON-007; CON-042

**Level:** integration + inspection

**Priority:** P0

**Given:** Immutable preparation belongs to D1.

**When:** Change provider secret/configuration and attempt dispatch with the old lease.

**Then:** Recheck revision/fingerprint and resolve fresh purpose-limited secrets; none in cache/log/DTO.

**Fault injection:** Snapshot-update failure after canonical save must not report false save failure.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-055

**Title:** Provider health does not justify silent fallback

**Module ids:** MOD-PROVIDERS; MOD-AGENTS

**Contract ids:** CON-005; CON-007

**Level:** integration

**Priority:** P1

**Given:** Selected provider is unhealthy or quota-limited.

**When:** Start assigned work.

**Then:** Typed unavailable/limit/remediation; fallback only under explicit cost/capability/policy permission.

**Fault injection:** Different billing, rate-limit, and credential failures.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-056

**Title:** Agent saved but CRM synchronization fails

**Module ids:** MOD-AGENTS; MOD-CRM

**Contract ids:** CON-002; CON-012

**Level:** failure injection

**Priority:** P0

**Given:** Valid Agents save with CRM destination outage.

**When:** Commit the agent, fail synchronization, then restore the projector.

**Then:** Agent stays saved with truthful warning; CRM eventually has one binding.

**Fault injection:** Repeated invalidation failures and restart.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-057

**Title:** CRM governance survives technical refresh

**Module ids:** MOD-CRM; MOD-AGENTS

**Contract ids:** CON-001; CON-012

**Level:** PostgreSQL

**Priority:** P0

**Given:** CRM owns notes, role, and capacity alongside projected technical fields.

**When:** Change technical name/status and publish a new snapshot.

**Then:** Only source-owned fields change; personnel facts remain.

**Fault injection:** Deliver an older technical snapshot after a newer one.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-058

**Title:** Privacy-safe CRM lookup and confidential detail

**Module ids:** MOD-CRM; MOD-SECURITY; MOD-AGENTS

**Contract ids:** CON-010; CON-011

**Level:** security integration

**Priority:** P0

**Given:** Party has a public label and a confidential note.

**When:** Query agent picklist, workflow contact options, and explicitly authorized confidential detail.

**Then:** Each purpose receives only permitted fields; safe pagination without leakage.

**Fault injection:** Narrow-scope user shares a server cache key with an administrator.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-059

**Title:** Project participation is not task performance

**Module ids:** MOD-CRM; MOD-PROJECTS; BND-WORK

**Contract ids:** CON-014; CON-024

**Level:** migration + integration

**Priority:** P0

**Given:** Customer/owner/delivery roles and node performer have different meanings.

**When:** Migrate the boundary and read/edit both paths.

**Then:** Roles, cardinality, intervals, primary status, and audit survive without heuristic loss.

**Fault injection:** Ambiguous legacy row appears in a reconciliation report.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-060

**Title:** ProcessStepAssignment does not overwrite the plan

**Module ids:** MOD-PROCESSES; BND-WORK; MOD-CRM

**Contract ids:** CON-024; CON-027; CON-028

**Level:** integration

**Priority:** P1

**Given:** Task names agent A; process owns its executor binding.

**When:** Runtime selects an allowed executor/child.

**Then:** Runtime evidence and plan remain distinct; only an explicit Work Management command changes the plan.

**Fault injection:** Projection event names a different agent than the plan.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-061

**Title:** Gantt dependencies versus row order

**Module ids:** BND-WORK; MOD-STRUCTURE

**Contract ids:** CON-025

**Level:** UI + PostgreSQL

**Priority:** P0

**Given:** Tasks have schedules and a dependency graph.

**When:** Reorder rows, edit dates, and attempt a cyclic dependency.

**Then:** Reordering does not change dependencies; policy rejects cycles; drag/undo/conflict is truthful.

**Fault injection:** Concurrent predecessor edit before commit.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-062

**Title:** Duplicate scheduler firing

**Module ids:** MOD-SCHEDULER; BND-WORKFLOWS; MOD-PROCESSES

**Contract ids:** CON-037; CON-027; CON-030

**Level:** Quartz/runtime integration

**Priority:** P0

**Given:** Plan P has firing F and a supported target.

**When:** Deliver F twice, including after restart.

**Then:** One launch receipt/run; history distinguishes retry from a new firing.

**Fault injection:** Crash between target admission and dispatcher acknowledgement.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-063

**Title:** Timezone, DST, misfire, and plan disabling

**Module ids:** MOD-SCHEDULER

**Contract ids:** CON-037

**Level:** unit + integration

**Priority:** P1

**Given:** Plan declares timezone and misfire policy.

**When:** Exercise DST, missed firing, and disable during dispatch.

**Then:** Declared semantics hold without unexpected duplicate/lost launches.

**Fault injection:** Trigger projection changes during reconciliation.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-064

**Title:** Actual scheduler target support

**Module ids:** MOD-SCHEDULER; MOD-PROCESSES; BND-WORKFLOWS

**Contract ids:** CON-037

**Level:** characterization + integration

**Priority:** P1

**Given:** Rebaseline the actual target registry.

**When:** Exercise every offered UI target through its real launch adapter.

**Then:** Record supported/unavailable; do not infer full process support from README or remove it from an older map.

**Fault injection:** Target implementation is absent from the host.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-065

**Title:** Memory source revision and revocation

**Module ids:** MOD-MEMORY; MOD-CRM; MOD-RESOURCES; MOD-STRUCTURE

**Contract ids:** CON-038; CON-039

**Level:** provider integration

**Priority:** P0

**Given:** Index contains authorized CRM/resource/Structure snapshots.

**When:** Change/delete source or revoke access, then retrieve.

**Then:** Staleness/provenance is visible and current access enforced; Memory cannot rewrite the source master.

**Fault injection:** Lose revocation notification and delay physical index cleanup.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-066

**Title:** Prompt-version pinning

**Module ids:** MOD-PROMPTS; MOD-AGENTS; BND-WORKFLOWS

**Contract ids:** CON-033; CON-030

**Level:** integration

**Priority:** P1

**Given:** Run admitted prompt P1.

**When:** Publish P2 during execution and rebuild a projection.

**Then:** Run/evidence remains P1; a new explicit use may select P2.

**Fault injection:** Deleted source retains historical reference under retention policy.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-067

**Title:** Curator uses owner commands

**Module ids:** MOD-PROMPTS; MOD-AGENTS

**Contract ids:** CON-034; CON-053

**Level:** tool + integration

**Priority:** P0

**Given:** Curator proposes a prompt change.

**When:** Approve/reject and persist the change.

**Then:** Prompts performs mutation with revision/audit/approval; no Agents EF writer.

**Fault injection:** Proposal becomes stale after human editing.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-068

**Title:** Plugin disable or grant revocation during execution

**Module ids:** MOD-PLUGINS; MOD-AGENTS; BND-WORKFLOWS; MOD-SECURITY

**Contract ids:** CON-040; CON-051

**Level:** integration

**Priority:** P0

**Given:** Activated tool initially has a grant.

**When:** Disable/revoke before the next invocation.

**Then:** Use-time denial and safe state/receipt; cache invalidation works.

**Fault injection:** Old descriptor remains in floating UI.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-069

**Title:** Purpose-limited OAuth and secret access

**Module ids:** MOD-PLUGINS; MOD-SECURITY

**Contract ids:** CON-040; CON-042

**Level:** security integration

**Priority:** P0

**Given:** Two plugin connections have distinct purposes.

**When:** Resolve/refresh a secret in another scope or export it.

**Then:** Broker denies without value leakage; legitimate refresh still works.

**Fault injection:** Revoked connection or replayed callback under the current OAuth contract.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-070

**Title:** Collaboration membership and object references

**Module ids:** MOD-COLLAB; MOD-CRM; MOD-STRUCTURE

**Contract ids:** CON-050; CON-010

**Level:** integration + UI

**Priority:** P1

**Given:** Thread has members and a project-object link.

**When:** Add a message, remove a member, then read through references.

**Then:** Membership/source access enforced; no substitution of a Simple Chat or agent transcript.

**Fault injection:** Cross-scope ID with the same display label.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-071

**Title:** Simple Chats durable turn and window closure

**Module ids:** BND-SIMPLECHATS; BND-CONVERSATIONS; BND-API

**Contract ids:** CON-049

**Level:** integration + UI/SSE

**Priority:** P0

**Given:** Ordinary turn with OperationId was admitted.

**When:** Close/reconnect floating view, restart dispatcher, and replay journal.

**Then:** One turn with durable transcript/terminal evidence; UI closure does not cancel it.

**Fault injection:** Lose transient signal and replay SSE cursor.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-072

**Title:** Simple Chats have no implicit agent capabilities

**Module ids:** BND-SIMPLECHATS; MOD-AGENTS; BND-CONVERSATIONS

**Contract ids:** CON-049; CON-044

**Level:** architecture + integration

**Priority:** P0

**Given:** Ordinary chat shares presentation with an agent chat over a project.

**When:** Open it and send an ordinary prompt.

**Then:** No implicit tools, skills, MCP, Project Structure context, or agent-runtime registration.

**Fault injection:** Shared component must not inherit previous agent configuration.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-073

**Title:** Simple Chat conversation-create compatibility

**Module ids:** BND-SIMPLECHATS; BND-API

**Contract ids:** CON-049

**Level:** HTTP integration

**Priority:** P1

**Given:** Current ordinary conversation-create API is documented non-idempotent.

**When:** Compare repeated HTTP create before/after refactor; separately exercise turn OperationId.

**Then:** Preserve the real contract; no unsafe global deduplication namespace or invented existing guarantee.

**Fault injection:** Turn retry remains safe and distinct from conversation creation.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-074

**Title:** TestLab verdict identifies target revision

**Module ids:** MOD-TESTLAB; MOD-STRUCTURE; BND-STORAGE

**Contract ids:** CON-036

**Level:** integration + UI

**Priority:** P1

**Given:** Test plan/case references evidence and content V1.

**When:** Save a verdict, change target to V2, and open Test.

**Then:** Evidence/verdict remains V1; new test is explicitly targeted or unsupported, not universal success.

**Fault injection:** Evidence bytes are missing.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-075

**Title:** Resource promotion and shared storage object

**Module ids:** MOD-RESOURCES; MOD-STRUCTURE; BND-STORAGE

**Contract ids:** CON-035; CON-043; CON-052

**Level:** integration

**Priority:** P1

**Given:** Authorized storage object already has a Structure binding.

**When:** Promote it to Resources and delete one binding.

**Then:** Resources owns metadata; bytes are not duplicated/deleted early and access is not broadened.

**Fault injection:** Stale origin scope or retargeted root.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-076

**Title:** Export/import native and projected data

**Module ids:** MOD-PROJECTS; MOD-STRUCTURE; MOD-PROMPTS; MOD-RESOURCES; MOD-CRM

**Contract ids:** CON-016; CON-046; CON-056

**Level:** PostgreSQL + storage integration

**Priority:** P0

**Given:** Project has note/task/links/references/assets/history.

**When:** Export and import into a clean schema with explicit ID remapping.

**Then:** Preserve native bodies, roles, bindings, and history; rebuild projections without creating source masters.

**Fault injection:** Unresolved remote reference is explicit; no secret in export.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-077

**Title:** Repeated import and partial transfer

**Module ids:** MOD-PROJECTS; MOD-STRUCTURE; MOD-CRM; BND-COMPOSITION

**Contract ids:** CON-016; CON-046; CON-056

**Level:** failure injection

**Priority:** P0

**Given:** Transfer has an operation plan and participants.

**When:** Interrupt after one participant commits, then resume.

**Then:** Discoverable receipts and correct resume without second writers/duplicate identities; partial status is truthful.

**Fault injection:** One participant has a schema mismatch.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-078

**Title:** Move/copy subtree during workflow execution

**Module ids:** MOD-STRUCTURE; MOD-PROCESSES; BND-WORKFLOWS

**Contract ids:** CON-021; CON-046

**Level:** concurrency integration

**Priority:** P0

**Given:** Native subtree includes tasks, links, assets, and a frozen output destination.

**When:** Copy/move/transfer under supported policy while running.

**Then:** Explicit identity/remapping and origin rules; no retargeting based on current parent.

**Fault injection:** Late completion after move.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-079

**Title:** Retain files, delete owned files, and legacy payload

**Module ids:** MOD-STRUCTURE; BND-STORAGE; BND-API

**Contract ids:** CON-019; CON-043

**Level:** integration + UI + tool

**Priority:** P0

**Given:** Branch has managed content and an old durable deletion payload.

**When:** Exercise both dispositions and process the old payload.

**Then:** Explicit choice affects correct files; Unspecified is rejected and legacy meaning is preserved.

**Fault injection:** Retry with changed disposition produces conflict.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-080

**Title:** Shared-file references and final deletion

**Module ids:** MOD-STRUCTURE; MOD-RESOURCES; BND-STORAGE

**Contract ids:** CON-022; CON-043

**Level:** PostgreSQL + storage concurrency

**Priority:** P0

**Given:** Two valid bindings reference the same content.

**When:** Delete one, then the last under delete-owned policy.

**Then:** First preserves bytes; final deletion requires ownership and liveness checks.

**Fault injection:** New binding races cleanup.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-081

**Title:** Multi-root deletion reports partial outcomes

**Module ids:** MOD-STRUCTURE; BND-STORAGE; BND-API

**Contract ids:** CON-019; CON-043

**Level:** failure injection + UI

**Priority:** P0

**Given:** Three independent roots include one ownership failure.

**When:** Batch-delete with explicit disposition.

**Then:** Completed roots and safe failures are reported; repeat does not roll back successes; continuation policy is preserved.

**Fault injection:** Unexpected exception after the first commit.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-082

**Title:** Confirmation cannot bypass protected run roots

**Module ids:** MOD-STRUCTURE; MOD-PROCESSES; BND-STORAGE

**Contract ids:** CON-028; CON-043; CON-052

**Level:** security integration

**Priority:** P0

**Given:** Active/waiting process owns an artifact root.

**When:** Agent/admin confirms destructive deletion with a forged path.

**Then:** Provenance and liveness protection still applies; a projected branch may only be hidden.

**Fault injection:** Wrong RunId, traversal, and external-target alias.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-083

**Title:** Project deletion participants and recovery

**Module ids:** MOD-PROJECTS; MOD-CRM; MOD-STRUCTURE; MOD-RESOURCES; MOD-TESTLAB

**Contract ids:** CON-016; CON-046

**Level:** failure injection

**Priority:** P0

**Given:** Several owners have project-dependent data.

**When:** Delete, crash during cleanup, then recover.

**Then:** Each owner handles its data; operation is discoverable; a partial inaccessible project is not reported complete.

**Fault injection:** Producer adds a node during Deleting.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-084

**Title:** Outbox rollback and commit without publication

**Module ids:** BND-COMPOSITION; MOD-CRM; MOD-AGENTS

**Contract ids:** CON-012; CON-045

**Level:** PostgreSQL failure injection

**Priority:** P0

**Given:** Owner saves state and event together.

**When:** Roll back before commit; separately crash after commit before dispatch.

**Then:** Rolled-back changes are not published; committed event is eventually delivered; no silent loss.

**Fault injection:** Lost publish acknowledgement causes a safely handled duplicate.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-085

**Title:** Inbox marker is atomic with projection update

**Module ids:** MOD-CRM; MOD-STRUCTURE; BND-COMPOSITION

**Contract ids:** CON-012; CON-032

**Level:** PostgreSQL failure injection

**Priority:** P0

**Given:** Duplicate event updates a materialized view.

**When:** Crash between marker insert and projection update, in either order.

**Then:** Rollback/retry applies the projection once; marker cannot suppress a missing effect.

**Fault injection:** Parallel subscribers process the same event identity.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-086

**Title:** Out-of-order deltas and sequence gaps

**Module ids:** MOD-STRUCTURE; MOD-CRM; MOD-MEMORY

**Contract ids:** CON-012; CON-032; CON-039

**Level:** integration

**Priority:** P0

**Given:** Source emits revisions n, n+1, n+2.

**When:** Deliver n+2 before n+1 and replay n.

**Then:** Preserve snapshot/delta semantics; do not discard needed deltas without safe recovery.

**Fault injection:** Expired event retention requires resnapshot, not silently incomplete current data.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-087

**Title:** Projection rebuild during concurrent editing

**Module ids:** MOD-STRUCTURE; MOD-CRM; BND-COMPOSITION

**Contract ids:** CON-032; CON-045

**Level:** PostgreSQL concurrency

**Priority:** P0

**Given:** New materialized-view generation is being built.

**When:** Edit/delete source entities and a native note concurrently.

**Then:** Snapshot/cursor/reconciliation retains updates; note is untouched; read switch is atomic or explicitly partial.

**Fault injection:** Lost cursor or incarnation mismatch.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-088

**Title:** Tombstone and old events

**Module ids:** MOD-STRUCTURE; MOD-CRM; MOD-MEMORY

**Contract ids:** CON-012; CON-032; CON-039

**Level:** integration

**Priority:** P0

**Given:** Source agent/resource was deleted or archived.

**When:** Replay an old update and rebuild/refresh cache.

**Then:** No source/access resurrection; historical reference follows retention policy.

**Fault injection:** Detect tombstone retention shorter than the supported replay window.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-089

**Title:** Migration preserves data and concurrency stamping

**Module ids:** BND-COMPOSITION; MOD-AGENTS; MOD-CRM; MOD-STRUCTURE

**Contract ids:** CON-002; CON-024; CON-056

**Level:** PostgreSQL migration

**Priority:** P0

**Given:** Representative old DB contains real tokens, constraints, and audit.

**When:** Upgrade to bounded owner contexts, read, and attempt a stale concurrent update.

**Then:** Preserve identity/data/hash/constraints; tokens advance correctly and conflicts work.

**Fault injection:** Mapping omits IHasConcurrencyToken stamping.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-090

**Title:** Subset host cannot generate destructive migrations

**Module ids:** BND-COMPOSITION

**Contract ids:** CON-048; CON-056

**Level:** schema/architecture

**Priority:** P0

**Given:** Canonical migration model includes all tables; sandbox only a subset.

**When:** Compare design-time models and schema differences.

**Then:** Only the complete model is migration authority; no unintended DropTable from a subset.

**Fault injection:** Missing entity/configuration assembly.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-091

**Title:** Runtime contexts do not import foreign entities via navigation

**Module ids:** BND-COMPOSITION; MOD-CRM; MOD-STRUCTURE; MOD-AGENTS

**Contract ids:** CON-001; CON-010; CON-018

**Level:** architecture + EF model

**Priority:** P0

**Given:** Contexts have explicit mappings.

**When:** Inspect actual IEntityType models and all write paths.

**Then:** No foreign-master writes or global registry scan; ExcludeFromMigrations is not accepted as write protection.

**Fault injection:** Navigation discovery introduces a foreign type.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-092

**Title:** Single-writer cutover and rollback

**Module ids:** BND-COMPOSITION; MOD-CRM; BND-WORK

**Contract ids:** CON-024; CON-046; CON-056

**Level:** migration + failure injection

**Priority:** P0

**Given:** Legacy and new adapters coexist during migration.

**When:** Switch writer, mutate through the old path, and exercise rollback.

**Then:** Legacy delegates or blocks, never dual-writes; rollback preserves compatible schema/data and one writer.

**Fault injection:** Old worker accepts queued work after the switch.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-093

**Title:** Storage/DB compensation and orphan cleanup

**Module ids:** MOD-STRUCTURE; BND-STORAGE

**Contract ids:** CON-022; CON-043

**Level:** failure injection

**Priority:** P0

**Given:** Staging/finalization and DB binding are separate steps.

**When:** Fail at every intermediate boundary, retry, and run cleanup.

**Then:** Truthful content states; referenced bytes survive; bounded orphan recovery uses provenance.

**Fault injection:** Cleanup races new finalization/binding.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-094

**Title:** Content-edit optimistic concurrency

**Module ids:** MOD-STRUCTURE; BND-STORAGE; MOD-TESTLAB

**Contract ids:** CON-022; CON-043

**Level:** storage integration

**Priority:** P1

**Given:** Two edits start at V1; test evidence references V1.

**When:** Save V2, then submit the stale second write.

**Then:** Conflict without content loss; metadata edit does not replace bytes; evidence remains V1.

**Fault injection:** Viewer keeps a stale cache after update.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-095

**Title:** Two circuits do not share a tracked context

**Module ids:** BND-CONVERSATIONS; BND-COMPOSITION; MOD-STRUCTURE

**Contract ids:** CON-018; CON-019

**Level:** concurrency + UI

**Priority:** P0

**Given:** Two browser circuits and parallel loads exist.

**When:** Read/edit different scopes.

**Then:** Per-operation contexts prevent tracking/user leakage; results respect source generation.

**Fault injection:** Slow old load finishes after a newer load.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-096

**Title:** DI unavailability versus false success

**Module ids:** BND-COMPOSITION; MOD-WORKSPACE; MOD-AGENTS; MOD-SECURITY

**Contract ids:** CON-007; CON-042; CON-048

**Level:** composition integration

**Priority:** P0

**Given:** Host lacks an optional reader or a required writer/security service.

**When:** Resolve and invoke actions.

**Then:** Optional absence is transparent; required writes/start fail closed or invalidate startup; Noop cannot report committed.

**Fault injection:** Change real/noop registration order; inspect both single and enumerable resolution.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-097

**Title:** Backup restore and data incarnation

**Module ids:** BND-COMPOSITION; MOD-STRUCTURE; MOD-PROCESSES; MOD-PROVIDERS

**Contract ids:** CON-021; CON-048; CON-056

**Level:** restore integration

**Priority:** P0

**Given:** Backup contains outbox, checkpoints, and receipts from another continuity.

**When:** Run old cache/worker/replay against the restore.

**Then:** Incarnation fence and recovery policy prevent cross-history writes/duplicate external effects.

**Fault injection:** Identical source sequence and ProjectId in the new database.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-098

**Title:** URLs, dialogs, and desktop shell

**Module ids:** MOD-STRUCTURE; MOD-AGENTS; MOD-CRM; BND-CONVERSATIONS

**Contract ids:** CON-044; CON-045

**Level:** desktop UI

**Priority:** P1

**Given:** Existing deep links, tab/dialog selection, and floating overlay are supported.

**When:** Reload/back/forward, open details, and exercise focus after refactoring.

**Then:** Preserve existing bookmark states, dialogs, z-index/focus, and navigation; do not invent mobile requirements.

**Fault injection:** Stale entity ID produces safe unavailable without breaking the shell.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-099

**Title:** Pagination and batch queries avoid N+1

**Module ids:** MOD-AGENTS; MOD-CRM; MOD-STRUCTURE; MOD-PROJECTS; MOD-RESOURCES

**Contract ids:** CON-001; CON-010; CON-015; CON-018

**Level:** performance characterization

**Priority:** P1

**Given:** Representative production-scale catalog/structure.

**When:** Page/search/scroll/filter and reload a compound projection.

**Then:** Deterministic order/coverage and measured queries; no new per-entity query pattern.

**Fault injection:** Slow/unavailable owner cannot be disguised as an empty current projection.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-100

**Title:** Build/hot-reload improvement is measured

**Module ids:** BND-COMPOSITION; BND-CONVERSATIONS; MOD-AGENTS

**Contract ids:** CON-003; CON-044

**Level:** build graph + developer-loop measurement

**Priority:** P1

**Given:** Same recorded hardware/configuration and supported baseline host.

**When:** Measure transitive graph and cold/warm edit loop after extraction.

**Then:** Evidence of improvement/non-regression with preserved behavior; no guessed percentages or partial-class quotas.

**Fault injection:** UI still transitively references runtime hosting.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-101

**Title:** Unknown source/capability is not an empty catalog

**Module ids:** MOD-CRM; MOD-STRUCTURE; MOD-PROVIDERS

**Contract ids:** CON-001; CON-005; CON-032

**Level:** integration

**Priority:** P0

**Given:** Provider owner or projection source is unavailable.

**When:** Open planning/view and attempt a source-dependent mutation.

**Then:** UI distinguishes stale/unavailable; mutation makes a current decision; no replacement master.

**Fault injection:** Unsupported source schema version.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-102

**Title:** Exports, logs, and receipts exclude secrets

**Module ids:** MOD-SECURITY; MOD-PROVIDERS; MOD-PLUGINS; MOD-CRM; BND-API

**Contract ids:** CON-009; CON-042; CON-046

**Level:** security inspection + integration

**Priority:** P0

**Given:** Synthetic canary secret and confidential data exist in authorized stores.

**When:** Trigger errors, export, quote, usage, UI snapshots, and tool receipts.

**Then:** Canary is absent from unauthorized outputs/logs; redaction preserves safe correlation.

**Fault injection:** Provider error contains a token/URL or serializer captures the entire configuration.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-103

**Title:** Retry and dead-letter operational visibility

**Module ids:** BND-COMPOSITION; MOD-STRUCTURE; BND-CONNECTORS

**Contract ids:** CON-021; CON-041; CON-045

**Level:** failure injection

**Priority:** P1

**Given:** Required message repeatedly fails.

**When:** Exhaust bounded retries and reconcile as an operator.

**Then:** Durable pending/failed status, correlation, and safe actionable reason; required effect is not discarded as success.

**Fault injection:** Drop transient status notifications.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-104

**Title:** Client disconnect and unknown commit

**Module ids:** BND-API; MOD-STRUCTURE; MOD-AGENTS

**Contract ids:** CON-002; CON-021

**Level:** HTTP/tool failure injection

**Priority:** P0

**Given:** Request has a valid stable OperationId.

**When:** Disconnect during commit and later query/retry.

**Then:** Durable receipt resolves Unconfirmed without false cancellation/rollback or duplicate write.

**Fault injection:** Commit acknowledgement times out after actual commit.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-105

**Title:** Non-idempotent external effect

**Module ids:** BND-CONNECTORS; MOD-PLUGINS; MOD-PROCESSES

**Contract ids:** CON-041; CON-028

**Level:** adapter failure injection

**Priority:** P0

**Given:** Remote API has neither deduplication nor reliable status lookup.

**When:** Lose response after a possible effect.

**Then:** NeedsReconciliation/uncertainty; automatic retry cannot claim exactly one effect.

**Fault injection:** Redeliver the local outbox message.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-106

**Title:** Atomic assignment/reservation shares the connection

**Module ids:** BND-WORK; MOD-CRM; BND-COMPOSITION

**Contract ids:** CON-013; CON-024

**Level:** PostgreSQL integration

**Priority:** P0

**Given:** Coordinated operation spans two contexts.

**When:** Fail a constraint in the second owner, then retry safely.

**Then:** Both effects roll back using the same DbConnection/DbTransaction and a correct overall receipt.

**Fault injection:** Test detects equal connection strings on different connections.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-107

**Title:** Project hierarchy owner and cycles

**Module ids:** MOD-PROJECTS; MOD-STRUCTURE

**Contract ids:** CON-017; CON-020

**Level:** integration + UI

**Priority:** P1

**Given:** Projects DAG is displayed by Structure.

**When:** Change hierarchy through the owner; attempt cycle or generic-link overwrite.

**Then:** One Projects authority with Structure refresh and correct cycle/cardinality policy.

**Fault injection:** Two concurrent edges jointly form a cycle.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-108

**Title:** Identity during rename, copy, and transfer

**Module ids:** MOD-STRUCTURE; MOD-PROJECTS; MOD-RESOURCES

**Contract ids:** CON-019; CON-046; CON-056

**Level:** integration

**Priority:** P0

**Given:** NodeKeys/source references are used in URLs, receipts, and export.

**When:** Rename and perform supported move/copy/transfer.

**Then:** Rename retains identity; copy/remapping follows explicit policy; historical receipts reference origin.

**Fault injection:** Name/path collision after rename.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-109

**Title:** Recruiting and onboarding effects

**Module ids:** MOD-CRM; MOD-AGENTS

**Contract ids:** CON-011; CON-002; CON-012

**Level:** UI + integration

**Priority:** P1

**Given:** Application includes interviews, lifecycle/conversion, and personnel data.

**When:** Execute supported conversion/onboarding and reload.

**Then:** Preserve owner records, audit/index/activity, and dialogs; create a technical agent only when explicitly requested.

**Fault injection:** Cross-domain partial failure remains discoverable.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-110

**Title:** CRM account/opportunity/interaction lifecycle

**Module ids:** MOD-CRM

**Contract ids:** CON-010; CON-011

**Level:** UI + integration

**Priority:** P1

**Given:** Account, opportunity participants/stage history, and scoped interactions exist.

**When:** Mutate and query through UI/API with pagination.

**Then:** State, history, relationships, and effects match baseline; unaffected features are not dropped behind a new API.

**Fault injection:** Conflict or permission denial on a related party.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-111

**Title:** Workspace settings delegate to owners

**Module ids:** MOD-WORKSPACE; MOD-PROVIDERS; BND-COMPOSITION; MOD-SECURITY; BND-STORAGE

**Contract ids:** CON-006; CON-042; CON-047; CON-048

**Level:** UI + integration

**Priority:** P1

**Given:** Settings include storage, provider preference, profile health, and token administration.

**When:** Edit every actually available section.

**Then:** Correct owner commands, no foreign Workspace masters, preserved permissions and UI availability.

**Fault injection:** Missing provider catalog adapter or storage driver.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-112

**Title:** Resource connector is genuinely executable

**Module ids:** MOD-RESOURCES; BND-CONNECTORS; MOD-WORKSPACE

**Contract ids:** CON-035; CON-041

**Level:** integration

**Priority:** P1

**Given:** Settings/manifest describe supported and unsupported connector kinds.

**When:** Configure, test, use, and browse according to actual capability.

**Then:** Form is not false success; supported adapter executes; missing handler is unavailable.

**Fault injection:** Invalid schema, secret reference, or endpoint.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-113

**Title:** Projects phases/options/file portfolio

**Module ids:** MOD-PROJECTS; MOD-STRUCTURE; BND-STORAGE

**Contract ids:** CON-015; CON-016; CON-052

**Level:** UI + integration

**Priority:** P1

**Given:** Project has phases/options and read-only/editable portfolio roles.

**When:** Edit/read/search portfolio and open files/related projects.

**Then:** Preserve lifecycle/options validation, scope, and read-only policy without a full foreign EF graph.

**Fault injection:** Referenced phase/file scope was deleted.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-114

**Title:** Plugin installation and package activation

**Module ids:** MOD-PLUGINS; BND-COMPOSITION; BND-MAF

**Contract ids:** CON-040

**Level:** integration

**Priority:** P1

**Given:** Valid/invalid manifests and installed-disabled plugin with a host recipe.

**When:** Install, activate, disable, and restart through supported paths.

**Then:** Correct activation, manifest/grant validation, and runtime registration; no ad hoc page assembly loading.

**Fault injection:** Installation crash or unsupported version/capability.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-115

**Title:** Prompt Gallery picker/import/search/compatibility

**Module ids:** MOD-PROMPTS; MOD-AGENTS; BND-SIMPLECHATS

**Contract ids:** CON-033; CON-034

**Level:** UI + integration

**Priority:** P1

**Given:** Prompts have versions/tags/tokens and compatibility metadata.

**When:** Search/import and use picker/composer in agent and ordinary chat views.

**Then:** Preserve warnings, version selection, order, and correct product boundaries.

**Fault injection:** Malformed import or unavailable search projection yields explicit failure/coverage.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-116

**Title:** Memory profile setup and operation views

**Module ids:** MOD-MEMORY; MOD-SECURITY; BND-API

**Contract ids:** CON-038; CON-039; CON-042

**Level:** UI/API + integration

**Priority:** P1

**Given:** Supported memory profile and diagnostics exist.

**When:** Create/edit/probe/start, observe, and cancel where supported.

**Then:** Transport/persistence remains in the Memory owner; Web avoids UI entities and status is truthful.

**Fault injection:** Missing provider/configuration/credentials or revoked source.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-117

**Title:** Meeting/recording/transcript kinds and actions

**Module ids:** MOD-STRUCTURE; BND-STORAGE; MOD-CRM

**Contract ids:** CON-019; CON-020; CON-022

**Level:** characterization + UI

**Priority:** P1

**Given:** Current native kinds expose different metadata/actions.

**When:** Create/edit/link/open every actually offered kind, including participants.

**Then:** Preserve inputs and stored data; metadata scaffolds are not claimed as implemented recording/STT runners.

**Fault injection:** Unavailable action returns typed unavailable rather than a misleading generic button.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-118

**Title:** Plan analytics and manager summaries

**Module ids:** BND-WORK; MOD-STRUCTURE; MOD-PROJECTS; MOD-CRM

**Contract ids:** CON-015; CON-018; CON-026

**Level:** integration + UI

**Priority:** P1

**Given:** Plan mixes humans, AI, processes/workflows, and partial costs.

**When:** Change dates, assignments, and costs; refresh summaries.

**Then:** Correct coverage, source revisions, and sums; unknown is not zero; UI invalidation does not mutate master data.

**Fault injection:** Lost event or partially unavailable source.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-119

**Title:** Audit/index/lifecycle survives API extraction

**Module ids:** MOD-CRM; MOD-PROMPTS; MOD-RESOURCES; MOD-STRUCTURE; BND-API

**Contract ids:** CON-011; CON-019; CON-034; CON-035

**Level:** integration

**Priority:** P0

**Given:** Same operation is available from several surfaces.

**When:** Execute equivalent UI, HTTP, and tool mutations.

**Then:** All use the owner application service and consistent audit/effect policy, not a shortcut EF API path.

**Fault injection:** Audit/index failure after business commit has explicit outcome/recovery.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-120

**Title:** Bidirectional owner APIs avoid reentrant mutation loops

**Module ids:** MOD-CRM; BND-WORK; MOD-STRUCTURE; BND-WORKFLOWS

**Contract ids:** CON-013; CON-024; CON-021; CON-030; CON-055

**Level:** integration + concurrency

**Priority:** P0

**Given:** CRM changes an assignment needing reservation; workflow projector observes a run.

**When:** Mutate with callbacks/invalidation and concurrent refresh.

**Then:** Each owner performs only its effect; no repeated top-level command, deadlock, or launch caused by a query/projection.

**Fault injection:** Deliberately loop a callback into the same intent; detect the cycle rather than retry forever.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-121

**Title:** Receipt retention and restore cannot repeat external effects

**Module ids:** BND-COMPOSITION; MOD-STRUCTURE; MOD-AGENTS; MOD-PROVIDERS

**Contract ids:** CON-021; CON-048; CON-054; CON-056

**Level:** recovery + integration

**Priority:** P0

**Given:** Completed external effect is redelivered after retention or restore.

**When:** Retry the original intent after local incarnation changes.

**Then:** Find original receipt/provenance or report expired/unknown for reconciliation; do not rerun inference/create replacement assets by changing remote keys.

**Fault injection:** Remote provider lacks idempotency/status support: disclose uncertainty and require a safe decision.

**Execution status:** NOT_RUN

**Required evidence:** Baseline and post-change: actual discovered tests, observed outcome, owner data/receipt/revision, and the affected UI/API/tool result. Without this evidence the scenario is not PASS.

## QA-122

**Title:** Preserve existing HR CRM party creation

**Module ids:** MOD-AGENTS; MOD-CRM

**Contract ids:** CON-058; CON-063

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Current managed HR with CRM scope and allowed nonsensitive fields.

**When:** Invoke the real existing party-create tool and reload CRM.

**Then:** One canonical Party, correct owner audit/activity and safe returned identity; no second Agents writer.

**Fault injection:** Owner rejects validation or CRM fails after admission; tool must retain truthful outcome.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-123

**Title:** Preserve affiliation limits and restricted fields

**Module ids:** MOD-AGENTS; MOD-CRM

**Contract ids:** CON-059

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Existing affiliation and restricted HR facts.

**When:** List and upsert affiliation through actual HR tool.

**Then:** CRM owns the update; restricted fields remain unchanged and endpoints/roles validated.

**Fault injection:** Attempt to smuggle confidential or workforce fields through affiliation metadata.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-124

**Title:** Operational agent queries CRM without impersonating HR

**Module ids:** MOD-AGENTS; MOD-CRM; BND-WORK

**Contract ids:** CON-057; CON-023

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Ordinary task agent receives only a safe planning-query grant.

**When:** Resolve a person and create an authorized task.

**Then:** Useful scoped lookup works; protected HR management stays unavailable.

**Fault injection:** Rename the ordinary agent to HR or submit the real HR AgentId in tool input.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-125

**Title:** Query-only capability cannot create a contact

**Module ids:** MOD-AGENTS; MOD-CRM

**Contract ids:** CON-057; CON-058

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Actor has CRM summary read but no create grant.

**When:** Read a permitted person then request party creation.

**Then:** Read succeeds and mutation is denied without a new Party.

**Fault injection:** Use a forged descriptor or direct adapter call to bypass tool-list filtering.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-126

**Title:** CRM provider-disclosure restriction

**Module ids:** MOD-AGENTS; MOD-CRM

**Contract ids:** CON-057

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Actor can view internal personnel data but selected external model is not allowed to receive it.

**When:** Request the agent to summarize those data.

**Then:** Enforced disclosure policy withholds/denies sensitive content; no provider request contains the canary.

**Fault injection:** Cached administrator query result or prompt text asks to ignore data-use policy.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-127

**Title:** Source read cannot launder confidential data into a task

**Module ids:** MOD-AGENTS; MOD-CRM; BND-WORK

**Contract ids:** CON-057; CON-023

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Actor can read a confidential HR detail; project task audience is broader.

**When:** Attempt to write the detail or an equally identifying summary into a task.

**Then:** Destination disclosure policy blocks or requires an authorized safe transformation; no silent declassification.

**Fault injection:** Embed the confidential text in a title, attachment, or generated summary.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-128

**Title:** Ambiguous person identity and stale eligibility

**Module ids:** MOD-AGENTS; MOD-CRM; BND-WORK

**Contract ids:** CON-057; CON-024

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Two people share a name; selected candidate is later deactivated.

**When:** Resolve by typed reference and assign after deactivation.

**Then:** No guessing by name; current assignment eligibility is validated and stale use rejected or explicitly handled.

**Fault injection:** Identical IDs in another profile or misleading display label.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-129

**Title:** HR reads safe Simple Chats definition settings

**Module ids:** MOD-AGENTS; BND-SIMPLECHATS

**Contract ids:** CON-061

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Authorized managed actor and definition with confidential unrelated conversations.

**When:** Search definitions and retrieve permitted editable settings.

**Then:** Only allowed definition fields/token are returned, with bounded pagination and no transcripts.

**Fault injection:** Attempt to use a conversation ID, cached full entity, or another scope.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-130

**Title:** HR creates a Simple Chats definition through its owner

**Module ids:** MOD-AGENTS; BND-SIMPLECHATS

**Contract ids:** CON-062

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** New adapter and proven owner-atomic command receipt are implemented in the scoped change.

**When:** Approve typed definition creation and inspect normal Simple Chats UI.

**Then:** One ordinary definition/revision; current owner service performs persistence; no agent runtime activation.

**Fault injection:** Disallow provider/model or submit unsupported field; no partial completed definition.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-131

**Title:** Simple Chats definition update uses concurrency

**Module ids:** MOD-AGENTS; BND-SIMPLECHATS

**Contract ids:** CON-061; CON-062

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** HR read token V1; human saves V2.

**When:** Submit approved V1 update.

**Then:** Conflict without automatic full-form overwrite; human V2 and revision history survive.

**Fault injection:** Adapter rereads V2 and silently resubmits stale changes; test must catch it.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-132

**Title:** Simple Chats approval binds exact payload

**Module ids:** MOD-AGENTS; BND-SIMPLECHATS

**Contract ids:** CON-062

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Definition update has approved provider, prompt, and expected version.

**When:** Change model, system prompt, target, or status after approval.

**Then:** Old approval is rejected; materially revised intent requires new review.

**Fault injection:** Only approval ID is checked while payload fingerprint is omitted.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-133

**Title:** Simple Chats definition administration does not grant transcript access

**Module ids:** MOD-AGENTS; BND-SIMPLECHATS

**Contract ids:** CON-061; CON-062; CON-049

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** HR can edit a definition, but has no conversation grant.

**When:** Try to enumerate/read/edit/delete transcripts or inject a user message.

**Then:** These operations are unavailable/denied; definition administration still works.

**Fault injection:** Use shared UI state or definition service response to reach conversation stores.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-134

**Title:** Ordinary product remains dependency- and tool-free

**Module ids:** MOD-AGENTS; BND-SIMPLECHATS; BND-CONVERSATIONS

**Contract ids:** CON-062; CON-049

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** External HR adapter manages a definition used by floating ordinary chat.

**When:** Build/evaluate dependency graph and send an ordinary turn.

**Then:** Product/persistence has no new Agent Core/MAF/tools/process references; ordinary runtime has no tools/implicit context.

**Fault injection:** Shared conversation component retains an earlier agent tool configuration.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-135

**Title:** Definition edit does not rewrite admitted turns

**Module ids:** MOD-AGENTS; BND-SIMPLECHATS

**Contract ids:** CON-062; CON-049

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Ordinary turn was admitted using a definition revision.

**When:** Update or change definition status through HR during dispatch.

**Then:** Existing owner pinning/status policy and historical transcript remain; no re-execution or silent config replacement.

**Fault injection:** Delayed provider dispatch uses the mutable latest definition instead of admitted settings.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-136

**Title:** Simple Chats create receipt is atomic with effect

**Module ids:** MOD-AGENTS; BND-SIMPLECHATS

**Contract ids:** CON-062

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Two independent instances submit the same legitimate create intent.

**When:** Crash after definition insert at every receipt/commit boundary, then retry.

**Then:** Exactly one local canonical definition and recoverable receipt, or explicit uncertain status without unsafe retry if guarantee is unavailable.

**Fault injection:** Post-hoc integration receipt implementation should fail this test.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-137

**Title:** Intent collision and changed-payload protection

**Module ids:** MOD-AGENTS; MOD-CRM; BND-SIMPLECHATS

**Contract ids:** CON-058; CON-062

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** A contact/chat create has a committed scoped intent.

**When:** Reuse the key with changed payload, another principal, or another owner.

**Then:** Correct conflict/isolation; no disclosure or overwrite of another intent.

**Fault injection:** Use equal display names and locally colliding profile IDs.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-138

**Title:** Replay after deletion or later manual edit

**Module ids:** MOD-AGENTS; MOD-CRM; BND-SIMPLECHATS

**Contract ids:** CON-058; CON-062

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Created contact/chat is later edited, archived, or deleted by a user.

**When:** Replay the original create intent.

**Then:** Historical receipt is distinct from current state; no resurrection or overwrite.

**Fault injection:** Producer changes keys to bypass suppression of the same managed intent.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-139

**Title:** Workflow creates CRM data without an open page

**Module ids:** BND-WORKFLOWS; MOD-CRM

**Contract ids:** CON-058; CON-072

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Registered typed CRM executor, bounded run authority, and approval where required.

**When:** Run headlessly and create a permitted contact.

**Then:** CRM owns the effect; workflow stores its receipt and scope without a Razor dependency.

**Fault injection:** Missing approval channel or revoked run delegation blocks rather than autoapproves.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-140

**Title:** Process step writes a Resource through the owner

**Module ids:** MOD-PROCESSES; MOD-RESOURCES; BND-STORAGE

**Contract ids:** CON-070; CON-073

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Typed Resource step has authorized content and original scope.

**When:** Create a Resource and checkpoint its identity.

**Then:** Resources owns metadata, Storage bytes, process its checkpoint; explicit content state.

**Fault injection:** Provider succeeds but metadata commit fails; reconcile without orphan deletion of shared content.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-141

**Title:** Workflow checkpoint gap after foreign commit

**Module ids:** BND-WORKFLOWS; MOD-CRM

**Contract ids:** CON-058; CON-072

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Owner command intent was persisted before dispatch.

**When:** Commit Party, crash before workflow checkpoint, restart.

**Then:** Recovery queries same owner receipt and records existing Party; no duplicate create.

**Fault injection:** Regenerate a new model tool-call ID during replay.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-142

**Title:** Process loop iteration versus retry identity

**Module ids:** MOD-PROCESSES; MOD-CRM

**Contract ids:** CON-058; CON-073

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Process intentionally creates two contacts in distinct loop iterations.

**When:** Retry one iteration and execute the next.

**Then:** Retry deduplicates its original effect while legitimate next iteration has its own effect identity.

**Fault injection:** Key ignores iteration or includes retry counter; detect lost/duplicated work.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-143

**Title:** Background adapter cannot borrow interactive managed identity

**Module ids:** BND-WORKFLOWS; MOD-PROCESSES; MOD-AGENTS

**Contract ids:** CON-063; CON-072; CON-073

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Current HR provider only supports InteractiveChat.

**When:** Try attaching it to a background executor with spoofed HR identity.

**Then:** Denied/unavailable; legitimate separate owner-operation adapter can work under its own approved delegation.

**Fault injection:** Set purpose or actor in untrusted workflow JSON.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-144

**Title:** Revocation between approval and resumed dispatch

**Module ids:** MOD-AGENTS; MOD-CRM; BND-WORKFLOWS

**Contract ids:** CON-058; CON-072

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Approved contact command waits across restart.

**When:** Revoke actor/grant/scope before resume.

**Then:** Current owner authorization blocks mutation; approval is not indefinite authority.

**Fault injection:** Keep old descriptor and approval caches warm.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-145

**Title:** Required foreign effect remains a visible obligation

**Module ids:** MOD-PROCESSES; MOD-CRM

**Contract ids:** CON-058; CON-073

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Technical process work succeeded but required CRM effect is unavailable.

**When:** Exhaust retries then inspect run/task state.

**Then:** Required delivery remains blocked/pending or explicitly failed; no false completed business outcome or repeated inference.

**Fault injection:** Drop transient notifications and restart worker.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-146

**Title:** Composite failure does not delete pre-existing data

**Module ids:** MOD-AGENTS; MOD-CRM; BND-WORK

**Contract ids:** CON-058; CON-024

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Onboarding uses an existing contact and a newly created agent; task assignment then fails.

**When:** Reconcile or compensate the composite.

**Then:** Preserve existing contact and subsequent human edits; only explicit owner-authorized compensation is considered.

**Fault injection:** Implement a naive reverse-order delete rollback.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-147

**Title:** Scheduler workflow-only managed surface is preserved

**Module ids:** MOD-SCHEDULER; MOD-AGENTS

**Contract ids:** CON-066; CON-067

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Current managed Scheduler provider and actual host registration.

**When:** Search workflows/schedules and create an approved workflow schedule.

**Then:** Supported positive paths work; no saved input JSON disclosure or invented direct process schedule tool.

**Fault injection:** Request unsupported target kind or remove its runtime implementation.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-148

**Title:** Schedule dispatch authority survives browser departure safely

**Module ids:** MOD-SCHEDULER; BND-WORKFLOWS

**Contract ids:** CON-037; CON-067

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Recurring plan has explicit target/version and bounded approved execution policy.

**When:** Close UI, revoke relevant authority, then fire.

**Then:** Dispatch uses durable principal and current policy, not browser identity or unlimited prior approval.

**Fault injection:** Target or input revision changes after plan review.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-149

**Title:** Staffing edits cannot change technical agents or AI prices

**Module ids:** MOD-CRM; MOD-AGENTS; MOD-PROVIDERS; BND-WORK

**Contract ids:** CON-060; CON-024; CON-005

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Actor may update selected CRM staffing fields.

**When:** Edit availability, then attempt provider tariff, agent instruction, confidential field, or task performer through same patch.

**Then:** Allowed staffing edit succeeds; foreign/forbidden fields are rejected/delegated under separate grants.

**Fault injection:** Open JSON properties or alternate API path bypass the allowlist.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-150

**Title:** HR technical administration cannot self-escalate

**Module ids:** MOD-AGENTS

**Contract ids:** CON-063; CON-065

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Managed HR actor has safe administration and catalog access.

**When:** Attempt self/protected-target edit or privileged capability grant.

**Then:** Current protected rules remain enforced; admitted authority cannot widen through definition editing.

**Fault injection:** Create a child/template with forbidden capability then ask it to execute.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-151

**Title:** Curator changes remain owner-versioned

**Module ids:** MOD-AGENTS; MOD-PROMPTS; BND-WORKFLOWS

**Contract ids:** CON-034; CON-064; CON-065

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Prompt/workflow/capability curators with their actual grants.

**When:** Propose and approve supported edits while an admitted run uses the old version.

**Then:** Owner creates validated revisions; admitted run remains pinned; catalog editing does not grant execution.

**Fault injection:** Concurrent human edit or altered proposal after approval.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-152

**Title:** Resources metadata is not an unrestricted content grant

**Module ids:** MOD-AGENTS; MOD-RESOURCES; BND-STORAGE

**Contract ids:** CON-069; CON-071

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Visible Resource metadata points to restricted or large content.

**When:** Request bytes through automation.

**Then:** Separate current content permission, bounded size/MIME, source version and disclosure policy; no raw locator/secret leak.

**Fault injection:** Root/ACL changes after metadata query or remote URL points to a disallowed endpoint.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-153

**Title:** Resource creation and promotion share the canonical owner

**Module ids:** MOD-AGENTS; MOD-RESOURCES; BND-STORAGE

**Contract ids:** CON-035; CON-070

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Existing content is already attached to Structure.

**When:** Create/promote Resource through automation, then remove one attachment.

**Then:** Canonical Resource metadata and shared-byte references; no duplicate store or premature cleanup.

**Fault injection:** Repeat same intent after commit and lose response.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-154

**Title:** Cross-module query results resist instruction injection

**Module ids:** MOD-AGENTS; MOD-CRM; MOD-RESOURCES

**Contract ids:** CON-057; CON-069; CON-071

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** CRM text/document says to skip approval, reveal secrets, or change actor.

**When:** Retrieve and process it in an authorized task flow.

**Then:** Text is treated as data; deterministic policy still denies unauthorized actions.

**Fault injection:** Malicious instruction is embedded in a label, tool result, or attachment.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-155

**Title:** Bounded queries preserve privacy across caches and pages

**Module ids:** MOD-AGENTS; MOD-CRM; BND-SIMPLECHATS

**Contract ids:** CON-057; CON-061

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Admin and limited actor share names/scopes but different field permissions.

**When:** Page/filter/get using both identities and reuse cursors.

**Then:** Stable bounded results with correct coverage and no confidential leakage through shared cache/cursor.

**Fault injection:** Replay a cursor or operation receipt from the broader actor.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-156

**Title:** Collaboration contribution respects the publication audience

**Module ids:** MOD-AGENTS; MOD-COLLAB

**Contract ids:** CON-074

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Actor can post to a thread but source content has narrower access.

**When:** Publish an automated summary/attachment.

**Then:** Membership plus source-to-destination policy applies; no agent/ordinary transcript substitution.

**Fault injection:** Thread membership changes after preparation or source is confidential.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-157

**Title:** Memory ingestion is derived evidence, not master truth

**Module ids:** MOD-AGENTS; MOD-MEMORY; MOD-CRM

**Contract ids:** CON-075; CON-057

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Agent creates an interpretation of CRM data with source references.

**When:** Ingest and later revoke/delete the source.

**Then:** Provenance/trust and current source access are enforced; ingestion does not update CRM facts.

**Fault injection:** Treat generated assertion as verified Party field or bypass revoked source via index.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-158

**Title:** TestLab evidence authoring cannot manufacture execution success

**Module ids:** MOD-AGENTS; MOD-TESTLAB

**Contract ids:** CON-076

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Agent has evidence-attachment grant but no verdict/acceptance authority.

**When:** Attach a report and state that tests passed.

**Then:** Owner stores legitimate evidence with target revision; no fabricated runner PASS or acceptance.

**Fault injection:** Missing evidence bytes or mismatch between tested and current target version.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-159

**Title:** Process assistant does not imply nonexistent tools

**Module ids:** MOD-PROCESSES; MOD-AGENTS

**Contract ids:** CON-068; CON-028

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Process assistant/template and HTTP operations exist.

**When:** Inspect actual direct tools and invoke only supported bridges/adapters.

**Then:** Accurate availability; retained positive bridge/UI/HTTP behavior; no imaginary Process provider.

**Fault injection:** Generate a tool name from an endpoint or prompt capability template.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-160

**Title:** Read-only and missing handlers fail truthfully

**Module ids:** BND-WORKFLOWS; MOD-PROCESSES; MOD-RESOURCES

**Contract ids:** CON-069; CON-070; CON-072; CON-073

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Descriptor exists but handler is missing or read-only.

**When:** Request a mutation from each claimed surface.

**Then:** Explicit unavailable/denied before effect; no empty-success Noop.

**Fault injection:** Host register order accidentally selects a fallback.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-161

**Title:** Cross-owner adapters do not introduce assembly cycles

**Module ids:** MOD-AGENTS; MOD-CRM; BND-SIMPLECHATS; BND-WORKFLOWS; MOD-PROCESSES

**Contract ids:** CON-062; CON-072; CON-073

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Scoped integration implementation is complete.

**When:** Evaluate actual transitive dependencies and EF models.

**Then:** Owners avoid foreign implementations/EF entities; neutral UI and Simple Chats retain forbidden-dependency boundaries.

**Fault injection:** Move all domain contracts/policies into neutral MAF to hide the cycle.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-162

**Title:** No query or callback reenters the original operation

**Module ids:** MOD-CRM; BND-WORK; MOD-SCHEDULER

**Contract ids:** CON-024; CON-013; CON-067

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Multi-owner orchestration publishes completion/invalidation.

**When:** Deliver duplicate/reentrant callbacks and invoke refresh concurrently.

**Then:** Distinct owner effects, finite interaction, stable receipts; no new original create/start.

**Fault injection:** Callback explicitly calls the top-level intent or event echoes back indefinitely.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-163

**Title:** Receipt replay after restore remains safe for foreign effects

**Module ids:** MOD-PROCESSES; MOD-CRM; BND-SIMPLECHATS

**Contract ids:** CON-058; CON-062; CON-073

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** A foreign create completed before backup/restore; old intent is queued.

**When:** Replay under new local incarnation or expired receipt retention.

**Then:** Reconcile original provenance or return expired/unknown; never default to a new create/remote key.

**Fault injection:** Remove the receipt but retain external target data.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-164

**Title:** Durable checkpoints minimize sensitive query data

**Module ids:** BND-WORKFLOWS; MOD-PROCESSES; MOD-CRM

**Contract ids:** CON-057; CON-072; CON-073

**Level:** owner/cross-owner integration plus actual affected runtime surface

**Priority:** P0

**Given:** Step reads safe and separately authorized confidential data.

**When:** Persist checkpoint, error, audit, and operator report.

**Then:** Store only necessary protected decision evidence; no broad raw CRM dump or secret in logs.

**Fault injection:** Serializer captures entire query object/context or provider exception.

**Execution status:** NOT_RUN

**Required evidence:** Run baseline and post-change where implemented; inspect actual tool/executor registration, owner records/revisions/receipts, authorization outcomes and durable recovery. An extension remains NOT_RUN until implemented and executed.

## QA-165

**Title:** Language and package traceability remain English

**Module ids:** BND-COMPOSITION

**Contract ids:** None

**Level:** package language and downstream engineering artifact inspection

**Priority:** P0

**Given:** English replacement foundation and a future scoped implementation task.

**When:** Inspect instructions, catalogs, templates, comments, and generated artifacts.

**Then:** Engineering prose is English; technical IDs and required product localization remain unchanged; no Czech execution instruction survives.

**Fault injection:** Retain the old START_HERE language instruction or untranslated hidden JSON notes.

**Execution status:** NOT_RUN

**Required evidence:** Inspect the produced package and downstream scoped engineering artifacts. Package mechanical checks are separate from application runtime proof.
