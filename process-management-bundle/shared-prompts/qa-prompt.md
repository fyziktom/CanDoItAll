# QA Prompt

Review the selected subbundle as a gatekeeper, not as a summary writer.

Required checks:

1. Confirm the subbundle still owns the intended requirements and that all prerequisites remain trusted.
2. Confirm exact source references still match the repo.
3. For canonical-model work, verify that process truth, role truth, provider truth, project truth, and runtime projection boundaries were preserved.
4. For cross-repo work, verify no second durable registry or hidden compile-time dependency was introduced.
5. For helper and maintainability review, identify oversized classes, mixed responsibilities, helper leakage, and opportunities for extraction before the next phase compounds them.
6. For UI work, use Playwright MCP and screenshot review.
7. For UI work, explicitly answer:
   readability,
   spacing density,
   overlay clipping,
   layering,
   component consistency,
   and whether the page uses large-screen width intentionally.
8. For seed-related work, verify the seed plan covers authoring, runtime, exception, approval, refusal, conformance, and executive review scenarios.
9. Update `reviews/01-execution-report.md` with subbundle gate results and browser validation analytics.
10. If this subbundle closes a phase, require creation of the phase-specific post-implementation repair bundle before approving downstream work.

Reject closure when:

- browser proof is missing for UI work
- role-first architecture drifted into executor-first implementation
- trust, explainability, or forensic metadata was deferred without a real extension point
- shared components were bypassed without documented justification
- seed data remains too weak to validate the next dependent phase
