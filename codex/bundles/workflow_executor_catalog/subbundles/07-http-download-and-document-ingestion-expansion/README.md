# 07-http-download-and-document-ingestion-expansion

## Objective

Improve network/document workflows while preserving safety.

## Required work

1. Refactor HTTP executor to use `IHttpClientFactory`.
2. Add SSRF/allowlist policy:
   - block loopback/link-local/private networks by default for external fetches,
   - allow explicit trusted internal mode only by configuration/approval.
3. Add download-to-workspace operation:
   - bounded bytes,
   - content-type checks,
   - file name policy,
   - artifact content registration.
4. Expand source ingestion/document extraction:
   - DOCX
   - HTML
   - CSV table summary
   - ZIP manifest/list
   - PDF metadata/page counts
5. Add tests with fake HTTP handler and local fixture documents.

## Acceptance checklist

- HTTP fetch/download does not create an SSRF footgun.
- Downloaded content can feed source ingestion or artifact output.
- Document extraction has clear status values and truncation summaries.
