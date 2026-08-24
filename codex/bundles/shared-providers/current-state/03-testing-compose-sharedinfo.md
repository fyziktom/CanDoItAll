# Current testing, Compose, and SharedInfo

## Test policy

`docs/testing.md` explicitly rejects broad tests as the normal development loop. The required
pattern is:

1. build each affected production project;
2. choose the owning test solution;
3. use an exact or bounded fully-qualified-name filter;
4. run `--list-tests`;
5. record expected and actual discovery;
6. execute the filtered tests;
7. run the stable aggregate only at a named frozen checkpoint.

The bundle preserves this policy and makes the broad invalidation triggers explicit.

## Existing Docker stack

`compose.yaml` builds one app image from
`src/App/CanDoItAll.Web/Dockerfile`, uses sibling Components/FileTools build contexts, runs a
PostgreSQL service, uses health checks, read-only app filesystem, bounded logging, and
per-service resource limits.

The shared-provider proof should extend, not replace, this development topology. A dedicated
Compose file will reuse one app image for:

- `central`;
- `client-a`;
- `client-b`.

Each application uses an independent database and data root. A deterministic upstream
provider service is separate from the CanDoItAll app containers.

## Fixture rule

Direct SQL inserts do not prove application behavior and cannot safely seed vault-backed
secrets. The E2E setup must use canonical application services through a dedicated
non-production orchestrator/fixture tool. It must not add a production bypass endpoint.

## SharedInfo

SharedInfo's bundle skills require:

- semantic input coverage;
- dependency-aware subbundles;
- architecture artifacts;
- proof tiers;
- expected test discovery;
- durable handoff;
- final closure.

The shared OpenAPI directory records the snapshot and provenance. Existing API skill folders
show the route appendix and shared snapshot linking pattern to follow for the new skill.
