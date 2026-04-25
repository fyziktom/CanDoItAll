# Implementation Prompt

Implement only the current subbundle.

Respect these constraints:

- Keep process engine behavior in `CanDoItAll.Modules.Processes` unchanged unless a test exposes an existing bug that must be fixed.
- Put deterministic mock behavior behind `AgentFramework:ProcessMockAgents:Enabled`.
- Do not call real LLM providers from the mock runtime.
- Use `IWorkspaceFileService` for artifacts so execution receipts and process artifact projection are exercised.
- Use stable constants for provider base URL, role tags, branch keys, and artifact names.
- Preserve the existing Scenario Harness behavior.
- Add targeted tests before broad refactors.
