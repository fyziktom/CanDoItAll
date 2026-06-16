# SB18 Story Coverage

| Story | Coverage | Proof |
| --- | --- | --- |
| US-011 Basic step authoring. | Solved. Step title, summary, kind, execution intent, SLA/target lead time, and governance flags are projected and editable through a typed save command. | Unit projection/command tests, component render/save tests, Playwright step save receipt. |
| US-012 Execution strategy and operation contract authoring. | Solved. Operation target scope and allowed operation kinds are typed and saved through command DTOs. | Unit operation assertions, component typed command boundary, Playwright target scope and write-operation save. |
| US-013 Input/output contract summaries. | Solved. Input/output contract summaries flow from template summaries into the step projection and rendered panel. | Unit projection test, component render test. |
| US-014 Branch routing. | Solved. Branch outcomes use typed route target kinds and loop-budget metadata; backward routes without loop budgets are rejected. | Unit route acceptance/rejection tests, Playwright previous-step route plus loop budget save. |
| US-015 Role assignments. | Solved. Step-role bindings are projected into the step editor alongside the dedicated SB16 role editor. | Unit projection test, component render test. |
| US-016 Artifact expectations. | Solved. Artifact expectations include trust, sensitivity, retention, provenance, workflow output, child artifact, future usage, and validation summaries. | Unit projection/command tests, component render/command tests, Playwright add-artifact receipt. |
| US-017 Subprocess mapping. | Solved. Subprocess-capable steps expose typed subprocess definition options and mapping commands. | Unit subprocess command test, component typed-boundary test, Playwright subprocess selection/map receipt. |

## Notes

SB18 does not launch or execute subprocesses. SB21 and SB23 consume these authoring contracts for launch planning and runtime canvas behavior.
