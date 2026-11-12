# Planning decision journal and writer perimeter

The [schema-six source card](../plan/P04-planning-decision-journal.md) is implemented
and passes its scoped architecture gate. Models owns the strict bounded immutable
decision/work graph and codec. Core owns transitions and comparison at every existing
journal/file writer. No new project, reference, partial class, lock or store was added.
The legacy SDK and schema-five saved-effect entry points still refuse schema-six work;
this checkpoint does not activate the live policy runtime.

The final candidate passed all 94 discovered cases: 66 Unit and 28 Integration, including
both actual file-save paths, independent file reads, legacy schema protection, retained
ordinary/saved-effect behavior, strict encoding, immutable provenance/origins, two-step
RunSnapshot/StepSnapshot semantics, atomic work rollover, deliberate approval wait,
replan exhaustion, rollback, overflow and cancellation plus late settlement.

The first candidate passed 93/94. Its aggregate-size fixture exceeded the older transient
context limit before reaching the intended journal bound. The repair uses two bounded
provider responses plus original input and decision material: the equivalent schema-five
record fits, and adding the planning decision triggers the exact aggregate-size refusal.
Production is unchanged across the repair (5,210 source files compared); all 94 tests
were rerun, not inherited from the failed predecessor.

Both Release builds, final portability enforcement (15,610 reviewed findings, no delta),
rendering, documentation and diff checks pass. Architecture snapshot
`snap-20261019041830-daf4d496` covers five projects, 523 documents and 1,933 types. Its
four type cycles and project references are unchanged. Diagnostics comprise 189 existing
package advisories and six diagram-size notices; no other compilation error was found.

Both candidates verified unchanged frozen source/runtime and zero remaining observed
processes/database-name delta. The final record preserves six unowned temporary paths.
Proof: [initial candidate](../proof/2026-10-19-planning-authority/c4-decision-journal-validation.json)
and [passing repair](../proof/2026-10-19-planning-authority/c4-decision-journal-fixture-repair.json).

Next is the actual live owner and provider boundary, followed by the Workbench v2
publication adapter and user actions. These tests establish contract/file behavior;
they do not establish actual model input, closed-wait restart authorization, Process or
Workflow policy execution, UI journeys, or sustained/maximum-size publication capacity.
