# Subbundle Readiness Checklist

## Structure Checks

| Check | Result |
| --- | --- |
| SB01-SB28 folders exist. | Pass |
| Every subbundle has README.md. | Pass |
| Every README has required standard sections. | Pass |
| Every README has context reset instructions. | Pass |
| Every README has source evidence. | Pass |
| Every README has prerequisites. | Pass |
| Every README has in-scope and out-of-scope lists. | Pass |
| Every README has target projects/files. | Pass |
| Every README has implementation steps. | Pass |
| Every README has refactoring review checkpoint. | Pass |
| Every README has tests/proof/search proof. | Pass |
| C# hot-path subbundles are governed by performance scan checklist. | Pass |
| Every README has stop-and-report conditions. | Pass |
| Every README has do-not-do rules. | Pass |
| Every README has acceptance checklist. | Pass |
| Every README has handoff notes. | Pass |
| SB21 owns role candidate readiness, missing tool/right blocker proof, provisioning reassessment, and launch UI evidence. | Pass |

## Readiness Judgment

The subbundles are detailed enough for later Codex implementation after user approval, assuming the future agent reads the context reset files and previous subbundle reports named in each README.

The previous broad UI subbundle has been decomposed. Browser-facing Process work now has localized proof gates for workspace shell, definition list, definition editor, roles, canvas, step editor, templates, exchange/Git UI, launch, run history, runtime view, operator control, evidence/coordination, analytics/live, and project/API compatibility.

## Known Constraints For Future Agents

- Do not execute multiple dependent subbundles in one pass unless the user explicitly requests it.
- Do not skip SB01/SB02.
- Do not restore build by reviving old dispatcher/runtime semantics.
- Do not merge active removal without skeleton restoration if repository policy requires every commit to build.
- Treat proof and review gates as deliverables, not optional notes.
- Every future execution report must update user-story coverage for the story IDs it owns.
- Browser-facing story proof must be captured in the owning UI subbundle and repeated selectively in final regression.
- C# hot-path implementation proof must include exact performance scan counts from `validation/05-dotnet-performance-antipattern-checklist.md`.
- Role candidate readiness proof must keep HR score separate from deterministic missing tool/right/capability findings.
