# Rollout And Rollback Notes

## Rollout

- Apply the repair through the smallest affected test and browser slices first, then trust the broader app state only after those slices pass.

## Rollback

- If canonical assignment replacement or lifecycle reconciliation breaks the structure-page flows, revert the affected bridge and Workbench edits together. Do not keep a partial state where the editor read path has changed but lifecycle repair has not landed.
