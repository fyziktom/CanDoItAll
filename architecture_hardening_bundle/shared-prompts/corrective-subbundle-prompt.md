# Corrective subbundle prompt

Use this when a gate fails or a proof step reveals a foundational defect.

## Actions

1. Copy the closest corrective playbook or `subbundles/_corrective-template`.
2. Name the corrective subbundle with the failing-gate prefix.
3. Capture the exact root cause and failed proof.
4. Limit the correction to the smallest scope that truly fixes the issue.
5. Block all downstream work.
6. Rerun the failed proof and gate.
7. Update the plan, traceability, and review logs.

## Non-negotiable rule

A corrective subbundle is not optional cleanup. It is a blocking phase.
