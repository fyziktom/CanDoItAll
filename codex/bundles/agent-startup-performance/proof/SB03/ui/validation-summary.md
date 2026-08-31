# Candidate UI validation checkpoint

Status: Passed for the required candidate UI acceptance matrix, including authorized source-file reads, follow-ups, history and existing native approval behavior. Final bundle closure also requires the separately recorded runtime checkpoint and completed validator. Root performed the browser actions through Playwright MCP at 1920×1080 and inspected the screenshots below. Offline extraction corroborates those observations; it did not repeat browser actions.

| Row | Native 5032 | Docker 5214 | Evidence and limits |
|---|---|---|---|
| UI01 fresh conversation/provider | Pass | Pass | Seven genuine no-tool candidate runs in six conversations per host: separate first, five fresh warm sessions and continuation reusing warm session 05. Exact HTTP correlation and completion: bundle://proof/SB03/performance/independent-result-verification.json. Readable models: final-catalog-status.mcp.json. |
| UI02 actual source-file comparison | Pass | Pass | Actual PDF/XLSX and Markdown/SVG tool results match independently inspected sources; exact run and result evidence below. |
| UI03 prior source fact plus fresh source-file read | Pass | Pass | Native fresh workbook row plus prior price; client fresh SVG and Markdown checks in the same conversation. |
| UI04 hide/reopen/reload/history | Pass | Pass | Error-flow and successful source-file conversations/logs survived full reload. Native pending approval survived handle Stop/history reopen; client active file run survived Hide/Keep active. |
| UI05 confirmed missing path | Pass | Pass | Two actual workspace_stat_path calls per host; paired structured results Succeeded=false, Exists=false, PathKind=missing. Agent reported absence without fabricated content; all four runs settled Completed/Succeeded. |
| UI06 existing busy Stop restriction | Pass, preparation/admission scope | Pass, including catalog Running | Native first send/Hide showed Ready, Updating workspace context, User Sending/Creating run and This chat is still working with Stop disabled. No native persisted Running state is claimed. Client catalog explicitly showed Running and initial close dialog disabled Stop. Cancel/Keep active/reopen preserved work. No true cancellation is claimed. |
| Conditional approval matrix | Pass: approve once, reject, pending-handle preservation | Pass: existing AutoApprove preserved | Durable decisions and paired results corroborate actual UI actions; one approved conversion receipt and zero rejected-run receipts. No policy was changed. |

## Exact persisted correlation

| Host | Session | Initial missing-path run | Continuation run |
|---|---|---|---|
| 5032 | c94bbc5c-ff32-43bd-88f4-f61309b9dc0d | cc968f49-5d64-4807-8618-377293e5d020 | 59350507-ddde-4274-aca9-aeefd09d37f7 |
| 5214 | f856aa0e-42d4-4f80-a75b-f97f640887a9 | ad47ab86-d1ae-480d-bafe-95e5c39afe1e | a21411e5-12bf-43b2-9b4e-40d381ce2be2 |

Each run has one failed workspace_stat_path receipt, one metric tool call, two usage observations and zero approvals. Native has 15 logs per run; client has 16. Final whole-session snapshots contain two user and two assistant messages. Native's first offline snapshot predates continuation and shows two messages. Client's first offline snapshot was already after continuation and shows four; it does not prove an earlier two-message persisted state.

Persisted identities/outcomes: 5032/error-two-run-persisted.json and 5214/error-two-run-persisted.json. Actual call IDs and paired missing-result flags: each host's error-structured-calls.json. Exact extraction commands are alongside these artifacts. Counts alone are not behavior proof; they corroborate actual tool rows and absence results seen through UI.

UI actions/results: 5032-reload-proof.mcp.json, 5214-close-actions.mcp.json, 5214-error-reopen.mcp.json, 5214-reload-proof.mcp.json, error-continuation-actions.mcp.json and error-continuation-results.mcp.json. Initial native Cancel/Keep-active actions succeeded before a subsequent incorrect Active role locator timed out. The corrected Active tab and actual handle then reopened successfully. Failed MCP artifacts remain failures, not successful action proof.

## Root's inspected visual evidence

Root inspected 5032-active-close.jpg and 5214-active-close.jpg: centered dialogs, visible controls, disabled Stop and usable Cancel/Keep active. The later native close screenshot can be after terminal completion while the handle remained busy; the earlier admission observations establish the limited native busy contract.

