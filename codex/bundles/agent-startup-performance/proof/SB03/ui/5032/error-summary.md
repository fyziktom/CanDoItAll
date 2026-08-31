Missing-path tool handling and same-session continuation are supported for 5032. The two distinct runs in session c94bbc5c-ff32-43bd-88f4-f61309b9dc0d both persisted Completed/Succeeded, with 15 logs, one tool-call metric, one Failed audit receipt and two separate provider-usage observations each.

| Run | Structured call ID | Failed receipt |
|---|---|---|
| cc968f49-5d64-4807-8618-377293e5d020 | call_SYhwvDTt82EFslOvnyjJRy1P | 363f1146-6277-0552-a079-c70899cd4507 |
| 59350507-ddde-4274-aca9-aeefd09d37f7 | call_mIgYecYL1ibduG7JVFxiQNHB | 8ab95eeb-e8f6-6757-992c-701f21af35ba |

Each call is workspace_stat_path. Its directly paired structured result has Succeeded=false, Exists=false and PathKind=missing, with a Failed outcome and missing-path message. Raw arguments, paths, prompts and result text are not published. The receipt is associated through the exact run and its single call/single receipt; the receipt itself has no callId. The later runtime snapshot contains its new call and does not retain the earlier call, so the proof does not invent a cumulative tool-call history.

Root observed Cancel preserving the conversation, Keep active hiding it, reopening the active handle, and a full reload preserving the original run/logs and two messages. A later safe follow-up asked to stat the previously requested nonexistent path without repeating it; root observed the same expected missing-path result. Both current sessions contain four messages after continuation. The paired persisted capture explicitly reports whole-session counts and does not assign all four messages to either individual run.

Native close-dialog chronology is important: the initial Hide action was about1.5seconds after send, while admission was in flight (Updating workspace context / User Sending / Creating run). Stop was disabled. Later busy-handle/close transitions are not asserted to coincide with a specific persisted Running or Completed state. The terminal Completed/Succeeded result was established separately. The earlier derived completed-run timing field was corrected; raw MCP evidence remains unchanged.

This proves handled tool errors in successful conversations and continuation/reopen persistence. It does not prove a Failed execution run, provider failure, active-run cancellation, approval interaction, existing-file inspection/conversion, or successful file writes. Only the stat tool is recorded; no mutating tool call is recorded, but this is not an operating-system filesystem-write audit. The blocked private-file UI02/file-backed UI03 scenarios remain separate and unproven by this safe case.

Evidence: error-two-run-persisted.json and its extraction command; error-structured-calls.json and its extraction command; error-result-summary.json; error-ui-observations.json. The reviewed helper and bounded structured extractor perform no app calls, source writes, locks or recovery. All requested records passed identity and unchanged-file rechecks. The initial envelope-only structural inspection is retained separately and is superseded by the final extractor, which decodes the known payloadJson envelope before reading call/result identifiers.
