# Run .NET implementation slice subprocess

Launch and observe the .NET implementation slice subprocess for the approved scope and architecture. The child implementation slice owns solution setup, feature/function implementation, bounded repair subprocesses, tests, targeted proof, and accepted handoff evidence. When the first repair strategy repeats or remains incomplete, the child also owns a manager-assisted `dotnet-quality-repair` lane that separates diagnosis, product mutation, independent validation, and one bounded bughunt/re-repair. This parent step records child-run evidence and does not mutate product files directly.

Pass the architecture decision record and acceptance-driven validation plan to the child as durable inputs. Preserve literal `ProductAcceptanceCriteriaContract` ids when present: a child may refine the implementation approach, but it must not narrow a required behavior or treat planned proof as executed acceptance evidence.

When the approved scope is a full app or broad deliverable, launch the child with a first reviewable MVP implementation slice derived from the feature-intake and architecture artifacts. The MVP slice must not be scaffold-only, app-shell-only, setup-only, naming-only, or build-only proof; it must include the smallest observable product-specific path a user can exercise. Keep later runtime command writeback, screenshot writeback, security, release, and follow-up feature slices in their own downstream steps instead of forcing all work into this parent step.

When project structure lists visual target assets, carry their ImageAsset node ids and media paths into the child implementation request as source design inputs. The child implementation must use those assets to shape the visible UI, not only the text summary. Do not accept a generic scaffold or visually unrelated product surface when a target image is listed.

This parent step must not launch the app, navigate a browser, wait on browser state, capture screenshots, or perform viewport/runtime proof directly. Those actions belong to later root runtime-command and screenshot writeback steps that include `LaunchRuntime` and `CaptureRuntimeProof`. If accepted child evidence is missing, record a blocker or repair escalation instead of attempting browser proof from this parent step.

Accepted child evidence can come from `slice-handoff`, `slice-handoff-after-repair`, `slice-handoff-after-initial-manager-repair`, or `slice-handoff-after-manager-repair`. An intermediate `slice-repair-escalation` packet is manager diagnosis input, not accepted implementation proof.

## Contract
- Inputs: Approved .NET architecture path, acceptance-driven validation plan, app classification, scope packet, unresolved technical questions, and implementation-slice start criteria.
- Outputs: Observed child implementation slice with reviewable change set, test evidence, blockers, rollout inputs, accepted parent-ready handoff, or explicit repair escalation evidence.
- Evidence: Child run status, change-set projection, validation outputs, accepted handoff evidence, repair escalation evidence, output-placement notes, migration steps when applicable, touched-surface inventory, and blockers.
- Operation target scope: `ExternalActionControlled`