Root inspected 5032-reloaded-error-chat.jpg, 5032-reloaded-error-log.jpg, 5032-continuation-tool-log.jpg, 5032-completed-followup.jpg and their 5214 equivalents. Internal scrolling revealed actual tool rows; fixed Close actions remained accessible. Transcripts owned vertical scrolling, roughly 265 px visible height versus 849/888 px content; native/client scroll positions reached the final message at 583.5/622.5 px. Composer, readable answer/tool name, status/footer and enabled Send were visible. Existing clipped secondary header badges in the narrow 760 px chat window match the baseline; no UI edits were made.

Client file browser: 5214-file-browser.jpg and 5214-computed-file-browser.json, plus bundle://proof/deployment/independent-verification, establish published CSS, actual grid/flex styling and inspected layout. final-catalog-status.mcp.json preserves readable model labels on both hosts. Static CSS alone was not used as rendered proof.

## Authorization and retained earlier observations

Auto-review rejected transmission of existing private quotation PDF/XLSX contents or derived data to the configured model without explicit payload-and-destination permission. Root requested approval for Spreadsheet Analyst/gpt-5.4-mini on 5032 using those assets and Portfolio Architect/gpt-5.6-luna via the shared provider on 5214 using calculator Markdown/SVG. The user replied, “thats good progress. yes continue”; bundle://inputs/05-file-validation-approval.md preserves the exact reply and approved scope. The earlier rejection was respected; no indirect workaround or missing-path substitute bypassed the boundary.

The authorized file validation has now passed as recorded below. The earlier permission denial and explicit authorization remain in file-transmission-approval-blocker.json and the inputs. Final runtime safety evidence: bundle://proof/deployment/final-checkpoint.json and bundle://proof/deployment/native-quiescence-final-checkpoint.json.
Additional failed observation: 5032-confirmed-running-close.mcp.json records an optional generic no-file/no-tool run outside the fourteen performance samples. Exact Running DOM wait timed out; no Hide/Cancel actions followed and no corresponding JPG exists. Completion is not a Running-state or answer-quality acceptance claim. The initial native admission-busy scope is unchanged.

## Successful file runs and persisted history

| Host | Run | Source action and checked result |
|---|---|---|
| 5032 | 9fc77bd0-4cbb-404e-9389-88df9128212b | Read Pricing!A1:H4 and converted the authorized PDF after one approval. PDF page 1 and workbook rows 3/4 agree: ZM-x6600 41,500 / 46,000-49,000; ZM-x6600A 66,000 / 73,000-78,000. |
| 5032 | 78436614-b3b7-4efc-9e0b-b5d66446a4ae | Same conversation, fresh Pricing!A2:D2 read: ZM-x5600 35,000 / 39,900-42,000. Correctly compared prior ZM-x6600 EXW and calculated 6,500 difference. Five messages include the earlier approval-pause assistant message. |
| 5214 | 6994341e-3336-47c0-8cab-5474b41904c2 | Read both Markdown/SVG. Correct basic arithmetic scope, desktop calculator-left/history-right layout, newest-first examples and missing sign-toggle in SVG. |
| 5214 | abb9340f-4546-49a3-b8f3-9b07d662c3d4 | Same conversation, fresh SVG read; confirmed absent sign-toggle and oldest entry 100 divided by 2 = 50, labelled 5m, with actual element coordinates. |
| 5214 | 2673d6a8-04c7-403c-ab07-018c1b8841aa | Same conversation, fresh Markdown read; correct divide-by-zero error/recovery and no failed operation in history, Clear history, and optional result restoration requiring confirmation. Six final messages. |

Independent reference: reference-facts.json. Actual tool arguments and paired result evidence: 5032/files-structured-calls.json and 5214/files-structured-calls.json. Every client file-result content SHA-256 matches the corresponding independently hashed approved file; responses succeeded and were not truncated. Native paired spreadsheet results match the independently extracted workbook. Conversion succeeded for the exact approved PDF, and the final UI answer matches its independently extracted price facts; the retained sanitized conversion result records preview length and success, not a separate preview-price assertion. These are content checks, not only receipt counts.

Actual UI evidence: file-start-actions.mcp.json, 5214/file-comparison.mcp.json, file-followups-progress.mcp.json, 5214/file-active-close.mcp.json and file-followups-after.mcp.json. The first client follow-up read in file-followups-progress.mcp.json caught a transient Idle view immediately after reopening; its correctly identified completed log is retained, and the settled answer is in 5214/file-active-close.mcp.json. The transient image file-followup-completed.jpg is not used as completed-answer proof.

