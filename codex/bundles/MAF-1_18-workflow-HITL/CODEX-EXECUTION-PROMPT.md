# Codex GPT-5.6 xhigh Execution Prompt

You are the senior C# implementation architect for this bundle.

Execute the bundle against `fyziktom/CanDoItAll`, branch `development`, beginning from the current checked-out repository state. The preparation baseline was commit `5cdf1666dbafdcea975909101c1854773f5f3556`; re-anchor if HEAD has advanced.

## Primary outcome

Upgrade CanDoItAll from Microsoft Agent Framework .NET 1.17 to 1.18 while preserving serial and governed tool behavior. Then replace the current non-resumable workflow human-in-the-loop implementation with native MAF request ports, persisted checkpoints, exact-version rehydration, crash-recoverable response processing, and an authorized API contract.

## Required sequence

1. Read the bundle root documents and repository-local instructions.
2. Run `python scripts/validate_bundle.py .`.
3. Execute SB00 and record the current baseline.
4. Execute SB01 and SB02 as an independently closable MAF upgrade wave.
5. Do not start SB03 until the upgrade wave passes focused proof.
6. Execute SB03–SB05 in dependency order.
7. Execute SB06 only after all focused gates pass.
8. Update bundle status and traceability after each subbundle.

## Hard constraints

- Keep concurrent tool invocation disabled. Explicitly set application-owned MAF options to serial behavior where supported.
- Do not enable `StoreInvocableFunctionCallsForFutureTurns`.
- Do not add a public parallel-tool setting.
- Do not implement pause/resume by throwing `WorkflowExternalRequestPendingException` and later restarting.
- Use native MAF `RequestPort`, streaming workflow events, `CheckpointManager`, `ICheckpointStore<JsonElement>`, and rehydration APIs after verifying their exact 1.18 signatures.
- Keep MAF types behind adapter projects.
- Do not mark the in-process backend durable.
- Do not duplicate the existing workflow pending-request and response endpoints.
- Never trust response body actor identity.
- Do not consume an external request irreversibly before there is a recoverable response operation.
- Do not claim arbitrary exactly-once side effects. Add stable invocation deduplication at the workflow-executor boundary and prove it with a side-effect probe.
- Never fall back to starting from initial input when a checkpoint is missing, corrupt, stale, or topologically incompatible.
- Do not run broad tests repeatedly.

## Important current facts to verify, not blindly assume

- Current versions are centralized in `src/MAF/MicrosoftAgentFramework.Packages.props`.
- Stable packages use `1.17.0`; A2A preview packages use `1.17.0-preview.260804.1`.
- Target versions are `1.18.0` and `1.18.0-preview.260818.1`.
- The explicit 1.18 breaking rename is from session isolation provider symbols to agent isolation provider symbols; no direct use was found during preparation.
- The current MAF in-process workflow backend advertises external requests but `SupportsExternalResponseResume = false`.
- The current API already exposes pending requests and response submission.
- The current compiler pauses HumanInput and approval flows by throwing an exception.
- Existing agent approval/session round-trip tests are valuable upgrade regressions.
- Existing lifecycle tests explicitly encode unsupported in-process resume and must be evolved, not merely deleted.

## Completion response

At completion, provide:

- commits or uncommitted diff scope, depending authorization;
- package versions actually resolved;
- breaking changes encountered and adaptations made;
- tool-concurrency policy proof;
- workflow HITL architecture implemented;
- API and persistence contract changes;
- migrations added;
- focused test commands, discovered counts, and results;
- broad gate result;
- remaining blockers and explicitly deferred experiments.

Do not report the bundle complete while any required traceability row lacks implementation and proof.
