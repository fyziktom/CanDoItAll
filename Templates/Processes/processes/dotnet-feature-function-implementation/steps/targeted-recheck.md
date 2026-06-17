# Re-run focused validation after repair

Re-run the failing proof and the smallest regression checks needed to verify the repair.

This step owns the repaired feature branch decision:

- Select `feature-accepted` only when the repaired evidence satisfies the accepted behavior.
- Select `feature-repair-escalation` when proof still fails, required evidence is missing, the same blocker remains, or another repair would exceed this subprocess scope.
- Return a completed process-step outcome with the selected branch outcome. Do not return `Blocked` for product proof that has been evaluated and can be escalated.
- Return `Blocked` only when an environment, permission, unavailable tool, or process-contract issue prevents recheck execution.

For UI proof, use a trusted runtime launch receipt or explicit running-app check, navigate only to the confirmed URL, and capture browser snapshot, console messages, and screenshot.
