# Neuro Patch Test Plan

## Test Groups

### 1. Cognitive Workspace Tests

- Create workspace frame for probe session.
- Create workspace frame for MAF workflow run.
- Add focus slots and verify context budget enforcement.
- Add inhibited candidates and verify recall trace includes inhibition reason.
- Expired workspace frame does not become durable source truth.
- Important workspace frame can be persisted as episodic source input with policy.

### 2. Attention Router Tests

- Ambiguous query routes to clarification.
- Source-sensitive query routes to source audit.
- Weak topic routes to probe before learning.
- Sufficient workspace routes to answer from workspace.
- High-risk unsupported procedure routes to review or abstention.
- Routing decision includes score vector, matched shape, scalar projection, missing dimensions, and explanation.

### 3. Claim/Evidence/Belief Tests

- One memory item can contain multiple claims with different belief states.
- Claim with supporting and attacking evidence becomes contested.
- Claim with source version change becomes stale or needs review.
- Unsupported generated summary cannot be promoted.
- Evidence anchor stores source item id, structured path, text span, quote hash, trust level, and redaction state.
- Claim-level contradiction is visible even when memory item summary is fluent.

### 4. Mutation Authority Tests

- Direct public upsert path is not exposed in architecture acceptance.
- Duplicate mutation command with same idempotency key is idempotent.
- Mutation with stale version token is rejected or sent to review.
- High-risk claim mutation requires human review.
- Mutation invalidates relevant projection records after durable write.
- Audit event includes actor, evidence, policy decision, and timestamps.

### 5. Entity/Context Binding Tests

- Production Docker and test Docker are related but not substitutable.
- Alias resolution maps names to entity ids with source evidence.
- Context frame includes project/environment/runtime/process/role/time dimensions.
- Recall filters or inhibits candidates by context boundary.
- Cross-project entity merge requires policy and approved reusable source.

### 6. Prediction Error And Signal Tests

- Overconfident incorrect probe answer creates prediction error and calibration-risk signal.
- Workflow failure creates procedure-failed prediction error and rework-cost signal.
- Confirmed useful procedure creates usefulness/reward signal.
- Stale source creates staleness-pressure signal.
- Signals preserve dimensions and do not collapse into one score.
- High salience cannot bypass access policy or source truth.

### 7. Temporal Episode Tests

- Episode preserves ordered steps and actors.
- Episode links decisions, artifacts, prediction errors, claims, and procedures.
- Query "why did we do this?" can retrieve decision episode and source evidence.
- Probe session can become episodic source input without becoming truth.

### 8. Replay Scheduler Tests

- High-risk stale procedure is prioritized over low-risk stable fact.
- Repeated wrong-scope prediction errors create context-boundary replay job.
- Failed probe regression creates replay job.
- Replay job output creates draft review/projection invalidation only.
- Distributed replay result with wrong input hash is rejected.

### 9. Procedural Skill Tests

- Procedure skill includes preconditions, steps, postconditions, failure modes, evidence, maturity, and risk.
- Draft skill cannot be used as automatable workflow template.
- Validated skill can suggest workflow/template promotion under policy.
- Failure mode updates from prediction error evidence.
- Simulation output remains speculative until reviewed.

### 10. Metamemory Answer Gate Tests

- Source-poor answer triggers source audit or warning.
- Ambiguous context triggers clarification.
- Contested claim triggers warning/review/abstention.
- High-risk procedure without validation triggers abstention/review.
- Redaction-limited answer explains limitation.
- Answer gate decision is included in recall/probe trace.

## Golden Scenario: Docker Context Separation

Create or reuse fixture with:

- production Docker deployment procedure,
- test Docker simulation procedure,
- local Docker Compose development notes,
- CI Docker job notes,
- plugin sandbox Docker runtime notes.

Expected results:

- Entity/context binding creates distinct context frames.
- Semantic similarity relates the records but does not merge them.
- Production query inhibits test simulation procedure as authoritative answer.
- Wrong answer generates wrong-scope prediction error.
- Regression replay verifies the bug remains fixed.
- Metamemory gate asks clarification when environment is unspecified.

## Closure Criteria

- New requirements are mapped to subbundles.
- Diagrams and contracts are consistent with architecture docs.
- Existing source/projection/probing/governance decisions remain intact.
- No public architecture path allows direct truth mutation from probing, simulation, distributed worker output, or generated summaries.
