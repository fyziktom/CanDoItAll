# Portable-Path Alpha Data Repair

This is a temporary operator prompt for installations that kept experimental CanDoItAll
data from before the August 2026 portability changes. It is intentionally a manual,
backup-first repair instead of permanent compatibility code. Do not use it for a fresh
installation or when the listed diagnostics are absent.

Related current contracts:

- [Installing instances](../operations/installing-instances.md)
- [Installed Windows app](../operations/installed-web-app.md)
- [Storage, paths, and host portability](../architecture/storage-and-path-portability.md)

## Prompt For A Coding Agent

Copy the following prompt into Codex, Claude, or another coding agent running on the
affected machine:

> Diagnose and repair this pre-release CanDoItAll installation without changing product
> code or adding compatibility branches, fallback paths, EF migrations, or legacy seed
> behavior. This repair is only for experimental alpha data retained across a reinstall.
>
> 1. Reproduce one failing `/projects/{id}/structure` page and capture the browser HTTP
>    status plus the application logs. On the dedicated Windows app, start with
>    `%LOCALAPPDATA%\CanDoItAll\WebApp\logs\stdout.log`. On Unix, use the configured
>    service log root. Continue only when the evidence matches one or more of these retired
>    alpha states:
>    - `The trusted workspace filesystem bootstrap entry is not authoritative for the current workspace root.`
>    - `Process strategy result receipt is invalid or exceeds the bounded contract.`
>    - `ProcessPlanMigrationRequiredException` for an old experimental in-flight run.
> 2. Discover the active install root, workspace root, database provider, database name,
>    and host-binding policy from the current launcher/configuration and source. Do not
>    guess paths or copy identifiers from another machine.
> 3. Stop the app and create a complete database backup outside the install root. Verify
>    that the backup can be listed or restored before modifying data. Record its path,
>    byte length, and SHA-256 hash. Never remove the PostgreSQL volume, database, workspace,
>    secrets, or user-created files as part of this repair.
> 4. Inspect `Storage_Catalog`. Proceed only when there is exactly one enabled,
>    system-default filesystem row named `Workspace file system`, its physical root is the
>    current configured workspace root, and it is rejected solely because its host-binding
>    fields still have legacy/unbound values. In one assertion-guarded transaction, preserve
>    the row id and bind it to the current host. Derive the format version, platform enum,
>    native path-syntax enum, active-state enum, and host-binding id from the checked-out
>    version's `HostBoundPathPolicy`, `StorageCatalogHostBindingPolicy`, and configuration.
>    Do not hard-code the example machine's identity. Set the validation timestamp and
>    verify the bound row resolves to the exact current workspace root.
> 5. Audit `process_strategy_result_receipts`. If a result hash is exactly 64 lowercase
>    hexadecimal characters, normalize only that value to `sha256:<existing-hash>` in the
>    same guarded transaction. Do not rewrite already-prefixed hashes, fabricate receipt
>    payloads, or weaken the current bounded receipt contract. Verify every remaining
>    result hash has the current `sha256:` form.
> 6. Restart the app. If logs identify an old blocked experimental run whose compiled
>    instance plan is not executable on the current contract, retire it through the
>    supported `POST /api/processes/runs/{runId}/cancel` endpoint with an explicit alpha
>    migration reason. Do not edit plan JSON, replay it against new templates, or delete
>    process history. Confirm the run becomes `Cancelled` and its execution retry loop
>    stops. The separate run-record facts projector may finish its already-scheduled,
>    bounded retries against the obsolete plan. Let it reach the configured maximum
>    naturally; verify `FactsStatus=Failed`, `FactsAttemptCount` equals that maximum, and
>    `FactsNextAttemptAtUtc` is null. Do not mutate or delete the reporting record.
> 7. Audit current packaged templates before assuming they need repair. Compare the
>    installed and source-release manifests for `Templates/Agents`,
>    `Templates/Capabilities`, `Templates/Processes`, and `Templates/Workflows`.
>    Capabilities cover managed skills, tools, and MCP definitions. Check managed seed
>    versions in the agent and capability catalogs. Update managed seeds only through the
>    current pack/seed mechanism when a real version mismatch exists; preserve custom
>    agents, workflows, processes, and capability records.
> 8. Verify `/health`, `/api/runtime/operations`, the original project structure page, a
>    second existing project, the projects list, and a nonexistent project id. The runtime
>    must report `Ready`, database migrations must be ready, valid project pages must load
>    without browser console errors, and a nonexistent id must return to the projects
>    surface without HTTP 500. Re-read the fresh application log and confirm the storage,
>    receipt, and execution-retry diagnostics do not recur. If the bounded run-record
>    facts projector logged its final process-plan warning, confirm there is no further
>    log growth and no next-attempt timestamp after it reaches the configured maximum.
> 9. Report the backup evidence, exact rows changed, any run cancelled, template versions
>    audited, and all verification results. If the data shape differs from these assertions
>    or unrelated failures remain, stop and report them instead of broadening the repair.

## Expected Scope Of This Repair

For the known August 2026 alpha case, the only database mutations should be:

- one trusted `Storage_Catalog` row changed from legacy/unbound metadata to the current
  host-bound representation;
- legacy bare SHA-256 result hashes prefixed without changing their digest bytes; and
- cancellation through the public process API of any obsolete in-flight run that cannot
  execute under the current process-plan contract.

The derived run-record projection is expected to retain its terminal failure as historical
evidence; it does not require an additional database edit when its next-attempt timestamp
is null.

Current template packs normally arrive with the reinstalled application and reseed their
managed records. A manifest or managed-seed version match is evidence to leave that catalog
alone; it is not a reason to overwrite user customizations.
