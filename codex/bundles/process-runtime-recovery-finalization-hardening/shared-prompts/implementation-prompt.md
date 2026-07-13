# Implementation Prompt

Use this prompt when executing any subbundle in this initiative.

```text
Implement only the assigned subbundle from codex/bundles/process-runtime-recovery-finalization-hardening.

Read first:
- README.md
- inputs/00-original-request.md
- requirements/01-normalized-requirements.md
- requirements/02-user-stories-and-exceptions.md
- architecture/01-target-solution.md
- architecture/01-csharp-boundary-map.md
- architecture/02-csharp-dependency-direction.md
- architecture/03-csharp-pattern-selection-records.md
- architecture/04-csharp-testability-plan.md
- architecture/05-process-flow-and-target-protocol.md
- plan/01-phase-plan.md
- plan/architecture-checkpoints.md
- the assigned subbundle README.md

Hard constraints:
- Keep the process runtime and dispatcher generic.
- Do not add software-development, AgentFramework, MAF, browser, GitHub, or .NET-delivery concepts to generic runtime contracts.
- Do not silently retry missing artifacts, missing tools, denied access, missing manager handoff, invalid template connections, or unknown failures.
- Do not add partial files to ProcessRuntimeEngine or AgentFrameworkProcessExecutionAdapter as the final design.
- Use strongly typed identifiers, states, categories, and contracts. Avoid magic strings except UI text, SQL, or unavoidable external protocol values.
- Keep changes minimal and aligned with existing C# style.
- Do not generate XML documentation comments.

Before editing:
- Confirm prerequisites from the assigned subbundle are complete.
- Confirm no downstream subbundle depends on a contract you are about to weaken.
- Capture characterization or failing-first proof for the behavior being changed.
- Identify the owning project for every new type.

During implementation:
- Prefer extraction of cohesive services over partial-class expansion.
- Keep runtime decision logic unit-testable without Module or AgentFramework dependencies.
- Keep driver-specific policy behind driver abstractions or concrete drivers.
- Update persistence and projections only where needed for durable runtime facts.
- Keep manager diagnostics actionable and sensitive data masked.

Proof required:
- Run the targeted tests listed in the assigned subbundle.
- Capture failing-first and passing proof for critical behavior.
- Add source assertions for dependency direction and partial-class policy when architecture is affected.
- Update reviews/01-execution-report.md with commands, outcomes, changed files, proof artifact paths, and raw-note impact.
- Update subbundle proof manifests when the subbundle requests them.

Stop conditions:
- Stop and report a blocker if a prerequisite subbundle is not complete.
- Stop if a required artifact lineage or step contract cannot be represented without breaking generic runtime boundaries.
- Stop if the only available solution is a prompt-only workaround or silent fallback.
- Stop if tests would need broad manual state seeding that bypasses production launch, dispatch, adapter, artifact, or manager paths for critical proof.
```
