# Package-only validation report

Date: 28 September 2026.

## Passed checks

- JSON registries parsed and stable IDs checked for uniqueness and valid references.
- Every invariant has a proposed test link; forward/reverse mappings agree.
- Phase dependency graph is acyclic and every phase has its review card.
- All local Markdown file links resolve within the package.
- Narrative registry identifiers resolve to existing register entries.
- All five Python utility/self-test files parse successfully.
- Eleven isolated utility self-tests passed: unchanged Git evidence with no logical repository changes; changed blob; relocation candidate; dirty-tree disclosure; refusal of output inside the application repository; refusal to overwrite an existing output; invalid option-like ref rejection; content-tamper detection; unknown reference detection; phase-cycle detection; broken-link detection.
- Final SHA-256 manifest and ZIP entry/CRC/content identity were checked during packaging.

## Contents checked

22 architecture documents; 9 phase cards; 11 JSON registers; 7 generated readable register views; 3 review templates; 5 Python files; package metadata, README, index, changelog, this report and integrity manifest.

The registers contain 27 source entries, 25 invariants, 22 proposed decisions, 16 selected findings, 25 mutation groups, 36 conditional risks, 96 proposed test scenarios, 11 planned performance workloads, 9 proposed phases and 16 open questions. Counts of mutation groups are not counts of completely audited writers.

## Application validation not performed

No CanDoItAll source build, migration, database reproduction, owner test, UI/browser journey, live provider/process execution, evaluated application graph or performance benchmark was run for this package. Proposed application cases remain `proposed-not-executed`; measured performance fields remain null. Historical test claims in repository documentation were not revalidated.

Utility tests run only in disposable temporary local Git fixtures and temporary copies of this package. They do not connect to the application's database or external services. Their passing status must never be cited as application correctness proof.

## Revalidation after edits

Run the register-view generator, review narrative changes, rebuild the manifest, then run the package validator and utility self-tests. Rebuilding a manifest acknowledges the current package contents; it is not a signature or an architectural approval. Any changed source/caller/contract still requires the actual review and test gates described in the phase plan.
