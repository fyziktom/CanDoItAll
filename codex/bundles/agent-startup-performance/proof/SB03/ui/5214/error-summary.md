Missing-path tool handling and same-session continuation are supported for 5214. The two distinct runs in session f856aa0e-42d4-4f80-a75b-f97f640887a9 both persisted Completed/Succeeded, with 16 logs, one tool-call metric, one Failed audit receipt and two separate provider-usage observations each.

| Run | Structured call ID | Failed receipt |
|---|---|---|
| ad47ab86-d1ae-480d-bafe-95e5c39afe1e | call_L3DFuEZIxkrVpU5ef9jWjcw6 | bb90f54f-6a7e-4250-8132-ebf60461d086 |
| a21411e5-12bf-43b2-9b4e-40d381ce2be2 | call_QRcefb1DPCFgbkHBoI7cE4vA | aa925478-2e8d-5d5f-a689-08bfc97424d4 |

Each call is workspace_stat_path. Its directly paired structured result has Succeeded=false, Exists=false and PathKind=missing, with a Failed outcome and missing-path message. Raw arguments, paths, prompts and result text are not published. The receipt is associated through the exact run and its single call/single receipt; the receipt itself has no callId. The later runtime snapshot contains its new call and does not retain the earlier call, so the proof does not invent a cumulative tool-call history.

Root observed Cancel preserving the conversation, Keep active hiding it, reopening the active handle, and a full reload preserving the original run/logs and two messages. A later safe follow-up asked to stat the previously requested nonexistent path without repeating it; root observed the same expected missing-path result. Both current sessions contain four messages after continuation. The paired persisted capture explicitly reports whole-session counts and does not assign all four messages to either individual run.

Root observed the Auto Approve UI badge and no approval UI. The persisted per-run autoApprovePendingToolCalls value is false; these distinct fields are recorded without asserting equivalence or inferring a policy bug. Stop was observed disabled, but the offline capture does not establish the exact run state at that click. The first client persisted capture already occurred after continuation and therefore correctly contains four session messages.

This proves handled tool errors in successful conversations and continuation/reopen persistence. It does not prove a Failed execution run, provider failure, active-run cancellation, approval interaction, existing-file inspection/conversion, or successful file writes. Only the stat tool is recorded; no mutating tool call is recorded, but this is not an operating-system filesystem-write audit. The blocked private-file UI02/file-backed UI03 scenarios remain separate and unproven by this safe case.

Evidence: error-two-run-persisted.json and its extraction command; error-structured-calls.json and its extraction command; error-result-summary.json; error-ui-observations.json. The reviewed helper and bounded structured extractor perform no app calls, source writes, locks or recovery. All requested records passed identity and unchanged-file rechecks. The initial envelope-only structural inspection is retained separately and is superseded by the final extractor, which decodes the known payloadJson envelope before reading call/result identifiers.
