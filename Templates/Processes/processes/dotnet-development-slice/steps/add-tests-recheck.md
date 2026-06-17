# Recheck slice proof after repair

Review the repair child run and rerun the smallest proof needed for the chosen slice behavior.

This step owns the repaired slice branch decision:

- Select `slice-accepted` only when repaired evidence satisfies the chosen slice behavior.
- Select `slice-repair-escalation` when proof still fails, accepted child handoff evidence is missing, or another repair would exceed the slice boundary.
- Return a completed process-step outcome with the selected branch outcome. Do not return `Blocked` for evaluated product proof that can be escalated to the parent.
- Return `Blocked` only when an environment, permission, unavailable tool, or process-contract issue prevents recheck execution.
