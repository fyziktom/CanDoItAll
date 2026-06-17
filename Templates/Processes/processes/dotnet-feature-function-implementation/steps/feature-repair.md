# Repair focused validation findings

Repair only the concrete validation failure described by the `feature-repair-required` evidence.

- Read the original feature scope, validation contract, code-change summary, and targeted validation evidence.
- Mutate only the product files needed to address the failing proof.
- Do not widen the accepted feature behavior or add deferred backlog work.
- Rerun the smallest useful build, test, launch, or browser check that proves the repair direction before writing the repair change-set artifact.
- Use runtime launch and browser proof only for the same failing behavior described by targeted validation, and stop any runtime you start when proof is captured.
- Record changed files, failing proof addressed, commands rerun, exit codes, remaining risks, and evidence refs.

Return `Completed` only after a repair change set and repair evidence are written. Return `Blocked` only for missing access, unavailable tools, contradictory scope, or a repair that would exceed this subprocess.
