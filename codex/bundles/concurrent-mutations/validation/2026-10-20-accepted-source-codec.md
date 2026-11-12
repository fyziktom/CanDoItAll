# Accepted Process source codec foundation

The first checkpoint of the [storage design](../plan/P07-accepted-source-projection.md)
is implemented in Processes.Persistence. Typed header and assignment records exclude
prompts and unrelated plan data. The pure codec binds assignment bytes to their header,
checks original identities/policy, rejects unknown or noncanonical evidence and enforces
the declared 1 MiB UTF-8 metadata limit. It has no production call site yet.

All **41 executions pass**: 26 isolated correctness cases and five measurement shapes in
three fresh test processes (45 rounds). Cases cover source/policy preservation, wrong
version/admission/step/preparation, cross-header assignments, payload alteration, unknown
fields including custom identifier wrappers, missing evidence, duplicate assignments,
original-owner mismatch and metadata byte limits.

| Steps | Prompt characters | Selected bytes | Median ms/read | Median allocated bytes/read |
| --- | --- | --- | --- | --- |
| 1 | 256 | 2,556 | 0.182 | 31,920 |
| 64 | 256 | 2,557 | 0.212 | 31,928 |
| 256 | 256 | 2,558 | 0.234 | 31,936 |
| 1024 | 256 | 2,559 | 0.190 | 31,936 |
| 1 | 1,048,576 | 2,556 | 0.208 | 31,920 |

The extra three bytes reflect selected step-key digits. Unlike the [baseline](2026-10-20-accepted-source-scaling.md),
unrelated steps and prompts no longer increase read payload/allocation. Strict decode and
canonical validation cost about 0.106 ms more for the one-step sample (0.182 vs 0.076 ms).
The 1024-step baseline was 71.075 ms and 31,073,808 allocated bytes. These isolated samples
do not establish SQL latency or throughput. One-time 1024-step capture takes a median
38.073 ms and allocates 13,694,520 bytes, excluding full-launch decode and database writes.
Actual acceptance-lock/write cost and small-launch latency still require storage proof.

Release builds pass: Persistence 5.158 seconds and Unit 67.659 seconds. Exact discovery
is 26 correctness cases and five cases per measurement process. Full portability passes
with 15,647 unchanged findings; validation-tool, rendering, documentation, diff and
retained-proof secret checks pass. Architecture snapshots `snap-20261020114836-6de05ed1`
and `snap-20261020125138-6de05ed1` retain project references, zero cycles, one registration,
31 entities and 190 diagnostics. Types change 833 to 840, members 7907 to 7958 and findings
291 to 294. Three new informational member-count findings cover one cohesive 110-line
codec and two explicit evidence records; they introduce no runtime orchestration.

The root is Released. Its 35.146-second observer covers all four commands; frozen source
and runtime are unchanged. Zero observed processes/database-name delta remain. Nine
unowned TEMP entries are preserved; no new host or database was created. Historical
probe-output cleanup rejection remains unchanged.

Source: 9651 files, `938902BD2B141F5F643E710AB61B8481EC212F4EB7DAAE701AA1989C15AA53BB`.
Runtime: 1484 files, `67F91D0419CAF96EF61A7CF29F6CC036DBAFB1FCF499182A2F27176DFD4A335A`.
Proof: `proof/2026-10-19-planning-authority/c4-accepted-source-codec.json`,
SHA-256 `E7F956A4326270E98E25323A1C27373877A278ACA9E8E4725E951D7D43388746`.

Next: atomic storage/migration and query wiring, authenticated-operator and Agent source
variants, actual SQL widths, legacy compatibility, acceptance-write cost and retained-data
backfill. Producer activation and sustained capacity remain gated. SPA remains on its
own branch; the existing signed parent checkpoint is still pending at PGP.
