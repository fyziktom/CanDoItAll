# Page Inputs: CRM/HR Suite

## PI-CRMHR-HUB `/crm-hr`

Source reference:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrHomePage.razor`

Current display:
- `PageScaffold` module hub with summary tiles `Parties`, `Organizations`, `Workforce`, `Pipeline`, and `Agents`.
- Actions include `Create party`, `Open directory`, `Directory`, `CRM`, `Workforce`, `Recruiting`, `Agents`, `Assignments`, `Open`, and `Open CRM`.

Current UX flows:
- User scans CRM/HR state, creates a party, navigates to directory/CRM/workforce/recruiting/agents/assignments.

Target proposal:
- Use `06-supporting-pages-tabs-dialogs-proposal.png` panel 1.
- Compact B2B hub with metric strip, quick actions, and recent activity table.

Function coverage confirmation:
- Covers summary counts and all hub navigation actions.
- Removes hero-like page feel and supports customer video clarity.

## PI-CRMHR-DIRECTORY `/crm-hr/directory`

Source references:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrDirectoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Components\PartyMergeDialog.razor`

Current display:
- Directory page with summary tiles `Directory`, `Organizations`, `Sensitive`, and `Visible`.
- Form sections `Identity`, `Classification and contact`, `Contact methods and addresses`, `Context and handling`, `Relationships and duplicate stewardship`.
- Actions include `New party`, `Create party`, `Save party`, `Reset`, `Add role`, `Remove`, `Add confidential note`, and `Merge into current party`.
- `PartyMergeDialog` handles duplicate merge confirmation.

Current UX flows:
- User searches/selects party, edits identity/classification/contact/context/relationships, adds roles/notes, reviews duplicate candidates, merges duplicate.

Target proposal:
- Use `06-supporting-pages-tabs-dialogs-proposal.png` panel 2.
- Party tree/list/detail with merge dialog and confidential note inspector.

Function coverage confirmation:
- Covers all form sections and duplicate merge flow.
- Adds tree/list clarity for large directories.

## PI-CRMHR-CRM `/crm-hr/crm`

Source references:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrCrmPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Components\OpportunityConversionDialog.razor`

Current display:
- CRM page with summary tiles `Accounts`, `Recently active`, `Overdue next actions`, and `Visible`.
- Form sections `Relationship profile`, `Stakeholders`, `Interaction journal`, and `Opportunity pipeline`.
- Actions include `Open directory`, `Open directory record`, `Save CRM profile`, `Add stakeholder`, `Save stakeholders`, `Log interaction`, `Start opportunity`, `Reset`, `Remove`.
- Opportunity conversion dialog opens from pipeline flow.

Current UX flows:
- User selects account, edits relationship profile/stakeholders, logs interactions, starts opportunity, converts opportunity.

Target proposal:
- Use `06-supporting-pages-tabs-dialogs-proposal.png` panel 3.
- Account list/pipeline split with opportunity conversion dialog.

Function coverage confirmation:
- Covers account profile, stakeholders, interaction journal, pipeline, and conversion.
- Reduces long form stacks by moving detail into inspector/dialog areas.

## PI-CRMHR-WORKFORCE `/crm-hr/workforce`

Source reference:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrWorkforcePage.razor`

Current display:
- Workforce page with summary tiles `Profiles`, `Bench`, `Near available`, `Overallocated`, and `Without profile`.
- Form section `Workforce profile`.
- Actions include `Create delivery unit`, `Open directory`, `Open directory record`, `Save workforce profile`, and `Reset`.

Current UX flows:
- User selects worker/unit, creates delivery unit, edits workforce profile/allocation, opens directory record.

Target proposal:
- Use `06-supporting-pages-tabs-dialogs-proposal.png` panel 4.
- Allocation list/detail plus team allocation summary and worker/profile dialog.

Function coverage confirmation:
- Covers all workforce actions and profile editing.
- Improves operational clarity for allocation management.

## PI-CRMHR-RECRUITING `/crm-hr/recruiting`

Source reference:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrRecruitingPage.razor`

Current display:
- Recruiting page with summary tiles `Applications`, `Interviewing`, `Offer or hired`, and `Open tasks`.
- Form section `Hiring conversion`.
- Actions include `New application`, `Convert to workforce`, and `Open workforce`.

Current UX flows:
- User creates candidate/application, reviews candidate pipeline, converts candidate to workforce, opens workforce.

Target proposal:
- Use `06-supporting-pages-tabs-dialogs-proposal.png` panel 4.
- Candidate pipeline plus conversion dialog side panel.

Function coverage confirmation:
- Covers recruiting creation, pipeline status, and conversion to workforce.
- Keeps hiring conversion visible but out of primary list clutter.

## PI-CRMHR-AGENTS `/crm-hr/agents`

Source reference:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrAgentsPage.razor`

Current display:
- Agent projection page with summary tiles `Projected agents`, `Capability-backed`, `Bound providers`, and `Review required`.
- Actions include `Open technical catalog`, `Open directory`, `Open technical record`, `Open directory record`, and `Reset`.

Current UX flows:
- User selects projected business-facing agent, opens technical AgentFramework record or directory record.

Target proposal:
- Use `06-supporting-pages-tabs-dialogs-proposal.png` panel 5.
- Compact agent list/detail with technical/directory links.

Function coverage confirmation:
- Covers projection, review, open technical/directory flows.
- Aligns CRM/HR agents with AgentFramework visual language.

## PI-CRMHR-ASSIGNMENTS `/crm-hr/assignments`

Source reference:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Pages\CrmHrAssignmentsPage.razor`

Current display:
- Assignment planning page with summary tiles `Projects`, `Open demand`, `Open requests`, `Bench`, and `Overallocated`.

Current UX flows:
- User scans staffing demand, assignments, bench, and overallocated state.

Target proposal:
- Use `06-supporting-pages-tabs-dialogs-proposal.png` panel 5.
- Full-width assignment planning grid with compact details.

Function coverage confirmation:
- Covers demand/requests/bench/overallocated monitoring.
- Provides clear planning surface for large screens.
