# Implementation Prompt

Implement only the current subbundle. Preserve the base process contract while moving domain and technology instructions to the right specialization boundary.

Rules:

- Do not add another global fallback prompt.
- Do not weaken artifact, evidence, or `PROCESS_STEP_OUTCOME` contracts.
- Keep C# changes small and strongly typed.
- Add tests near the current coverage surface.
- If a prompt assertion can be expressed as absence/presence of a focused phrase, prefer that over snapshotting the full prompt.
- Use PostgreSQL for process-execution validation.
