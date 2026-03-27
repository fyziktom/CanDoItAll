
# Validation prompt

Validate **I03 — Meeting nodes for online and onsite work** as a strict QA inspector.

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

- dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter ProjectCalendarPageTests|ProjectStructurePageTests
- dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj

## Mandatory screenshot review focus

- Online meeting node details with channel selector and repeating cadence.
- Onsite meeting node details with address and map link.
- Calendar screenshot showing a meeting-derived event.
- Meeting context menu with meeting-specific actions.

## Fail this item if

- the visible UI does not match the note intent,
- implementation scope drifted outside the normalized design,
- the implementation ignores shared module reuse opportunities,
- the evidence is incomplete or hand-wavy.
