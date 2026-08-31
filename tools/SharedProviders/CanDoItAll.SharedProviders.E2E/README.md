# Shared Providers E2E canonical-state and black-box tool

This project prepares and mutates the three Shared Providers application databases through the
same application services used by CanDoItAll. It builds one role-bound dependency-injection
container per process, bootstraps the current schema, and never starts hosted workers.

The tool does not start or stop Docker. Its pre-Compose `prepare` command generates the bounded
artifact tree and runtime secrets, and its resumable `run-scenarios` phases execute the black-box
HTTP, audit, database-isolation, capture, cancellation, and redaction checks. The surrounding
PowerShell lifecycle owns Docker ordering, the central outage, bounded log collection, and host
secret/database scans.
The tool reads database connection strings, signing keys, and upstream credentials only from
files; those values must not be passed directly on the command line.

## Commands

- `prepare --repository-root <root> --artifact-root <root> [--reset true|false]` validates the
  exact checkout and `.artifacts/shared-providers-e2e` root, optionally resets only a previously
  marked root, creates all bind directories, and privately generates the database, signing-key,
  connection-string, and deterministic-upstream secrets without printing values.
- `seed-central` creates or updates the six deterministic central provider fixtures, controls
  publication through `SharedProviderPublicationStore` and
  `SharedProviderPublicationApplicationService`, creates the stable service identity, and issues
  central access tokens.
- `seed-client-a` creates the personal provider through the AgentFramework workspace, creates or
  updates the central source, probes it, and imports the chat-completions fixture.
- `seed-client-b` creates its service identity and access token, creates or updates the central
  source, probes it, and imports the chat, structured-output, and image fixtures.
- `snapshot --role central|client-a|client-b` writes a sanitized read-only database snapshot.
- `unpublish-text` and `republish-text` toggle the central chat-completions publication through
  the publication application service.
- `sync-client-a` and `sync-client-b` run the canonical source synchronization with each client's
  fixed selection.
- `sync-client-a-expect-offline` and `sync-client-b-expect-offline` require a typed connectivity
  failure and persist the resulting non-destructive offline snapshot.
- `point-client-a-at-client-b` updates client A's source through `SharedProviderSourceService` and
  requires the subsequent source probe to report `SourceIdentityMismatch`.
- `restore-client-a-source` restores client A's central URI and token through the source service,
  requires a successful probe, and synchronizes the fixed client-A selection.
- `run-scenarios --phase normal|unpublished|republished|identity-mismatch|identity-restored|outage|recovery`
  executes and idempotently merges the phase's strongly typed checks into the exact 19-scenario
  machine-readable report.

Every command requires either command-line file/path options or their environment equivalents:

| Option | Environment variable | Purpose |
| --- | --- | --- |
| `--artifact-root` | `SHARED_PROVIDERS_E2E_ROOT` | Shared ignored E2E artifact root. |
| `--instance-root` | `SHARED_PROVIDERS_E2E_INSTANCE_ROOT` | Role-local control-plane, workspace, vault, and runtime root. |
| `--connection-string-file` | `SHARED_PROVIDERS_E2E_DATABASE_CONNECTION_STRING_FILE` | Role database connection-string secret file. |
| `--api-signing-key-file` | `SHARED_PROVIDERS_E2E_API_SIGNING_KEY_FILE` | Role API signing-key secret file. |
| `--upstream-token-file` | `SHARED_PROVIDERS_E2E_UPSTREAM_TOKEN_FILE` | Required only by central and client-A seed commands. |
| `--upstream-uri` | `SHARED_PROVIDERS_E2E_UPSTREAM_URI` | OpenAI-compatible deterministic upstream base URI. |
| `--comfyui-uri` | `SHARED_PROVIDERS_E2E_COMFYUI_URI` | Deterministic ComfyUI base URI; defaults to the fixture root. |
| `--central-uri` | `SHARED_PROVIDERS_E2E_CENTRAL_URI` | Central CanDoItAll instance base URI. |
| `--client-b-uri` | `SHARED_PROVIDERS_E2E_CLIENT_B_URI` | Client-B CanDoItAll instance base URI. |
| `--host-binding-id` | `SHARED_PROVIDERS_E2E_HOST_BINDING_ID` | Stable role-local host binding. |

