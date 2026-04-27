# AI-assisted change delivery with guarded delegation

**Key:** `ai-assisted-change-delivery`  
**Criticality:** High  
**Autonomy level:** Guarded  
**Operating mode:** AssistedExecution  
**Customer name:** Engineering, AI governance, and release stakeholders  
**Owner name:** AI delivery governance board

## Summary
Structure work so AI agents can assist delivery through bounded delegation, prompt packaging, evaluation, safety review, merge control, and evidence-rich trace capture.

## Value statement
Gain AI-delivery speed without losing human accountability, evidence quality, safety boundaries, or the ability to audit how a change was produced.

## Interface contract summary
Change demand, delegation boundaries, prompts, execution traces, evaluation evidence, safety reviews, and human approvals are combined into one guarded delivery contract.

## Governance notes
AI execution is never treated as magic labor. Delegation scope, refusal boundaries, allowed artifacts, evaluation criteria, and merge authority remain explicit and typed.

## Architecture and constitution rules
- Governance policy: Delegated work requires explicit task decomposition, prompt package, output evaluation, safety review, provenance trace, and human merge approval.
- Constitution rule: No AI-generated change may enter protected branches or production lanes without human understanding of scope, evidence, and residual risk.

## Operating and simulation notes
- Operating mode summary: Guarded autonomy: agents may generate drafts and execute bounded tasks, but humans own delegation boundaries, acceptance, safety, and merge decisions.
- Simulation readiness: Designed specifically for AI-agent orchestration, with enough structure to drive delegated task runners, evaluators, reviewers, and post-hoc audits.

## Source frameworks
- nist-ssdf
- nist-ssdf-ai
- owasp-samm
- slsa
- spdx

## Process metrics
- Delegated task success rate
- Rate of AI outputs accepted without rework
- Evidence completeness score per delegated change
- Number of blocked changes due to unsafe delegation or weak evaluation

## Process risks
- Delegation scope is too broad and exceeds evidence or safety boundaries.
- Prompts and tool permissions are not packaged reproducibly.
- Evaluation evidence is weak or unrepresentative.
- Merge decisions rely on output polish rather than verified correctness.

## Tailoring rules
- For low-risk documentation changes, simplify benchmark depth but keep trace capture.
- For code touching auth, billing, or data boundaries, require mandatory security and model-risk review.
- For non-code outputs, keep the same delegation boundary and evidence pattern but tailor artifact kinds.

## Role usages
- `product-owner` / **Product owner** — Convert business intent into an explicit delivery contract with clear acceptance boundaries and prioritized value trade-offs.
- `solution-architect` / **Solution architect** — Protect maintainability and operability by reviewing design options, target architecture fit, and downstream integration impact before costly implementation commitment.
- `ai-safety-reviewer` / **AI safety reviewer** — Assess whether the AI-assisted workflow or AI-enabled output path has enough safety constraints and refusal logic for intended use.
- `model-risk-approver` / **Model risk approver** — Accept, conditionally accept, or reject AI-related change exposure based on model behavior risk and control coverage.
- `software-engineer` / **Software engineer** — Produce working change artifacts that satisfy the process contract, surface blockers quickly, and leave enough proof for review and reuse.
- `ai-evaluation-lead` / **AI evaluation lead** — Ensure AI-assisted work is judged using explicit evaluation criteria rather than optimism about model capability.
- `qa-lead` / **QA lead** — Challenge whether the delivered change is proven enough for its risk profile and make test evidence decision-ready for release governance.
- `security-reviewer` / **Security reviewer** — Ensure changes touching trust boundaries, sensitive data, dependencies, or operational attack surface are reviewed proportionally and documented defensibly.
- `release-approver` / **Release approver** — Decide whether the accumulated evidence is sufficient to expose the change to real users, data, and operational load.

## Steps
### 1. Capture change demand and human acceptance boundary (`task-intake`)
- Step kind: Start
- Depends on: None
- Inputs: Feature request, defect, or improvement demand with business context.
- Outputs: Bounded change brief with explicit human acceptance boundary.
- Evidence: Intake brief and acceptance criteria map.
- Decision rights: Product owner owns value boundary; no agent may redefine acceptance criteria alone.
- Exception policy: Do not delegate ambiguous or ownerless tasks.
- Artifact expectations:
  - `task-intake-intake-brief` => `intake-brief` / Intake brief
  - `intake-acceptance-criteria-pack` => `acceptance-criteria-pack` / Acceptance criteria pack
- Checklists: implementation-readiness-checklist
- Validations: validation-intake-complete
- Prompts: prompt-implementation-brief

