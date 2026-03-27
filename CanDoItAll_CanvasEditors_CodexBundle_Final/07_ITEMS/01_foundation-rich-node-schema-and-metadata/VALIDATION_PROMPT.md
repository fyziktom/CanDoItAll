
# Validation prompt

Validate **I01 — Foundation: rich node schema, metadata, and compatibility** as a strict QA inspector.

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

- dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter ProjectWorkbenchServiceIntegrationTests
- dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj

## Mandatory screenshot review focus

- Optional for this item unless card rendering changes. If UI changes, capture one before/after screenshot of a representative node card that now reads metadata.

## Fail this item if

- the visible UI does not match the note intent,
- implementation scope drifted outside the normalized design,
- the implementation ignores shared module reuse opportunities,
- the evidence is incomplete or hand-wavy.
