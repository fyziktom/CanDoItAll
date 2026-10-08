# Workflow PDF staging

The document adapter reads PDF text with the existing PdfPig 0.1.13 parser's
`ContentOrderTextExtractor`. Its layout-aware whitespace keeps separately positioned
lines (including booking URLs and footers) separate. MarkItDown 10.0.7 uses raw
`page.Text`, which concatenates them. PDF extraction emits document text only; converter
timestamps and disposed page-image paths are not source facts. A PDF without selectable
text returns an explicit failure; OCR is not configured. Other supported formats retain
the MarkItDown path and writable temporary workspace. This is format dispatch, with no
silent parser fallback. The converter regression includes multiline URLs, empty/damaged
PDFs, truncation, cancellation, DOCX and XLSX.

Concurrent workflow admission, lifecycle transition and external-response acceptance
retry only PostgreSQL serialization failures or deadlocks. Each of at most five attempts
opens a fresh context/transaction and reacquires source authority. Provider calls are
outside this boundary. Other errors and exhausted retries propagate; run/event identities
remain stable. PostgreSQL 18 tests exercise aborted commits, exhaustion, non-retryable
errors and twelve concurrent independent lifecycles.

The Content Studio workflow needs to convert uploaded menus through the existing
`document.to-markdown` executor. Agent attachment staging only accepts images.

`POST /api/workflows/attachments/documents` adds a separate document ingress boundary.
It requires `api.workflows.write`, exactly one `file` part, matching PDF extension/media
type/signature and no more than 10 MiB. It returns a unique workspace-relative path.
Staging never invokes a model or converter. The existing workspace path owner authorizes
the destination; client directories are discarded and the basename is encoded through
the portable filename policy. Conversion remains the workflow executor's responsibility.

The small cohesive staging service owns validation and bounded writes. The HTTP adapter
owns multipart shape and scope. No new provider or converter abstraction is introduced.
Cancellation/failed writes remove only a file created by this operation. The service
does not claim to sanitize or validate the entire PDF document.

Verification: Web Release build; ten WorkflowDocumentStagingTests; six
WorkflowDocumentApiIntegrationTests, including 401/403/write permission and ambiguous
forms. Portability review accepts ordinal case-insensitive MIME/extension comparison,
explicit removal of both client path separators, and create-new/delete-on-failure writes
to a unique authorized workspace path. The endpoint never replaces an existing file.
Live conversion and generated API documentation evidence belong to T20-b on the hotel
planning board and the SharedInfo contract export.

The document converter configures its MarkItDown scratch root as
`candoitall-document-conversion` beneath the host temporary directory, independent
of the process working directory. This supports the standard read-only container image;
conversion does not require a writable application directory. The conversion result owns
disposable scratch artifacts. Existing conversion, truncation, missing
source and cancellation tests apply; live PDF conversion is also exercised in the
read-only hotel engine container.
