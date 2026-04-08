# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| `REQ-01` bundle the work from findings | `analysis/01-current-state.md`, `plan/01-phase-plan.md` | `subbundles/01-findings-normalization-and-gap-inventory` | `validate_bundle.py --stage prepared` | Foundation for all later work. |
| `REQ-02` direct project-reference answers | `inventories/01-sharptools-parity-gap-inventory.md`, `architecture/01-target-solution.md` | `subbundles/02-project-and-solution-navigation-parity` | Build plus targeted query validation on a real snapshot | Maps to Zyphonote Scenario 1. |
| `REQ-03` reuse sibling libraries | `architecture/01-target-solution.md` | `subbundles/02-project-and-solution-navigation-parity` | Code diff stays within sibling repo plus thin host wrapper | No copied analysis code allowed. |
| `REQ-04` add missing SharpTools-style analysis tools | `inventories/01-sharptools-parity-gap-inventory.md` | `subbundles/02-project-and-solution-navigation-parity`, `subbundles/03-member-behavior-and-source-inspection-parity` | Build and targeted tests | Limited to analysis surfaces for this pass. |
| `REQ-05` stabilize method-behavior path | `analysis/01-current-state.md`, `architecture/01-target-solution.md` | `subbundles/03-member-behavior-and-source-inspection-parity` | Reproduce and close the member-behavior failure | Maps to Zyphonote Scenario 4. |
| `REQ-06` update reinstall and registration | `inputs/01-source-artifacts.md` | `subbundles/04-host-integration-reinstall-and-skill-guidance` | Reinstall script succeeds and generated config includes new surfaces if needed | Host rollout work. |
| `REQ-07` add skill guidance | `architecture/01-target-solution.md`, `shared-prompts/implementation-prompt.md` | `subbundles/04-host-integration-reinstall-and-skill-guidance` | Skill file or repo docs updated and synced | User explicitly anticipated this need. |
| `REQ-08` rerun the five Zyphonote scenarios | `inputs/02-structured-input.md` | `subbundles/05-zyphonote-rerun-and-closure` | Recorded rerun scorecard and comparison summary | Must use the same scenario matrix. |
| `REQ-09` capture remaining issues as findings | `reviews/01-execution-report.md` | `subbundles/05-zyphonote-rerun-and-closure` | New findings files under the bundle if any gaps remain | Prevents regressions turning back into chat-only memory. |
