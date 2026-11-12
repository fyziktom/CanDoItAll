# D1 competing acquisition cleanup amendment

Source-only successor to the exact frozen `AgentSavedEffectDispatchTests.cs` image `61FEC6FA4F46504729C0FADCE19F94BE504A62434BB023F520AEA03103D00EC1`. The original test and D1 inventory `695F5E78186BC56F93867174209B56487A4A34A52BDB86A0A10E4FD81BCB2D07` remain under `R1d-D1-acquisition-cleanup-predecessors/`.

The competing-command arrangement now owns both acquisition tasks through terminal completion. Its `finally` cancels the shared token, concurrently observes both tasks, and disposes every returned lease. Concurrent observation matters: waiting for one acquisition before releasing the other's returned lease can deadlock fixture cleanup. A reference-identity set records each disposal attempt before awaiting it, so the normal winner release cannot be repeated by the drain even if disposal fails. Expected admission refusal and cancellation are observed; unexpected acquisition, cancellation-callback and disposal errors are retained together with the primary assertion/failure.

The original winning/losing admission and persisted-approval assertions remain. Admission waits are bounded; cleanup awaits terminal ownership without a waiter-only timeout or detached task. The outer owned test-process deadline handles a hung cleanup. This source change makes no claim that a timed-out or failed release completed successfully.

The class remains six Facts and the full D1 selection remains 247 ordinary rows. No product, fixture-prefix, ABI, schema, test filter or test-count change accompanies this amendment. Root source review passed at the successor hash recorded in the overlay. Builds, exact discovery/execution and physical cleanup proof remain parent-owned and unexecuted here.
