# C3 receipt codec runtime correction

The first frozen C3 candidate discovered exactly 60 Unit and 127 Integration cases.
Unit execution aborted after 36 passing results because `BoundedStream.Write` recursively
dispatched between its array and span overloads. Integration and the optional process
and workload lanes did not execute. This is a failed candidate, not 36/60 correctness
coverage. The original logs, TRX, selection and complete source/runtime manifests remain
under the owned `c3-focused-validation` evidence root.

On a `MemoryStream` subclass, the framework span overload can delegate to the virtual
array overload. That override now checks the same finite receipt limit and calls the
base array implementation directly. The shared capacity check preserves the original
bounded growth; no receipt format, limit, test selection or publication semantics change.
Independent source review passed the corrected codec at
`95DDC40EBD98C21CC2DFAA9D9AC35A3892EA11DECF91614D6A7D258FF22D3BAB`.
The original codec `C3FED851E52834E89CF9DB60842C99078CBF2D3A05B9297DAB38177A269A5837`
is retained beside the failed evidence. Fresh standard Release builds, static closure,
the complete 60/127 selection and separately gated optional lanes remain required.

Before releasing the failed candidate's source/build freeze, the parent independently
verified all 8,776 source paths and 4,340 runtime files against their original manifests,
with unchanged HEAD and exact path sets. The runner and its directly observed children
were absent; PostgreSQL retained only its previously recorded unrelated cleanup blocker
and the inventory connection. One newly created, empty bootstrap-registration fixture
directory remained after the test-host crash. Automatic approval review rejected its
exact removal as blocked by policy. That path remains in the failed-candidate resource
record; no alternate deletion was attempted and resource cleanup is not complete.

The corrected candidate uses separately named `c3-codec-repair-validation` evidence and
successor tooling. The original failed evidence must not be overwritten or relabelled.
This record does not qualify production Agent provenance, persisted planning settings,
ordinary Process/Workflow mutation continuation or performance capacity.

## Corrected codec runtime result and test-only successor

The corrected codec passed all 60 Unit cases. Integration executed all 127 cases:
125 passed and two failed, with no skips. The parent verified unchanged complete
source/runtime identities, HEAD and path sets before releasing this second freeze at
2026-10-17T06:05:00Z. All 27 temporary fixture prefixes and the PostgreSQL database/client
inventory matched their pre-run records; owned commands were absent. Previously blocked
resources remain recorded. Optional process/workload lanes did not execute.

Independent source review confirmed two test-only corrections. The transfer inspection
must expect exactly seven Projects entities: the two finite-operation metadata entities
belong to the neutral context since C2. Its full exact owner dictionary remains enforced,
including all sixteen Workbench entities. The duplicate-publication SQL probe must stop
after the publication outcome, source-release and disclosure assertions, before the
fixture deliberately reads the complete accepted receipt to verify persisted data.
Its no-payload assertion remains active during publication in all three failure variants.

Both exact predecessor test images remain beside this attempt's evidence. The test-only
successor uses `c3-fixture-repair-validation`, unchanged 60/127/2/1 case counts, a refreshed
standard Release Integration build and final static enforcement. All 190 application
cases now pass; the [focused checkpoint](P04-C3-focused-runtime-checkpoint.md) records
the unchanged candidate, measurements and incomplete independent process observation.
Neither failed predecessor is relabelled as passing.
