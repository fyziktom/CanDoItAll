# Task 05 – Propagate child diagnostics and use ledger-based child artifact bridging

## Problem

Parent `prepare-solution-skeleton` reported only:

```text
Child process run ab4... is Blocked
```

It did not surface the child root cause:

```text
process.adapter.product_required_file_content_missing
Calculator.slnx does not contain src/Calculator/Calculator.csproj
```

Also, `ParentSubprocessArtifactBridge.TryResolveChildOutputRefs` checks physical file existence under `artifacts/process-runs/{child}/steps/{step}.md`, not whether the child runtime accepted produced artifact slots.

## Implementation

### Part A – Child stopped blocked result

1. Extend `ParentSubprocessArtifactBridgeResultKind` with a stopped blocked/failed result, for example:

```csharp
ChildStoppedBlocked
ChildStoppedFailed
```

2. In `ParentSubprocessArtifactBridge.ResolveExistingAsync`, do not `continue` when stopped child is not completed. Load latest child state/receipt diagnostics.
3. Return result with:
   - child run id,
   - child status,
   - current/blocked child step key,
   - child step instance id,
   - child diagnostic code(s),
   - child safe summary,
   - child recovery decision.
4. Parent adapter creates diagnostic:

```text
process.adapter.subprocess_child_blocked
```

5. Parent safe summary must include the child concrete blocker.

### Part B – Ledger/slot-based accepted output bridge

1. Use process artifact ledger / produced artifact slots as primary evidence source.
2. Accepted child output is valid only when runtime receipt says it was produced/accepted.
3. File existence fallback must be clearly marked as recovery/fallback and should not silently bridge a child artifact rejected by completion gates.
4. If child physically wrote `steps/create-dotnet-project.md` but `ProducedArtifactsJson` is empty, parent must not accept it as valid child output.

### Part C – Template schema direction

Move hardcoded accepted/no-go child output mapping out of `ProcessSubprocessContractResolver` into template schema. Keep resolver fallback only temporarily.

## Acceptance criteria

For the incident:

- parent diagnostic includes child diagnostic code `process.adapter.product_required_file_content_missing`,
- parent diagnostic includes child run id and child step id/key,
- UI/operator packet no longer makes the user inspect the child blindly,
- parent does not claim there is no actionable AgentFramework result without explaining this is a runtime-owned subprocess parent,
- physical child artifact rejected by runtime is not accepted through bridge.

## Regression tests

```text
ParentSubprocessBridge_returns_child_stopped_blocked_with_latest_child_diagnostics
ParentSubprocessBridge_does_not_skip_blocked_child_runs
ParentSubprocessBridge_prefers_ledger_produced_artifacts_over_file_existence
ParentSubprocessBridge_does_not_accept_rejected_child_markdown_artifact
ParentProjection_shows_child_root_cause_when_parent_has_no_agentframework_run
```
