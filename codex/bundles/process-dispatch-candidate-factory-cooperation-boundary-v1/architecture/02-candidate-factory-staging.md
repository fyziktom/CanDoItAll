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

## Stage 3: Route-specific factory methods

Add factory methods with exact parity tests:

- `CreateSubprocessCandidate`
- `CreateWorkflowCandidate`
- `CreateDirectAgentCandidate`

## Stage 4: Cooperation metadata helper

Move workspace profile/cooperation mode resolution out of dispatcher partial into a local helper. Do not introduce driver selection. Record driver-readiness only as documentation.

## Stage 5: Candidate factory consumption

Update `LoadDispatchCandidateAsync` to call the factory while keeping source reads and explicit side effects outside the factory.
