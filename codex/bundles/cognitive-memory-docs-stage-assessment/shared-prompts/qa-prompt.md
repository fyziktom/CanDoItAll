# QA Prompt

Review the bundle and docs for closure.

Coverage checks:

- Confirm the raw request is preserved.
- Confirm each normalized requirement maps to a subbundle and proof artifact.
- Confirm `docs/cognitive-memory` has current-state, architecture, operations, and roadmap subfolders.
- Confirm the stage is stated as validation-grade alpha with beta blockers.
- Confirm Mermaid graph types are present: architecture-beta, flowchart, classDiagram, and sequenceDiagram.

Dependency gates:

- Subbundle 02 cannot pass unless subbundle 01 source audit is credible.
- Subbundle 03 cannot pass unless the new docs section and diagrams exist.
- Closure cannot pass unless bundle validators and `git diff --check` pass.

Browser and host validation:

- Browser validation is N/A because no UI route, component, CSS, or rendered behavior changed.
- Record N/A explicitly in execution report analytics.

Raw-note closure:

- Every explicit user ask must be marked solved with a proof path.

Blocker handling:

- If the validator fails, repair bundle metadata before closure.
- If markdown diff validation fails, repair whitespace before closure.
- If runtime code changed unexpectedly, run targeted .NET tests before closure.
