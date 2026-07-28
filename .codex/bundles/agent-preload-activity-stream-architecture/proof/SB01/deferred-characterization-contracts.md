# SB01 deferred characterization contracts

These are explicit failing-first contracts for defects that belong to later implementation phases. They are not claimed as passing in SB01.

| Concern | Current defect | Required failing-first contract | Owning phase |
| --- | --- | --- | --- |
| Shared reference-data cancellation | The first caller token owns shared factory work; later callers cannot independently cancel only their wait | `Cancelling_a_later_waiter_cancels_only_that_waiter`; `Cancelling_the_factory_starting_waiter_does_not_poison_other_waiters` | SB03 |
| Preparation-pool cancellation | Cancelling the factory-starting caller can poison a same-key shared refresh | `Cancelling_the_first_waiter_does_not_cancel_or_reload_the_shared_preparation` | SB03 |
| Current-profile relay lifecycle | The scoped relay retains every resolved workspace in an unlocked `HashSet`, subscribes, and never unsubscribes | `Switching_profile_unsubscribes_the_previous_workspace`; `Concurrent_resolution_does_not_duplicate_or_corrupt_subscriptions` | SB02 |
| Immediate activity | No operational event exists before catalog/provider/session work | Block the first catalog load; `StartSendMessage` must already return a stream ID and replay `Accepted`/`CapturingContext` while no execution run exists | SB02 |
| Reader cancellation | Current UI event subscription is tied to synchronous multicast rather than a reader lifetime | Cancelling/disposal of one reader must not cancel the command, publisher, or another reader | SB02 |
| Floating UI pre-run feedback | New-run `ExecutionUpdated` normally cannot update selected-run text before the new run is selected, leaving generic context-update text | Component consumes stream from sequence zero and renders a typed pre-run phase before run binding | SB06 |
| Process Manager bypass | Send/approval call the workspace directly and skip context capture/completion publication | Manager send and approval must use the same immediate orchestrator handle and activity lifecycle as floating chat | SB04/SB06 |
| Context completion | Completion publication exists only on the orchestrator path; direct sends do not notify module context owners | Successful manager send publishes exactly one source-matched completion; failure/cancellation does not falsely request successful refresh | SB04 |
| Module snapshot race | Current context fragments transport selections but not an immutable, revisioned surface attachment | Concurrent selection/edit during capture yields exactly the old or new complete publication, never mixed data | SB04 |

The full adversarial matrix is maintained in `bundle://architecture/04-csharp-testability-plan.md`.
