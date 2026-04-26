# QA Prompt

Validate only the behavior owned by this bundle.

Check the following:

- Mock agents are absent or unusable when `AgentFramework:ProcessMockAgents:Enabled` is false.
- Mock catalog creates multiple role-specific agents when enabled.
- Mock runtime returns deterministic `PROCESS_STEP_OUTCOME` markers.
- QA first pass selects the repair branch.
- QA recheck selects the approval branch.
- Artifacts are written through the workspace file service and are visible to execution/process artifact tracking.
- No production process dispatcher path is special-cased for mock agents.
