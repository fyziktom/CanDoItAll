# Execution Plan

## Wave A — MAF 1.18 upgrade

### SB00 Re-anchor and baseline

Confirm current repository facts, instructions, package graph, custom chat-client composition, workflow/API/persistence paths, and focused baseline tests. Update the bundle when HEAD materially differs.

### SB01 Package and compile migration

Change only central MAF versions and compile adaptations caused by 1.18. Resolve real breaking changes. Produce a reviewable package-upgrade unit.

### SB02 Agent/tool behavior hardening

Make serial invocation an explicit CanDoItAll policy, verify custom pipelines, and run approval/session/usage/telemetry regressions. This closes the upgrade wave.

**Wave A exit gate:** package graph is 1.18, affected projects build, tool calls remain serial, and existing governed approval behavior passes.

## Wave B — Workflow HITL completion

### SB03 Native MAF request/checkpoint foundation

Replace exception-as-pause for new resumable workflows with native request ports and streaming execution. Add the MAF checkpoint adapter over a framework-neutral payload port. Implement start-to-wait behavior and in-memory/fake-store rehydration proof before database work.

### SB04 Persistent checkpoint and recovery state machine

Add EF persistence, response-operation claim/lease/idempotency, topology verification, stable invocation deduplication, exact-version rehydration, and crash recovery. Make the backend genuinely resume-capable while remaining non-durable.

### SB05 API governance and contract

Complete existing endpoints with typed JSON, authorization, validation, idempotency, auditing, status mapping, and operation/read-model status. Add integration tests.

### SB06 End-to-end proof and closure

Run realistic workflow cases, freeze source/schema, execute the broad gate once, update docs, audit every requirement, and produce closure evidence.

## Wave C — API sample and browser proof

### SB07 Standalone HITL API sample

1. Prove the current API contract against the live host and capture the failing generic
   HumanInput presentation case.
2. Scaffold the .NET 10 static-SSR sample and focused tests under
   `C:\programovani\dotnet\candoitall-sample-hitl-api`.
3. Add the file-backed SimWiki corpus/API, typed CanDoItAll client, workflow provisioner,
   conversation service, and authenticated SSE proxy.
4. Add the smallest safe Web projection repair demonstrated by step 1, with direct and HTTP
   contract tests. Do not expose raw `RequestJson`.
5. Start the authenticated CanDoItAll host, provision the stable workflow, and prove native
   HumanInput plus governed HTTP-read approvals through the public API.
6. Run focused builds/tests and Playwright direct-hit, retry-hit, and three-miss cases. Record
   sanitized proof under `proof/SB07` and close RQ-046 through RQ-054.

**Wave C exit gate:** the browser completes real workflows through API/SSE, all three search
outcomes are proven, safe request presentation is bounded, credentials are absent from client
traffic/proof, and focused validation is green.

## Commit/review boundaries

Recommended commit boundaries:

1. `build(maf): upgrade Microsoft Agent Framework to 1.18`
2. `test(maf): lock serial tool invocation and approval regressions`
3. `feat(workflows): compile native MAF external request checkpoints`
4. `feat(workflows): persist and recover HITL response operations`
5. `feat(api): authorize and expose resumable workflow HITL`
6. `docs(test): close MAF 1.18 and workflow HITL rollout`

Do not commit unless authorized. Even without commits, keep diffs separated by subbundle and record file lists.

## Migration rollout

1. Apply schema migration.
2. New workflow runs use native checkpoint protocol.
3. Existing terminal/non-HITL runs remain unchanged.
4. Existing legacy waiting runs are explicitly non-resumable.
5. Observe response operation failures and checkpoint payload growth.
6. Do not enable parallel tool execution during rollout.

## Rollback

Wave A can revert central versions and compile adaptations if no Wave B code depends on 1.18 APIs yet.

After Wave B schema deployment:

- code rollback must tolerate new tables/columns;
- do not destructively drop checkpoint/operation data;
- disable new response processing through service registration/config only if a safe switch already fits repository conventions;
- leave waiting runs inspectable;
- document compatibility between binary versions and checkpoint format versions.

## Progression rules

- SB01 cannot close with unresolved package downgrades.
- SB02 cannot close without a meaningful serial-order probe.
- SB03 cannot close with only metadata checkpoint tests.
- SB04 cannot close with only an in-memory store.
- SB05 cannot close with endpoint-only authorization.
- SB06 cannot begin before source and migration are frozen.
