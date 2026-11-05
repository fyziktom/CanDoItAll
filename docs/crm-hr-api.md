# CRM-HR API

The CRM-HR HTTP API exposes the normal application commands needed to create and inspect realistic party, workforce, skill, capacity, and recruiting scenarios. It is mapped under `/api/crm-hr` by the Web host.

## Boundary

```text
HTTP client
  -> CanDoItAll.Web route binding and result mapping
  -> CRM-HR application/query services
  -> normal persistence, audit, activity, and search side effects
```

There is no seed endpoint, direct SQL/EF path, or startup demo-data hook. Scenario operators use the same typed application services as the UI through HTTP.

## Discovery And Access

1. Call `GET /api/access/status`.
2. Inspect `/swagger/v1/swagger.json` for the running request/response schema.
3. If `authorizationEnabled` is true, send an approved bearer token.
4. Use the canonical
   [CRM-HR API skill](https://github.com/fyziktom/CanDoItAll.SharedInfo/blob/main/codex/skills/candoitall-api-crmhr/SKILL.md)
   for dependency order, payload fields, and readback.

The checked-in local configuration deliberately leaves bearer authorization disabled for a trusted loopback host. Any remotely reachable deployment must enable it. JWT `scope` and `scopes` claims are currently issued as metadata but are not enforced as CRM-HR route policies.

## Route Families

- `/parties`: bounded safe directory reads and typed party creation.
- `/parties/{partyId}/relationships`: read and explicit full replacement of one party's relationships.
- `/workforce`: bounded workforce discovery and explicit party workspaces.
- `/workforce/profiles`: workforce-profile saves.
- `/workforce/skills`: skill catalogue reads and saves.
- `/workforce/party-skills`: party proficiency saves.
- `/workforce/capacity-blocks`: leave/unavailable/reserve/tentative capacity facts.
- `/recruiting/applications`: bounded application discovery, aggregate workspace read, and application saves.
- `/recruiting/interviews`: interview saves.
- `/recruiting/lifecycle-tasks`: onboarding/offboarding task saves.
- `/recruiting/support-assignments`: manager, buddy, and mentor assignment saves.
- `/recruiting/conversions`: canonical candidate-to-workforce conversion.

Exact verbs, DTO fields, enum values, and limits live in the running OpenAPI document.
The canonical skill is maintained in `CanDoItAll.SharedInfo`; this product repository
does not maintain a duplicate skill source.

Workforce capacity views retain historical project allocations. Only allocations bound
to a current, unretired project lifetime contribute to capacity and next availability.
An allocation bound to an older lifetime does not inherit the name of a project recreated
with the same public ID; legacy unbound references retain their existing display labels.

## Privacy And Safety

- Party and workforce collection reads use the source-paged party query service; they do not load every party and fake paging in Web.
- Sensitive collection items keep the module's redacted external code, summary, and tags.
- Party creation does not accept confidential notes.
- Explicit workspace/detail reads can contain personal or HR data. Do not log bodies or responses and do not expose the API remotely without bearer authorization.
- Relationship replacement is authoritative for the selected party. Read, merge, and send the complete intended relationship list.
- Invalid references return the shared structured error response. Do not retry through a database bypass.

Affiliation application commands require the original `ExpectedUpdatedAtUtc` for an
existing row, including updates through the safe Party command and HR tools. A missing
original returns `crmhr.affiliation.concurrency-version-required`; a stale original
returns a concurrency error. Read and explicitly reconcile instead of fetching a new
version merely to retry the old payload. New native UI rows carry a stable identity and
an explicit `IsNew` absence expectation; that intent cannot replace an existing row.
Full affiliation replacement retains audited history: end-date saved rows rather than
omitting them. Successful results contain the values accepted by that command, captured
inside its transaction, even if another writer commits before the response is delivered.
Party type changes, including atomic imports, cannot invalidate saved affiliation
endpoints, including historical rows. They return `crmhr.party.affiliation-type-conflict`.
Affiliation writes and type changes share native transaction exclusion; an endpoint
cannot change type between validation and commit. Ordinary name/content edits remain
available and still require their original Party observation.

Relationship replacement requires an explicit `Original` observation, including for an
empty set. Build it from the complete relationship read: each observed row contributes
its `Id`, `RelatedPartyId`, and `Version`. Send the complete intended list with that
observation; fetching a fresh version to retry an old list can delete another editor's
work. Missing originals return 400 (`crmhr.party.relationship-original-required`); stale row
versions, changed membership, or changed endpoints return 409. Saved rows keep their
identities, and a successful response contains `PartyId` and the exact `Accepted` rows
from the committed transaction. A later read is a new observation, not the receipt.

Recruiting support reads also include `Original`, limited to the manager, buddy and
mentor relationships they own. Return that observation on support saves. These saves
use the same versioned owner and preserve unrelated relationships; buddy and mentor
remain distinct roles even when assigned to the same person. Canonical support
endpoints must remain people, including during Party type changes and imports.

## Idempotent Scenario Operation

Use stable external codes such as `DEMO-CRMHR-*` and stable application names:

1. Search before create.
2. Reuse the exact returned GUID.
3. Create dependent records only after all referenced parties exist.
4. Read each aggregate back.
5. Repeat the operator flow and prove the same business identities resolve without duplicate creation.

External code is an operator reconciliation key, not a database uniqueness guarantee. After an ambiguous timeout, query before retrying.

## Validation

For API changes:

```powershell
dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter FullyQualifiedName~CrmHrApiIntegrationTests
dotnet build src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --no-restore
```

Then inspect OpenAPI and exercise the live loopback API. UI validation should confirm that API-created data is visible in the Directory, Workforce, and Recruiting catalogues and their record dialogs.
