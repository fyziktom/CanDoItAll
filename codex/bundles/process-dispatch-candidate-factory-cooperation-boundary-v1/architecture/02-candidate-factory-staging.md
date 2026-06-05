# Candidate Factory Staging

## Stage 1: Source inventory and candidate field map

Codex must enumerate every `new DispatchCandidate(...)` call and map each argument.

## Stage 2: Candidate assembly context

Create an internal `ProcessDispatchCandidateAssemblyContext` that groups:

- run
- definition
- step run
- step definition
- work brief
- expected artifacts
- recorded artifact expectation ids
- prepared artifact inputs
- external reference keys
- branch context
- assignment/current role facts
- technical agent binding facts
- recovery execution id/manual directive
- cooperation metadata

The context must be module-local.

## Factory Cutline

`ProcessDispatchCandidateAssemblyContextFactory` may group already-loaded facts for the current step. It must not query EF, resolve technical-agent bindings, write journals, run execution clients, or mutate agents.

`ProcessDispatchCandidateFactory` may only construct `DispatchCandidate` instances from:

- `ProcessDispatchCandidateAssemblyContext`,
- route-specific immutable values such as `technicalAgentId`, `reusableChatSessionId`, `recoveryExecutionRunId`, `manualRecoveryDirective`,
- already-resolved `AgentProcessCooperationMetadata`.

The dispatcher must continue to own:

- `LoadExpectedArtifactsAsync`,
- execution-run queries,
- `ProcessDispatchTechnicalAgentBindingCoordinator.ResolveAsync`,
- project-structure access-grant logging,
- manual recovery directive loading,
- artifact-recovery id reuse decisions,
- workflow/subprocess execution and process-step transitions.

## Stage 3: Route-specific factory methods

Add factory methods with exact parity tests:

- `CreateSubprocessCandidate`
- `CreateWorkflowCandidate`
- `CreateDirectAgentCandidate`

## Stage 4: Cooperation metadata helper

Move workspace profile/cooperation mode resolution out of dispatcher partial into a local helper. Do not introduce driver selection. Record driver-readiness only as documentation.

## Stage 5: Candidate factory consumption

Update `LoadDispatchCandidateAsync` to call the factory while keeping source reads and explicit side effects outside the factory.
