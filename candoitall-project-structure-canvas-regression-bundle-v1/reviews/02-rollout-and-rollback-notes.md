# Rollout And Rollback Notes

## Rollout

- Launch a local app instance dedicated to the MCP session.
- Use clearly named temporary data created during the sweep.
- If a repair is required, rerun the exact failing interaction before moving downstream.

## Rollback

- If a repair regresses adjacent canvas behavior, revert the scoped repair and reopen the failing subbundle with narrower evidence.
- Do not keep a repair that only makes the original failing flow pass while breaking another covered interaction.
