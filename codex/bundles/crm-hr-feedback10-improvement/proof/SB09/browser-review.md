# SB09 Populated Browser And Console Review

Review date: `2026-07-24`

## Environment

- Release listener: `http://127.0.0.1:5032`
- Viewport: `1800x1100`
- Seed source: public CRM-HR API operator recorded in `bundle://proof/SB09/seed-first-run.md`

## Recruiting Review

- `CRM Recruiting` displayed `8 applications`, `2 interviewing`, and `2 offer or hired`.
- Selecting Omar Farouk displayed `Hired`, `Approved`, and `Workforce` state.
- Omar's context showed two interviews, two lifecycle tasks, manager/buddy/mentor assignments, stage history, and the existing converted workforce profile.
- The selected context completed loading before publication after the race repair; the final browser console remained clean.

Inspected artifact:

| State | Artifact | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| Populated Recruiting with Omar selected | `repo://output/playwright/crmhr-feedback10-final/.playwright-cli/page-2026-07-24T17-32-18-258Z.png` | `324819` | `DC0F88BDE552BCE07BF3657EC258D543C5D16736FDA9638D81ABE0E26EEFCF79` |

The screenshot was opened and visually inspected. It shows the populated counts, selected Omar context, stage history, and workforce conversion without clipping.

## Cross-Route Agreement

The same seeded runtime was inspected in Directory and Workforce. Their exact interaction findings and screenshot hashes are recorded in `bundle://proof/SB07/browser-normal-and-dialog-review.md`:

- Directory: `78` records, `18` cards on the first page, `Page 1 / 5`, successful `Next` to page 2, Amina dialog with five tabs.
- Workforce: `32` records, `12` cards on the first page, `Page 1 / 3`, successful `Next` to page 2, Lucas dialog with five tabs, Grace Kim as manager, and a `Tentative 2026-09-01 -> 2026-09-30 / 30%` allocation.

## Adversarial Race And Console Proof

An initial populated Recruiting selection exposed a real `System.ArgumentException` when selected query context was published before its workspace load completed. The defect was fixed and a dedicated regression was added.

Dedicated command:

```powershell
dotnet test tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --configuration Release --no-build -nologo --filter "FullyQualifiedName~Recruiting_query_selection_publishes_context_only_after_workspace_load_completes"
```

- Exit code: `0`
- Result: `1 passed`, `0 failed`, `0 skipped`
- Elapsed: `17s`

Final console logs:

- `repo://output/playwright/crmhr-feedback10-final/.playwright-cli/console-2026-07-24T17-31-18-739Z.log`
- `repo://output/playwright/crmhr-feedback10-final/.playwright-cli/console-2026-07-24T17-31-55-594Z.log`
- `repo://output/playwright/crmhr-feedback10-final/.playwright-cli/console-2026-07-24T17-31-59-333Z.log`

Each final log contained informational entries only: `0` errors and `0` warnings. The earlier failing log is retained as diagnostic provenance and is not cited as the final state.

## Conclusion

- Semantic positive: API-created linked records agree across Recruiting, Directory, Workforce, application detail, and workforce conversion views.
- Adversarial negative: the populated-state render race was reproduced, repaired, covered by a focused test, and rechecked in a clean final console.
- Shallow-pass trap rejected: the screenshot uses the actual seeded Release database, not test-only data or a static fixture.
- Anti-stub audit: no fixture branch, seed startup hook, direct persistence, or fake financial state supplied the UI.

`Pass`.
