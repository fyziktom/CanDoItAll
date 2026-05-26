# SB14: 14-skills-tools-and-agent-capability-regression

## Goal

Verify agents have needed skills/tools after MAF upgrade.

## Required work

- Re-run tool registry / capability matrix tests under MAF 1.6.
- Verify Blazor implementation, browser proof, project-structure writeback, and process artifact skills are discoverable after any skills API change.
- Account for MAF 1.6 skills discovery changes and `SkillFrontmatter` if relevant.
- Ensure agent roles do not improvise because skills/tools are missing.
- Add tests for missing skill/tool causing typed launch/dispatch block.

## Required proof

- Failing-first or adversarial proof.
- Passing proof on production code path.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on MAF 1.6 impact if this subbundle touches agent runtime.
- Notes on process core genericity if this subbundle touches Processes.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB14` are updated and the next subbundle can safely depend on it.
