# Implementation Prompt

Implement the selected subbundle only.

Before editing, read:

- `bundle://README.md`
- `bundle://analysis/01-current-state.md`
- `bundle://analysis/02-assumptions-and-risks.md`
- `bundle://architecture/01-target-solution.md`
- `bundle://plan/01-phase-plan.md`
- the selected `bundle://subbundles/SBxx-*/README.md`

Hard constraints:

- Preserve behavior from `6775de820 phase1`.
- Do not remove software-delivery, .NET, screenshot, subprocess, product mutation, or recovery behavior to simplify the refactor.
- Keep generic process runtime/application contracts domain-neutral.
- Keep prompt fragment composition, completion evidence policy, and actual step execution dispatch behavior in drivers or SB01-approved driver abstractions.
- Do not add MAF, AgentFramework implementation, or `CanDoItAll.Modules.AgentFramework` references to any `src/Processes/*` project.
- Prefer the smallest extraction that creates a real boundary and direct tests.
- Use typed contracts and explicit diagnostics. Do not add silent fallback mechanisms.
- Update DI and tests in the same subbundle when extracted services become injectable.

Proof requirements:

- Capture failing-first proof for behavior-changing critical work.
- Capture passing proof for the same invariant after implementation.
- Write `proof/SBxx/manifest.md` and `proof/SBxx/semantic-invariants.md` for critical subbundles before closure.
- Record changed-file hashes, command transcripts, source assertions, anti-stub audit output, and any UI/browser/host artifacts required by the subbundle.
- Update `reviews/01-execution-report.md` gate rows before marking the subbundle complete.

Stop conditions:

- Stop and reopen SB01 if dependency direction or project references contradict the selected target placement.
- Stop and reopen SB01 if the chosen prompt/evidence/dispatch design requires Processes to depend on the MAF wrapper or AgentFramework module.
- Stop and reopen the current subbundle if tests can pass while a shallow mechanical file split preserves the same monolithic responsibility.
- Stop if a behavior is intentionally removed; that requires explicit user approval or a new scope exception.
