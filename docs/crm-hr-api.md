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
The canonical skill is maintained in `CanDoItAll.SharedInfo`; the checked-in
`codex/skills` tree is not a current contract.

## Privacy And Safety

- Party and workforce collection reads use the source-paged party query service; they do not load every party and fake paging in Web.
- Sensitive collection items keep the module's redacted external code, summary, and tags.
- Party creation does not accept confidential notes.
- Explicit workspace/detail reads can contain personal or HR data. Do not log bodies or responses and do not expose the API remotely without bearer authorization.
- Relationship replacement is authoritative for the selected party. Read, merge, and send the complete intended relationship list.
- Invalid references return the shared structured error response. Do not retry through a database bypass.

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
