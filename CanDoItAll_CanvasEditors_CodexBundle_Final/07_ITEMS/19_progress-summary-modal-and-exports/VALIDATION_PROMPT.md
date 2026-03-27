
# Validation prompt

Validate **I19 — Progress summary modal, tree checklist, and exports** as a strict QA inspector.

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

- dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter ProjectStructurePageTests
- dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj

## Mandatory screenshot review focus

- Progress summary modal opened from a node with children.
- Tree checklist with inline progress selector on one row.
- Evidence of XLSX and Mermaid Gantt export output.

## Fail this item if

- the visible UI does not match the note intent,
- implementation scope drifted outside the normalized design,
- the implementation ignores shared module reuse opportunities,
- the evidence is incomplete or hand-wavy.
