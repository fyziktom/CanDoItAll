# Assumptions And Risks

## Assumptions

- Workflow control during development means HTTP API control through the existing web API, not a new MCP server.
- The minimal useful missing commands are workflow definition lifecycle commands and import/export; run observation and cancellation already exist.
- The new skill should match the concise structure of existing API skills rather than the larger bundle workflow skills.

## Critical Path Risks

- If lifecycle commands reconstruct definitions incorrectly, saved graphs or runtime policy could be lost across status changes.
- If import/export uses a weak DTO shape, future agents may copy raw internal records or bypass validation.
- If the skill is not synced by the reinstall script, the user will restart Codex and still not see the new workflow skill.

## Validation Risks

- Full solution tests may be expensive; targeted workflow API integration tests are the primary validation proof.
- Running the full MCP reinstall publishes several projects and may take time, but it is required to prove local skill setup.
- OpenAI docs MCP is unavailable in this session, so the skill-format validation cites official OpenAI docs via web instead.

## Reopen Triggers

- Reopen subbundle 01 if tests show lifecycle commands create invalid versions or do not preserve graph/runtime policy.
- Reopen subbundle 02 if the skill does not appear under the user skill root after reinstall.
- Reopen subbundle 02 if OpenAI skill docs require metadata not present in the new skill.
