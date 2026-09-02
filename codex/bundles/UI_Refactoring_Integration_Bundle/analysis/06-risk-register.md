# Risk Register

| ID | Risk | Severity | Mitigation / proof |
|---|---|---:|---|
| K-001 | v2 commits enter the original integration | Critical | Dynamic denylist and ancestor checks at four gates |
| K-002 | Components upstream remains red | Critical | Repair approval/asset tests before downstream closure |
| K-003 | Clean source checkout lacks BaseLib CSS | Critical | Commit distributed output and verify deterministic regeneration |
| K-004 | Main CI pins stale sibling SHAs | High | Update to exact final commits and add source-asset assertion |
| K-005 | Old Material Icons classes render blank glyphs | High | Migrate asset, raw spans, CSS selectors, and tests |
| K-006 | Tailwind preflight changes host layout | High | Large-desktop visual proof on representative pages |
| K-007 | FileTools gains an accidental Components dependency | High | Existing package validator plus explicit dependency search |
| K-008 | Package families produce different versions | High | One selected `V`, local pack inspection, consistency script |
| K-009 | Package/source modes are mixed in one obj graph | High | Clean outputs and consistent MSBuild property per graph |
| K-010 | Old Podman instructions mislead macOS users | Medium | Relocate and reconcile with current operations docs |
| K-011 | Broad test runs consume excessive context/time | Medium | Targeted tests per phase; broad gates only in SB08 |
| K-012 | Approval updates conceal unintended API removal | High | Capture and review semantic diff before update |
| K-013 | Main/Docker works only because of local untracked CSS | Critical | Clean-clone/source-asset and container proof |
| K-014 | Package version already exists on a feed | Medium | Query every configured feed before choosing `V` |
| K-015 | Direct merge to main creates duplicate/opaque history | Medium | Canonical `ui-refactoring -> development -> main` flow |
| K-016 | Existing dirty work is overwritten | High | Baseline inventory and stop-on-unowned-diff contract |
| K-017 | Icon token aliases produce different glyph semantics | Medium | Do not mass-rename; inspect visible failures only |
| K-018 | macOS Podman path cannot be executed in environment | Low | Mark proof unavailable and validate commands/document consistency |
