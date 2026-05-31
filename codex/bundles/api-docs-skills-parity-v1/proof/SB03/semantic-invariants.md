# SB03 Semantic Invariants

- Invariant ID: SB03-INV-001
- Source raw note: Missing calls must be identified and repaired without hiding gaps.
- Expected behavior: Process and project-structure runtime tool gaps are explicitly documented as HTTP-only unless typed tools are added with policy and approval coverage.
- Disallowed shallow implementation: Letting skills imply every HTTP operation is a direct MAF tool.
- Failing-first test: N/A, non-production documentation boundary; no runtime tool behavior was added.
- Passing test: `bundle://proof/SB03/transcripts/tool-boundary-audit.md`
- Changed source files: `repo://docs/agent-runtime-tool-surface.md`, `repo://codex/skills/candoitall-api-processes/SKILL.md`, `repo://codex/skills/candoitall-api-project-structure/SKILL.md`
- Production assertions: No direct runtime tools were added; no approval policy behavior changed.
- Red-team negative case: `bundle://proof/SB03/transcripts/tool-boundary-audit.md`
- Downstream dependency check: SB04 and SB05 reference the explicit HTTP-only boundary rather than claiming direct-tool parity.

