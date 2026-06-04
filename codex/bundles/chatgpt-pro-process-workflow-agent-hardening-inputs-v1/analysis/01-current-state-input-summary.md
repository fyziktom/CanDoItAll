# Current State Input Summary

The system has moved from earlier process/workflow/agent experimentation toward a working governed process path. The successful Tetris app run is important because it proves the new shape can execute end to end:

- Office365 email workflow captured a real incoming request.
- Process scope and architecture steps created upstream artifacts.
- A Blazor implementation step produced a working app.
- QA initially failed or required recovery, then passed.
- Security review, release readiness, rollout, and post-release learning completed.
- Artifacts, execution runs, workflow runs, tool receipts, and runtime proof were persisted.

This should be treated as positive evidence. The later hardening bundle should not start from the assumption that the current direction was wrong.

The current state is also broad and complicated:

- process templates encode governance and runtime behavior
- dispatch services validate artifacts, tools, browser proof, project paths, and completion
- agent templates and skills encode behavioral rules in prose
- MAF runtime maps capabilities to actual tool functions
- workflows expose executor catalogs and runtime backends
- UI editors expose process/workflow/agent capability editing
- project-structure writeback creates another durable evidence surface

The hardening target is therefore not a single service. It is a set of contracts that must stay canonical across runtime code, persisted templates, UI editor models, agent prompts, skill docs, and tests.

## Most Useful Live Evidence

Start with these files:

- `inputs/api-captures/process-run-6724-detail.json`
- `inputs/api-captures/agent-execution-runs-for-process-6724.json`
- `inputs/api-captures/workflow-run-e58-detail.json`
- `inputs/api-captures/workflow-run-e58-events.json`
- `inputs/api-captures/agents-include-templates.json`
- `inputs/api-captures/agent-capabilities.json`
- `inputs/api-captures/workflow-executor-catalog.json`

Then read:

- `inputs/01-live-runtime-evidence.md`
- `inputs/02-repository-delta-since-6e4f6dae.md`
- `inputs/03-agent-tools-skills-mcp-evidence.md`
- `analysis/02-observed-weak-spots.md`
- `inventories/01-hotspot-files-and-apis.md`

## Key Positive Signals

- The development database contains a completed Tetris process run.
- The Office365 category workflow completed and stored process-relevant summary information.
- Required process artifacts were satisfied in the final run.
- Runtime/browser proof was captured.
- Tests have been expanded around process launch planning, template governance, dispatch service behavior, status resolution, provider pricing, and API/skill parity.
- API skills now document current HTTP API routes and warn about numeric enum shape.
- Agent instructions were improved to prevent stale evidence, wrong product roots, fake shims, and browser-proof shortcuts.

## Key Risk Signals

- A successful run still required QA recovery/rework before final pass.
- Some agent output lineage references a stale or missing process run id.
- Browser proof and runtime command semantics are spread across templates, prompts, tools, and runtime policy.
- Several critical files exceed 1,000 to 3,900 lines.
- String keys, JSON paths, executor ids, action ids, and capability ids appear across UI, templates, skills, and runtime code.
- Development environment profile drift occurred between ports 5032 and 5034.
- Existing app processes can lock build outputs and affect validation commands.
- Workflow mailbox execution mutates external state by moving categories.
