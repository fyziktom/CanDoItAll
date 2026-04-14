# Evaluate outputs against benchmark and acceptance evidence

**Process:** `ai-assisted-change-delivery` / AI-assisted change delivery with guarded delegation  
**Step key:** `evaluation-and-benchmarking`  
**Step kind:** Review  
**Target lead hours:** 8

## Summary
A polished answer is not enough

## Notes
Run deterministic checks, benchmark tasks, and human review prompts to determine whether the agent output actually satisfies the bounded acceptance criteria.

## Contracts
- Input contract: Generated diff, execution trace, acceptance criteria, test harness, and benchmark cases.
- Output contract: Evaluation report with pass/fail evidence and required rework.
- Evidence contract: Benchmark report, test evidence, and evaluator notes.

## Governance
- Decision rights: AI evaluation lead owns measurement quality; QA lead reviews reproducibility and adequacy of tests.
- Exception policy: Do not infer correctness from style, brevity, or model confidence.
- Requires approval: False
- Requires decision record: True

## Dependencies
- agent-execution

## Role assignments
- `ai-evaluation-lead` / AI evaluation lead => Responsible; required=True; fallback-order=0; rebind=Evaluation stewardship remains explicit.
- `qa-lead` / QA lead => Reviewer; required=True; fallback-order=0; rebind=QA validates reproducibility.
- `software-engineer` / Software engineer => Reviewer; required=True; fallback-order=1; rebind=Engineer reviews implementation realism.

## Artifact expectations
- `evaluation-and-benchmarking-evaluation-benchmark-report` -> `evaluation-benchmark-report` / Evaluation benchmark report | kind= | trust= | sensitivity= | validation=Must explain dataset shape, threshold logic, and known blind spots.
- `evaluation-and-benchmarking-test-evidence-pack` -> `test-evidence-pack` / Test evidence pack | kind= | trust= | sensitivity= | validation=Must contain reproducible evidence sources, coverage statement, open defects, and residual risk summary.
- `evaluation-and-benchmarking-prompt-package` -> `prompt-package` / Evaluation prompt package | kind=Prompt | trust= | sensitivity= | validation=Must include prompt version, intended role, refusal conditions, and validation expectations.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.

## Checklists
- `qa-evidence-checklist`
- `ai-governance-checklist`

## Validations
- `validation-ai-evidence-sufficient`

## Prompts
- `prompt-ai-evaluation`
- `prompt-qa-test-design`
