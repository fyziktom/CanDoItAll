# Agents implementation feedback, 2026-09-05

Source: owner-supplied review of Agents implementation commit 96ee03a97c510d5363636fb06b903b9bc12f47dc, followed by the explicit request to analyze and repair it.

Shared changes make semantic selection authority and effect acknowledgments explicit; distinguish rejection, commit, commit with secondary warning and unknown persistence; require fail-closed core loading and owner cancellation fences; prohibit policy-to-Razor dependencies; require public completeness guards for manual mutable snapshots; distinguish temporary unsafe characterization from acceptance; and place tests in their owning layers with unique dialog IDs.

This remains an architecture reference. It does not implement routes, a lightweight assembly, a sandbox, global dialog policy, or dotnet-watch acceleration. Agents SB08 owns runtime evidence.

Browser validation also showed that canceling a pending dialog wait left the actual global-host presentation alive across startup disposal/remount. The corrected host passes its token into DialogService, with tests for owned disposal, unrelated presentation preservation and same-target remount.
