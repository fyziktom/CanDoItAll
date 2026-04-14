# Author

**Key:** `author`  
**Scope:** local  
**Process:** branching-code-review  
**Preferred executor:** person-or-agent  
**Preferred project role:** TeamMember  
**Seniority:** Engineer or approved implementation agent under human supervision  
**Minimum years in primary discipline:** 3  
**Minimum years in software delivery:** 5

## Summary
Change author responsible for preparing a reviewable packet and implementing requested repairs.

## Purpose
Keep authorship and first-pass implementation evidence attached to the person or agent that produced the change.

## Staffing intent
Authorship role for implementation context and review packet preparation.

## Snapshot summary
Authorship role for implementation context and review packet preparation.

## Domain tags
code-review, implementation, repair-loops

## Knowledge requirements
- Solid command of the target codebase language, architecture conventions, and test approach.
- Understanding of version control, code review, CI pipelines, and release hygiene.
- Ability to interpret architecture guidance and convert it into implementable tasks without scope drift.
- Knowledge of defect prevention techniques, observability basics, and safe rollback-friendly change packaging.
- Ability to produce evidence that links what changed to why it is safe enough to proceed.
- Understanding of secure coding and dependency hygiene expectations for the stack in use.

## Experience requirements
- Has delivered production code with automated test coverage and peer review evidence.
- Has decomposed medium-complexity work into implementable slices with clear review boundaries.
- Has debugged at least one production issue or high-severity defect linked to code they worked on.
- Has participated in release or deployment readiness discussions and adjusted code accordingly.
- Has collaborated with QA and architecture reviewers to repair inadequate evidence or design drift.

## Decision rights
- Choose implementation approach within approved architectural guardrails.
- Stop and escalate when required inputs, access, or environment assumptions are missing.
- Recommend technical debt acceptance or rejection when delivery pressure appears.
- Refuse AI-generated output that cannot be validated to the required standard.

## Owned artifacts
- Code change set
- Implementation notes
- Test evidence pack
- Rollback-aware deployment notes

## Collaboration expectations
- Clarify ambiguity with product and architecture roles before making silent assumptions.
- Provide QA and reviewers with concrete evidence, not only assertions of completion.
- Document trade-offs when delivery timing forces constrained implementation choices.
- Support incident and hotfix response for changes the engineer helped deliver.

## Anti-patterns
- Treating passing local tests as sufficient release evidence.
- Shipping code that depends on tacit environment knowledge no one else has.
- Hiding uncertainty to appear fast.
- Accepting AI-generated code without line-level comprehension and verification.

## Fitness evidence
- Merged changes with traceable test and review evidence.
- Examples of defect analysis or incident participation showing engineering accountability.
- Consistent adherence to repository conventions and delivery guardrails.
- Code reviews from peers indicating clarity, maintainability, and risk awareness.
