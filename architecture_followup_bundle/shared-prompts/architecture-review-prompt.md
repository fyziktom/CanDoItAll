# Architecture review prompt

Review the just-completed subbundles against the current live repository state.

Required output:
- explicit pass/fail decision;
- answers to the gate questions;
- the exact red/amber gaps that still remain;
- whether a corrective subbundle must be opened before continuing;
- proof references, not generic assurances.

Reject the gate if any domain invariant is still only assumed in code when it should be enforced in the schema or command boundary.
