# Offline recovery of a committed project deletion

A committed deletion can retain an `AwaitingOwnerRelease` completion when its caller
loses acknowledgement that its database or source leases were released. Acceptance of
the deletion and completion of cleanup are separate facts. Retrying a callback, waiting
for its claim to expire, or reading the current project does not clear this gate.

The `project-deletion-recovery` command inspects and reconciles one retained completion.
It does not delete a project, invoke cleanup participants, activate a dataset, migrate a
database, repair a profile, or start the application host. Current implementation and
validation limits are recorded in the [execution record](../../../codex/bundles/concurrent-mutations/validation/2026-10-18-lifecycle-integration.md).

## Preconditions

Use the reviewed application build with the original host's configuration, explicit
database profile and existing Data Protection keys. The profile must select PostgreSQL,
and its local activation record and SQL dataset must identify the same active namespace.
Missing keys, pending profile-path migration, an unsupported protocol or changed original
workspace binding cause refusal. The command does not choose the active/default profile.

Before reconciliation, stop every host that can write this database **or any captured
source workspace**, including older binaries, workers, suspended processes and transfer
commands. Disable automatic restart and configuration/activation maintenance. Keep them
stopped through command exit and resource reconciliation. `--writers-stopped` is an
operator attestation; the command cannot prove that another host will not restart.

The recovery address consists of the original profile ID, dataset namespace ID and
completion ID. Obtain these from the retained deletion acknowledgement or an exact
completion observation. Do not substitute a recreated project's lifetime or a newer
namespace. No administrator-supplied SQL, path or participant override is accepted.

## Inspect and reconcile

Run the already-built Web assembly from its configured content root. The UUID arguments
use the full `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` format. These examples contain
placeholders; use the exact retained values.

```text
dotnet CanDoItAll.Web.dll project-deletion-recovery inspect --profile-id <profile> --expected-namespace <namespace> --completion-id <completion>
dotnet CanDoItAll.Web.dll project-deletion-recovery reconcile --profile-id <profile> --expected-namespace <namespace> --completion-id <completion> --expected-plan-digest <digest> --writers-stopped
```

`inspect` returns a JSON `Observed` result with `result.plan.digest.hex`. Review the exact
address and participant identities, then pass that digest to `reconcile`. Inspection
does not grant release permission. Reconciliation rechecks profile identity, complete
source capture, native retirement, installed participant protocols and exact records.
It acquires the original file exclusions and nonwaiting database exclusions, confirms
their terminal release, and only then compares and sets the unchanged gate to Released.

Keep the same completion identity on every retry. An already-released exact completion
returns its original release timestamp; it does not repeat the deletion or callbacks.
A changed plan requires fresh inspection and investigation. A busy exclusion requires
identifying its owner and establishing the stopped-host condition, not bypassing the
lock or increasing a lease expiry.

## Results and follow-up

| Exit code | Result | Meaning |
| --- | --- | --- |
| 0 | Observed | Inspection succeeded; no release was performed. |
| 0 | Released | The exact gate is released and command cleanup was acknowledged. |
| 2 | Refused | The requested operation was not established. Inspect the typed reason; setup failures expose only a safe failure type. |
| 3 | Unconfirmed | The attempted release or installation could not be conclusively observed. Keep hosts stopped and repeat the same address after the old command terminates. |
| 4 | Released with diagnostic or cleanup uncertainty | The release is known, but the command did not confirm all of its own terminal work. Keep hosts stopped, resolve the reported uncertainty and repeat the same address. |

After confirmed release and terminal command cleanup, normal participants still require
current authorization and the original active dataset. Resume the application and use
its exact pending-cleanup operation. A successful selected cleanup does not imply that
other participants finished. Retained media warnings remain part of that participant's
completion history.

The command currently supports complete native completion evidence, including captured
canonical organization catalogs and separate Project/Sandbox execution publications.
Missing original directories are refused without creating replacement workspaces. Old
evidence without complete caller or preparation-exclusion capture remains held.
Do not edit the gate or activation file manually to make it executable.

## Legacy installation

The legacy installation command is validated on this branch; its exact acceptance is
recorded in the [architecture gate](../../../codex/bundles/concurrent-mutations/validation/2026-10-19-legacy-installation-architecture.md). Keep every writer stopped,
including old binaries, and establish termination of all former source-owner hosts at
former workspace locations. The command cannot reconstruct or verify lost historical
paths. If that condition cannot be established, the work remains held.

Retain an exact project lifetime from native evidence and choose one completion UUID
for this installation. Keep that UUID for every retry. Use the active selected namespace:

```text
dotnet CanDoItAll.Web.dll project-deletion-recovery inspect-legacy --profile-id <profile> --expected-namespace <namespace> --completion-id <completion> --project-id <project> --lifetime-id <lifetime>
dotnet CanDoItAll.Web.dll project-deletion-recovery install-legacy --profile-id <profile> --expected-namespace <namespace> --completion-id <completion> --project-id <project> --lifetime-id <lifetime> --expected-plan-digest <installation-digest> --writers-stopped --former-source-hosts-stopped
```

