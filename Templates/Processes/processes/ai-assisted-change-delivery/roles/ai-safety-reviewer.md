# AI safety reviewer

**Key:** `ai-safety-reviewer`  
**Scope:** local  
**Process:** ai-assisted-change-delivery  
**Preferred executor:** person  
**Preferred project role:** Reviewer  
**Seniority:** Senior AI safety or applied ML security specialist  
**Minimum years in primary discipline:** 7  
**Minimum years in software delivery:** 9

## Summary
Specialist reviewer for prompt, model-behavior, and misuse-resistant delivery controls.

## Purpose
Assess whether the AI-assisted workflow or AI-enabled output path has enough safety constraints and refusal logic for intended use.

## Staffing intent
A specialist in practical AI safety controls and failure containment.

## Snapshot summary
Specialist reviewer for prompt, model-behavior, and misuse-resistant delivery controls.

## Domain tags
ai-safety, prompt-governance, misuse-resistance

## Knowledge requirements
- Knowledge of prompt design risk, prompt injection, jailbreak patterns, unsafe completion modes, and content-boundary controls.
- Ability to assess whether refusal, escalation, and human-oversight conditions are real and enforceable.
- Understanding of model provenance, tool-use risk, retrieval contamination, and output validation gaps.
- Ability to review system prompts, evaluation prompts, and execution policies critically.
- Knowledge of safety logging, red-team patterns, and incident response for AI misuse or harmful output.
- Ability to align safety recommendations with delivery reality without weakening required control points.

## Experience requirements
- Has reviewed AI workflows, prompts, or tool-using agents for safety and misuse risk.
- Has identified unsafe prompt or policy assumptions before production use.
- Has collaborated with engineers to implement or refine refusal and escalation controls.
- Has supported red-team, tabletop, or incident work involving AI misuse scenarios.
- Has documented safety decisions in a way usable by release governance.

## Decision rights
- Approve or reject AI safety adequacy for covered use cases.
- Require stronger refusal, guardrail, or validation controls before release.
- Escalate high-risk misuse patterns to model risk approver and security stakeholders.
- Set conditions for monitored or limited-scope rollout where appropriate.

## Owned artifacts
- AI safety review
- Prompt control note
- Refusal and escalation matrix

## Collaboration expectations
- Work with evaluation, product, security, and implementation roles.
- Review actual prompts and execution traces rather than abstract descriptions only.
- Help convert vague safety concern into implementable controls.
- Participate in post-release review when harmful behavior is observed.

## Anti-patterns
- Assuming generic model provider safeguards cover the actual workflow risk.
- Approving safety without reading prompts, tools, or evaluation traces.
- Treating human review as magic without staffing or workflow reality.
- Overfocusing on theoretical attacks while ignoring realistic misuse paths.

## Fitness evidence
- Actionable safety reviews tied to concrete controls.
- Examples of prevented unsafe rollout due to review findings.
- Evidence of prompt/control improvements after review.
- Cross-functional understanding of safety conditions created by the role.
