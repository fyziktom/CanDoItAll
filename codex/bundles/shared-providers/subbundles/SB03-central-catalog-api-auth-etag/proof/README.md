# SB03 proof artifacts

State: `PASS`

Store portable evidence here:

- `proof-manifest.json`
- `transcripts/`
- `architecture/`
- `behavior/`
- `security/`
- `screenshots/` when applicable
- `hashes.sha256`

Do not store credentials, prompt/response content, binary model outputs, or unredacted logs.
Every artifact referenced by the manifest must exist and have a SHA-256 at completion.

SB03 proof includes exact 18/14/10 discovery and passing Release runs, clean Unit/Web/Integration
Release builds, publication eligibility and explicit publish/unpublish behavior, canonical
sanitized catalog and routing projection, persisted cross-instance cache invalidation, native and
OpenAI catalog API/auth/error/ETag/OpenAPI coverage, production descriptor-matrix exclusions,
before/after CodeAnalytics/reference evidence, public-surface and independent architecture
review, and secret/content/redaction scans. No inference POST, broad, browser, live-network,
paid-provider, or multi-instance lane ran; those remain owned by downstream subbundles.
