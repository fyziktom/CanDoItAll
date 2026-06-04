# Input Coverage

| User request item | Covered by |
| --- | --- |
| Prepare input information for ChatGPT Pro | Entire bundle, especially `shared-prompts/chatgpt-pro-input-brief.md` |
| Do not propose architecture | `README.md`; no architecture directory or target solution file was created |
| Do not propose subbundles | `README.md`; no subbundle decomposition was created |
| Do not implement | No source files were edited; only input/evidence bundle files were created |
| Use process API for Tetris run | `inputs/01-live-runtime-evidence.md`; `inputs/api-captures/process-run-6724-detail.json` |
| Use agent API for Tetris run | `inputs/03-agent-tools-skills-mcp-evidence.md`; `inputs/api-captures/agent-execution-runs-for-process-6724.json`; `inputs/api-captures/agents-include-templates.json` |
| Use workflow API for Office365 workflow run | `inputs/01-live-runtime-evidence.md`; `inputs/api-captures/workflow-run-e58-detail.json`; `inputs/api-captures/workflow-run-e58-events.json` |
| Do not rerun workflow if run exists | Workflow run existed and was not rerun |
| Include changes since baseline commit | `inputs/02-repository-delta-since-6e4f6dae.md`; `inventories/01-hotspot-files-and-apis.md` |
| Focus on larger files/classes needing review | `analysis/02-observed-weak-spots.md`; `inventories/01-hotspot-files-and-apis.md` |
| Include canonicity/performance/threading/parallelism weak spots | `analysis/02-observed-weak-spots.md` |
| Include agents, skills, tools, MCPs | `inputs/03-agent-tools-skills-mcp-evidence.md` |
| Explain used/troubled skills/tools | `inputs/03-agent-tools-skills-mcp-evidence.md`; `analysis/02-observed-weak-spots.md` |
| Place bundle in `codex/bundles` | `codex/bundles/chatgpt-pro-process-workflow-agent-hardening-inputs-v1` |
