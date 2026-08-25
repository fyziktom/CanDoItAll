# SB01 focused behavior proof

## Protocol and canonical representation

The 12-test `SharedProviderProtocolContractTests` lane discovers and passes exactly the planned
selection. Observable assertions prove:

- only the versioned native catalog and bounded OpenAI-compatible route constants are exposed;
- resolving catalog/OpenAI routes preserves a configured reverse-proxy base path and removes
  query/fragment state;
- catalog JSON uses exact case-sensitive names and frozen enum strings, rejects unknown/duplicate
  members, unsupported versions, missing members, invalid defaults, incoherent capabilities, and
  cross-publication routes;
- canonical serialization recursively sorts providers, models, and capabilities, defensively
  copies collections, ignores incoming revision fields, and changes revisions when sanitized
  public health state changes;
- serialized catalog records contain no internal profile ID, secret, private URI, prompt, tool
  payload, attachment, raw error, or volatile check timestamp;
- ports validate absolute bases, bounds, operation/capability coherence, and strong ETag/catalog
  revision agreement; and
- the contract assembly has no Web, EF, Workspace, MAF, provider-SDK, package, or project
  dependency.

Evidence: `../transcripts/sb01-list-protocol-release.txt` and
`../transcripts/sb01-run-protocol-release.txt`.

## Routing model identity

The 10-test `SharedProviderRoutingModelIdTests` lane discovers and passes exactly the planned
selection. It freezes deterministic vectors for the 80-character
`sp1.<publication-guid-N-lowercase>.<base64url-full-SHA256>` format. It proves exact UTF-8 model
identity is case-sensitive and not trimmed; duplicate model names in different publications do
not collide; only public publication/fingerprint data is parseable; and internal profile IDs,
upstream model text, URIs, and caller-controlled paths are absent. Unknown versions, uppercase
publication encodings, truncated/non-base64url fingerprints, invalid Unicode, and wrong
publication/model resolution all fail closed.

Evidence: `../transcripts/sb01-list-routing-release.txt` and
`../transcripts/sb01-run-routing-release.txt`.

## Access context

The 10-test `SharedProviderAccessContextTests` lane discovers and passes exactly the planned
selection against the real Web test host. It proves:

- absence is valid and leaves the scoped accessor empty;
- one exact 1..256-character ASCII `[A-Za-z0-9._~:-]` value binds without trimming or decoding;
- null/empty/whitespace, controls, Unicode, disallowed characters, oversized values, repeated
  values, conflicting values, and comma-combined values return the native HTTP 400 envelope;
- invalid default values fail predictably;
- concurrent requests do not observe another request's value;
- middleware re-execution is idempotent; and
- a forged access reference does not satisfy authentication or an API scope.

Evidence: `../transcripts/sb01-list-access-release.txt` and
`../transcripts/sb01-run-access-release.txt`.
