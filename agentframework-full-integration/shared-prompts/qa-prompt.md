# Shared Prompt — QA / Review

```text
Review the current implementation against the bundle as a strict QA inspector.

Check all of the following:
1. Requirement closure:
   - Compare the implementation to `requirements/01-normalized-requirements.md`.
   - Use `traceability/01-requirement-traceability.md` and `traceability/02-input-coverage-matrix.md`.
2. User-story closure:
   - Validate every story in the workbook and `traceability/03-story-to-ui-surface-matrix.md`.
   - If any story cannot be completed in the UI, treat it as an open defect.
3. Source-of-truth integrity:
   - Confirm there is still only one canonical write path for providers, agent definitions, resource identities, launch plans and conversations.
4. Process integrity:
   - Verify that no direct agent communication can happen without process messaging policy.
   - Verify that process launch really goes through recommendation and approval before creating the actual run.
5. Collaboration quality:
   - Check unread state, escalation routing, thread context and run transcript completeness.
6. UI/UX quality:
   - Use Playwright MCP plus screenshot review questions from the bundle.
   - Evaluate readability, spacing, hierarchy, discoverability and consistency with the existing shell.
7. Scenario honesty:
   - Demand real scenario runs and artifacts.
   - Reject any proof that only seeds DB rows or bypasses the declared flow.
8. Regression quality:
   - Check targeted build, unit, component, integration and Playwright evidence.
9. Cleanup quality:
   - Confirm legacy duplicate paths are retired or strictly gated.
10. Final readiness:
   - Produce explicit blocker / concern / pass conclusions from QA, development manager and architect perspectives.

Output format:
- Passed
- Passed with concerns
- Blocked

For every concern, cite the exact requirement or user story and the missing proof.
```
