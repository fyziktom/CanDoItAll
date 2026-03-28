# Execution Loop

## Phase Entry

1. Read the selected subbundle completely.
2. Read the root plan and traceability files so the current phase stays aligned with the larger bundle.
3. Inspect the actual code and tests before changing anything.
4. Run the subbundle entry gate and confirm the prerequisites still hold.

## Implementation Pass

1. Make the smallest coherent edit set that satisfies the current subbundle.
2. Keep behavior changes explicit and strongly typed.
3. Add or update tests when the subbundle requires behavior proof.

## Validation Pass

1. Run the exact test or build commands required by the bundle.
2. For UI work, prove the result in the browser and capture artifacts when needed.
3. Record the browser analytics and subbundle gate result while the proof is fresh.
4. Stop when proof fails. Diagnose first. Do not keep stacking edits.

## Bundle Update Pass

1. Record what changed.
2. Record the validation commands and outcomes.
3. Record artifact paths such as screenshots.
4. Record whether the progression gate passed and whether downstream work may continue.
5. Record follow-up work as explicit new items when closure is incomplete.
