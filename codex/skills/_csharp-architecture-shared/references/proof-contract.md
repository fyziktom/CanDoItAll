# Architecture Proof Contract

Architecture proof must show that the new design is real, not cosmetic.

## Required proof artifacts

For a critical architecture subbundle, collect:

- current-state responsibility inventory
- target project/type map
- dependency direction map before and after
- pattern selection records
- changed-file list with hashes
- build transcript
- targeted unit test transcript
- integration/composition smoke transcript when wiring changed
- source assertions showing the old class no longer owns moved behavior
- anti-stub audit showing no placeholder, fixture-only, or NotImplemented path
- extension-seam proof when the work is about future tools/providers/drivers
- follow-up list for any remaining temporary bridge

## Source assertions

The proof should include assertions such as:

- old runtime file no longer contains provider-specific SDK construction
- old partial class count did not increase
- new provider implementation is in a dedicated project
- contracts project does not reference implementation project
- builder validates required fields before producing runtime definition
- factory selects implementations by contract and does not call back into the monolith for behavior
- unit tests instantiate extracted service directly with fakes

## Blocking proof gaps

Do not close the subbundle if:

- the new tests pass only because they manually seed internal state that production never creates
- a facade was added but all real logic remains in the original class
- moved code is duplicated instead of deleted from the old class
- the target project has no independent unit tests
- a cyclic reference was avoided by moving unrelated code into a shared dumping ground
