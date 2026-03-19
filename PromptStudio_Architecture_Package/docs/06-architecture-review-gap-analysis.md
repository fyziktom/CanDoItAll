# 06 — Architecture Review and Gap Analysis

## 1. Review purpose

This document reviews the proposed architecture with a critical perspective and checks whether the design genuinely covers the requested capabilities without hiding unresolved complexity behind vague wording.

The review is intentionally skeptical. The goal is not to praise the design but to pressure-test it.

## 2. Review criteria

The architecture was reviewed against the following criteria:

1. completeness against requested scope
2. practicality for a first release
3. modularity and future growth
4. UI cohesion
5. security posture
6. testability
7. delivery realism for Codex-assisted implementation
8. risk of accidental over-engineering
9. risk of hidden coupling
10. readiness for future sidecars/microservices

## 3. What the architecture gets right

### 3.1 Strong project-centric model
The architecture correctly makes the project, not the prompt, the center of the system. This is essential because the user’s stated need is not “a prompt notebook” but a project-context prompt engine.

### 3.2 Good separation between prompt management and prompt generation
Separating the **Prompts module** from the **Factory module** is correct. Prompt lifecycle management and guided prompt generation are related but materially different concerns.

### 3.3 Strong handling of extensibility
The generalized option model and descriptor-driven resource model are both necessary. Without them, every new stack option or resource type would cause repeated redesign.

### 3.4 Correct choice of modular monolith
The selected architectural style is realistic. It avoids the operational cost of microservices while preserving future extraction seams.

### 3.5 Serious treatment of secrets and dangerous actions
This is one of the most important strengths. The architecture does not pretend that prompts, credentials, SSH, Docker, and scripts can live casually in the same domain without explicit safety boundaries.

### 3.6 Clear validation and testing path
Validation is treated as a core area rather than an afterthought. This aligns with the user’s requirement that planning, architecture, implementation, and tests must all be reviewable.

## 4. Pressure-test findings

## 4.1 Finding A — Resource breadth is the biggest implementation risk
The requirement list includes many resource types:
- folders
- files of many formats
- web links
- FTP
- PowerShell
- repositories
- Docker
- SSH
- keys/secrets
- prompts

This breadth is manageable only if the generalized resource model remains disciplined.

### Decision
Keep the generalized `ProjectResource` model, but enforce a typed descriptor registry from the start.

### Required control
No resource type should bypass the descriptor model.

## 4.2 Finding B — File preview expectations can grow too fast
The request includes many file types. A hidden trap is trying to provide deep rich preview for all of them immediately.

### Decision
Support all file types for registration in v1, but prioritize rich preview for:
- markdown
- text
- mermaid
- common document text extraction
- generic metadata fallback for unsupported binaries

### Required control
Preview and indexing are separate capabilities. A file may be linkable even if deep preview is not implemented yet.

## 4.3 Finding C — Prompt factory complexity is easy to underestimate
The factory is not just a form; it is a composition engine that gathers structured context, chooses blueprints, validates completeness, and records output.

### Decision
Keep the factory as its own module with explicit pipeline steps and session persistence.

### Required control
Do not bury factory logic in UI components.

## 4.3A Finding C1 — Prompt reuse will decay unless shared blocks are first-class
The user’s repeated architecture, review, planning, implementation, and testing flows are too similar to manage as copied prompt fragments. Without a shared block model, reuse will become inconsistent quickly.

### Decision
Treat shared prompt blocks and prompt-flow templates as first-class Factory assets that are centrally managed and branch-aware.

### Required control
Do not hardcode repeated prompt instructions into pages, one-off handlers, or project-local records.

## 4.4 Finding D — Validation can become a “feature landfill”
The validation center covers many review types. Without a consistent internal model, each one would become custom code.

### Decision
Use a common `ValidationRun` / `ValidationFinding` model and specialized strategies per validation kind.

### Required control
All validation types must write results into the same core result model.

## 4.5 Finding E — The temptation to overuse LLMs is dangerous
Because this is a prompt-oriented application, there is a risk that every validation step becomes AI-driven.

### Decision
Use deterministic rules and checklists as the base. AI-assisted critique is optional and additive.

### Required control
Every validation screen must distinguish:
- hard failures
- checklist gaps
- AI suggestions

## 4.6 Finding F — Single `AppDbContext` vs many contexts
A purist modular architecture might argue for one context per module. That is not the most practical v1 decision.

### Decision
Use one `AppDbContext` in v1 with module-owned configurations and `IDbContextFactory`.

### Reason
This simplifies migrations and implementation without eliminating future extraction paths.

### Required control
Module code must not query across boundaries casually just because the context is shared.

## 4.7 Finding G — Execution-related integrations need hard boundaries
Docker, SSH, FTP, and PowerShell introduce risk far beyond ordinary metadata management.

### Decision
V1 focuses on:
- storing profiles/resources safely
- validating connectivity where appropriate
- keeping execution behind explicit approval gates

