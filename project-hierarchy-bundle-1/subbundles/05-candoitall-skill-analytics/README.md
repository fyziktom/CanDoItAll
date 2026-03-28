# candoitall-skill-analytics

## Status

- `Completed`

## Objective

- Turn the analytics and workflow defects observed during this bundle run into repo-local skill-pack and install/sync improvements so the next machine and the next run inherit the repaired process instead of the same failure mode.

## Covered Inputs

- `R016`
- Raw notes `N013`, `N014`
- Browser analytics and subbundle gate results produced by subbundles 02 through 04

## Prerequisites

- Subbundles 01 through 04 completed.
- `reviews/01-execution-report.md` contains populated browser analytics rows and gate results.
- The raw-note closure table is no longer pending for feature behavior.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\skills\bundles\candoitall-bundle-workflow\SKILL.md`
- `C:\repositories\CanDoItAll\codex\skills\bundles\candoitall-bundle-preparation\SKILL.md`
- `C:\repositories\CanDoItAll\codex\skills\bundles\candoitall-bundle-preparation\scripts\validate_bundle.py`
- `C:\repositories\CanDoItAll\codex\skills\bundles\candoitall-bundle-execution\SKILL.md`
- `C:\repositories\CanDoItAll\codex\skills\bundles\candoitall-bundle-validator\SKILL.md`
- `C:\repositories\CanDoItAll\codex\skills\bundles\candoitall-subbundle-validator\SKILL.md`
- `C:\repositories\CanDoItAll\codex\scripts\install-candoitall-skills.ps1`
- `C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1`
- `C:\repositories\CanDoItAll\project-hierarchy-bundle-1\reviews\01-execution-report.md`

## Deliverables

- An analytics review summarizing what the workflow still got wrong or right in this run.
- Repo-local skill changes that address the observed gaps.
- Repo-local copies of any required missing validator skills.
- Updated install/sync behavior that propagates the repaired skill pack.
- Final prepared/completed validator passes recorded against the bundle.

## Dependency Impact

- This phase closes the process requirement. If it is weak, the feature may ship but the improved workflow still fails to propagate or enforce itself on the next run.

## Validation Depth

- `Process-critical closure`

## Implementation Steps

1. Review the browser analytics and gate rows written during subbundles 02 through 04.
2. Compare the repo-local skill pack with the installed global skill set used during this run and identify missing or stale repo-managed skills.
3. Apply the needed repo skill updates, including validator-skill additions if the repo still lacks them.
4. Update the install/sync scripts so those repo-managed skills are copied to another machine correctly.
5. Run a skill-install sync proof, then rerun the bundle validators and sync the bundle docs to reality.

## Scope Exceptions

- Do not fork or modify unrelated public skills from external repos.
- Keep this phase focused on skills and distribution mechanics that were materially involved in this run.

## Do Not Do

- Do not leave repaired workflow rules only in the global home folder.
- Do not claim the installer works without at least one concrete sync proof.
- Do not rewrite unrelated Codex setup logic that is not needed for the repaired skill propagation.

## Acceptance Checklist

- The analytics review names the concrete workflow lessons from this run.
- The repo contains the workflow skills required by the repaired process, including validator skills if they were previously missing.
- The repo-local validator path supports the staged readiness/closure contract required by the repaired workflow, or the bundle records an explicit justified alternative.
- `codex/scripts/install-candoitall-skills.ps1` copies the repaired repo-managed custom skills.
- `tools/Reinstall-CanDoItAllMcps.ps1` remains compatible with the repo skill-pack layout.
- The bundle passes the prepared and completed validators after the skill-pack updates.

## Proof Required

- `powershell -File codex/scripts/install-candoitall-skills.ps1 -CodexHome .artifacts/tmp-codex-home -SkipPublicSkills`
- Static verification that `tools/Reinstall-CanDoItAllMcps.ps1` still syncs repo-managed skills recursively after the repo changes
- `python codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py project-hierarchy-bundle-1 --profile initiative`
- The final completed-stage bundle validation after execution is done

## Browser Validation Logging

- `N/A`
- This phase audits workflow analytics, skill files, and install/sync scripts rather than a browser-visible application surface.

## Progression Gate

- The repaired skill pack exists in the repo and is installable from the repo.
- The analytics review explains why the changes were made.
- The final bundle validators pass with the updated docs.

## Suggested Agent Prompt

```text
Implement subbundle 05 only. Review the analytics from this bundle run, turn them into repo-local workflow-skill fixes, add any missing validator skills to the repo-managed skill pack, update the install/sync script path so another machine receives those skills, prove the skill install flow, and rerun the validators before closing the bundle.
```