Inspection returns `LegacyObserved` with `result.plan.digest.hex`. Review the identities
and native participants. Installation repeats the inspection under the current canonical
organization catalog exclusion and nonwaiting database exclusions. It requires a native,
non-imported retirement, no live/recreated project or residual Workbench project objects,
valid exact source references and a complete unambiguous participant set. Completed
partners are included when another participant remains unfinished. Unsupported/unbound
historical lifetimes are not inferred from a current project or a latest retirement.

Installation atomically adds one Awaiting completion and binds the existing native
records. Payloads, claims, attempts, diagnostics and timestamps remain unchanged. The
new origin records an installation namespace and the explicit host attestation; it
creates no historical mutation receipt, revision or deletion timestamp. The captured
source is the current canonical catalog used for installation, not an assertion about
where the original operation ran.

`InstalledAwaitingRelease` with exit 0 confirms installation and terminal cleanup, but
does not release callbacks. Run ordinary `inspect` and `reconcile` above using the same
completion address and the new release-plan digest. Exit 3 preserves an uncertain
installation commit; keep hosts stopped and retry the same identity. Exit 4 with a known
installation preserves that fact while reporting cleanup/diagnostic uncertainty.
An exact installed or already Released completion is observed without rebinding it.

Each owner reads at most 17 subject headers to enforce the 16-record inspection cap;
multiple candidates for one owner currently refuse. Workbench checks its 2 MiB payload
limit before materializing one payload at a time. The complete encoded installation
plan is limited to 64 KiB. These are administrative compatibility limits, not claims
about all retained historical data. Oversized or ambiguous cases need an explicit
owner-specific repair. Installation does not migrate the database or create missing
workspace directories.

## Interrupted reactivation

If a Reactivate plan remains Prepared while SQL still has its exact previous Active
binding, restore that previous local selection before releasing native deletion work:

```text
dotnet CanDoItAll.Web.dll dataset-activation restore-previous --profile-id <profile-uuid> --expected-namespace <previous-namespace-uuid> --expected-plan-id <prepared-reactivation-plan-uuid> --writers-stopped
```

Keep every writer stopped and prevent restart until this command terminates. Supply the
original Prepared plan ID on every retry. The command checks the existing profile, full
previous SQL binding and installed finite-operation pool protocols. It does not migrate,
start the host, rotate an epoch or invoke participant callbacks. It refuses missing
selection files/directories, a different plan, an advanced SQL binding or unavailable
exclusion. It cannot recover an activation whose SQL rotation already committed; that
case requires resuming the original activation.

The version-2 activation record retains the complete abandoned plan and a distinct Attach
plan for its previous binding. A retry resumes that same attachment, including after a
lost Active-publication acknowledgement. Valid version-1 records remain readable; new
publications use version 2, which older readers must refuse.

Exit 0 with `Restored` confirms selection restoration. Exit 2 is `Refused`; exit 3 is
`Unconfirmed` and requires inspecting/retrying the same original plan after the old
command terminates. Exit 4 retains a known `Restored` result with terminal cleanup or
diagnostic uncertainty; resolve that uncertainty before restarting. Restoring selection
does not authorize participant cleanup or assert readiness of unbound legacy work. The
normal recovery inspection/reconciliation and current-authority cleanup still follow.

Completion gates are currently retained without automatic expiry. Finite mutation
receipt expiry neither authorizes cleanup nor removes a gate. Readiness checks the exact
completion and native retirement of every unfinished Agent and Workbench deletion. Valid
Awaiting work permits startup for inspection; its callbacks remain held. Unbound, partial,
foreign, oversized or otherwise invalid pending work refuses readiness.

Initial bootstrap and attachment can establish their exact Active selection before
readiness refuses legacy work. This leaves the selected namespace available to offline
repair without starting ordinary hosts or releasing cleanup. An interruption after SQL
selection can be resumed with the same retained plan; readiness still refuses until the
native bindings are repaired. Unknown finite-operation history and incompatible pool
protocols continue to refuse selection itself.

Reactivation checks for Awaiting gates anywhere in the database and for unfinished native
deletion work, including Released work. It checks before publishing Prepared and repeats
under exclusive SQL lifecycle exclusion. An early refusal preserves the Active selection;
a concurrent deletion after preflight can leave a Prepared plan, recoverable through the
exact restoration command above. A plan whose SQL rotation already committed resumes
against its new binding. These guards perform only SQL reads, with 128-header pages,
2 MiB per Workbench payload and 8 MiB per page; completion interpretation uses batches
of at most 64 references. Three partial indexes exclude terminal history. The guard
candidate's runtime acceptance is recorded separately in
[lifecycle validation](../../../codex/bundles/concurrent-mutations/validation/2026-10-18-lifecycle-integration.md).
