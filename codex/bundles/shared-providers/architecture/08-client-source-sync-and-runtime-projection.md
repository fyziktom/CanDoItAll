# Client source sync and runtime projection

## Source workflow

1. User creates a source with name, base URI, secret reference, and trusted-network policy.
2. Service normalizes URI and tests `GET .../api/shared-providers/v1/catalog`.
3. Authenticated catalog response is validated:
   - supported schema;
   - source instance identity;
   - ETag;
   - sanitized provider entries;
   - unique publication/model IDs;
   - compatible relative inference base path.
4. User selects publications.
5. Reconciliation transaction creates/updates import rows and linked local provider profiles.
6. AgentFramework projection observes committed profiles and updates the existing catalog.

The Razor component does not call the source directly.

## URI handling

Store a canonical base URI with a trailing path-safe root. Relative route joining must preserve
a reverse-proxy prefix.

Reject:

- non-HTTP(S);
- userinfo;
- query;
- fragment;
- ambiguous escaped host;
- invalid IDN normalization;
- overlong URI;
- redirect to a destination outside policy.

Policy:

- HTTPS allowed when host/IP passes configured policy;
- loopback HTTP allowed for local development;
- private-network HTTP requires explicit trusted-network configuration;
- Docker E2E enables only the compose network names;
- never use a global certificate-validation bypass.

Resolve and validate every connection destination, not only initial text, to mitigate DNS
rebinding. Prefer a custom `SocketsHttpHandler.ConnectCallback` or current canonical safe HTTP
helper if one exists.

## Reconciliation rules

### Successful catalog response

For each selected remote publication:

- find by `(SourceId, RemotePublicationId)`;
- create linked local profile only when absent;
- preserve existing local provider ID;
- update sanitized remote snapshot/revision;
- update effective runtime fields;
- preserve local alias and enabled intent;
- set availability based on catalog;
- notify provider profile commit observers after transaction.

For existing selected imports not present in a successful authoritative catalog:

- mark `Missing`/`Unpublished`;
- retain local profile and bindings;
- prevent runtime invocation;
- do not hard delete.

For reappearance:

- reuse import and provider profile ID;
- clear unavailable state;
- update snapshot.

### Failed catalog request

- update source failure status;
- do not conclude publications are missing;
- keep last validated snapshot;
- runtime may fail source availability/authorization explicitly;
- do not delete or automatically substitute another provider.

### Remote source identity mismatch

If the same configured URI returns a different source instance ID:

- block reconciliation;
- mark source/imports `SourceIdentityMismatch`;
- require explicit operator trust/reset action;
- do not bind existing imports to the new source automatically.

### De-selection

Mark import retired and make it unavailable for selection. Before hard deletion, use the
current provider reference/deletion policy. Preserve referenced profiles or provide an explicit
migration/removal workflow.

## Local provider behavior

`provider.candoitall-shared` manifest:

- display name identifies CanDoItAll shared provider;
- configuration is source/import managed, not raw endpoint/key fields;
- basic health calls source/catalog/publication status;
- ordinary editor cannot change remote purpose/model/capabilities;
- local alias and enabled intent remain editable;
- pricing is read-only source-provided or unknown unless a trusted central public price is
  intentionally included.

## AgentFramework projection

The outer Workspace-to-AgentFramework adapter resolves the import/source and builds an
effective internal profile:

- `ProviderKind.OpenAi`;
- central/EGCP OpenAI-compatible inference base;
- source token secret reference;
- public routing model ID;
- Responses or Chat Completions transport from catalog;
- Chat or ImageGeneration purpose;
- catalog-derived capabilities and models;
- connector/origin metadata remains `provider.candoitall-shared`;
- tags include shared/source/publication state without secret data.

This lets existing MAF OpenAI runtime handle local orchestration. It does not mean the central
upstream is necessarily OpenAI.

## Hybrid behavior

Local provider catalog contains personal and imported profiles. Selection is explicit. Shared
failure must not cause a generic resolver to silently pick a personal provider.

Tests must cover:

- same local alias;
- same model display name;
- personal default plus shared explicit selection;
- shared default plus personal explicit selection;
- source outage;
- unpublish and reappearance;
- two independent clients importing the same remote publication.
