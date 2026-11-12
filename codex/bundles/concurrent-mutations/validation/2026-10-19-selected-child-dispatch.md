# Selected Process child dispatch authority

Status: all 86 selected PostgreSQL cases pass. The selected authority query is implemented;
producer activation, registered source-policy routing and inherited Workbench effects
remain open under the P07 integration plan.

## Behavior and boundary

ReadDecisionDispatchAsync requires the exact child execution ID and explicit supported
execution/content contracts. It reuses the existing claim, actual Agent, creation interval,
readiness and original prepared-launch checks. A cohesive persistence helper verifies
the child/grant, original policy, material scope and full stamp through bounded metadata
reads. The material projection excludes PayloadJson. No new schema, DI registration,
public interface, project reference, runtime partial, cache or global lock was added.

The selected fingerprint includes the original source identity, canonical parent binding
and canonical original policy in its own versioned domain. Mutable phase, claim expiry
and closure do not change that identity. Historical reads remain bound after current
dispatch is denied. The existing ReadDispatchAsync fingerprint is unchanged, and ordinary
reservations and the legacy mutation guard still refuse these planning children.

Dispatch additionally requires a current active generation/shared scope, open allocation,
positive work, no recorded budget overrun, active runtime state and the exact unexpired
claim/attempt. The clock is sampled after the awaited metadata reads. A deterministic
interceptor test advances it during the final SELECT and proves expiry is observed before
authorization returns. Publication still requires the separate enlisted root fence.

## Validation

| Selection | Passed |
| --- | ---: |
| ProcessDecisionDispatchAuthorityTests |39|
| ProcessExecutionAuthorityPersistenceTests |20|
| ProcessDecisionChildMaterialReaderTests |18|
| ProcessDecisionRunScopePersistenceTests |9|
| Total |86|

Cases cover RunSnapshot/StepSnapshot, descendants, prepared launches, original-policy/
Agent/readiness substitution, wrong child/material/scope/grant/contracts, claim identity
and creation interval, mutable revocation with historical identity, restart, closed legacy
mutation routes and three exact SELECTs independent of sibling count. The prepared-launch
fixture exercises the actual accepted preparation path. No result bridge is used.

Persistence, Modules.Processes and Integration pass serialized standard Release builds;
all three were refreshed after the final expiry-timing change. Discovery confirms 86 cases.
Full portability-static passes with 15,641 reviewed findings. Exactly one new allowance
was reviewed: ordinal case-insensitive comparison of the existing external ExecutorKind
protocol, independent of filesystem/OS semantics. The baseline diff has that one addition,
no stale allowances, and final enforcement has no write flag. Scanner tests, rendering,
documentation and diff checks pass.

Scoped CodeAnalytics before snap-20261020032126-809c058b and after
snap-20261020032925-809c058b retain five projects, 107 registrations, 31 entities, 743
findings, 16 open questions and 205 loader diagnostics. Two cohesive internal types and
eleven source members were added. There are no new or changed findings or cycles and
no project cycle. This scoped result does not claim a healthy whole-repository dashboard.

## Evidence and resources

Source: 9,569 files, SHA256
D4C00526442D77F613A73F16E980EA02CF96A9877016212C146C5CAC28B068EB.
Runtime: 2,170 files, SHA256
679A6A9BDE45C617B239681B1270DF71C4311ECF791F2D5D3C2456BF08694F76.
[Retained proof](../proof/2026-10-19-planning-authority/c4-selected-child-dispatch.json)
SHA256 0229E15BDA354A756C3F409FC124D7BFD80517086CD7E05CBF70E1677AA02CBD.

The c4-selected-child-dispatch root records ownership before creation and reuses the
isolated PostgreSQL 18 endpoint on 53619, standard outputs and 119 resource families.
The 362.1683068-second observer covers the complete 346.3828495-second test command.
Source/runtime identity verifies unchanged. No observed process or database-name delta
remains; three unattributed TEMP entries are preserved. The freeze is released. The
shared fixture, historical cleanup denials and failing broad Stable gate remain open.
