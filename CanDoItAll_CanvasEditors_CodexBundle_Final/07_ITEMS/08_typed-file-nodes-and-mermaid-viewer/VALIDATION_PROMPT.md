
# Validation prompt

Validate **I08 — Typed file nodes and Mermaid viewer** as a strict QA inspector.

## Validation checklist

1. Confirm the implementation matches `SPECIFICATION.md`.
2. Verify every acceptance criterion explicitly.
3. Run or review the required tests.
4. Inspect the screenshots from `SCREENSHOT_REQUIREMENTS.md`.
5. Reject the item if screenshots are missing, weak, or unrelated.

## Questions you must answer

- What exactly changed in the product?
- Which acceptance criteria are visibly proven?
- Which tests prove behavior beyond the screenshots?
- What risks remain?

## Required tests to review

- dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj

## Mandatory screenshot review focus

- Canvas screenshot with pdf, excel, docx, txt/json/md, and Mermaid nodes side by side.
- Mermaid viewer screenshot with detected graph type visible.

## Fail this item if

- the visible UI does not match the note intent,
- implementation scope drifted outside the normalized design,
- the implementation ignores shared module reuse opportunities,
- the evidence is incomplete or hand-wavy.
