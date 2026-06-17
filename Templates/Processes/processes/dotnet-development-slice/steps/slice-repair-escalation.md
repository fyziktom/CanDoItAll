# Escalate unresolved slice repair

Produce a parent no-go packet when repaired slice validation still fails or remains unproven.

Include:

- Chosen slice behavior and acceptance criteria.
- Initial validation failure.
- Repair child run attempted.
- Recheck proof that remains failing or incomplete.
- Why another repair is not safe inside this slice.
- Recommended next parent action: new slice, architecture review, environment repair, or explicit human decision.

Do not mark the slice accepted in this branch.
