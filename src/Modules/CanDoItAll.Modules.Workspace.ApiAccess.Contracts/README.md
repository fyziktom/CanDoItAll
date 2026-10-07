# Workspace API Access contracts

Safe configuration, scope choices, immutable account/token metadata and narrow owner ports
for the API Access UI leaf. This assembly has no project or authentication-runtime dependency.
It contains no signing options, hashes, private credential bindings or durable store records.

Write outcomes distinguish refusal, acknowledged commit, diagnostic warning and ambiguous
acknowledgement. Password intents and one-time disclosures keep sensitive values in private,
disposable one-use fields; ordinary projections and operation receipts are metadata only.
Production adapters live in the Workspace module. The independent API sandbox implements
the same ports with explicitly non-authenticating fixtures.

See [ownership and proof](../../../docs/architecture/workspace-api-access-ui-boundary.md).
