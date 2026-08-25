# SB02 persistence security and content containment

The relational model stores references and sanitized metadata only.

- A source owns one `ApiTokenSecretId` FK to the existing secret system. It never stores the token
  value or an Authorization header.
- Source URIs are canonicalized; unsafe userinfo, query, fragment, missing secret references, and
  disallowed schemes are rejected before persistence.
- Imported catalog JSON is a maximum-256-KiB, schema-versioned envelope produced from the strict
  SB01 sanitized public contract. Relational columns own source/publication/profile identity.
- Invocation records store subject, opaque access reference, trace/correlation IDs, operation,
  public/upstream model identifiers, timing, outcome, optional usage/pricing metadata, and
  retention time only.
- No prompt, response, message content, image, attachment, tool argument, secret value, cookie,
  raw upstream payload, or stack trace column exists.
- Provider and secret FKs use `Restrict`; the application deletion policy and transfer preflight
  fail before destructive mutation with typed/actionable state.

The final credential/content scan passed over the SB02 production sources, exact tests, generated
migration, and proof directory. The model metadata test independently enumerates persistence
properties and rejects forbidden content-shaped names, so the proof is not a text scan alone.