### Required control
Do not let the generated prompt workflow silently trigger execution workflows.

## 4.8 Finding H — Search can become a premature architecture sinkhole
Full-text search, semantic search, vector search, and indexing can easily absorb too much implementation time.

### Decision
Start with a simple search document abstraction and relational implementation.

### Required control
Do not optimize for future vector search before core workflows work.

## 4.9 Finding I — UI cohesion can be lost through module growth
Many modules can become a fragmented UX quickly.

### Decision
Use one shell, one page pattern, one right-side context/action drawer, and a small set of page templates.

### Required control
Every new feature must fit the shell instead of inventing a new interaction pattern.

## 4.10 Finding J — Browser tabs are the wrong concurrency model for this product
The product is supposed to become a daily workstation with many concurrent artifacts open at once. If the architecture leaves that to browser tabs, Interactive Server circuits and render trees will multiply in a way that is expensive and operationally messy.

### Decision
The application needs an internal tab workspace with active, background, and sleeping states.

### Required control
Tab lifecycle, restore, and browser storage persistence must be architecture-level concerns, not page-level convenience code.

## 4.11 Finding K — The project structure surface was underspecified
The package previously described projects, prompts, validations, and tests, but it did not define the visual orchestration surface that connects them operationally.

### Decision
Introduce the project structure canvas as a first-class workbench feature using the documented playlist-builder canvas strategy as the starting point.

### Required control
Version one must wrap the proven JavaScript engine instead of inventing a new graph renderer in C#.

## 4.12 Finding L — The project calendar is not optional administration detail
For delivery work, milestones, prompt deadlines, reviews, releases, and test windows matter. Without a project calendar linked to project artifacts, the product will be structurally complete but operationally awkward.

### Decision
Introduce a project events calendar as a first-class workbench feature using the documented calendar wrapper strategy.

### Required control
Calendar items must link back into internal tabs and project artifacts.

## 4.13 Finding M — Console parsing alone is too weak for a trustworthy dev loop
`dotnet watch` output is useful, but a purely text-parsing approach can create false-ready behavior if the app builds successfully and then faults during startup or restore.

### Decision
Introduce a separate local development manager and require a development-only runtime readiness endpoint in the main app.

### Required control
The manager must emit `Ready` only when both normalized watch output and runtime readiness agree.

## 4.14 Finding N — Source documentation will drift unless capsules are enforced
The proposed compressed source documentation is high leverage, but it will decay quickly if it is treated as an optional nice-to-have.

### Decision
Require structured source capsules for significant handwritten components and types, plus coverage and drift reporting.

### Required control
Missing or malformed capsules must be visible through manager APIs, checklists, and milestone gates.

## 4.15 Finding O — Tuning mode can become unsafe or noisy without hard boundaries
Direct UI-to-Codex request flows are powerful, but they can also create accidental over-automation, unclear provenance, or local security mistakes.

### Decision
Keep tuning mode explicitly development-only and route it through the manager with request correlation, approval policy, and watch-ready confirmation.

### Required control
Tuning requests must stay loopback-only, workspace-bounded, redacted, and traceable.

## 4.16 Finding P — Generated artifacts can destabilize the local loop
If capsule outputs, tuning attachments, or manager logs sit in watched paths, the local development loop can trigger itself repeatedly and become unreliable.

### Decision
Store manager artifacts in excluded paths and treat self-triggering watch loops as a first-class failure mode.

### Required control
The first implementation must include explicit exclusions and tests for loop prevention.

## 5. Architecture adjustments made after review

The following adjustments were made during review:

1. **Resource model standardized further**  
   Explicit descriptor registry added as a required pattern.

2. **Preview/indexing decoupled**  
   File registration no longer depends on deep preview support.

3. **Prompt factory separated more strongly from prompt library**  
   Prevents application logic from drifting into pages.

4. **Validation result model unified**  
   All review flows use the same core storage model.

5. **Execution boundaries made stricter**  
   Store/validate/approve/execute are distinct steps.

6. **Search intentionally simplified for v1**  
   Avoids early complexity sink.

7. **Single DbContext decision explicitly justified**  
   Keeps the v1 implementation realistic.

8. **Internal workbench made explicit**  
   The application now requires internal tabs, sleeping-tab lifecycle, and browser-state restore.

9. **Project structure canvas added as a first-class surface**  
   Prompt and project orchestration now have a visual model instead of only lists and forms.

10. **Project calendar added as a first-class surface**  
    Scheduling and artifact linkage are now covered in the architecture itself.

11. **Development manager added as a first-class local tool**
    Watch supervision, readiness signaling, and Codex-facing APIs are now explicit architecture.

12. **Source capsules and drift reporting added**
    Codex-friendly compressed documentation is now a governed part of the source tree.

13. **Dev-only tuning mode added with guardrails**
    Targeted UI refinement is now structured instead of ad hoc.

14. **Shared prompt blocks and flow templates added as first-class architecture**
    Repeated delivery instructions are now modeled centrally instead of being left to copy-paste.