URI options default to the Compose DNS names. `--host-binding-id` defaults to a stable value
derived from the command role. `--role` is accepted only by `snapshot`. API issuer and audience
are derived, not ambient configuration: `CanDoItAll.SharedProviders.E2E.Central`,
`CanDoItAll.SharedProviders.E2E.ClientA`, or `CanDoItAll.SharedProviders.E2E.ClientB` for the
matching role.

## Canonical boundaries

All writes use these strongly typed services:

- `SecretService` for provider and source credential records;
- `ICanDoItAllAgentWorkspaceFactory.GetOrganizationWorkspaceService().SaveProviderAsync` for
  personal provider profiles;
- `SharedProviderPublicationStore` and `SharedProviderPublicationApplicationService` for
  publication identity and state;
- `IApiTokenService` for access tokens;
- `SharedProviderSourceService` and `SharedProviderSourceSyncService` for source configuration,
  probes, reconciliation, and imports.

Entity Framework is used only for read-only sanitized snapshots. There is no direct SQL or
direct entity mutation path.

## Artifacts

Each command that changes or inspects state writes
`handoff/<role>-state.json`. Schema version 1 contains:

- role, capture timestamp, and public service-instance ID;
- fixture, provider-profile, and public publication IDs;
- non-secret provider names, connector keys, model IDs, and capability flags;
- source IDs, base URIs, state, public remote identity, ETag, status code, and timestamps;
- import IDs, public publication/routing IDs, selection/availability state, and timestamps.

Snapshots intentionally exclude provider/source secret IDs, provider configuration JSON,
upstream private endpoints, remote catalog snapshots, source status messages, and all request or
response content.

Generated JWTs are the only files under `credentials/`:

- `central-access.token`;
- `central-catalog-only.token`;
- `central-invoke-only.token`;
- `client-a-access.token`;
- `client-b-access.token`.

Credential files are written atomically with `DurableFileWriteOptions.Private`; on Unix this
enforces owner-only directory and file modes. On Windows, the exact artifact tree uses protected,
non-inherited ACLs that allow only the current identity, SYSTEM, and built-in Administrators, and
new credential files inherit that restricted boundary. Point the artifact root at the repository's
ignored `.artifacts/shared-providers-e2e` location when running from the checkout. The program
never prints credential, key, password, prompt, response, or generated-content values.

## Frozen backend checkpoint scenarios

`BackendCheckpointScenarioCatalog.All` contains exactly these stable scenario IDs:

1. `central-catalog-publication-boundary`
2. `client-a-text-import-with-personal-provider`
3. `client-b-text-and-image-imports`
4. `source-resync-idempotency-and-stable-local-ids`
5. `duplicate-upstream-model-routing`
6. `chat-completions-and-responses-buffered`
7. `chat-completions-and-responses-streaming`
8. `function-tool-call-roundtrip`
9. `structured-output-capability-allow-deny`
10. `openai-and-comfyui-image-generation`
11. `catalog-etag-not-modified`
12. `catalog-and-inference-scope-isolation`
13. `malformed-access-context-rejected`
14. `access-context-central-only`
15. `unpublish-and-reappearance`
16. `central-outage-recovery-no-fallback`
17. `source-identity-mismatch`
18. `streaming-disconnect-cancellation`
19. `secret-content-audit-redaction`

The in-project black-box runner consumes this catalog and the sanitized handoff. A scenario passes
only after every required normal, mutation, outage, and recovery stage has produced successful
behavioral checks; seeding or a state mutation alone never marks a scenario passed.
