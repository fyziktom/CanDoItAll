# SB16 Semantic Invariants

- Source raw note: Bundle required every critical subbundle to include source assertions, negative proof, passing proof, anti-stub audit, and changed-file hashes.
- Invariant ID: SB16-INV-001
- Expected behavior: Final closure includes red-team source audit, build/test proof, anti-stub audit, and portable changed-file hashes for the generic Blazor WASM PWA hardening.
- Failing-first test: proof/SB16/transcripts/failing-first.txt
- Passing test: proof/SB16/transcripts/passing.txt
- Changed source files: repo://Templates/Processes; repo://codex/skills/candoitall-api-processes/SKILL.md; repo://src; repo://tests/CanDoItAll.Tests.Integration; repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs
- Production assertions: Final build, targeted integration/component tests, source assertions, and hashes are recorded in proof/SB16/transcripts.
- Red-team negative case: proof/SB16/transcripts/failing-first.txt proves no prohibited demo-topic terms remain in protected process/template/runtime surfaces.
- Downstream dependency check: This is the terminal closure gate; the next live UI demo should supply app-topic details only at run start.
- Disallowed shallow implementation: prompt-only, docs-only, fixture-only, template-only, or source-assertion-only changes that do not affect required behavior.
