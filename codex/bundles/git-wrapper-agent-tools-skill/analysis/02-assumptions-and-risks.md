# Assumptions And Risks

## Assumptions

- Standard git operations for agents means bounded local workflow operations: inspect, stage, unstage, commit, branch creation, and branch switching.
- Remote operations and destructive history rewriting are intentionally excluded because they need stronger policy, credentials, and user-facing approval semantics.
- `CanDoItAll.AgentFramework.Core` may reference `CanDoItAll.Git` because the git project is infrastructure-light, command-line specific, and already used as a shared boundary by process code.
- The complementary skill should be an inline capability under `Templates/Capabilities`, not only a Codex operator skill, because app-managed agents consume template-backed capabilities.

## Critical Path Risks

- SB01 is a critical foundation. If the wrapper does not produce correct bounded command specs, every runtime git tool can be wrong while still producing receipts.
- SB02 depends on SB01. If tool names, permission mapping, or process-operation classifications are wrong, agents may be denied needed tools or receive mutation tools in read-only steps.
- SB03 depends on SB02. If the catalog or default-agent assignments do not match runtime tool names, the skill can teach tools that agents cannot call.

## Validation Risks

- Git commands depend on `git` being available in the test host. Focused unit tests should prove command plans without requiring a real repository where possible.
- Capability template tests have broad expected lists and may require updates beyond the obvious files.
- Tool composition tests use reflection into private runtime state; failures may point to access-policy filtering rather than missing tool factory entries.
- No browser proof is needed because the change is non-UI.

## Reopen Triggers

- Reopen SB01 if any runtime tool needs a git command shape not represented by typed wrapper specs.
- Reopen SB01 if tests expose option-like branch or revision values getting through validation.
- Reopen SB02 if capability filtering classifies any git mutation operation as read-only or non-approval.
- Reopen SB03 if a default agent receives the git skill without the corresponding runtime tools, or receives mutation tools without the software-development workspace profile.
- Reopen SB04 if final tests pass only through fixture-specific assertions and not through source-backed command/tool/template behavior.
