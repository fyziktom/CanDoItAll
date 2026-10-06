# Review of completed WB3

## Scope of the conclusion

The reviewed product is 3957fe73e2a0736c042daa504a2767050423c36b. Its child d7384b12
adds only the historical input package. Connected source inspection supports preserving
WB3's bounded graph-authoring extraction. It does not establish that all Workbench UI,
every native effect or release readiness is complete. No new blocker in the inspected WB3
paths was identified. The text-asset issue below belongs to an explicitly deferred old path.
Sources: S01-S11; [full register](SOURCES.md).

The actual leaf constructs CanvasWorkbench, toolbar/toolbox, generic composer wiring and
structural dialogs. The route captures actor, project lifetime and opening, passes native
expectations, and preserves known results before view refresh. In particular, the old
hierarchy continuation no longer dereferences the current successor dialog to finish A.
The inspected native composer tests cover gated commits, lost replies, close/replacement,
recreated lifetime, changed node kind and rich metadata. We did not execute these tests.

Do not reinterpret ProjectStructureNodeExpectations as universal optimistic concurrency:
it checks occurrence/kind/parent at its actual writer boundary. Rich field-level merge and
concurrency rules still belong to each operation. Do not introduce a sweeping backend
rewrite because the class name sounds general.

## Evidence appraisal, not a reviewer rerun

The implementation report (S02) records a real final image campaign: 19/19 protocol vectors
and 14/14 native consumer cases on image
`sha256:910e406948625628f8d55d7c13a1d427cc1583a70b2af6364678e6aea9c80285`.
That image used isolated main ee9a850201e410e59588d48983c82cef0bb52a80 and Components
af7aace210a84c1b9931467d3d41284959811f15. The report states the primary delivered implementation
trees are equivalent; this is not independently rerun or reconstructed in this review.
The campaign includes client-B execution, real accepted/incomplete Workflow outputs,
Scheduler restarts, source model naming and opaque routing, real Agent tools/approvals,
file canary/download and native History. It is stronger than a selector-only smoke test.

The frozen Stable result was 16,669 discovery items expanded into 16,724 executions:
16,722 passed, two failed, zero skipped, across 30 assemblies. All 3,289 Integration
cases passed, as a subset. One Workflow component assertion timed out; its original isolated
control passed before the test was changed to await dispatched events. Do not claim that
this proves a production Workflow defect or its root cause. The other failure was the
retained synthetic secret control. The command exited 1 and stays a failed original run.

Later reconnect/state-publication and minimap changes have separate 19-case/26-case native
and component proof and the final image campaign. They are not retroactively part of the
frozen Stable binaries. Pre-run source/compiled-input hashes and their unchanged observation
are reported. Native-owner changes in WB4 must receive their own current proof.

The measured CSS insert/remove behavior remains qualified: insertions appeared, removal
could remain sticky even after manual reload. Tool/sampling delays mean the reported values
are upper-bound observations, not steady-state compiler benchmarks. No SDK downgrade,
timeout increase or global watch redesign is requested solely for a green timing headline.

## Source-pair delivery

The report requires primary Components a120106b (after 49decea8), whereas connected remote
development is still 24d182c6 and the required ref returned 404 (S03/S25). This matters:
StructureWorkspace uses newly added composer events and the report's minimap/validation
behavior. Do not equate a push of main with delivery of the tested dependency.
Use the existing verified local sibling if present; do not rebuild its fixes from the report.
Record remote delivery separately. See [DEPENDENCY_DELIVERY.md](DEPENDENCY_DELIVERY.md).

## Preserved and open families

Keep WB1 Planning, WB2 Insights, WB3 Structure and their native protocols. WB4 begins content
and file presentation, not a fourth pass over the same canvas. Participant/meeting and
assignment, protected secrets, runtime/terminal/web preview and Workflow/Process admission
remain actual native integrations. Their existence as slots was intentional, not proof of
extraction. The final Workbench census must follow real children/callers, not file counts.
