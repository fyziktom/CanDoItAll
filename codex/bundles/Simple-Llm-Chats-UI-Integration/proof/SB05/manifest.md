# Proof Manifest — SB05

- Status: `Completed`.
- Proof tier: `Governed`.
- Owned requirements: `SCUI-004`, `SCUI-005`, `SCUI-009`, `SCUI-017`, `SCUI-021`, `SCUI-022`, `SCUI-024`, `SCUI-062`.
- Start commit: `9d806df5ef8fa58096669f401e601c1dabf2fee9`.
- Candidate commit: `9d806df5ef8fa58096669f401e601c1dabf2fee9` (checkpoint; no production diff).
- SharedInfo commit/hash: `7b7808e8591d7219f40826cf0e5624e182981d90`.
- Semantic contract: `bundle://proof/SB05/semantic-invariants.md`.
- Architecture decision: `bundle://proof/SB05/architecture-gate.md`.
- Execution report: `bundle://proof/SB05/execution-report.md`.

## Scope

SB05 validates the final SB02-SB04 source union, proves Agent catalog/settings/main/floating/contextual parity in the real browser, verifies the pre-CP1 activation fence, and issues the architecture/progression decision. It makes no production change, does not add a Simple Chat route or catalog item, does not activate floating Simple Chat integration, and does not run Stable or full Playwright. The bundle-only phase validator is made execution-aware so its activation assertions follow completed CP1/CP2 checkpoints instead of remaining hard-coded to the prepared state.

## Bundle and proof integrity

Hashes are SHA-256 over exact file bytes at the start commit and working candidate.

| Path | Before | After |
|---|---|---|
| `bundle://subbundles/SB05-cp1-hardening-and-agent-parity-gate/README.md` | `e42278db505cda4da5d5390667010c0a90edb6067580749fb183f672b71b2793` | `30f0459ea2cb310ffd4332b1f956d3cd2abb631df3aeb8cc899d720e1c604aaa` |
| `bundle://bundle-status.json` | `0db3e3f51e1630f032529faccfb8052197d4ead85e2660aba5c59a92b48fddf3` | `0e5ba9fd90c63feb2a33f2d7b5aa70497da22d222850096579ab25bd5c8c403a` |
| `bundle://reviews/01-execution-report.md` | `12a18ab3c2e29f25f19c4d90f780f5c5aa9f702185665f7f4718c816880ad99f` | `27031b59aa56d81a10bab9e6dbdc49aa1e9bbaf64c56b72c9f627bdc7a573b2d` |
| `bundle://reviews/csharp-architecture-gate.md` | `45256616a49f7aa077b0777af60197d9c90743bc2c0a24c11d81d7981dab2c60` | `1bc43224b3e650d85e358ce4842abed2416b8aaa7178dede992ea102dd29484c` |
| `bundle://scripts/check_phase_exclusions.py` | `d133f9c260bc27ff7aad54cb82db45304bf40f1bb92c97df5cc10230bf34db58` | `1a4558c1d236f3babf7a3352ce080336eb56ad8c41f81a06e12b7f8b97a69ad1` |
| `bundle://proof/SB05/architecture-gate.md` | absent | `3ac8fb74b2ca028ef2576e1e15b549c6a4d3ffc41a14727317006bd91a81b1a9` |
| `bundle://proof/SB05/execution-report.md` | absent | `5a64bc30b8b3d12ba1ec72d75ba45fc9bcbc4263fa3eee3a7f97532cfbf1dfc7` |
| `bundle://proof/SB05/semantic-invariants.md` | absent | `5bafb4bd57bec0ec5f4fbcb92ceb44b19c77778a3cae0ba9cf9c8ef7b1a0181a` |
| `bundle://proof/SB05/transcripts/01-impact-analysis.md` | absent | `721d243939f1f3c48730d21107196b7032e0071a7ab6867b7d4aab165be1385d` |
| `bundle://proof/SB05/transcripts/02-required-components.md` | absent | `36f22f4c576b9a6034fa306f21b45f3c7db17c2a048902b25dbc4d967ce0a9bb` |
| `bundle://proof/SB05/transcripts/03-required-unit.md` | absent | `d23611ce2bedecb419d6e0e82f69d969646c6c0ba9a6c5e5e1c1a35760070730` |
| `bundle://proof/SB05/transcripts/04-required-integration.md` | absent | `699f59eedb286deab66b99d81dd0c7215fdbe439c7fd8b0ec31cbb0b18af9d54` |
| `bundle://proof/SB05/transcripts/05-browser-agent-parity.md` | absent | `7202d157feb02cecaf08943ffa23734791993ce8899666fca167e4d497876b95` |
| `bundle://proof/SB05/transcripts/06-architecture.md` | absent | `ea2aa595a02da488c59f2894a15bb2299ea64e6f754695473d120670a53d55ac` |
| `bundle://proof/SB05/transcripts/07-source-and-scope.md` | absent | `fff463c2b1aa9cbdcfa1ebaadb83ca05b83f541a9031ad6bfa05226f8afaeea8` |
| `bundle://proof/SB05/screenshots/SB05-agent-floating-chat-open-1600x1000.png` | absent | `00a6242c2a09a04f312b16fa8570ce4ceacde2afee69e3a7b62623ab628b0242` |
| `bundle://proof/SB05/screenshots/SB05-agent-main-chat-1600x1000.png` | absent | `fdf8bd30cbfd406754ec74bfe795776d728c1c88e74641a32ca94ee6884e2451` |
| `bundle://proof/SB05/screenshots/SB05-agent-settings-runtime-open-1600x1000.png` | absent | `f00b731f6a9556720685ca223ac96aefe607312b2ad98b5ecbca12eaff213ef7` |
| `bundle://proof/SB05/screenshots/SB05-agents-catalog-1600x1000.png` | absent | `d8241248143e2d51956067197eb1de7c61d0b4263831b9af15753486559017f1` |
| `bundle://proof/SB05/screenshots/SB05-project-structure-contextual-agent-1600x1000.png` | absent | `ba597d1de109930a507c50c30d60f45d80af955551967ab8c5ba34be0d7a037b` |
| `bundle://proof/SB05/screenshots/SB05-simple-chat-route-absent-404-1600x1000.png` | absent | `8781944c57d59bed7a7edc9ee2e87d9cac7c1036ef6cc8d3ea8703c59facc9a4` |

The manifest omits its own self-referential digest. Its integrity is checked by the bundle validator and the proof commit.

## Validation and artifact matrix

- Impact selection: `code-analytics_9baf462acc914870b572970e410b0448`; healthy workspaces with low-confidence `AllSuppliedSuites` fallback.
- Component: 1,007 passed, 0 failed, 0 skipped.
- Unit: 6,229 passed, 0 failed, 0 skipped.
- Integration: 850 passed, 4 failed, 1 expected skip; three unchanged baseline defects, plus one CAS test that passed 1/1 on exact isolated retry.
- Architecture: snapshot `snap-20260816214112-d26d371e`; no project-reference or new-cycle change.
- Browser: real managed app, Playwright MCP, 1600x1000, Agent settings/main/floating/contextual scenarios pass; pre-CP1 `/chats` returns 404.
- Production source: no change at SB05.

## Acceptance and progression

All acceptance criteria pass with the architecture criterion interpreted by the bundle's explicit dependency rule: no new cycle. CP1 passes, `simpleChatUiActivationAllowed` becomes true, and SB06 is unlocked. Floating Simple Chat integration and full Playwright remain locked until their later checkpoints.
