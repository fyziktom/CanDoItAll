# QA / Red-Team Prompt

Review the implementation against the bundle, not just against the execution report.

Reject the work if:
- a critical subbundle closes without artifact-backed manifest proof,
- runtime driver registry/selector/DI/manager/scheduler/workflow hook appears,
- a verifier reads arbitrary files, executes commands, calls external services, or writes process/workspace/storage state,
- Core references any driver package,
- driver implementation package references Modules, Infrastructure, AgentFramework, EF, UI, workspace, storage, or external connectors,
- diagnostics/audit facts leak secrets, emails, tokens, or connection strings,
- `NoMutationPerformed` can be false in verification-only paths,
- tests only prove non-empty output or happy-path fixtures,
- UI/media artifacts appear.
