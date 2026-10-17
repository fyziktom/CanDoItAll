# R1c send affordance repair

Source-only repair, not built or executed. Parent's retained unoptimized baseline
candidate discovered all 25 selections exactly, then executed native 57 with 56
passed and one failed. `Native_recovery_distinguishes_known_commitment_from_uncertainty_and_access_denial`
passed the repaired setup-notification assertions and failed its existing disabled
Send assertion inside the activity-fault loop. No performance row executed before
that failure. This repair does not replace that retained evidence.

The native `BlocksNewMessage` guard already refused dispatch after known commitment,
lease-release uncertainty or activity-finalization failure. The rendered composer
received only `IsChatInteractionBusy`, which deliberately excludes those terminal
states. Its Send button therefore looked available although the native handler
would refuse the command.

Four small production edits carry the existing native decision as optional typed
`IsSendBlocked`: `AgentChatPanel.razor` → `ChatWorkspacePanel` → immutable
`ChatWorkspacePresentation` → the existing composer's `IsSendDisabled` input.
The native guard remains authoritative. The renderer retains its existing busy and
pending-approval conditions. Draft input, attachments and the ordinary busy state
are not changed by this flag. Other callers default to false and retain their
existing behavior. There is no new service, package, data read, effect or CSS.

The existing failed Fact is the meaningful regression: it requires disabled Send,
nonbusy presentation, preserved prompt/attachments, exact known commitment and
independent uncertainty flags. Its ordinary recovery arrangements remain in the
same class. No test source or row count changes are needed for this repair; the
current parent selection remains native 57 plus API documentation 23 = 80.
Historical native review source is not part of this transfer.

Parent owns standard builds, focused discovery/execution and full static enforcement.
The separate rendering patch updates only the three changed Razor inputs, preserving
the native host and feature renderer boundary. Do not replace main's newer whole
register with the managed copy. Source inspection and diff checks are not runtime
or visual proof.
