# Run delegated execution and capture full trace

**Process:** `ai-assisted-change-delivery` / AI-assisted change delivery with guarded delegation  
**Step key:** `agent-execution`  
**Step kind:** Work  
**Target lead hours:** 8

## Summary
Every automated move must stay reproducible

## Notes
Execute the delegated work, capture tool calls and outputs, and preserve the trace so later reviewers can understand what the agent actually did.

The current project structure is the source of truth for implementation scope. Implement only the requested deliverable in the grounded product root, honor explicit no-framework/no-package/no-build constraints, and escalate instead of adding tooling, features, or architecture not requested by the delegation contract. A trace-only result is not a completed execution step: produce a concrete product-file change set or return a governed Blocked outcome with the exact architecture, permission, cost, or tooling blocker.

## Contracts
- Input contract: Delegation contract, prompt package, repository workspace, and allowed tools.
- Output contract: Draft change output plus full execution trace.
- Evidence contract: Product-file change set, execution trace pack, generated diff when applicable, and intermediate reasoning artifacts permitted by policy.

## Governance
- Decision rights: Software engineer supervises the lane; AI safety reviewer may halt execution when boundary breaches appear.
- Exception policy: Stop immediately if the agent attempts disallowed files, tools, or data domains.
- Requires approval: False
- Requires decision record: False

## Dependencies
- delegation-design

## Role assignments
- `software-engineer` / Software engineer => Responsible; required=True; fallback-order=0; rebind=Engineer remains accountable for supervised execution.
- `ai-safety-reviewer` / AI safety reviewer => Reviewer; required=True; fallback-order=0; rebind=Safety reviewer monitors boundary adherence.
- `solution-architect` / Solution architect => Backup; required=False; fallback-order=1; rebind=Architect remains fallback for design ambiguity.

## Artifact expectations
- `agent-execution-execution-trace-pack` -> `execution-trace-pack` / Execution trace pack | kind= | trust= | sensitivity= | validation=Must protect sensitive content and include enough context to interpret the sample correctly.
- `agent-execution-delegated-change-set` -> `implementation-change-set` / Delegated change set | kind=Deliverable | trust=ReviewRequired | sensitivity=Internal | validation=Must identify concrete product files created or changed under the grounded product root and validation evidence after the latest mutation. If no product files can be changed, the governed step outcome must be Blocked; trace-only artifacts are not sufficient.
- `agent-execution-provenance-report` -> `provenance-report` / Agent execution provenance summary | kind=Evidence | trust= | sensitivity= | validation=Must identify origin, producing system, trust assumptions, and gaps or manual overrides.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `agent-delegation-checklist`

## Validations
- `validate-delegation-boundary`
