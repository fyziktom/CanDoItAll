# Accepted Process source scaling

The registered source path needs one accepted assignment, but currently reads and decodes
the entire prepared launch, validates every original decision policy and hashes the full
JSON. Four indexed queries therefore do not establish bounded byte or CPU cost. Do not
activate the producer based on the small-fixture timings alone.

First measure this specific synchronous read using the existing pure prepared-launch
fixture and Unit project. Use 1, 64, 256 and 1024 steps with 256-character prompts, plus
one step with a 1 MiB prompt. Every assignment carries a valid decision policy; update
plan, runtime-state and review identities together and recompute the plan hash. Select
the last exact assignment after decoding, as the actual query does. Check original
step, executor, prompt and full policy outside each measured interval.

Use five warmups and three measured rounds per shape, with 32 repetitions for the small
shapes and eight for large shapes. Record UTF-8 payload bytes, elapsed time and current
thread allocations per read. Execute the five-case LongRunning filter in three fresh
test processes with frozen source/runtime. Keep raw rounds and environment data. Do not
turn machine-dependent timing into a unit-test SLA or call this a database/load benchmark.
Reuse standard Release outputs; no new project, standalone probe or container is needed.

Use the evidence to decide the smallest adequate repair. Bounded incremental UTF-8
hashing may remove avoidable whole-payload copies, but cannot remove full graph decoding.
Any hash change requires exact existing digest compatibility, including domain ordering,
non-ASCII text and surrogate boundaries. Do not assume source-generated serialization
solves the algorithmic cost. Do not add a global authorization cache.

If whole-plan cost is material, design an immutable accepted source/assignment projection
inside Processes.Persistence. Specify its authoritative data, atomic capture, versioned
storage, old-row migration and integrity behavior before implementation. New-version
records must fail explicitly if required projection evidence is missing or inconsistent;
an implicit fallback would hide corruption. Current claims, generation and source/executor
permissions must still be checked live. Keep source fingerprints and old receipt identity
compatible, or version their protocol explicitly. Review migration/restore and query plans
before enabling this path. Existing contract and implementation gates stay in place.

Build-backed discovery, full portability, source/runtime identity, resource reconciliation
and retained failed attempts apply. Test-only measurement changes do not justify a broad
stable rerun; the integrated foundation/schema checkpoint remains separately open.
