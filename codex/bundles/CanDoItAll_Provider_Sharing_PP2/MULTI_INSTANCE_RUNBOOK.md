# Multi-instance verification: actual containers, actual contracts

## Existing assets to use, not reimplement blindly

R10 is `tools/SharedProviders/Run-SharedProviderE2E.ps1`; its inspected entry declares central,
client-a, client-b, separate PostgreSQL identities, deterministic shared/personal upstreams,
19 protocol scenario IDs and bounded evidence capture. R11 is the existing Compose topology.
R08/R09 are the production browser acceptance and model/price parity helpers. The full runner
and Compose must be read locally before invocation; this review inspected their entry sections,
not every cleanup and orchestration branch of the 120 kB script.

Run the native protocol fixture AND browser acceptance. Neither substitutes for the other.
Do not create a second app/server implementation to simulate publication or import. The only
substitute is the upstream model server; the original OpenAI/Ollama/etc. drivers must talk to it.

## Isolation before any Reset or setup

Record current Git tree, source roots, running host PID/start-time/command, listening ports,
Docker objects and existing labeled proof/manual resources without dumping environment secrets.
The ordinary app on 5032 and retained real-provider pairs are not fixtures. Default runner roots
and project names are fixed in PowerShell despite Compose interpolation. Passing a unique
Compose environment variable alone is not adequate isolation.

Use unique task-owned artifact root, Compose project, image tag and fixture run marker. Prefer a
small parameterization of the existing runner that keeps its path/ownership/safety checks intact.
If using its defaults, first prove every existing object/root is owned by this task and disposable;
otherwise select a separate root/project. Never invoke `-Reset`, `down -v`, prune or recursive
removal before an exact ownership check. Do not use Set-ManualClientDataVolume.ps1 on ordinary data.
Record container IDs, image digest, safe role/config fingerprint, mount roots, network attachments
and source/binary hashes. Do not export full docker-inspect Env or credential-bearing Compose config.

## Required topology

| Role | Requirement |
|---|---|
| Central | Real Web image, source publication/catalog/relay and independent data/key stores |
| Client A | Real Web image, initial provider-empty/native setup lane and imported plus personal provider lane |
| Client B | Real Web image, independent imports, secret IDs, catalog, scopes and restart |
| PostgreSQL | Existing isolated central/client-a/client-b databases and roles; no shared business schema |
| Shared upstream | Existing deterministic upstream with request/model counters, controlled protocol replies and latency/errors |
| Personal upstream | Independent marker/counters proving that failed shared calls never silently fall back |

A browser/test runner can run outside those app containers. Its two/three isolated contexts must
have origin-scoped authorization. A request by C1 must not gain C2's keys or backend scope.
Do not mount the developer's GPG, vault or provider credentials into a container. The existing
Development fixture's key-protection/HTTP allowances are fixture-only, not product defaults.
Keep data and key roots independent. A shared test PostgreSQL process is acceptable; shared
business databases, common DI scopes or common catalog directories are not.

R11 defines private ingress and application/API settings. Discover actual published ports;
never assume 5032. Preserve production API authorization. Local browser operator access is only
for explicitly trusted fixture ingress. For container-to-container HTTP use the existing explicit
private-network source policy on the owned test source. PublicOnly negative controls stay intact.
Use pinned fixture certificates when TLS is used; never install an accept-any-certificate callback.

## Local fixture authorization now granted

The operator requests multi-container tests. This explicitly permits creating, publishing,
synchronizing, issuing, rotating and revoking TEMPORARY TEST credentials and data inside this
owned topology. Old PP1 'no external publication' statements do not prohibit this disposable local
fixture. They still prohibit touching retained/manual/public instances or spending real-provider
credits. Preserve existing paid/live journals; fixture calls are a separately counted synthetic
upstream lane. Enforce no production-provider egress, bounded request sizes, deadlines and retries.

Secrets must be synthetic and generated privately. Store through actual fixture secret/API paths
or the existing owned bootstrap, preserving scope validation. Pause/omit traces and screenshots
while secrets/bearers are entered or shown; redacting only a textarea does not sanitize past traces.
Do not attach full traces, HAR, environment dumps or upstream logs with Authorization. Save only
allowlisted safe metadata/counts/hashes. Retain failures privately until cause/effect is collected.

