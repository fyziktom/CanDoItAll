# Pooled preparation and original-policy authority correction

Status: qualified closure of 183 distinct affected behaviors. This closes these two
bounded corrections, not Process executor activation or sustained capacity.

## Behavior and boundaries

Persistence now returns every rented output buffer with clearing. Exact JSON, retained
UTF-8 limits and historical reads are preserved. Ordinary execution requires both the
original and current assignment to have no decision policy. Removing a current policy
cannot downgrade an originally governed assignment; the original metadata was already
loaded, so this predicate adds no SQL query.

The first candidate passes 180/183. Its three new lifecycle tests incorrectly assume
that primitive arrays and a single custom converter force incremental output. The
test-only correction uses ordinary object records and puts the failing converter after
earlier writes. The adjacent boundary test additionally proves that byte refusal stops
enumeration before its end. All nine selected correction cases pass; all 5,375 production
source files and 392 production DLL copies match the first candidate.

## Validation and immutable evidence

The initial selection contains 68 Unit and 115 Integration cases in 13 exact lanes.
Discovery matched all 183. Persistence Release builds in 5.087 seconds; Unit and Integration
refresh in 75.325 and 52.839 seconds without rebuilding unchanged dependencies.
The correction rebuilds Unit in 74.588 seconds and discovers exactly nine cases.
Full portability enforcement passes with 15,657 reviewed executable-source findings
unchanged in both candidates. Rendering register, documentation and whitespace pass.
No broad suite was rerun for these local predicates/buffer changes.

| Candidate | Result | Proof bytes | SHA-256 |
| --- | --- | ---: | --- |
| [Initial pooled candidate](../proof/2026-10-19-planning-authority/c4-process-capacity-repair.json) | 180 passed, 3 failed | 328470 | EDAA7EAB4FEC485E0B2FA7B019DA6553B91C9347F45D31BB03AEE5FD191CB350 |
| [Corrected lifecycle tests](../proof/2026-10-19-planning-authority/c4-process-capacity-repair-verified.json) | 9 passed | 105756 | F2B60B6E3A1FE01B910B97B9D8CBEFE21933D5FC6FC5C03628F57BBCE670A995 |

Both candidates retain 9,821 source inputs and 2,595 runtime files. Source/runtime
manifest hashes are respectively:

- Initial: `25C4C7709FB266E08B1F074BBABB672126214528836A9C3EB3E664D99D544CC7` /
  `F54B5D7266913A183B705E38C685DA38B7C6A5A26D3568DD85155F86F4264209`.
- Correction: `7FBE7C29AF65E2800162AD9DC12D35632793DF9394033A4BF364C9AEC30BF0AC` /
  `887E243718C8E7A8646FBB80F12A2BE84749629A2182636514CD1A050798EC59`.

Initial observation covers all 13 commands for 869.765 seconds; correction observation
covers both commands for 23.786 seconds. Both reconcile zero remaining observed processes,
database-name deltas and temporary-path deltas. Both retained proofs pass secret scanning.
The shared isolated PostgreSQL fixture remains in use; earlier cleanup denials remain.

## Finite allocation and timing evidence

These are six alternating rounds of three serializations with matching exact output.
The cold row uses a new private output-buffer pool; it does not reset all serializer or
runtime caches. All 72 raw measurement rows, including selected SQL input measurements,
are retained in the initial proof. The nine-case correction does not remeasure performance.

| Steps / neighboring prompt | UTF-8 bytes | Baseline / pooled median ms | Baseline / pooled median allocated bytes | Cold buffer allocated bytes |
| --- | ---: | ---: | ---: | ---: |
| 1 / 256 characters | 9724 | 0.247 / 0.255 | 25696 / 25752 | 42160 |
| 128 / 256 characters | 560580 | 12.812 / 13.130 | 1518568 / 1518624 | 3599560 |
| 128 / 1 MiB characters | 1608900 | 3.459 / 4.262 | 3615208 / 3615264 | 6220464 |
| 1024 / 256 characters | 4449292 | 65.480 / 74.070 | 12055995 / 12056051 | 28816800 |

The preceding MemoryStream candidate allocated 27,159,029 bytes at 1,024 steps; the
pooled warm path allocates 12,056,051. This addresses repeated output allocation while
retaining a visible clearing/copy cost and cold rental cost. Variable elapsed samples
are not p95 or sustained throughput. The retained byte bound is not a cap on a giant
scalar/custom-converter scratch allocation, caller input graph, final UTF-16 string or
shared pool working set. Concurrent working-set capacity remains an activation gate.

## Architecture and progression

Snapshots `snap-20261021062953-77361fd7` and `snap-20261021070241-77361fd7`
retain seven project edges and six existing cycles. The only added finding is an
informational member count for required members on the private Stream implementation.
No project reference, schema, runtime partial, new service or additional query was added.
The unchanged finite root-completion and pure startup-verifier cases remain historical
evidence, not an asserted 244-case rerun.

Proceed with explicit manager-review admission and the real Process adapter. Keep the
executor guard until waiting completion, reject/cancel/refresh, original-source routing
and recovery are proved together. Workflow, settings/SPA, remaining writer cutovers,
sustained capacity and final broad/resource closure remain required.
