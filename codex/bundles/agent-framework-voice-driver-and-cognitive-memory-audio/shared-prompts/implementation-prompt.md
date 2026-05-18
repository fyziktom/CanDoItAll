# Implementation Prompt

Implement this bundle phase by phase. Start each subbundle by reading its README, `requirements/01-normalized-requirements.md`, `architecture/01-target-solution.md`, and `plan/01-phase-plan.md`.

Rules:

- Use the smallest correct change that keeps AgentFramework boundaries clear.
- Keep OpenAI behind provider-neutral interfaces and a strongly typed driver factory.
- Use existing provider credential resolution; never persist raw API keys.
- Keep Blazor components focused on rendering/orchestration and call services for voice work.
- Preserve Cognitive Memory review gates. Voice correction must create probe feedback/review candidates, not direct canonical memory edits.
- Add targeted tests as each phase lands.
- Record commands, browser proof, screenshots, gate results, and residual risks in `reviews/01-execution-report.md`.
