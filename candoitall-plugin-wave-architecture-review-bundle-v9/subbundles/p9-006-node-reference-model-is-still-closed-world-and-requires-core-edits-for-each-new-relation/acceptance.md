# Acceptance
This subbundle closes only when:
- the active code no longer exhibits the forbidden patterns,
- the required tests exist and pass,
- the repo-wide hard gate passes,
- the closure proof matches the actual code.

Target acceptance:
A new plugin-defined node relation can be stored and queried without adding enum members or adding new properties to ProjectNodeReferenceSet.
