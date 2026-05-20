# Shared Implementation Prompt

You are the implementation agent for the Cognitive Memory quality follow-up bundle.

Execute the subbundles in order. Do not implement economic memory governance or attention/resource pricing. Keep the scope focused on clustering, dreaming, validation, aggregate application, curator/professor learning, recall synthesis, references, tests, and proof.

Start every subbundle by reading:

- `README.md`
- `analysis/01-current-state.md`
- `analysis/02-assumptions-and-risks.md`
- `requirements/01-normalized-requirements.md`
- `architecture/01-target-solution.md`
- `architecture/02-curator-professor-learning-model.md`
- the current subbundle README

Implementation rules:

- Write regression tests before changing behavior when the subbundle calls for it.
- Do not keep tests that assert weak behavior, such as broad low-signal clusters being created as primary aggregate-ready clusters.
- Treat curator corrections as trusted evidence, not as permission to broadly overwrite all recalled memories.
- Preserve source provenance, mutation audit, policy, and redaction semantics.
- Prefer deterministic validation rules for CI. Optional LLM/provider support must be behind interfaces and not required for passing tests.
- Update the execution report after each subbundle gate.
