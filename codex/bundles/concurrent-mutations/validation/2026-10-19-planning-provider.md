# Planning provider integration

The actual configured MAF/SDK planning-step port passes its 13 integration cases. The
repaired candidate passes **103/103**: those 13, all 20 live-owner cases using the
repaired fixture, and 70 policy/journal/request-digest cases. This closes the provider
adapter slice, not product admission, Workbench publication, saved actions or UI.

## Behavior and architecture

Core owns the module participant contract and real live-work capability. MAF sends
only the original request/target and the selected captured basis, using a fresh SDK
session per logical step. The actual admission client verifies final input, performs
current-read authorization, records one provider origin and applies the remaining
active-work deadline. No tools, prior chat facts or provider conversation are exposed.
The configured provider factory is exercised through a deterministic HTTP transport;
history attribution and actual usage normalization are preserved.

Known bounded responses survive cancellation and lost file acknowledgement. Invalid
output creates no executable proposal. Capacity is reserved before provider entry:
the input limit is 2 MiB, encoded response/arguments 256 KiB each, text 128 KiB and
output 8,192 tokens. Source preparation remains 64 KiB. These bounds do not qualify
maximum-size throughput, provider-specific context limits or sustained contention.

CodeAnalytics `snap-20261019051846-daf4d496` covers five projects, 530 documents and
1,953 types. The nine project-reference edges and five existing local type cycles are
unchanged. Its 195 diagnostics are 189 existing package advisories and six truncated
diagram notices; there are no other diagnostics. No project, reference, store, lock,
registry or execution-service partial was added. The fixture repair has an exact
unchanged-production bridge to that snapshot.

## Failed candidate and repair

`c4-planning-provider-validation` executed all 158 cases: **146 passed, 12 failed**.
The seed Agent requested medium reasoning for the fixture model without a discovered
capability. Production correctly refused before transport. The repair removes that
inherited override before original fixture admission; it does not weaken the capability
policy. The initial compile also caught and corrected a test-only history-owner member
name. Failed results remain in the original proof.

All 27 ordinary/native SDK replay cases and 28 saved-effect/file-journal cases passed
on the first candidate. They remain historical proof connected to the repair by its
complete unchanged-production source comparison. This is not a claim of one 158-case
green run. The repair reruns every consumer of the changed fixture.

Both candidates have passing final standard Release Unit/Integration builds, complete
portability enforcement (15,610 unchanged reviewed findings), rendering, documentation
and diff checks. Discovery confirmed 70 + 88 originally and 70 + 33 after the repair.

| Candidate | Frozen source SHA-256 | Frozen runtime SHA-256 |
| --- | --- | --- |
| Initial | `5FD6F1507D626F759857ED175746F53CDAF6B33E869417D815BDF4B89D480217` | `7DEFF5AA95B8C2D7FB8A4CA5C8BFFB846518E486D576F51AA4351171F0C51898` |
| Fixture repair | `C0915F749FC5CBC87147FD3492BA1988C2E74AA729D72080C0E2FAE956C3BC45` | `4D75BBA3CB68659F0760F1E3BEB98F23CDA32C0CDE2F14A242F1590704C9917B` |

Both final comparisons confirm 9,197 unchanged source inputs and 6,436 unchanged
runtime files. The observers and test processes exited; no database-name delta remains.
Nine initial and six repair TEMP deltas without ownership evidence were preserved.
The shared isolated PostgreSQL fixture remains available for subsequent lanes. The
previous separately reported profiler-directory cleanup blocker is unchanged.

Retained proof under `../proof/2026-10-19-planning-authority/`:

- `c4-planning-provider-validation.json`: `DC49DBD415F29078AF0E7D3E198C18F1B3C6B71966C8085D6A25CA589A11AEAD`.
- `c4-planning-provider-fixture-repair.json`: `D8D3303B9F7D5B210508D2EB86204CB167BA99BAACC71C53113708D1D0CCEF75`.

The source freeze is released after those identity/resource checks. Next: connect
Workbench publication to the original decision and active-work budget, preserving
v1 receipt/digest interpretation and current material/authority checks.
