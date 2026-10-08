# Workflow PDF staging

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
