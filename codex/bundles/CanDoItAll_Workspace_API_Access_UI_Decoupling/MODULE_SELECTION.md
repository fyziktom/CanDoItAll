# Why API Access is the next Workspace slice

The last step extracted Defaults, Secrets, Files and Provider history, plus a host-composed
Settings shell. Three local sections remain: Data Sources, Storage and API Access. This
bundle chooses only API Access; Providers is still a redirect rather than a local editor.
WS05, WS06.

API Access is security-sensitive, but its existing visible tree is bounded: status host,
issuance form, token metadata dialog, scope picker and user administration. Its owners are
already explicit. Unlike Data Sources and Storage, this UI does not need to orchestrate
switching the business database, transferring module records, uploading bytes or reconciling
storage placements. This is a scope/dependency judgment, not a promise of low effort.
AP01–AP07, AP11, AP12.

## Included as a complete section

- API/auth/key-presence/effective-feature status and safe configured-admin explanation.
- Machine-token issuance, raw lifetime/scopes, explicit one-time disclosure and scope picker.
- Lazy token metadata search/paging, refresh, revoke/delete confirmations and recovery state.
- Ordinary account search/paging, create/edit/enabled/scopes, password reset and deletion.
- Correct nested-dialog ownership, sensitive transient state and real shipped controls.

## Explicitly deferred

Data Sources, profile switching/transfer, Storage catalog/recovery/pickers, Projects,
Processes and Workbench remain outside extraction. No login page, new account role,
credential-kind browser, OAuth flow, signing-key editor or general authentication redesign
is being commissioned. Existing HTTP session endpoints remain compatible and tested.

## Separate leaf, not a larger Core

Use a dedicated API Access renderer/contracts/sandbox by default. The generic Core library
must not acquire API-specific project references just to display a Settings tab. The module
route composes the new leaf through its existing slot. Core sandbox remains independent.
This placement controls dependency growth while still completing the real production UI.
See [dependency design](ARCHITECTURE.md).

After this slice, Workspace is still partially complete. Reassess Data Sources and Storage
as separate subsequent tasks based on their actual owner boundaries; do not assign them in
this run or claim that a new `.UI` project means all Workspace is finished.
