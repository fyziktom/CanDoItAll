# Define delegation boundary, allowed tools, and refusal conditions

**Process:** `ai-assisted-change-delivery` / AI-assisted change delivery with guarded delegation  
**Step key:** `delegation-design`  
**Step kind:** Decision  
**Target lead hours:** 6

## Summary
The agent gets a sandbox, not a blank check

## Notes
Translate the human change brief into a precise delegation contract that states what the agent may touch, what it must refuse, and which evidence it must produce.

Keep the contract proportional to the work. For low-risk local deliverables with a complete project-structure mindmap, the delegation boundary may be concise: allowed product root, requested files or modules, forbidden extras, validation hooks, and escalation conditions. Do not add unrequested frameworks, package managers, release gates, or broad governance work just because they could be useful.

For greenfield work, do not require product files to already exist in this delegation-boundary step. Record the intended product root and access boundary, then let the delegated execution step create or modify the product only when the selected process step explicitly permits implementation. If a grounded product root is inaccessible to the execution tools, escalate the access problem instead of relocating the product to a managed output folder.

## Contracts
- Input contract: Bounded change brief, repository context, data-sensitivity map, and tool inventory.
- Output contract: Approved delegation contract for agent execution.
- Evidence contract: Delegation contract and refusal boundary note.

## Governance
- Decision rights: Solution architect and AI safety reviewer define safe task decomposition; model-risk approver reviews autonomy fit for sensitive domains.
- Exception policy: If the delegation boundary cannot be explained precisely, do not delegate.
- Requires approval: False
- Requires decision record: True

## Dependencies
- task-intake

## Role assignments
- `solution-architect` / Solution architect => Responsible; required=True; fallback-order=0; rebind=Architecture owner defines safe technical boundary.
- `ai-safety-reviewer` / AI safety reviewer => Reviewer; required=True; fallback-order=0; rebind=AI safety review is required.
- `model-risk-approver` / Model risk approver => Reviewer; required=False; fallback-order=1; rebind=Model-risk review becomes required for sensitive change classes.

## Artifact expectations
- `delegation-design-prompt-package` -> `prompt-package` / Delegation contract and prompt package | kind=Prompt | trust= | sensitivity= | validation=Must include prompt version, intended role, refusal conditions, and validation expectations.
- `delegation-design-execution-trace-pack` -> `execution-trace-pack` / Delegation configuration snapshot | kind=Evidence | trust= | sensitivity= | validation=Must protect sensitive content and include enough context to interpret the sample correctly.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- `delegate` / Delegate — The task can be delegated under stated constraints.
- `human-only` / Human only — The task stays human-executed because safe delegation criteria are not met.

## Checklists
- `agent-delegation-checklist`
- `ai-governance-checklist`

## Validations
- `validate-delegation-boundary`

## Prompts
- `prompt-ai-risk-brief`