### 2. Define delegation boundary, allowed tools, and refusal conditions (`delegation-design`)
- Step kind: Decision
- Depends on: task-intake
- Inputs: Bounded change brief, repository context, data-sensitivity map, and tool inventory.
- Outputs: Approved delegation contract for agent execution.
- Evidence: Delegation contract and refusal boundary note.
- Decision rights: Solution architect and AI safety reviewer define safe task decomposition; model-risk approver reviews autonomy fit for sensitive domains.
- Exception policy: If the delegation boundary cannot be explained precisely, do not delegate.
- Branch outcomes: delegate (Delegate), human-only (Human only)
- Artifact expectations:
  - `delegation-design-prompt-package` => `prompt-package` / Delegation contract and prompt package
  - `delegation-design-execution-trace-pack` => `execution-trace-pack` / Delegation configuration snapshot
- Checklists: agent-delegation-checklist, ai-governance-checklist
- Validations: validate-delegation-boundary
- Prompts: prompt-ai-risk-brief

### 3. Run delegated execution and capture full trace (`agent-execution`)
- Step kind: Work
- Depends on: delegation-design
- Inputs: Delegation contract, prompt package, repository workspace, and allowed tools.
- Outputs: Draft change output plus full execution trace.
- Evidence: Execution trace pack, generated diff, and intermediate reasoning artifacts permitted by policy.
- Decision rights: Software engineer supervises the lane; AI safety reviewer may halt execution when boundary breaches appear.
- Exception policy: Stop immediately if the agent attempts disallowed files, tools, or data domains.
- Artifact expectations:
  - `agent-execution-execution-trace-pack` => `execution-trace-pack` / Execution trace pack
  - `agent-execution-provenance-report` => `provenance-report` / Agent execution provenance summary
- Checklists: agent-delegation-checklist
- Validations: validate-delegation-boundary

### 4. Evaluate outputs against benchmark and acceptance evidence (`evaluation-and-benchmarking`)
- Step kind: Review
- Depends on: agent-execution
- Inputs: Generated diff, execution trace, acceptance criteria, test harness, and benchmark cases.
- Outputs: Evaluation report with pass/fail evidence and required rework.
- Evidence: Benchmark report, test evidence, and evaluator notes.
- Decision rights: AI evaluation lead owns measurement quality; QA lead reviews reproducibility and adequacy of tests.
- Exception policy: Do not infer correctness from style, brevity, or model confidence.
- Artifact expectations:
  - `evaluation-and-benchmarking-evaluation-benchmark-report` => `evaluation-benchmark-report` / Evaluation benchmark report
  - `evaluation-and-benchmarking-test-evidence-pack` => `test-evidence-pack` / Test evidence pack
  - `evaluation-and-benchmarking-prompt-package` => `prompt-package` / Evaluation prompt package
- Checklists: qa-evidence-checklist, ai-governance-checklist
- Validations: validation-ai-evidence-sufficient
- Prompts: prompt-ai-evaluation, prompt-qa-test-design

### 5. Review safety, security, and residual risk (`safety-and-security-review`)
- Step kind: Approval
- Depends on: evaluation-and-benchmarking
- Inputs: Evaluation report, execution trace, code diff, and sensitive-scope map.
- Outputs: Approved, held, or rejected merge recommendation.
- Evidence: Safety/security approval record with residual risk statement.
- Decision rights: Security reviewer and model-risk approver may block merge; release approver owns final risk acceptance for guarded lanes.
- Exception policy: Do not merge when control impact is ambiguous.
- Branch outcomes: approved (Approved), rework (Rework)
- Artifact expectations:
  - `safety-and-security-review-security-review-note` => `security-review-note` / Security review note
  - `safety-and-security-review-release-readiness-report` => `release-readiness-report` / AI-assisted merge approval note
- Checklists: security-review-checklist, ai-governance-checklist
- Validations: validation-security-clear, validation-release-authorized
- Prompts: prompt-security-review, prompt-ai-risk-brief

### 6. Merge under control and capture learning signal (`controlled-merge-and-learning`)
- Step kind: End
- Depends on: safety-and-security-review
- Inputs: Approved merge note, trace pack, evaluated diff, and rollout instructions if applicable.
- Outputs: Merged change with traceable evidence and improvement actions.
- Evidence: Merge note, trace references, and improvement backlog.
- Decision rights: Software engineer performs the merge; AI evaluation lead captures benchmark improvement; product owner reviews value fit if needed.
- Exception policy: Do not lose traceability between approved artifact and merged change.
- Artifact expectations:
  - `controlled-merge-and-learning-execution-trace-pack` => `execution-trace-pack` / Merged execution trace reference
  - `controlled-merge-and-learning-retrospective-improvement-log` => `retrospective-improvement-log` / AI delivery improvement log
- Checklists: ai-governance-checklist
- Validations: validation-ai-evidence-sufficient

