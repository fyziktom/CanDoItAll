# Assumptions And Risks

## Assumptions

- The benchmark-driven parity target for this pass is the SharpTools analysis surface, not its source-editing surface.
- The sibling CodeAnalytics repo can accept new abstractions and application-service queries if the host wrapper alone is insufficient.
- The updated MCP can still use snapshot-backed answers for most parity tools instead of introducing a second standalone Roslyn query pipeline.

## Critical Path Risks

- The project-reference parity subbundle is a critical foundation. If it returns anything other than clean direct references, the Zyphonote Scenario 1 rerun will remain untrustworthy.
- The member/source inspection subbundle is a critical foundation. If scenario 4 still depends on brittle focused-context behavior with no fallback analysis path, the rerun will remain below parity.
- Host integration is on the critical path because Codex cannot exercise newly added MCP tools until the reinstall flow publishes them and the session is restarted if necessary.

## Validation Risks

- This Codex session cannot hot-add brand-new MCP tool definitions. A restart may be mandatory after reinstall before the final rerun can start.
- Snapshot-backed answers depend on workspace source files being present and consistent with the built snapshot. A stale snapshot will invalidate comparison proof.
- If the focused-context failure is data-dependent and not reproducible in local validation, the safer path is to add a deterministic alternative tool for behavior reconstruction rather than claim the issue is fixed.

## Reopen Triggers

- Reopen subbundle 02 if the new project tool still requires manual `.csproj` inspection to answer direct-reference questions.
- Reopen subbundle 03 if scenario 4 still fails, or if it can only be answered by manual file reading without a stable MCP flow.
- Reopen subbundle 04 if reinstall succeeds but the skill guidance does not reflect the new tool-selection strategy.
- Reopen subbundle 05 if the rerun bundle lacks enough evidence to show improvement on the original five scenarios.
