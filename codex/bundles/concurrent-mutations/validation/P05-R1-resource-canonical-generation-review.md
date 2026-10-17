# Resource canonical migration: independent source and script review

Reviewed the parent-generated `20261017082009_AddResourceMetadataRevisions` in MAIN,
read-only. No source blocker found in the bounded model, migration body or emitted
SQL review. This is not database execution or Resource/stage2 activation proof.
The adjacent review inventory binds the exact source, raw scaffold, parent receipts
and SQL scripts; original plan 6178, inventory 117D and cleanup 4796 remain unchanged.

## Model and source

The preserved before-snapshot is 33889602. A comparison preserving quoted string
contents and ignoring formatting found 169 entities and 219 entity/configuration
blocks before and after; only `CanDoItAll.Modules.Resources.ProjectResource#1`
changed. No block was added or removed. Every generated designer block and the
model preamble match the snapshot. Final formatting and XML-comment removal preserve
the entire generated model token sequence and exact migration identity.

The delta is exactly Resource columns 19→20: required UUID Revision, concurrency
token, no model/database default; nonempty check; nonunique index in the order
ProjectId, ProjectLifetimeId, ConnectorPluginKey. The owner configuration explicitly
uses ValueGenerated.Never. Canonical 169 / Resource 1 / Workbench 16 remains the reviewed
model. The scaffold confirms the expected 63-byte identifier
`IX_Resources_ProjectResources_ProjectId_ProjectLifetimeId_Conn~`.

Final source identities:

- Migration body: D60CDAB823C3A544DA5C23D7E4BEDFBCA1E69A278FC108C7230A4A8C295BCD51.
- Designer: FCC8C6A47BF8827A9143BA68FFB308F14C34452DC3389B117B28B641E9F20563.
- Snapshot: CB50D5C6AEF13F6D05169FEFD2AAF0C24526AB4F28D3F04D1FCEC3D3453F3C6E.

Up equals the reviewed D797 wrapper plus the verified index name and exactly five
approved statements: add nullable Revision, populate only Revision with
gen_random_uuid(), set NOT NULL, add the nonempty check, create the nonunique index.
There is one UPDATE and no persistent default. Both actual/expected catalog passes,
predecessor/OID checks, ordinary-relation restrictions and transaction-local lock
timeout remain intact. The temp descriptor has the prior nineteen-column schema and
PK; its own postflight additions mirror the new canonical shape. No Project, Search,
receipt or lifecycle row is read or rewritten by this migration. No placeholder or
extra scaffold operation remains. Both Sql calls use the normal EF transaction.
Down is exactly the approved permanent P0001 refusal, with no destructive DDL.

## Emitted SQL and parent execution evidence

Both Up scripts contain the reviewed body exactly after indentation normalization,
one START TRANSACTION/COMMIT pair, and one history insertion after the body. The
idempotent script has two exact target-history guards, respectively around Up and
its history insertion. It does not adopt a partial Resource schema. Down has the
same permanent refusal before the generated history deletion; no column, check or
index drop is emitted. The three script hashes are:

- Idempotent Up: 11F7374810BBF5AAD9945B283CEED2D7A9CF7D023E98C27C75A6608FA01657C1.
- Up: 9B3A8F95863AB2E0C1F91FC2CF7AC5C4E45B6043DA78271283185BD9E10747A8.
- Down: 43CB9E481317BCF5847CAAC3E585AE99BB809349EC3021B10323562BF2B55D8C.

Parent receipts record generation exit 0, guarded migration Release build exit 0
(178 warnings/0 errors; MSBuild reports 24.63s, outer receipt 24.916s), pending-model
exit 0 with no model changes, and all three script commands exit 0. Existing package
warnings and tool 10.0.3 / runtime 10.0.4 warning are retained; this work does not claim
to resolve them. No migration SQL was executed by those commands or this review.

The initial generation command's separately typed predecessor-body hash check was
inert/malformed. The exact snapshot guard was enforced. Parent recorded a subsequent
positive full-body recheck before scaffold editing, and this independent review
also observed the unchanged C3 body 207226CED8E8DF29CEB9782B1CAD070A5CC399C9FA8EE19A9380D3EDE2C3A861.
This limitation is retained rather than retroactively describing the initial guard
as successful.

## Remaining gates

Ordinary V2 inventory 3FC8393F and selection 7BD20401 bind 121 sources, including the
explicit installed-EF correction 26D53890/overlay CBF1. All 13 tooling inputs and four
predecessors were rechecked; 346 ordinary rows / 254 methods and four separate browser
Facts are unchanged. Stage1 fourteen must pass before the later ordinary lanes.
The complete candidate freeze must include the three generated canonical inputs.

Actual PostgreSQL 18 migration 3 proof, the remaining standard builds, exact ordinary
discovery/execution, browser 4, and final portability/rendering enforcement remain
parent-owned gates. Script emission does not establish PostgreSQL runtime behavior,
retained data preservation, quarantine or physical cleanup. Five seconds bounds
lock acquisition only; backfill/index duration and production-size capacity remain
unqualified. Stage3 and P03 implementation/activation remain held until their
required stage2 proof and separate release.
