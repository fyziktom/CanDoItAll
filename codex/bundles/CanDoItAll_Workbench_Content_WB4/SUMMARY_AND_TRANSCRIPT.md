# Progress Summary, stored exports and transcript operations

Sources S20/S21. This is not WB2 Manager Summary: it is the existing selected subtree
progress overlay, with inline status and generated assets. Preserve the separate semantics.

## Progress Summary

Extract the entire actual hierarchy/rows/status presentation, exact loading/error states,
all current status options and export buttons. A row intent carries the accepted root,
original row occurrence and allowed action. Native revalidation remains at the writer.
Do not let an old callback use the newest summaryActionContext or a same-named node from
another project. Two summaries and reopened same roots must remain independent.

An accepted status update is a write even if subsequent chart/summary refresh fails. Keep
its exact changed row/result before publication and never repeat it as a read retry. The
input snapshot, accepted state and newer draft/presentation are distinct where relevant.

## These exports write project assets

ExportSummaryWorkbook creates an XLSX file node and storage payload. ExportSummaryGantt
creates a Mermaid file node with native metadata. ExportMindmapImage captures the actual
canvas and creates an image node. They are not just browser downloads. Preserve that behavior
and explicit user choice; don't replace a graph asset with a client-only download to simplify
UI dependencies. Pure encoding may be neutral, but native persistence remains owned.

Freeze the exact root and accepted summary or canvas occurrence, dates/timezone basis and
filename inputs. Keep actual created identities through parent links, layout, readback and
notification failure. The original file bytes/row values should be independently decoded
and compared to the accepted source. Do not derive expected values from the same exporter.
Double dispatch must not accidentally create duplicate assets; genuinely unconfirmed create
requires observation rather than another export. A later explicit export remains a distinct
user operation after the earlier outcome is known.

## Transcript scaffold is not transcription

CreateTranscriptFromRecording currently creates an empty transcript scaffold with a typed
recording reference and a DerivedFrom relationship. It does not recognize audio. Preserve
this distinction in labels and tests. Native node acceptance, relationship acceptance and
readback are separate facts; no implicit speech request or automatic task creation is added.

## Explicit transcript provider action

The current confirmation permits Summarize, FindMyTasks and FindOthersDeliveries. These store
analysis text and metadata; they do not materialize tasks. Retain explicit provider selection
and confirmation that transcript content is sent externally or locally. Use safe provider
projections, not unrestricted provider objects/credentials in a renderer. New metadata loading
or a changed transcript must not silently trigger or repeat a request.

Capture original transcript content/revision, project/node occurrence, selected provider,
action kind and opening before dispatch. One accepted submission owns the external effect.
Separate provider refusal, provider completion, native metadata persistence and readback.
A successful response with failed/unconfirmed native save cannot be re-sent as an ordinary
Retry. Preserve the received result only for its proper lifetime; safe receipts omit full
transcripts and secrets. If current protocols cannot retain it safely, report the exact phase
and refuse a blind replay instead of inventing a new durable transcript journal.

A concurrent legitimate transcript edit must not be overwritten by rebuilding an entire
old metadata envelope. Use the existing native expectations/owned-field mechanisms, preserve
unknown metadata and typed references, and show the real conflict. Do not automatically merge
unrelated text or attach a result to a fresh same-ID transcript after retirement.

## Views and security

Legacy Mermaid-in-notes must have an actual eligible caller; strict security and HtmlLabels=false
are preserved. Stored file content comes only through native content authority. Text output
renders as safe text/approved shared Markdown, never arbitrary executable HTML. Sensitive
transcript/source content is not copied into URL, full exception logs, screenshots or tracked
fixtures. Use synthetic non-sensitive records in the owned test environment.

Test normal and cancelled confirmations, two openings, delayed provider option load, current
rejection, duplicate clicks, post-provider persistence fault and recovery without another
request. Verify canonical text/ref/provider metadata and unchanged neighbors. Exercise the
actual Progress Summary status and asset exports through UI as well as the native serializer.
