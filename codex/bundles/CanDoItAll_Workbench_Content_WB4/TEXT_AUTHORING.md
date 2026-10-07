# Text creation and upload: whole real form

Primary source family S10-S13. Include Text, JSON, Markdown, Mermaid and Log, both Create new
and Upload existing, filename, title, subtitle/context, notes, content, upload selection,
validation/loading/cancel and actual submit. Retain current definition labels, allowlists,
length/byte limits and native content validation. No format is dropped to make the sandbox small.

## Submission and draft

The old form freezes title before preparation, but reads subtitle/notes after awaiting native
payload generation. Source-mode controls and several inputs can still change meanwhile.
Make the submission an immutable semantic snapshot before the first await. Freeze upload
object identity, mode, subtype and original project/parent as well as visible text. Choose
one explicit interaction policy during submit; preserve newer editable fields or truthfully
disable only the relevant form. Never mix the first title with the second upload/content.

An ordinary section/source switch must not lose the current draft, raw invalid fields or
validation context unintentionally. A new opening is a new draft. Input callbacks reference
their original receiver and revision, not an index or a later parameter value. Test Enter,
button submission, queued duplicate events and programmatic dispatch against the same gate.

## Upload authority and IO

Use the real browser file control and bounded native stream adaptation. Browser-supplied
filename, media type and declared size are not sufficient validation. Test oversized and
truncated streams, encoding/BOM handling according to current policy, malformed JSON,
filename/path traversal attempts, disallowed extensions and cancel while reading. Preserve
text bytes and Unicode according to the actual normalizer; do not invent normalization rules.
The leaf may own the browser stream interaction but native validation and storage do not
move into it. Large raw content, base64 and secrets do not enter receipts, URLs or telemetry.

## Results

Native asset payload creation may happen before any durable node exists. Distinguish that
from the first returned node/storage identity. Preserve the existing known-created-follow-up
exception behavior: if links, placement or reload fail after a node committed, report that
node and offer observation, not another Create. Exact identity and byte readback are required.
Do not globally close whichever dialog happens to be on top: close the captured original
DialogReference, and only after the appropriate result. A lost browser close acknowledgement
is not a lost native write. Cancellation after dispatch does not imply no effect.

## Minimum distinguishing native specimens

A normal new text file and each supported subtype; real existing upload; rejected malformed
payload with no write; same node name with distinct identity; original target retired;
accepted create followed by failure; upload replaced during preparation; newer input typed
while a request is pending; separate dialogs with independent outcomes. Verify actual source
and neighbor rows, governed content and decoded downloads. Existing coordinator/dialog tests
are entry points to extend, not replacements for the original page-level reproduction.
