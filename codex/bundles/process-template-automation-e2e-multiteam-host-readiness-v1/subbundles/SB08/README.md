# SB08: Release matrix, large-screen proof, and red-team closure

## Status
Prepared.

## Objective
Run build/unit/focused integration, optional live OpenAI classification, large-screen UI proof if touched, code-first ratio, and red-team scans.

## Covered Inputs
- `inputs/00-original-request.md`
- `requirements/01-normalized-requirements.md`
- `analysis/01-real-code-review.md`
- `analysis/04-gap-analysis.md`

## Prerequisites
- Previous subbundle closure gate must pass or record an explicit blocker.
- Current branch must be `maf-processes-refactor`.

## Exact Source References
- repo://CanDoItAll.slnx
- repo://tests
- repo://src/CanDoItAll.Processes.Core
- repo://src/CanDoItAll.Modules.Processes

## Deliverables
- Real production and/or test code changes for this coherent implementation area.
- Focused tests that prove behavior, not only file existence or non-empty output.
- Concise execution-report update.

## Dependency Impact
Final closure and next-phase decision.

## Validation Depth
Critical. Require semantic positive proof, adversarial negative proof, source assertions, anti-stub scan, and downstream progression decision.

## Implementation Steps
1. Re-open the exact source references and nearby tests.
2. Implement the smallest coherent source/test design that closes this subbundle fully.
3. Avoid creating new large proof trees.
4. Run focused tests and relevant scans.
5. Update `reviews/01-execution-report.md` with concise proof.

## Scope Exceptions
- Execution-capable process-driver side effects remain out of scope.
- Process Core moves are out of scope unless they are pure/generic/read-model only and explicitly justified.

## Do Not Do
- Do not use manual step transitions as primary proof for representative automation execution.
- Do not add reflection discovery, fallback selector, or driver self-registration.
- Do not mutate process state through driver packages.
- Do not add domain terms to Process Core.
- Do not create new boilerplate-heavy bundle/proof directories.

## Acceptance Checklist
- Source/test changes are meaningful for this subbundle.
- Focused tests pass.
- No new Core dependency drift.
- No execution-capable side effects.
- No bundle-path coupling.
- No changed runtime file grows beyond the agreed file-size guard without a split.

## Proof Required
- Focused test transcript.
- Source assertions.
- Anti-stub scan.
- Boundary scan.
- For template execution subbundles, proof must show dispatch/finalizer/artifact readback, not manual transition-only proof.

## Browser Validation Logging
N/A unless UI/project-structure routes/components are touched or used as user-facing proof. If browser proof is required, use a 1900x1200 large desktop viewport only.

## Progression Gate
Do not proceed downstream until this subbundle has source-backed proof and no shallow-pass trap remains.

## Suggested Agent Prompt
Implement SB08 as a code-first coherent runtime/template-host improvement. Keep the proof concise, preserve Process Core genericity, and keep execution-capable driver behavior blocked.
