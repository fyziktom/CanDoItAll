# SB11 semantic invariants

- SB11-I1 (N015): sanitized shared HTTP status yields actionable source-access guidance,
  never raw remote body, secret, URL or a blanket expiry claim. Negative proof varies
  401/403/other HTTP statuses, local versus shared credentials, nested and tool failures.
  Shallow trap: expose the original exception or label every error as expired JWT.
- SB11-I2 (N015): correct existing authorization remains enforced. UI renewal uses bounded
  source catalog/invoke scopes. No indefinite token or change to auth middleware.
  Producer: existing API token UI and validation; consumer: shared connection. Expired
  negative run and renewed positive run must both have original evidence.
- SB11-I3 (N015): Portfolio Architect uses actual Calculator project context, generates
  an image with the real shared image provider and attaches an image asset. Prove through
  current-run receipts, source invocation/usage, attached asset and inspected image.
  Shallow trap: a provider health check, mocked image, detached chat or file existence only.
- SB11-I4 (N015): image-tool schema describes accepted sizes/qualities/formats. Invalid
  options return a safe IAgentToolFailure with allowed values and corrected-input retry,
  not an opaque provider failure or a silently substituted dimension. The actual failing
  request used 1536x864 twice. Tests cover size, quality, output format, hostile input,
  generated function schema, shared default selection, and successful corrected dispatch.
- SB11-I5 (N015): vision image data is bounded by the existing complete request budget,
  not the smaller text-field limit. Realistic image sizes pass; oversized requests,
  oversized text, invalid base64/MIME, external URLs, wrong roles and absent vision
  capability still fail. Validate Chat Completions and Responses. Real UI analysis
  must read the existing generated asset and source usage must show vision execution.

Exact red/green commands, changed hashes and final producer/consumer artifacts are indexed
by manifest.md. Reopen if any of these remain unproven; handoff prose cannot close them.
