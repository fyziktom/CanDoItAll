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
- connector types were added ad hoc with no common descriptor model
- validation flows each invented different result storage models
- project options became hardcoded UI instead of a generalized catalog
- the shell fragmented into unrelated tool pages
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

## 10. Review verdict

### Verdict
**Approved with controlled complexity**

### Meaning
The architecture is complete enough and strong enough to move into implementation, provided the implementation plan follows the phase order and does not collapse the design into shortcuts.