## Sequence A — protocol fixture

Build actual final-entry app and upstream images from known source/sibling inputs. Run the existing
19 protocol scenarios: catalog boundary; personal/imported coexistence; separate text/image client;
idempotent resync/stable local IDs; duplicate upstream names; buffered/streaming ChatCompletions
and Responses; function tool roundtrip; structured-output allow/deny; image generation;
ETag/304; catalog/invoke scope separation; malformed/access-context controls; unpublish/reappear;
central outage/no fallback; source identity mismatch; disconnect cancellation; redaction.
Use currently available supported contracts, not newly invented model features. Record the exact
scenario result vector and actual network actors. An exit code alone is not complete proof.

## Sequence B — native defaults, kept separate from synthetic rewrites

Capture baseline source provider metadata and all accepted configured model names/defaults before
R08 ConfigureSharedInstance/ConfigureAsync can overwrite them. Snapshot includes purpose,
transport, defaults, suggested flags, prices, Thinking and public identities, with no secrets.
Use current code-seeded/default OpenAI metadata, not a hand-written list from this bundle or the
internet. The captured source owner is the expected-value oracle; the importing materializer
must not compute its own expected values. Independently compare the publication contract.

Distinguish native DRIVER from runtime-only PROFILE. A runtime-only fallback is legitimately
ineligible for publication (R12). Exercise that refusal, then use the supported persisted native
profile setup preserving the accepted built-in model definitions. Do not force a runtime-only
profile into the publication table or weaken eligibility. This refusal cannot waive the persisted
OpenAI-default test. Keep a second customized OpenAI profile as a separate row in the matrix.

The existing browser fixture uses synthetic names, hard-coded provider labels and the hostname
`candoitall-spui-upstream`. Map/parameterize its fixture endpoints to the actual owned topology;
do not assume it works unchanged against the other Compose service names. Preserve its original
lane and add a native-default fixture. Never change production defaults to match a test constant.

Read every eligible model's label and associated metadata. Invoke representatives (default and
non-default, each supported transport), not every model merely to inflate request counts. Make
the upstream accept the preserved names in its test contract; do not overwrite source models to
those already accepted by a narrow stub. Responses carry a unique safe request marker. Assert
actual selected route and upstream dispatch, not just matching text from an echo server.

## Sequence C — UI and lifecycle campaign

Use source UI to publish, client UI to create the source reference/discover/select/import, and
current selectors to configure Agent and Simple Chat. Exercise initial empty catalogs and a
client retaining its own same-kind/same-model local provider. Compare all required surfaces in
MODEL_PARITY_CONTRACT.md. Use separately authorized browser contexts for the second client.

Change source names/default/model set/prices/Thinking through real UI; resync from one client
circuit while another stays open. Test toolbar metadata refresh (S0-R2), explicit source-refresh,
switches A→B→A, failed read, removed model, late reply, retry and same-target no-op. Assert the
published snapshot version, not just element availability. Save selected non-default models,
close/reopen, restart client and central separately using retained owned volumes, resync and
recheck identities/default inheritance and model dispatch. Verify unpublish/republish uses its
existing stable public identity; re-add/retire follows current reconciliation semantics.

Test source identity mismatch, auth scope failure, credential revoke/rotate, source disable,
network outage, 304 and recovery. Preserve prior accepted data only with explicit stale/unavailable
state; unavailable profiles cannot become success through a personal fallback. Do not treat the
client's selected-publication list as a new remote token ACL: test the actual current scope model.

## Final-image repeat

After PP2, rebuild application/image from final signed source and repeat the required protocol
and UI/model scenarios. Build proofs from S0 cannot certify a different PP2 image. Keep original
baseline failures separate and rerun affected downstream owners after fixes. Collect evidence
before stopping/removing only owned resources. Stop on actual unauthorized/wrong-target writes;
continue independent safe diagnostics. Missing environment is BLOCKED, not PASS or SKIPPED-green.
