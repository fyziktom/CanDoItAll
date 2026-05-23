# Office365 Live Validation

## Status

- `Completed`

## Objective

Validate the Office365 category email summary workflow against the local app or API after SB01 hardening, proving the failure no longer occurs at `summarize-office365`.

## Success Criteria

- The local app at `http://localhost:5032` is reachable or the exact reachability blocker is captured.
- The Office365 category summary workflow is run or inspected against the connected account/category the user says is ready.
- No proof path shows the same malformed JSON failure at `summarize-office365` after SB01.
- If live execution is blocked, the blocker is specific and unrelated to the SB01 JSON hardening.

## Covered Inputs

- N002, N003, N004.
- Requirements R4, R5.

## Prerequisites

- SB01 status is `Completed`.
- SB01 closure gate passed with artifact-backed proof.
- The running local app is expected at `http://localhost:5032`.

## Exact Source References

- `repo://Templates/Workflows/workflows/default-workflows.yaml`
- `repo://docs/oauth-email-plugins.md`
- `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs`
- `repo://codex/bundles/office365-email-summary-project-scope-fix`

## Deliverables

- Live validation transcript, API transcript, or explicit live blocker transcript.
- Execution report update with browser/API validation analytics.
- Raw-note closure for N002, N003, and N004.

## Dependency Impact

- This is the final validation phase. It does not unlock more code phases, but it determines whether the user-facing Office365 workflow claim can be closed.

## Validation Depth

- End-to-end live validation or honestly blocked live proof with strong SB01 regression evidence.

## Implementation Steps

1. Confirm SB01 completed and proof paths exist.
2. Check `http://localhost:5032` reachability.
3. Identify the Office365 summary workflow route/API using existing app endpoints or browser flow.
4. Run or inspect the workflow against the connected category email.
5. Capture transcript/screenshot/API response.
6. Update execution report, browser validation analytics, and raw-note closure.

## Scope Exceptions

- If live auth blocks browser/API validation, record the auth blocker and do not mark the live run as solved.
- If the category email was already consumed by a prior run, record the observed state and keep R5 partial rather than inventing proof.

## Do Not Do

- Do not manually seed a fake Office365 message and call that live proof.
- Do not mark the email as processed unless the workflow storage path has succeeded.
- Do not hide a local app/API/auth blocker as a pass.

## Acceptance Checklist

- [x] SB01 closure proof reviewed.
- [x] App reachability result captured.
- [x] Office365 workflow run/inspection captured or explicit blocker recorded.
- [x] Execution report and raw-note closure updated.

## Proof Required

- `proof/SB02/transcripts/app-reachability.txt`.
- `proof/SB02/transcripts/office365-live-validation.txt` or `proof/SB02/transcripts/live-validation-blocker.txt`.
- Browser screenshot only if browser UI is used.
- Execution report rows for SB02.

## Browser Validation Logging

- Route: `http://localhost:5032` and the specific workflow/process route discovered during validation.
- Viewports: desktop default; no responsive validation required unless UI flow is changed.
- Actions/assertions: navigate, authenticate state inspection if available, run/inspect Office365 workflow, verify no `summarize-office365` invalid JSON failure.
- Screenshots: only if browser UI is used.
- Review questions: confirm visible workflow/run status and no hidden error toast/panel if browser proof is used.

## Progression Gate

- Final bundle closure may proceed only after live validation succeeds or an explicit live blocker is recorded with alternate SB01 proof.
- Gate decision: `Passed`. The same project-structure workflow node was rerun against the local app, `summarize-office365` completed, the project-structure asset was created, and `mark-office365-processed` completed.

## Suggested Agent Prompt

```text
Execute SB02 only after SB01 is completed. Validate the local Office365 summary workflow path on http://localhost:5032, capture live proof or a concrete blocker, and update execution report/raw-note closure without inventing proof.
```
