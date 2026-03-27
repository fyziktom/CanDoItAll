
# Validation prompt

Validate **I05 — Participants and CRM-lite registry** as a strict QA inspector.

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

- dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj
- dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter ProjectStructurePageTests

## Mandatory screenshot review focus

- Participant selector with existing people and AI Agent variants.
- Organization-style subtree with Team block, Team Section, HR, Freelancer, Partner, and AI Agent nodes.

## Fail this item if

- the visible UI does not match the note intent,
- implementation scope drifted outside the normalized design,
- the implementation ignores shared module reuse opportunities,
- the evidence is incomplete or hand-wavy.
