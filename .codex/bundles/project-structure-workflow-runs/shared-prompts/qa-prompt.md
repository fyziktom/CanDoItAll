# QA Prompt

Validate the selected subbundle against the raw request, normalized requirements, and progression gate.

Check that every absolute requirement is preserved: project details and parent-node details are always included, workflow result nodes default under the workflow node, start confirmation does not show process matching/staffing UI, and summaries include file paths even when no asset node exists.

For backend phases, run targeted tests and inspect state persisted to workflow/project-structure records. For UI phases, use Playwright on a large desktop viewport and one narrower viewport. Capture open-state screenshots for context menus, add workflow dialog, start confirmation dialog, and selection status. Review screenshots for clipping, layering, overflow, readable text, and correct data.

For final validation, run at least 20 PostgreSQL-backed scenarios. The scenario output must show true work grounded in the input, not generic filler. Include `gpt-5-mini` and local Ollama `gptoss20b64k` runs. Any product defect found during scenarios reopens or creates a repair subbundle before closure.
