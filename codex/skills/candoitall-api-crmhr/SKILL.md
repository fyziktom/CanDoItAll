---
name: candoitall-api-crmhr
description: Use when listing, creating, or relating CanDoItAll CRM-HR parties; managing workforce profiles, skills, and capacity; or operating recruiting applications, interviews, lifecycle tasks, support assignments, and candidate conversion through the HTTP API.
---

# CanDoItAll CRM-HR API

Use this skill for CRM-HR HTTP operations. The CRM-HR application services remain canonical; never replace these routes with SQL, EF tooling, migrations, or startup seed code.

## Access

1. Start the intended CanDoItAll Web instance.
2. Call `GET /api/access/status`.
3. If `authorizationEnabled` is true, send `Authorization: Bearer <token>` from an approved token workflow.
4. Treat the response as the source of truth. Scope claims may be issued, but the current API boundary authenticates the whole `/api` group and does not provide CRM-HR scope policies.

Use `/swagger/v1/swagger.json` to inspect the running contract when source and host versions may differ.

## Operating Workflow

1. Read the relevant bounded collection before creating anything.
2. Resolve references to concrete GUIDs; do not send display names where an id is required.
3. For deterministic automation, search by a unique external code or unique scenario name and reuse the returned id.
4. Submit the smallest typed command that owns the intended change.
5. Read the resource back and verify relationships, workforce state, or recruiting workspace.
6. On a non-success response, inspect `errors[].code`, `errors[].message`, and `errors[].severity`. Do not hide the failure with another persistence path.

Read [references/api-contract.md](references/api-contract.md) before constructing request bodies or multi-step scenarios.
Body enums currently use the Web host's numeric encoding; use the reference mapping or a typed .NET client rather than guessing values.

## Party And Relationship Rules

- Party collection reads are source-paged. Keep `pageSize` within the documented limit; follow `pageIndex` rather than loading every record.
- Sensitive-party collection items intentionally mask external code, summary, and tags.
- Party creation does not accept confidential notes. Use ordinary tags, public contact points, roles, and addresses only when the business scenario needs them.
- Relationship replacement is a full replacement for the selected party. Read the current relationship list, merge deliberately, then send the complete intended list.
- Never create self-references or duplicate source/target/kind identities.

## Workforce Rules

- Create a saved person or organization-unit party first.
- Resolve delivery-unit and manager ids before saving a workforce profile.
- Reuse a skill definition by exact normalized name instead of creating near-duplicates.
- Resolve the saved skill id before assigning proficiency to a party.
- Capacity dates and percentages must describe real scenario facts. Do not fabricate project allocations or financial data.
- Candidate conversion is the canonical path for turning a recruitment application into an active workforce profile when the hiring workflow has reached that point.

## Recruiting Rules

- Create or resolve the candidate party before saving an application when deterministic identity matters.
- Use application collection paging and exact scenario names to avoid duplicate applications.
- Resolve recruiter, hiring manager, interviewer, task owner, target unit, buddy, and mentor ids before commands that reference them.
- Save the application stage first, then interviews/tasks/support assignments that depend on it.
- Read the recruitment workspace after mutations; it is the aggregate verification surface for stage history, interviews, lifecycle tasks, support assignments, and conversion state.
- Do not convert rejected or withdrawn scenarios merely to populate Workforce.

## Idempotent Demonstration Data

- Use a stable namespace such as `DEMO-CRMHR-*` for party external codes and stable candidate names for applications.
- First pass: search, create only when absent, then capture returned ids.
- Second pass: repeat every search and prove the same ids are resolved. Do not claim idempotency from total row counts alone.
- Prefer updating canonical records through supported save commands when an existing deterministic record needs adjustment.
- Do not add a product seed route, startup hook, direct-database script, or destructive reset endpoint.

## Validation

- Confirm collection `totalCount`, `pageIndex`, `pageSize`, and item identities.
- Read back each created party/workforce/recruiting workspace.
- Exercise at least one invalid reference and confirm a structured 400/404 without partial persistence when validating API changes.
- After API source changes, run the focused `CrmHrApiIntegrationTests`, build the Web project, inspect OpenAPI, and refresh this skill if routes or DTOs changed.
