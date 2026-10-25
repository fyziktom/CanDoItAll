# Canonical Process child and material reader

Status: 169 distinct selected cases pass across an exact single-test-file repair bridge.
This Application boundary is ready for the concrete inherited execution adapter; it
does not activate Process/Workflow dispatch, publication or settings.

## Behavior and architecture

Process Application now declares IProcessDecisionChildReader. The existing bounded
EfProcessDecisionChildQuery implements it without SQL changes; composition resolves
the same scoped instance. ProcessDecisionChildMaterialReader verifies the original
child, grant, assignment policy, execution/content contracts, namespace and full material
stamp. Historical closed or reconciling records stay readable without restoring dispatch
rights. Actor authorization and the enlisted publication fence remain separate owners.

The strict bounded binding codec preserves exact typed IDs and 64-bit grant values.
It rejects unknown, missing, duplicate, substituted and noncanonical wire data. Its
flat private wire record avoids default-valued readonly ID wrappers during deserialization.
No project reference, runtime partial, service locator, cache or second store was added.

Scoped CodeAnalytics snapshots snap-20261020012439-809c058b and
snap-20261020013511-809c058b show no new cycles: zero project cycles, the same existing
module cycle and five type cycles. The one new informational finding is the flat
23-member wire record; it contains protocol fields, not mixed behavior. The existing
composition-file size warning remains. The 205 scoped-loader diagnostics are unchanged;
actual Release builds provide compilation proof.

## Validation and repair

| Selection | Cases |
| --- | ---: |
| ProcessDecisionChildMaterialReaderTests |18|
| ProcessDecisionChildMaterialBindingTests |22|
| ProcessDecisionMaterialTests |26|
| ProcessDecisionMaterialPersistenceTests |49|
| ProcessDecisionPublicationFenceTests |33|
| ProcessDecisionRunScopePersistenceTests |9|
| ProcessDecisionRunScopeRecoveryTests |12|
| Total |169|

The initial candidate passed 151/169. All 18 new Integration failures resolved a scoped
service from the root test provider before reaching the assertions. The repair owns an
explicit service scope in those tests. Exact source comparison proves that only
ProcessDecisionChildMaterialReaderTests.cs changed; fixture and production inputs did not.
All 18 cases repeat and pass. Original failures and the 151 unaffected passes are retained.

The composed tests cover root/descendant and local material, both executor owners,
substitution rejection, fresh scope reopening, historical closure/reconciliation, and
exactly three bounded SQL reads with zero or twelve siblings. Each query contains exact
predicates and LIMIT 2. These checks establish bounded lookup, not workload throughput.

Application, Persistence, Modules.Processes, Integration and Unit build in standard
Release using local libraries and serialized outputs. An initial test compilation error
from two misplaced using directives was repaired before frozen execution. The complete
portability procedure passes after review of one new case-policy allowance: external
executor-kind comparison is intentionally ordinal and case insensitive. The baseline
changes from 15,639 to 15,640 findings; the final enforcement uses no write flag. Full scan,
tool tests, rendering register, documentation and diff checks pass after the test repair.

## Evidence and resources

Initial source: 9,544 files, SHA256
62F02694394950728C65A164EA514C14104C1838633AB4449D1A383AF5BCC7FC.
Initial runtime: 3,976 files, SHA256
10110BC73DD2E689CA3B2F1068F53A750E96D1683308BC29A37EE80034DC8063.
[Initial proof](../proof/2026-10-19-planning-authority/c4-canonical-child-material.json)
SHA256 A188C82407E5CCF08609A0F07FA19E97F4A9ABFC1460049F1616FC5ED6948654.

Final source: 9,544 files, SHA256
12D18A065584E9D39CF3FA33B22ACFA47340E3FF558BA7C8020608C0961898B7.
Final runtime: 3,976 files, SHA256
6298CAA4EAB618B0CD6E5943B477D34DABC2A203775963B24D4B6B863B86FB50.
[Passing repair proof](../proof/2026-10-19-planning-authority/c4-canonical-child-material-final.json)
SHA256 FAA659EB2672859512713E9BB9CAB9D84114C9D7474351FFCC79E3F33AF3D7D1.

Both roots record ownership before creation and reuse PostgreSQL 18 on loopback 53619,
standard outputs and existing test resource families. The 469.3336321- and 100.0683851-second
observers cover their complete test commands. Source/runtime identity verification passes
before freeze release; reconciliation finds zero surviving observed processes and zero
database-name delta. Seven initial and five final unattributed TEMP entries are preserved.
The final observer identity was confirmed during execution after correcting a startup
filename typo; retained timestamps independently prove full command coverage.

Historical cleanup denials and the failing historical broad Stable gate remain open.
This slice makes no global cleanup or R5/R6 closure claim. Continue with the
[workspace lifecycle and publication integration](../plan/P07-process-planning-integration.md).
