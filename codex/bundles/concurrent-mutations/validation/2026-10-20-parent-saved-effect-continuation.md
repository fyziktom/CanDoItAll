# Parent-authorized inherited saved-effect continuation

Status: implemented and validated after `065f2a297`. Backend remains on
`concurrent-mutations`; SPA stays on `codex/concurrent-mutations` without merge or push.

The [design](../plan/P07-parent-saved-effect-continuation.md) adds an explicit Core
decision-owner entry for an original inherited execution with a trusted live-parent
authorizer. The owner-issued lease retains the immutable original identity. Parent,
caller and background dispatch authority are rechecked after lease waits and before
claiming the approved proposal. A real receipt preflight, invocation claim, selected
publication snapshot, original payload and finite live work interval remain mandatory.
Generic saved effects and interactive continuation keep their existing restrictions.
Committed or uncertain outcomes can still be recorded after revocation without granting
new mutation or disclosure authority.

Tooling and Runtime.Abstractions expose explicit inherited context/request factories.
These data objects grant no lease. MAF uses the original context scope and the registered
governed Process provider purpose through its existing restoration, policy, receipt,
claim and outcome pipeline. No fabricated chat governance or Workbench provider activation
is introduced. The actual Workbench/Process producer, workspace approval lifecycle,
manager continuation, historical receipt and Workflow integration remain open.

## Validation

Direct standard Release builds pass: Runtime.Abstractions 1.9718159 seconds, Core
3.2950656, Tooling 2.0052344, MAF 5.4550551 and Integration 39.8783541. Tests use the
refreshed Integration assembly with `--configuration Release --no-build --no-restore`.
Built discovery exactly matches execution on the final frozen candidate:

| Filter | Discovered | Passed | Failed | Seconds |
| --- | ---: | ---: | ---: | ---: |
| `FullyQualifiedName~MafInheritedSavedEffectDispatcherTests` | 14 | 14 | 0 | 25.8561535 |
| `FullyQualifiedName~AgentInheritedPlanning\|FullyQualifiedName~AgentPlanningSavedActionTests\|FullyQualifiedName~AgentSavedEffectDispatchTests\|FullyQualifiedName~MafSavedEffectDispatcherTests\|FullyQualifiedName~AgentToolBackgroundJournalIntegrationTests` | 187 | 187 | 0 | 388.1237692 |

All 201 cases pass, including 44 new cases. They cover original identity substitution,
revocation before entry/after waits/before claim, actual claimed publication, original
scope and Process policy, work expiry, lost acknowledgements, outcome retention after
revocation, provider-purpose refusal and request-without-lease refusal. Pure context
identity cases run without a filesystem fixture. Current permission denial asserts the
saved result and a fresh catalog read before dispatch.

Two failed candidates remain in the proof history. The first passed 190/200 cases;
ten inherited MAF paths correctly encountered the still-interactive-only request
constructor. The explicit inherited request factory repaired that missing boundary.
The second passed 13/14 runtime cases; the Agent-denial fixture used a built-in Agent
whose canonical normalization restored permission. Its regression lane did not run.
The final fixture uses an ordinary Agent. Only that fixture, its assertions and the
design changed after the second candidate; the exact bridge is retained for architecture
snapshot reuse. All 201 tests were repeated; passing tests were not carried forward.

Full portability scanning and final no-write enforcement pass with 15,642 reviewed
findings. Two added/two stale absolute-path-field fingerprints were reviewed and refreshed:
the MAF context expression changed while the same fresh path-policy root remained.
No new platform assumption was introduced. Baseline/secret-tool tests, rendering checks,
documentation and diff checks pass. Historical broad Stable failures and unexecuted
cases remain open; this focused checkpoint does not claim broad or E2E closure.

## Architecture gate

Status: Pass with follow-up. One typed trusted continuation owns original-parent
validation; Core retains canonical metadata parsing and its existing lease state machine.
Tooling remains independent of Core and product modules. No project reference, runtime
partial, service locator, cache or registry was added. Existing saved-effect journal
methods carry the new origin through the same state machine rather than duplicating it.
Current parent callbacks execute outside held catalog/SQL publication authority.

Initial snapshots `snap-20261020061528-d47ccae3` and
`snap-20261020063437-d47ccae3` cover five projects: 2112 to 2113 types, 15916 to 15930
members and 641 findings unchanged. No added finding; five existing size findings change.
The expanded Runtime.Abstractions snapshots `snap-20261020064515-e6db4b7b` and
`snap-20261020064944-e6db4b7b` cover six projects: 2137 types, 16074 to 16076 members
and 646 findings unchanged, with no added/changed findings. There are no project/module
cycles; five existing type cycles are unchanged. The 189 existing analysis diagnostics
remain; the snapshots supplement the direct successful builds. The next module adapter
must compose original permission ceilings with the enlisted selected Process fence.

## Frozen evidence and resources

All three validation roots are Released. The final candidate verifies 9601 source files
and 2472 runtime files unchanged after both fully observed commands. Source manifest
SHA-256: `65BB6B980434554AC01C2B9E423BEC3D97A236DBD3D6C708E6FCBAE6F5C5B9C0`.
Runtime manifest: `A15AFAA0ADD70B3183F6A08B8D5E5D5343E065A004AB121919549CF406A51DA3`.
The observer covered 430.5070979 seconds. No observed process or database-name delta
remains. Seven final, four initial and three intermediate unattributed TEMP entries are
preserved without deletion authority. The existing isolated PostgreSQL 18 fixture on
53619 remains required for subsequent integration. Ordinary 5032, installed tooling and
historical cleanup denials remain preserved and open; this is not global cleanup closure.

Retained UTF-8/LF proofs under `proof/2026-10-19-planning-authority`:

| Candidate | Proof SHA-256 |
| --- | --- |
| `c4-parent-saved-continuation.json` | `29D4220760774A35B2893A3047ED03AB23D4D8269AE6417FB7A210CF096EA872` |
| `c4-parent-saved-continuation-final.json` | `73B0877FBF834576615FC935BD368414F0EF3B24A9719DFB6CE1F43D92F938A7` |
| `c4-parent-saved-continuation-verified.json` | `9BC315B9532CB73806F52DB8D6DF48E4D854C8E3427FDEEABBA1E96588900C9F` |

Proofs contain exact commands/results, failures, architecture review, frozen identities,
observation coverage and resource reconciliation. The bounded pre-commit secret review
covers these three files and the private fixture sentinel without echoing its value.