Both pages were fully reloaded and their own source conversations selected from history by unique preview. file-history-verified-rejection-start.mcp.json records native five messages/latest run 78436614 and client six messages/latest run 2673d6a8, with persisted work logs and no duplicated messages. Client 5214/file-active-close.mcp.json records immediate Hide after the third Send, disabled Send/Stop and Keep active with catalog Running; later reopening showed its completed source answer. The earlier follow-up Hide attempt was already Completed and is not counted as a busy-state check.

## Existing native approval policy

Approved conversion run 9fc77bd0 retained the exact approval ficc_call_1sc4nf6YPIt9iCHC5ZsoWMBo and call_1sc4nf6YPIt9iCHC5ZsoWMBo. Before approval it had five prior read/skill receipts and no conversion receipt. Root stopped only its quick-access handle using the documented pending-approval dialog, reopened it from history and verified that the same pending approval remained. Root clicked the per-proposal Approve once, never Approve remaining in this run. The durable decision was Approved at 2026-08-31T17:42:37.134717Z; the single conversion execution receipt began at 17:42:47.348963Z and completed at 17:42:48.456237Z. The run resumed and completed with six total receipts and zero pending approvals.

Evidence: 5032/files-before-approval-persisted.json, files-before-approval-source-hashes.json, files-after-approval-persisted.json, files-structured-calls.json, file-pending-handle-close.mcp.json, file-approve-once.mcp.json and file-after-approval.mcp.json. The managed converted-documents Markdown output is distinct from the originals; conversion creates derived output and is not described as entirely filesystem-read-only. The original PDF/workbook hashes remain unchanged. The low-level conversion receipt's ApprovalMode=NotRequired is not the outer approval decision. An approval-suspension metric marked Cancelled does not override the final run Succeeded state.

Rejected conversion run 3908f2a2-35f6-4009-80f5-f2d69e619bcd used the same PDF with previewCharacters=300 in a separate conversation. The immutable before snapshot had one pending proposal, no paired result and zero receipts. Root clicked only Reject for ficc_call_3Ut7txS4EYKYA65Ifg8JSDC7. Its durable decision was Rejected at 2026-08-31T17:55:55.072944Z. The actual paired result for call_3Ut7txS4EYKYA65Ifg8JSDC7 contains the rejection marker. There are zero execution receipts before and after, zero pending approvals, and no alternate content-read calls. The run completed with three messages; its answer explicitly says conversion was declined and no PDF content was processed.

Evidence: 5032/rejection-before-persisted.json, rejection-after-persisted.json, rejection-paired-result.json, file-rejection-before.mcp.json, file-rejection-click.mcp.json and file-rejection-after.mcp.json. The generic Invoking tool progress title is wrapper invocation, not proof that the rejected conversion executed. file-rejection-reloaded.mcp.json confirms the same three-message conversation and Approval rejected log after a full page reload. The first reload locator attempt timed out waiting for the Available tab; it is retained as file-rejection-reload-first-attempt.mcp.json. A fresh DOM observation showed the catalog closed; explicitly reopening it then passed. No second approval decision was sent.

Existing client AutoApprove remained unchanged and was recorded in every file run's UI/log. Its read-only tools succeeded without pending approval gates; native policy was not copied or manufactured on that host. Native approval dwell times are excluded from the measured startup cohort.

## Final visual and preservation review

independent-visual-review-file-validation.md records actual inspection of eleven file-flow screenshots. Root additionally inspected 5032/file-approval-pending.jpg, file-pending-handle-close.jpg, file-pending-reopened.jpg and file-rejection-reloaded.jpg: readable proposal/path/decision controls, explicit handle-only Stop warning, preserved approval after reopen, and readable declined answer with usable composer/footer after reload. Screenshots showing only the initial prompt are not claimed to show the entire answer; actual DOM and independently checked tool-result evidence establish content correctness. No new blocking visual defect was found. Existing narrow-window secondary badge clipping remains a documented baseline limitation.

file-fixtures-preflight.json and file-fixtures-final.json confirm all four original asset hashes/sizes and all thirteen frozen source hashes match, with unchanged candidate/publisher identities. The live source tests ran only through Playwright MCP UI with the user's explicit approval. They did not edit original assets, project content, provider/approval configuration or production code. The final runtime checkpoint confirms no owned requests or commit journals remain in flight and preserves the pre-existing historical approval unchanged.
