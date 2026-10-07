# Final network-separated verification

## Reuse infrastructure, not stale result labels

The S0 runner now accepts `FixtureName`, `PortBase`, `PostgresPort`, `IngressPrefix`,
`Configuration`, `ValidateOnly`, `Reset`, `SkipImageBuild`, and `StartAt` [S15]. Read the complete
current runner, Compose file, environment example and isolation tests before invoking anything.
This bundle does not hard-code a free port or subnet. Inventory running containers/networks and
ordinary processes, choose non-conflicting task-owned identities, validate before prepare/reset,
and retain exact ownership markers. No ordinary port 5032 or retained manual-provider pair.

The native browser settings require a marked `shared-providers-e2e-pp2-*` root, matching
host-run metadata and explicit `CANDOITALL_ALLOW_SHARED_PROVIDER_FIXTURE_WRITES=1`. Keep these
constraints. Inspect support commands first; do not point the helper at an unmarked arbitrary
endpoint. A prepared fixture may be reused only after verifying its ownership and source/image
manifest; otherwise create a new one. Do not overwrite old failing evidence with the same label.

## Final topology and production composition

Use central, client-a, client-b application containers with separate database roles/databases,
control planes, vaults/keys and data roots. Use the existing deterministic shared and personal
upstreams; clients must not bypass central and contact the source upstream directly. Retain
internal data networks, exact local ingress trust, least privilege and non-root mounts.
Permission preparation must finish successfully before apps; do not rerun recursive ownership
changes against active workspace probing. Record helper identity/state, image digests and source
fingerprints. All final application instances must use the final implementation image.

## Ordered final campaign

1. Fresh native presets: capture accepted OpenAI Responses, Chat Completions and Image catalogs
   before customization; handle runtime-only Ollama eligibility through the existing supported
   saved-profile path. Do not impose previously observed model counts as future fixed expectations.
2. Actual publication, source creation/Test/discovery/import in production UI; both clients.
   Compare independent native source/publication/import facts, names/defaults, exact route IDs,
   full models vs valid selector recommendations, prices/nulls/zero, Thinking and capability constraints.
3. Default/non-default native dispatch for relevant transports, plus two publications with the
   same display name. Correlate client-selected route, source publication/provider and upstream
   token using IDs/requests, not just matching text or a generic deterministic response.
4. Custom model list/default and pricing/Thinking overrides through source UI. Sync clients
   separately; keep another circuit open and invoke only toolbar refresh there. Include removed
   saved non-default model and explicit operator correction with no silent fallback.
5. PP2C-R1 in two circuits of one client: another local alias/enabled edit followed by clean and
   dirty refresh/save; prove native values and concurrency, not merely visible preservation.
6. Rotation/revocation of dedicated fixture credentials; scope isolation, identity mismatch,
   unavailable source, local disable/retire, unpublish/reappearance and both source/client restarts.
   Assert zero unexpected calls to a colliding personal provider and no leaked upstream key.
7. Actual Agent/Project Structure file and approval, Simple Chat transcript, Workflow/TestLab,
   image/vision artifact bytes, History and neighboring UI consumers (see APPLICATION_JOURNEYS).
8. Full 19-case protocol runner on the final image with all per-scenario outputs retained. The
   browser/native lane and protocol lane are complementary; capture a source freeze across each.

Isolate destructive lifecycle scenarios from native baseline observations, or restore/reprepare
with explicit manifests. If a protocol phase changes source identity or unpublishes a fixture,
that phase cannot silently be the prerequisite for a later unrelated positive UI test.

## Evidence requirements

Record exact build commands/configuration, commits/worktree digest, restored assets, image and
container IDs, database isolation, discovery/executed counts, outcome/stage, request correlation,
owner read-back and artifact hashes. Capture native source facts before modification. Mask secrets
and avoid raw configuration dumps. The supplied model-parity checker checks only the safe exported
schema/consistency; it neither authenticates files nor proves network calls or runtime permissions.
Use it for same-revision parity, not for expected unavailable/stale snapshots.

Only fixture-local publication/credentials and deterministic upstream calls are authorized.
No paid internet model requests, reset of the historical 40/40 budget, real remote publication,
blanket certificate validation bypass or external access in order to make a scenario pass.

Keep task-owned evidence after a failure until understood. Clean up by exact recorded IDs, retaining
original failure and cleanup failure separately. Never kill all dotnet/docker processes or prune
volumes globally. A missing prerequisite is BLOCKED, not PASS or an excuse to claim full PP2 done.