15. **Canvas ownership boundary made explicit**
    JavaScript remains the rendering and interaction layer while C# owns business state, validation, persistence, and command semantics.

## 6. Remaining residual risks

### Residual risk R1 — Too much v1 surface area
Even with strong architecture, the requested feature set is large.

**Mitigation**
- implement in milestones
- keep acceptance criteria strict
- do not treat every connector preview/parser as equal priority

### Residual risk R2 — Connector and parser edge cases
Real connector validation and document parsing can be messy.

**Mitigation**
- isolate adapters
- keep fallbacks
- log safe, actionable diagnostics

### Residual risk R3 — Prompt blueprint quality drift
If blueprints are created without governance, quality may degrade.

**Mitigation**
- version blueprints
- apply validation checklists
- require review for “recommended” blueprints

### Residual risk R4 — Hidden secrets in exported or logged content
Prompt contexts may accidentally include sensitive information.

**Mitigation**
- pre-send validation
- redaction layer
- explicit warnings
- safe defaults for export/send behavior

### Residual risk R5 — Background processing complexity
Jobs, indexing, health checks, and evidence handling can create operational noise.

**Mitigation**
- visible job states
- small number of job types initially
- avoid speculative background work

### Residual risk R6 — Workbench state corruption or stale restore
Restoring many internal tabs after interruption can fail in subtle ways if snapshot contracts are weak.

**Mitigation**
- version tab snapshots
- isolate browser-storage persistence behind explicit interfaces
- allow partial restore instead of all-or-nothing failure

### Residual risk R7 — Canvas over-generalization
The structure canvas could become a vague generic graph editor and lose delivery focus.

**Mitigation**
- keep typed node categories
- keep typed relationship vocabulary
- keep wrapper-first reuse of the documented engine

### Residual risk R8 — False-ready or stale-ready signals
If the manager or readiness probe becomes weak, automation may test or notify against the wrong runtime state.

**Mitigation**
- require runtime readiness confirmation
- keep explicit watch state history
- test failure and crash transitions, not only successful rebuilds

### Residual risk R9 — Capsule maintenance fatigue
Teams may skip capsule updates when moving quickly.

**Mitigation**
- require capsule coverage reporting
- keep the capsule format compressed and low-friction
- add checklist and prompt pressure to maintain it

### Residual risk R10 — Tuning loop abuse
A fast local tuning loop can tempt teams to bypass review or rely on automation without enough inspection.

**Mitigation**
- default to review-before-send
- keep dev-only scope explicit
- store correlation history and verification outcome

### Residual risk R11 — Shared prompt blocks become unmanaged copy variants
Even with the model in place, teams may still duplicate central prompt instructions locally when deadlines tighten.

**Mitigation**
- require the shared block catalog in milestone reviews
- make duplicated instruction fragments a review finding
- keep flow-template editing separate from ad hoc prompt text editing

### Residual risk R12 — Canvas command logic drifts into JavaScript
The more visually capable the canvas becomes, the stronger the temptation to let JavaScript mutate business state directly.

**Mitigation**
- keep typed C# command handlers mandatory
- test command routing without the renderer
- treat JavaScript-owned business mutations as an architectural defect

## 7. Why the architecture is still approved

Despite the risks, the architecture remains approved because it:
- covers the full requested capability set
- is realistic for local-first delivery
- avoids premature distribution
- handles safety concerns seriously
- gives Codex a structure that can actually be implemented incrementally

## 8. “What would fail this architecture?” checklist

The architecture would be considered inadequate if any of the following happened:
- secrets were embedded directly in general resource tables without protection
- prompt factory logic were implemented as page-only code without a service layer
- repeated prompt instructions were duplicated across screens instead of governed through shared blocks
- connector types were added ad hoc with no common descriptor model
- validation flows each invented different result storage models
- project options became hardcoded UI instead of a generalized catalog
- the shell fragmented into unrelated tool pages
- canvas interactions started mutating business state directly in JavaScript
- microservice readiness were claimed without real boundaries and contracts
- `DbContext` lifetimes were mishandled in Blazor and caused concurrency errors

## 9. Go-forward architectural constraints

The implementation team must preserve these constraints:
1. Keep module boundaries explicit.
2. Keep one `DbContext` per operation.
3. Keep secrets centralized and encrypted.
4. Keep prompt generation and prompt storage separate.
5. Keep review models standardized.
6. Keep execution approval explicit.
7. Keep UI patterns consistent.
8. Keep background work visible and diagnosable.
9. Keep internal tab lifecycle explicit and recoverable.
10. Keep project structure and calendar surfaces linked to the real artifact model.
11. Keep manager readiness signals explicit, local-only, and testable.
12. Keep capsule coverage and drift visible.
13. Keep tuning mode dev-only, correlated, and reviewable.

## 10. Review verdict

### Verdict
**Approved with controlled complexity**

### Meaning
The architecture is complete enough and strong enough to move into implementation, provided the implementation plan follows the phase order and does not collapse the design into shortcuts.
