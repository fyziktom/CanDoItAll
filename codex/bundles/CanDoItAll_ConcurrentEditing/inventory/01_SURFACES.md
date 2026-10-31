# Inventář konkrétních rodin ploch

**65 položek** pokrývá implementační moduly, sdílený shell a relevantní main-UI integrace. Stav značí sílu zdejšího důkazu, nikoli stav dokončení. Všechny položky jsou návrhem práce.

`DIRECT`: prohlédnutý současný kód; `DOC_CURRENT`: udržovaná architektonická dokumentace; `HISTORICAL_CHECK_REQUIRED`: starší audit je pouze discovery seed; `TREE_DISCOVERY`: plocha se musí lokálně rozpadnout na skutečné renderery a handlers. Smíšený stav rozlišuje přímo čtený adapter od ještě neuzavřeného owner obvodu. Přesný slovník všech kombinovaných stavů je v `evidence_states` souboru `surfaces.json`.

Úplný strojově čitelný inventář: [surfaces.json](surfaces.json). Reference Sxx: [zdroje](../evidence/README.md). **Žádná položka za pilotem se nesmí implementovat bez explicitního souhlasu uživatele.**

## Povinné uzavření census

P0 udělá lokální seznam všech main-UI routes, editable renderers, dialogs, inline-edit/drag handlers, registered dynamic settings renderers a entrypointů zapisujících jejich data. Každý kandidát dostane odpovídající ID níže nebo explicitní zdůvodněné read-only/out-of-scope. Samotné hledání EditForm není úplné: řada formulářů používá view-contract callbacks či custom fields. Použij CodeAnalytics reachability/call graph, router registraci, feature census dokumenty a lokální read-only helper. Nevyplněný kandidát není považován za dokončený.

Podrobnosti následují po modulech. Cesty uvedené ze staršího auditu jsou **entrypointy k ověření**, ne záruka, že se od té doby nepřesunuly. Nepřejmenovávej nic jen kvůli shodě s touto tabulkou.

## Prompts

### PG01 — Gallery seznam /prompt-gallery
**Politika:** LIST · **Důkaz:** DIRECT · **Vlna:** PILOT

**UI entry:** `src/Modules/CanDoItAll.Modules.Prompts/Pages/PromptGalleryPage.razor`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Prompts/Components/PromptGallerySearchSession.cs`.
**Rozsah konfliktu:** profile + catalog/filter query.
**Změna:** Quiet refresh místo blocking resetu; events create/update/archive/favorite; zachovat URL, query/page a otevřený detail.
**Merge:** Bez content merge; katalog není draft.
**Testy:** SearchSession generation, count/page clamp, two-window create/archive/filter; projection lag.
**Zdroje:** S12, S13, S16.

### PG02 — Prompt modal — scalars, text, tags, modely, consumers, recommendations
**Politika:** FORM · **Důkaz:** DIRECT · **Vlna:** PILOT

**UI entry:** `src/UI/CanDoItAll.Prompts.UI/Editor/PromptGalleryItemEditorSurface.razor`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Prompts/Components/PromptGalleryEditorSession.cs`; `src/Modules/CanDoItAll.Modules.Prompts/PromptsService.Gallery.cs`; `src/Modules/CanDoItAll.Modules.Prompts/PromptsService.Persistence.cs`.
**Rozsah konfliktu:** prompt editable aggregate + lifecycle witness.
**Změna:** Coherent B/R, accepted baseline, hint banner, save admission, typed merge a dirty close guard.
**Merge:** Scalar/text diff3; keyed modely; preferred invariant; množiny; raw čísla.
**Testy:** Coherent PostgreSQL read, third writer, invalid raw, close/navigation, unknown/no retry.
**Zdroje:** S07, S08, S09, S11, S14, S15.

### PG03 — Embedded editor v Gallery pickeru / chat a Workflow spotřebitelích
**Politika:** FORM · **Důkaz:** DOC_CURRENT · **Vlna:** PILOT-INTEGRATION

**UI entry:** `src/Modules/CanDoItAll.Modules.Prompts/Components/PromptGalleryPickerDialog.razor`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Prompts/Components/PromptGalleryItemEditorHost.razor`.
**Rozsah konfliktu:** same prompt owner; distinct opening/editor identity.
**Změna:** Použít stejný adapter; opravit parent close guard; nesmí zavřít cizí dialog ani změnit exact-version insertion.
**Merge:** Shodné s PG02; consumer/provider compatibility dál samostatná.
**Testy:** Picker/composer target A→B→A, nested dialogs, selected immutable version, suppression.
**Zdroje:** S16.

### PG04 — Finalize, archive/restore, favorite, warning suppression, curator/import
**Politika:** COMMAND · **Důkaz:** DIRECT/PERIMETER_OPEN · **Vlna:** PILOT

**UI entry:** `src/Modules/CanDoItAll.Modules.Prompts/Components/PromptGalleryEditorSession.cs`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Prompts/PromptsService.Gallery.cs`; `src/Modules/CanDoItAll.Modules.Prompts/PromptGalleryMutationService.cs`; `src/Modules/CanDoItAll.Modules.Prompts/PromptGallerySeedImporter.cs`.
**Rozsah konfliktu:** separate intent and content/lifecycle witness.
**Změna:** Inventarizovat všechny writery; publish po outer commit; lifecycle guard; favorites jako narrow intent, ne celý stale DTO.
**Merge:** Neplést persistent content s immutable version nebo preference; žádné implicitní restore.
**Testy:** Expected timestamp, postcommit projection failure, transaction rollback/no event, curator emits one event.
**Zdroje:** S07, S10, S11, S16.

## CrmHr

### CR01 — Home, directory/account/workforce/recruiting/agents catalogy a pickery
**Politika:** LIST · **Důkaz:** DOC_CURRENT · **Vlna:** R1

**UI entry:** `src/UI/CanDoItAll.CrmHr.UI`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.CrmHr/Pages`.
**Rozsah konfliktu:** query-specific scope; account/party directory projections.
**Změna:** Tiché bounded list refresh; retain explicit selected target i mimo filtr; role sensitive fields.
**Merge:** Žádný full DTO reset při rerenderu.
**Testy:** CrmHrSameTargetRerenderTests, PagedRecordBrowser/picker regression, privacy.
**Zdroje:** S18.

### CR02 — Directory Party — jméno, role, kontakty, adresy, poznámky
**Politika:** FORM · **Důkaz:** DIRECT · **Vlna:** R1

**UI entry:** `src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrDirectoryPage.razor`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrServices.cs`.
**Rozsah konfliktu:** Party editable aggregate; collections membership; confidential notes.
**Změna:** Opravit SavePartyCore original expectation + all child writers; immutable baseline, guard close.
**Merge:** Stable row IDs; delete/edit; sensitive note masking/access; no reinsert deleted rows.
**Testy:** Two independent DB contexts sequential stale save; row adds/removes; no blur; footer validation.
**Zdroje:** S17, S18.

### CR03 — Affiliations, Party relationships
**Politika:** FORM · **Důkaz:** HISTORICAL_CHECK_REQUIRED · **Vlna:** R1

**UI entry:** `src/UI/CanDoItAll.CrmHr.UI/Parties`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.CrmHr/Services/PartyOrganizationAffiliationService.cs`; `src/Modules/CanDoItAll.Modules.CrmHr/Services/PartyDirectoryManagementService.cs`; `src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmPartyCommandService.cs`.
**Rozsah konfliktu:** relationship endpoints + membership witnesses.
**Změna:** Ověřit current conditional affiliation batch, doplnit deletions/replacement/import/merge perimeter.
**Merge:** Graph-like relationship intent, stable keys; ne obecný union vztahů.
**Testy:** Foreign child insertion, deletion, overlap dates, ownership/privacy.
**Zdroje:** S18, S38.

### CR04 — Account profile — stage, poznámky, lifecycle/customer role
**Politika:** FORM · **Důkaz:** DIRECT · **Vlna:** R1

**UI entry:** `src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrCrmPage.razor`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrServices.cs`.
**Rozsah konfliktu:** account profile plus touched Party fields/roles.
**Změna:** Podmínky pro multi-entity zápis, včetně expected absence při prvním založení profilu.
**Merge:** Field groups a revalidace lifecycle/role invariantů; nezaměnit absent za null DTO.
**Testy:** SaveAccountProfile two editors, concurrent create profile, role side effects.
**Zdroje:** S17, S18.

### CR05 — Opportunity editor a pipeline/board move
**Politika:** FORM+LIST · **Důkaz:** HISTORICAL_CHECK_REQUIRED · **Vlna:** R1

**UI entry:** `src/UI/CanDoItAll.CrmHr.UI/Crm`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrServices.cs`.
**Rozsah konfliktu:** opportunity revision; referenced account/project authority.
**Změna:** Zachovat ExpectedUpdatedAtUtc; ověřit board drag, detail/create dialogs a tools; během drag nequiet-remountovat.
**Merge:** Scalars, amount/currency group, dates, references; board move narrow intent.
**Testy:** Opportunity concurrency, drag versus modal save, same-target draft, nested detail overlay.
**Zdroje:** S18, S38.

### CR06 — Opportunity conversion / linked records
**Politika:** COMMAND · **Důkaz:** DOC_CURRENT/PERIMETER_OPEN · **Vlna:** R1

**UI entry:** `src/UI/CanDoItAll.CrmHr.UI/Crm`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrServices.cs`.
**Rozsah konfliktu:** opportunity + existing project admission + linked set.
**Změna:** Neopakovat business conversion po chybě readback; zachovat vlastní cross-owner recovery a expected membership.
**Merge:** Explicit review changed conversion basis; ne generic field merge side effects.
**Testy:** CrmHrOpportunityConversionHostTests, stale linked project, duplicate create prevention.
**Zdroje:** S18, S38.

### CR07 — Next action, activity/interaction edit, account connected records
**Politika:** FORM+COMMAND · **Důkaz:** HISTORICAL_CHECK_REQUIRED · **Vlna:** R1

**UI entry:** `src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrCrmPage.razor`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrServices.cs`.
**Rozsah konfliktu:** per mutable next-action/interaction or linked-record set.
**Změna:** Uzavřít jednotlivé updates/delete; nové aktivity jsou append, nikoli přepis celé historie.
**Merge:** Typed fields; stable linked record identity; timeline read-only invalidation.
**Testy:** Edit vs append distinction, late callbacks, current same account draft.
**Zdroje:** S17, S18, S38.

### CR08 — Workforce profile, skills, capacity
**Politika:** FORM · **Důkaz:** HISTORICAL_CHECK_REQUIRED · **Vlna:** R1

**UI entry:** `src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrWorkforcePage.razor`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrServices.cs`.
**Rozsah konfliktu:** workforce configuration + skills/capacity intervals.
**Změna:** Ověřit a vynutit original-state replacement; capacity timeline drag je write, ne pouhý graf.
**Merge:** Ranges/units a overlapping capacity; keyed skills; never blind array union.
**Testy:** CrmHrFooterSaveValidationTests, same-target retention, stale capacity row.
**Zdroje:** S18, S38.

### CR09 — Recruiting — candidate, interviews, onboarding
**Politika:** FORM+COMMAND · **Důkaz:** DOC_CURRENT/PERIMETER_OPEN · **Vlna:** R1

**UI entry:** `src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrRecruitingPage.razor`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrServices.cs`.
**Rozsah konfliktu:** candidate profile + scheduled interviews/onboarding; technical agent evidence separate.
**Změna:** Zápisové expectations pro editable části; background evidence/recruiting run nesmí přepsat ruční návrh.
**Merge:** Typed interview/time fields, keyed checklist; run/attempt execution separately.
**Testy:** Recruiting selection/read races, stale interviewer update, evidence command duplicate.
**Zdroje:** S18, S38.

### CR10 — Business AI agent profile
**Politika:** FORM · **Důkaz:** DOC_CURRENT/PERIMETER_OPEN · **Vlna:** R1

**UI entry:** `src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrAgentsPage.razor`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrServices.cs`.
**Rozsah konfliktu:** business profile separate technical Agent definition.
**Změna:** Označit vlastníka každého editovaného pole; technical edits směřují na Agent owner, ne kopii v CRM.
**Merge:** Business scalars only; projected technical evidence read-only.
**Testy:** CrmHrAgentProjectionJourneyTests; business/technical concurrent updates.
**Zdroje:** S18, S38.

### CR11 — Assignments, staffing requests, project allocation a Gantt
**Politika:** FORM+CANVAS · **Důkaz:** HISTORICAL_CHECK_REQUIRED · **Vlna:** R1

**UI entry:** `src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrAssignmentsPage.razor`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrServices.cs`; `src/Modules/CanDoItAll.Modules.CrmHr/Services/ProjectPartyAssignmentInvariantPolicy.cs`.
**Rozsah konfliktu:** assignment snapshot + project/lifetime/pricing/rate basis.
**Změna:** Zachovat existující replacement guards; audit single save/delete/move; nezavést broad project conflict pro nezávislé řádky.
**Merge:** Dates/capacity/rates domain constraints; Gantt explicit review conflict.
**Testy:** CrmHrAssignmentsMutationTests, stale price/capacity, retarget while saving.
**Zdroje:** S18, S38.

### CR12 — Party dedup merge, bulk import/export
**Politika:** COMMAND · **Důkaz:** DOC_CURRENT/PERIMETER_OPEN · **Vlna:** R1

**UI entry:** `src/UI/CanDoItAll.CrmHr.UI/Parties`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.CrmHr/Services/PartyDirectoryManagementService.cs`.
**Rozsah konfliktu:** several source parties + target + affected memberships.
**Změna:** PartyMergeDialog není nový concurrency merge renderer; zachovat domain merge, additions original witnesses, import dry-run recheck.
**Merge:** Explicit business merge; no silent copying deleted/confidential records.
**Testy:** Two-source changed after preview; partial failure; postcommit event counts.
**Zdroje:** S18, S38.

## Projects

### PR01 — Project board / portfolio / hierarchy list
**Politika:** LIST · **Důkaz:** DIRECT · **Vlna:** R2

**UI entry:** `src/Modules/CanDoItAll.Modules.Projects/Pages/ProjectsPage.razor`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Projects`.
**Rozsah konfliktu:** profile portfolio/hierarchy query.
**Změna:** Quiet refresh without changing open ProjectModalHost target, pending save refs or package form.
**Merge:** List only; deletion status separate.
**Testy:** Projects board filter/selection; deleted card with retained draft.
**Zdroje:** S21.

### PR02 — Project wizard/detail — identity, dates, phases, options
**Politika:** FORM · **Důkaz:** DIRECT · **Vlna:** R2

**UI entry:** `src/UI/CanDoItAll.Projects.UI/ProjectEditorDraft.cs`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Projects.Contracts/ProjectDefinitionContracts.cs`.
**Rozsah konfliktu:** ProjectDefinitionExpectation + lifecycle + child membership.
**Změna:** Doplnit skutečnou immutable B; adaptovat existing Conflict/StartFromCurrent; preserve pending recovery and accepted mapping.
**Merge:** Scalar + stable child IDs/order; explicit new-base merge.
**Testy:** ProjectEditorDraft ack ABA, unresolved receipt, latest current while previous draft retained.
**Zdroje:** S21, S22, S39.

### PR03 — Hierarchy edits, starter seeding, package import/export, delete/recovery
**Politika:** COMMAND · **Důkaz:** DIRECT/DOC_CURRENT · **Vlna:** R2

**UI entry:** `src/Modules/CanDoItAll.Modules.Projects/Pages/ProjectsPage.razor`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Projects.Contracts`.
**Rozsah konfliktu:** native definition/lifecycle operation scopes.
**Změna:** Nezaměnit šablonu draftu s operation identity; seeding a delete mají vlastní accepted/unknown. Ověřit hierarchy dependencies.
**Merge:** Command basis review, no whole-project auto-replay.
**Testy:** Pending refs, seeding duplicate prevention, lifetime recreation, package transaction.
**Zdroje:** S21, S22.

## Workbench

### WB01 — Project Structure graph + changes floating window
**Politika:** CANVAS · **Důkaz:** DIRECT · **Vlna:** R2

**UI entry:** `src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureChangeObserver.razor`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/Changes/ProjectStructureChangeFeed.cs`.
**Rozsah konfliktu:** profile/project/lifetime + node/link/graph observations.
**Změna:** Zachovat current behavior; později optional migrate feed transport s parity, ne před pilotem.
**Merge:** Explicit pull; no automatic graph overwrite; diff list scoped.
**Testy:** Existing inbox/feed/SSE gap + scope tests; pre-read high-water race.
**Zdroje:** S19, S20.

### WB02 — Node/detail inspector, metadata/content/assets
**Politika:** FORM · **Důkaz:** HISTORICAL_CHECK_REQUIRED · **Vlna:** R2

**UI entry:** `src/UI/CanDoItAll.Workbench.Structure.UI`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureTaskDetailsService.cs`.
**Rozsah konfliktu:** owner-native node/task observations.
**Změna:** Adaptovat form merge na již existující očekávání; oddělit content/file bytes/technical runtime.
**Merge:** Typed node forms; unknown extra config preserve.
**Testy:** Dirty inspector plus graph refresh; stale asset/reference; node delete.
**Zdroje:** S06, S38.

### WB03 — Gantt schedule/progress, assignee a pricing editors
**Politika:** FORM+CANVAS · **Důkaz:** HISTORICAL_CHECK_REQUIRED · **Vlna:** R2

**UI entry:** `src/Modules/CanDoItAll.Modules.Workbench/Pages`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureGanttMutationService.cs`; `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureTaskPricingCommitService.cs`; `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureWorkItemAssigneeService.cs`.
**Rozsah konfliktu:** separate task field/schedule/cost/assignment observations.
**Změna:** Použít stávající original-value witnesses; neproměnit každou live progress změnu v global definition konflikt.
**Merge:** Meaningful times/units/rates; governed multi-field constraints.
**Testy:** Title vs schedule race, concurrent pricing, assignment eligibility, drag read freshness.
**Zdroje:** S38.

### WB04 — Planning, calendar, kanban/task boards
**Politika:** LIST+FORM+CANVAS · **Důkaz:** TREE_DISCOVERY · **Vlna:** R2

**UI entry:** `src/Modules/CanDoItAll.Modules.Workbench/Pages`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Workbench/WorkManagement`.
**Rozsah konfliktu:** per task/calendar item and query membership.
**Změna:** Dohledat přesné editory/inline handlers v census, přiřadit existing owners; drag/edit are mutation surfaces.
**Merge:** Forms merge; planning canvas explicit pull; readonly calendar quiet.
**Testy:** Calendar selection and timezone stability; board drag/remount; task draft retention.
**Zdroje:** S38.

### WB05 — Insights, dashboards, timeline / technical runtime projections
**Politika:** READ_ONLY · **Důkaz:** TREE_DISCOVERY · **Vlna:** R2

**UI entry:** `src/UI/CanDoItAll.Workbench.Insights.UI`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Workbench`.
**Rozsah konfliktu:** bounded readonly projection scopes.
**Změna:** Pouze quiet invalidation kde vhodná; bez merge, bez eventů per token/heartbeat do editor feedu.
**Merge:** Not editable.
**Testy:** Bounded query counts, no draft reset via dashboard rerender.
**Zdroje:** S06, S38.

## AgentFramework

### WF01 — Workflow catalog a component/template browser
**Politika:** LIST · **Důkaz:** DIRECT/DOC_CURRENT · **Vlna:** R3

**UI entry:** `src/Modules/CanDoItAll.Modules.AgentFramework/Pages`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs`.
**Rozsah konfliktu:** workflow catalog query; immutable version selection.
**Změna:** Quiet bounded refresh, selected editor definition nezávislá; neplnit celý JSON catalog při každém hintu.
**Merge:** Read model; exact version unchanged.
**Testy:** Workflow catalog filter and selected version; large definitions query volume.
**Zdroje:** S24, S25.

### WF02 — Workflow canvas/inspectors/settings
**Politika:** CANVAS+FORM · **Důkaz:** DIRECT/DOC_CURRENT · **Vlna:** R3

**UI entry:** `src/UI/CanDoItAll.AgentFramework.WorkflowAuthoring.UI`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Authoring/WorkflowDocumentOwner.cs`; `src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs`.
**Rozsah konfliktu:** whole definition original version + lossless graph.
**Změna:** Povinný ExpectedVersionId na existing replacement; všechny callers; explicit changes/pull; typed inspector merge jen s coherent whole-doc base.
**Merge:** No generic topology merge; preserve hidden input/settings/null semantics.
**Testy:** WorkflowDocumentOwner unknown, head CAS, node selection/viewport, Prompt picker exact-version.
**Zdroje:** S23, S24, S25.

### WF03 — Workflow publish/status/import/component library mutations
**Politika:** COMMAND · **Důkaz:** DIRECT/PERIMETER_OPEN · **Vlna:** R3

**UI entry:** `src/Modules/CanDoItAll.Modules.AgentFramework/Pages`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs`.
**Rozsah konfliktu:** current head or exact source version; separate runtime authority.
**Změna:** Audit status change, import/curator/legacy API for optional expectations; immutable runs untouched.
**Merge:** Review changed basis; no automatic launch/replay during refresh.
**Testy:** Status vs save race; create ID collisions; unknown save no retry.
**Zdroje:** S24, S25, S38.

## Processes

### PC01 — Process catalog/global/project override browser
**Politika:** LIST · **Důkaz:** DOC_CURRENT · **Vlna:** R3

**UI entry:** `src/UI/CanDoItAll.Processes.UI`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Processes`.
**Rozsah konfliktu:** profile/project/lifetime/exact definition key.
**Změna:** Quiet metadata-only refresh; inherited/global invalidation reaches affected local views; selected key stable after rename.
**Merge:** Catalog no merge.
**Testy:** Override inheritance change, archive/delete reveals global definition, metadata query bounds.
**Zdroje:** S26.

### PC02 — Definition form, roles, steps, branches, artifacts, child mapping
**Politika:** FORM · **Důkaz:** DOC_CURRENT · **Vlna:** R3

**UI entry:** `src/UI/CanDoItAll.Processes.UI`.
**Owner entry:** `src/Processes/CanDoItAll.Processes.Persistence/EfProcessAuthoringStore.cs`.
**Rozsah konfliktu:** original local/inherited ProcessAuthoringObservation.
**Změna:** Znovupoužít existing aggregate/reconciler/receipt; field-owned patches, shared merge adapter only for suitable form scopes.
**Merge:** Keyed domain elements, mappings validation; no reconstruction dropping governance.
**Testy:** Dirty read reconciliation, same native receipt, local/inherited concurrent change.
**Zdroje:** S26.

### PC03 — Process canvas, imports, publish/archive/delete
**Politika:** CANVAS+COMMAND · **Důkaz:** DOC_CURRENT · **Vlna:** R3

**UI entry:** `src/Modules/CanDoItAll.Modules.Processes`.
**Owner entry:** `src/Processes/CanDoItAll.Processes.Application/ProcessAuthoringStore.cs`.
**Rozsah konfliktu:** same durable aggregate, operation fingerprint and lifetime.
**Změna:** Explicit pull; persisted gestures not local-only undo; keep exact receipt recovery and publication capture.
**Merge:** No generic graph merge; command-specific compensation only later.
**Testy:** All nine canvas commands, repeated import ID, current read cannot prove unknown operation.
**Zdroje:** S26.

## AgentFramework

### AG01 — Agent / team / chat catalog lists
**Politika:** LIST · **Důkaz:** TREE_DISCOVERY · **Vlna:** R4

**UI entry:** `src/Modules/CanDoItAll.Modules.AgentFramework/Pages`.
**Owner entry:** `src/MAF/Common/CanDoItAll.AgentFramework.Core/Catalog`.
**Rozsah konfliktu:** profile-specific definition catalog not run history.
**Změna:** Quiet refresh with current selection and filters; history load frequency separate from config invalidation.
**Merge:** No whole-agent remount.
**Testy:** Filtered catalog live create/delete; independent open agent forms.
**Zdroje:** S32, S38.

### AG02 — Technical agent definition editor
**Politika:** FORM · **Důkaz:** DIRECT · **Vlna:** R4

**UI entry:** `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentEditorVerification.cs`.
**Owner entry:** `src/MAF/Common/CanDoItAll.AgentFramework.Core/Catalog/AgentFrameworkWorkspaceCatalogService.Agents.cs`.
**Rozsah konfliktu:** agent configuration revision; runtime state separate.
**Změna:** Reject missing expected revision for existing records; audit HR provisioning/tools/import and direct catalog mutations.
**Merge:** Typed configuration, model/provider refs, text prompt; permissions separate explicit review.
**Testy:** Tokenless update refuses; same-user two tabs; deletion+same ID recreation; postcommit typing.
**Zdroje:** S32, S38.

### AG03 — Teams, membership and capability assignment
**Politika:** FORM+COMMAND · **Důkaz:** HISTORICAL_CHECK_REQUIRED · **Vlna:** R4

**UI entry:** `src/Modules/CanDoItAll.Modules.AgentFramework/Services`.
**Owner entry:** `src/MAF/Common/CanDoItAll.AgentFramework.Core/Catalog/AgentFrameworkWorkspaceCatalogService.Teams.cs`.
**Rozsah konfliktu:** team config/member set and capability assignment witnesses.
**Změna:** Close set membership writers, concurrent add/delete and capability admission.
**Merge:** Keyed membership; no blind union capabilities/privileges.
**Testy:** Team member insert during replacement; retired assignment attempt; policy changes.
**Zdroje:** S38.

### AG04 — Tool/MCP capability authoring, setup/test and trusted settings
**Politika:** FORM+COMMAND · **Důkaz:** HISTORICAL_CHECK_REQUIRED · **Vlna:** R4

**UI entry:** `src/UI/CanDoItAll.AgentFramework.CapabilityAuthoring.UI`.
**Owner entry:** `src/MAF/Common/CanDoItAll.AgentFramework.Core/Catalog/CapabilityEditorConcurrency.cs`; `src/MAF/Common/CanDoItAll.AgentFramework.Core/Catalog/AgentFrameworkWorkspaceCatalogService.ProvidersAndCapabilities.cs`.
**Rozsah konfliktu:** capability fingerprint/config vs external setup attempt.
**Změna:** Make current required condition explicit; setup/run not replay on merge; preserve trust-selected renderer.
**Merge:** Schema fields and secret refs only; unknown configuration preserved.
**Testy:** Missing fingerprint, external setup Unknown, same-target later edits, authority revoked.
**Zdroje:** S25, S38.

### AG05 — Decision policy / project access / approvals
**Politika:** FORM+COMMAND · **Důkaz:** HISTORICAL_CHECK_REQUIRED · **Vlna:** R4

**UI entry:** `src/Modules/CanDoItAll.Modules.AgentFramework/Pages`.
**Owner entry:** `src/MAF/Common/CanDoItAll.AgentFramework.Core/Catalog/AgentFrameworkWorkspaceCatalogService.Agents.cs`.
**Rozsah konfliktu:** policy revision + native historical operation receipts.
**Změna:** Preserve retained submissions/historical acceptance; no policy union; refresh current authority.
**Merge:** Explicit policy group approval; old acceptance not fresh authority.
**Testy:** AgentDecisionPolicyRecoveryTests, current versus historical read, two tabs, deny after revoke.
**Zdroje:** S32, S38, S41.

## AgentFramework.ProviderManagement

### PV01 — Provider profiles and runtime defaults
**Politika:** FORM+LIST · **Důkaz:** HISTORICAL_CHECK_REQUIRED · **Vlna:** R4

**UI entry:** `src/Modules/CanDoItAll.Modules.AgentFramework/Services/ProviderEditorOperations.cs`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/Administration/DatabaseProviderProfileRegistry.cs`; `src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/Administration/ProviderRuntimeAdministrationService.cs`.
**Rozsah konfliktu:** configuration expected token separate from health/probe.
**Změna:** Audit optional token paths; defaults/config write intents; secret values not merge; list bounded.
**Merge:** Typed schema/defaults; preserve unknown fields; probe results readonly.
**Testy:** ProviderEditor held calls, probe vs user change, missing expected token, secret redaction.
**Zdroje:** S38.

### PV02 — Shared source/import/publication and sync configuration
**Politika:** FORM+COMMAND · **Důkaz:** HISTORICAL_CHECK_REQUIRED · **Vlna:** R4

**UI entry:** `src/UI/CanDoItAll.AgentFramework.SharedProviders.UI`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderSourceService.cs`; `src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderManagementService.cs`; `src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderSourceSyncService.cs`.
**Rozsah konfliktu:** source/import/provider witnesses and connection lifecycle.
**Změna:** Preserve enforced paths; close sync/tokenless paths; background sync cannot revert human config.
**Merge:** Explicit source and capability rights; no blind merge of shared access.
**Testy:** Sync vs config, disconnect vs callback, stale enable/delete, publish readback failure.
**Zdroje:** S38.

## AgentFramework

### CH01 — Simple Chat definitions / personas
**Politika:** FORM+LIST · **Důkaz:** HISTORICAL_CHECK_REQUIRED · **Vlna:** R4

**UI entry:** `src/Modules/CanDoItAll.Modules.AgentFramework`.
**Owner entry:** `src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence/Repositories/EfLlmChatDefinitionRepository.cs`.
**Rozsah konfliktu:** chat definition expected token/current revision.
**Změna:** Preserve native CAS and verify adapters; live definition catalog independent of active conversation.
**Merge:** Name/avatar/system prompt/temp; text merge, no transcript replacement.
**Testy:** Chat definition stale edit, raw temperature, ongoing conversation unaffected.
**Zdroje:** S38.

### CH02 — Conversation metadata and message composer
**Politika:** FORM+STREAM · **Důkaz:** HISTORICAL_CHECK_REQUIRED · **Vlna:** R4

**UI entry:** `src/UI/CanDoItAll.Conversations.Shell`.
**Owner entry:** `src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence/Repositories/EfLlmChatConversationRepository.cs`.
**Rozsah konfliktu:** metadata token separate append/dispatch identity.
**Změna:** Mutable title/metadata form only; never overwrite or merge whole transcript; preserve unsent composer.
**Merge:** Message append existing protocol; config hints do not reset composer.
**Testy:** Streaming while metadata changes, composer target retirement, own message dedupe.
**Zdroje:** S38.

## SchedulerPlanner

### SC01 — Plan catalog/calendar/history
**Politika:** LIST · **Důkaz:** DOC_CURRENT · **Vlna:** R5

**UI entry:** `src/UI/CanDoItAll.SchedulerPlanner.UI`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs`.
**Rozsah konfliktu:** plan metadata query; fire history separate projection.
**Změna:** Quiet plans refresh and preserved date/view/selection; firing not constant config conflict.
**Merge:** Calendar selection only; readonly history no merge.
**Testy:** SchedulerBrowserTests and selected event with pending edit; query bounds.
**Zdroje:** S27, S28.

### SC02 — Plan form — cron/timezone/input/target/enabled
**Politika:** FORM · **Důkaz:** DIRECT · **Vlna:** R5

**UI entry:** `src/Modules/CanDoItAll.Modules.SchedulerPlanner.Presentation`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs`.
**Rozsah konfliktu:** plan definition revision + exact target/source authority.
**Změna:** Add required original condition for SavePlan; owner captures expected not current auto adoption; preserve committed/projection warning.
**Merge:** Cron+timezone+window grouped; schema input; target version exact.
**Testy:** Sequential stale Save, timezone validation, current fire history irrelevant, unknown/projection failure.
**Zdroje:** S27, S28.

### SC03 — Pause/resume/delete and agent tools
**Politika:** COMMAND · **Důkaz:** DIRECT/PERIMETER_OPEN · **Vlna:** R5

**UI entry:** `src/Modules/CanDoItAll.Modules.SchedulerPlanner/AgentTools/SchedulerAgentRuntimeToolProvider.cs`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs`.
**Rozsah konfliktu:** narrow desired-state command or originally reviewed deletion target.
**Změna:** Choose explicit narrow intent semantics; no silent whole-form replacement; annotate conflicts, no Quartz repair on read.
**Merge:** No generic merge of schedules with history; changed command basis review.
**Testy:** Save vs enable, delete vs edited plan, known committed failure, no duplicate fire.
**Zdroje:** S27, S28, S38.

## Resources

### RS01 — Registry list and metadata/connector config editor
**Politika:** FORM+LIST · **Důkaz:** DIRECT_ADAPTER/DOC_OWNER · **Vlna:** R5

**UI entry:** `src/Modules/CanDoItAll.Modules.Resources/ResourceRegistryOwner.cs`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Resources/ResourceModels.cs`; `src/Modules/CanDoItAll.Modules.Resources.Contracts`.
**Rozsah konfliktu:** resource revision + dataset/project binding.
**Změna:** Use existing ReplaceResourceMetadataCommand; verify service CAS/migration active; readable typed config and quiet list.
**Merge:** Metadata/schema/refs; bytes not merged.
**Testy:** Resource original revision, promotion conflict, retired dataset, invalid raw config preserved.
**Zdroje:** S30, S31.

### RS02 — Promotion, delete, file binding and import
**Politika:** COMMAND · **Důkaz:** DOC_CURRENT/PERIMETER_OPEN · **Vlna:** R5

**UI entry:** `src/Modules/CanDoItAll.Modules.Resources`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Resources/ResourceModels.cs`.
**Rozsah konfliktu:** revision + binding + file promotion authority.
**Změna:** Preserve exact accepted/deleted expectation; current read cannot prove previous unknown command.
**Merge:** Explicit lifecycle/placement review; external bytes/checksums separate.
**Testy:** Migration mixed old writers, promotion race, delete vs metadata edit.
**Zdroje:** S30, S31.

## TestLab

### TL01 — Plan list and plan/case/evidence editors
**Politika:** FORM+LIST · **Důkaz:** DIRECT · **Vlna:** R5

**UI entry:** `src/Modules/CanDoItAll.Modules.TestLab/Workspace/TestLabWorkspaceOwner.cs`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.TestLab/TestLabModels.cs`.
**Rozsah konfliktu:** plan content revision + child set + project binding.
**Změna:** Add original-state owner contract; prevent replacement deleting foreign children; quiet list only after bounded query check.
**Merge:** Stable child rows, evidence refs, no result-history replacement from stale form.
**Testy:** Concurrent add/remove case, project rebind, broad List query count, accepted readback warning.
**Zdroje:** S29.

### TL02 — Test runs/result history and execution commands
**Politika:** STREAM+COMMAND · **Důkaz:** DIRECT/PERIMETER_OPEN · **Vlna:** R5

**UI entry:** `src/Modules/CanDoItAll.Modules.TestLab`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.TestLab/TestLabModels.cs`.
**Rozsah konfliktu:** result append identity and optional explicit historical annotation.
**Změna:** Separate editable annotations from real execution result appends; no history-wide generic token.
**Merge:** No automatic rerun or merge runtime outcome.
**Testy:** Plan edit during result append; no lost new run; manual run annotation witness.
**Zdroje:** S29.

## Workspace

### WS01 — Business defaults and decision data policy
**Politika:** FORM · **Důkaz:** DIRECT_FACADE/OWNER_OPEN · **Vlna:** R6

**UI entry:** `src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Workspace`.
**Rozsah konfliktu:** WorkspaceSettingsWriter V1/V2 snapshot and native context.
**Změna:** Inspect both current writers and adapters; do not repeat old generic settings gap claim; integrate observation and typed merge.
**Merge:** Currency/default provider/text; policy protected explicit review.
**Testy:** V1/V2 writer coexistence, missing settings first create, policy versus defaults edit.
**Zdroje:** S33, S34.

### WS02 — Storage catalog/driver configuration
**Politika:** FORM+LIST · **Důkaz:** HISTORICAL_CHECK_REQUIRED · **Vlna:** R6

**UI entry:** `src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/StorageSettingsPanel.razor`.
**Owner entry:** `src/Foundation/CanDoItAll.Infrastructure/Storage/Persistence/StorageCatalogService.cs`.
**Rozsah konfliktu:** human storage config separate driver health.
**Změna:** Verify save/health paths; close original-state gaps only if still current; readonly health must not full-save config.
**Merge:** Typed driver settings, secret refs and paths; health separate.
**Testy:** Probe/health vs save, config stale, name/path validation; quiet bounded list.
**Zdroje:** S34, S38.

### WS03 — Storage routing / placement recovery
**Politika:** FORM+COMMAND · **Důkaz:** DOC_CURRENT/PERIMETER_OPEN · **Vlna:** R6

**UI entry:** `src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/StoragePlacementRecoveryDialog.razor`.
**Owner entry:** `src/Foundation/CanDoItAll.Infrastructure/Storage`.
**Rozsah konfliktu:** routing set witness vs owner-specific durable recovery operation.
**Změna:** Routing edit needs set scope; recovery has exact target and effects, never merge/replay automatically.
**Merge:** Typed rules and priorities; explicit recovery basis review.
**Testy:** Routing foreign insert; recovery unknown; remote bytes failure no repeated move.
**Zdroje:** S34, S38.

### WS04 — Data Sources profile configuration, connection test
**Politika:** FORM+LIST · **Důkaz:** HISTORICAL_CHECK_REQUIRED · **Vlna:** R6

**UI entry:** `src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Workspace/Database/DatabaseProfileWorkspaceService.cs`; `src/Foundation/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs`.
**Rozsah konfliktu:** control-plane file revision; profile activation generation separate.
**Změna:** Preserve file owner; add/verify expected content, connection test writeback cannot overwrite human edit.
**Merge:** Typed metadata, secret references only; no raw credentials.
**Testy:** File coordination on supported OS, test vs edit, active profile changes with open draft.
**Zdroje:** S34, S38.

### WS05 — Data transfer, activate database, migrations/restore
**Politika:** COMMAND · **Důkaz:** DOC_CURRENT/PERIMETER_OPEN · **Vlna:** R6

**UI entry:** `src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor`.
**Owner entry:** `src/Foundation/CanDoItAll.Infrastructure/ControlPlane`.
**Rozsah konfliktu:** activation/schema/data incarnation and native operation ledger.
**Změna:** Read/refresh must never activate or retry import; switching retires all affected subscriptions/drafts safely.
**Merge:** No field auto-merge of restore state.
**Testy:** A→B→A profile fence, unknown transfer receipt, read-only refresh.
**Zdroje:** S34, S41.

### WS06 — API accounts/password/scopes
**Politika:** FORM+LIST · **Důkaz:** HISTORICAL_CHECK_REQUIRED · **Vlna:** R6

**UI entry:** `src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/WorkspaceApiAccessHost.razor`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiUserAdministrationService.cs`; `src/Foundation/CanDoItAll.Infrastructure/ControlPlane/FileApiUserStore.cs`.
**Rozsah konfliktu:** control-plane original version + authentication revision.
**Změna:** Preserve file CAS and auth invalidation; field merge only nonsecret metadata; privileges explicit.
**Merge:** Passwords never diff; scopes not union; write authorization independently current.
**Testy:** File CAS, stale role/scopes, password rotation notification redaction.
**Zdroje:** S34, S38.

### WS07 — API token issue/revoke/delete
**Politika:** COMMAND · **Důkaz:** HISTORICAL_CHECK_REQUIRED · **Vlna:** R6

**UI entry:** `src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/WorkspaceApiAccessHost.razor`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiTokenAdministrationService.cs`.
**Rozsah konfliktu:** token lifecycle and secret one-time disclosure.
**Změna:** Lifecycle feedback/known vs unknown; no generic merge or secret value in event.
**Merge:** Explicit narrow intents; list quiet after accepted.
**Testy:** Lost issue acknowledgement, revoke idempotence, no secret copies in diff/log.
**Zdroje:** S34, S38.

### WS08 — Provider-history capture/retention policy
**Politika:** FORM+COMMAND · **Důkaz:** HISTORICAL_CHECK_REQUIRED · **Vlna:** R6

**UI entry:** `src/Modules/CanDoItAll.Modules.Workspace/Pages/Components/ProviderHistoryPolicyPanel.razor`.
**Owner entry:** `src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Persistence/HistoryPolicyStore.cs`.
**Rozsah konfliktu:** ExpectedVersion + current profile policy; prune separate.
**Změna:** Preserve existing policy CAS; no reload-triggered prune; changed retention basis explicit review.
**Merge:** Typed durations/capture flags, destructive policy grouping.
**Testy:** Retention shorten vs stale form; prune preview invalidation; scope switch.
**Zdroje:** S34, S38.

### WS09 — Trusted dynamic settings renderers / schema fields
**Politika:** FORM · **Důkaz:** DOC_CURRENT · **Vlna:** R6

**UI entry:** `src/Modules/CanDoItAll.Modules.Workspace/Pages/Components`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Workspace`.
**Rozsah konfliktu:** actual feature owner, trusted renderer registry.
**Změna:** Renderer chooser remains authorized; preserve unknown JSON/raw values through feature adapter.
**Merge:** Schema-aware typed controls; JSON debug not user merge default.
**Testy:** Unknown property roundtrip, trusted source denies, schema change during edit.
**Zdroje:** S25, S34.

## Security

### SE01 — Secret metadata and vault rotation
**Politika:** FORM+COMMAND · **Důkaz:** HISTORICAL_CHECK_REQUIRED · **Vlna:** R6

**UI entry:** `src/Modules/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Security/SecurityModels.cs`; `src/Modules/CanDoItAll.Modules.Security/SecretVaults.cs`; `src/Modules/CanDoItAll.Modules.Security/NativeSecretVaults.cs`.
**Rozsah konfliktu:** metadata revision separate vault effect/cleanup.
**Změna:** Current service audit before change; CAS metadata, explicit rotation result, preserve unknown/committed-warning.
**Merge:** Never secret values; label/environment refs only where authorized.
**Testy:** Secret readback impossible/denied, rotation succeeds metadata fails, no repeat, draft privacy.
**Zdroje:** S34, S38.

## Plugins

### PL01 — Plugin catalog/config/connection forms and grants
**Politika:** FORM+LIST · **Důkaz:** DIRECT_GRANT/OTHER_OPEN · **Vlna:** R6

**UI entry:** `src/Modules/CanDoItAll.Modules.Plugins.Presentation`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Plugins/Catalog/PluginPermissionServices.cs`; `src/Modules/CanDoItAll.Modules.Plugins/Catalog/PluginCatalogServices.cs`.
**Rozsah konfliktu:** grant key scope and connection revision.
**Změna:** PluginGrantStore original conditions; audit connection/tokenless writers, quiet config catalog, grants not blind union.
**Merge:** Explicit permissions grouping, typed schema, secret refs.
**Testy:** Stale revoke vs grant edit, returned token propagation, setup outcome handling.
**Zdroje:** S36, S38.

### PL02 — Install/restart/uninstall, OAuth callbacks/refresh/disconnect
**Politika:** COMMAND · **Důkaz:** HISTORICAL_CHECK_REQUIRED · **Vlna:** R6

**UI entry:** `src/Modules/CanDoItAll.Modules.Plugins`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Plugins/OAuth/PluginOAuthService.cs`.
**Rozsah konfliktu:** external lifecycle + OAuth state/version + configuration.
**Změna:** No retry due to merge; callbacks cannot restore disconnected stale connection; preserve grants before effects.
**Merge:** Not ordinary form merge.
**Testy:** OAuth callback versus disconnect, unknown install, failed projection known effect.
**Zdroje:** S38.

## Memory

### ME01 — Provider profiles/configuration and capability transport
**Politika:** FORM+LIST · **Důkaz:** DIRECT · **Vlna:** R6

**UI entry:** `src/Modules/CanDoItAll.Modules.Memory.Presentation`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderProfileConfigurationService.cs`; `src/Memory/CanDoItAll.Memory.Persistence/EfMemoryProviderProfileStore.cs`.
**Rozsah konfliktu:** provider configuration revision separate runtime/query state.
**Změna:** Required original observation for configuration Save and store Upsert caller perimeter; keep provider-neutral architecture.
**Merge:** Typed HTTP/MCP fields, selected transport, environment secret refs.
**Testy:** Missing expected; concurrent driver switch; runtime status not causing stale form.
**Zdroje:** S35, S38.

### ME02 — Memory query/ingestion/retention operations and results
**Politika:** COMMAND+READ_ONLY · **Důkaz:** HISTORICAL_CHECK_REQUIRED · **Vlna:** R6

**UI entry:** `src/Modules/CanDoItAll.Modules.Memory`.
**Owner entry:** `src/Memory`.
**Rozsah konfliktu:** native operation identity and query inputs.
**Změna:** Preserve local query form; readonly streamed status no entity merge; destructive retention command explicit.
**Merge:** No rerun ingestion on hint/refresh.
**Testy:** Query response arrives after config/target switch; unknown ingestion no replay.
**Zdroje:** S38.

## Collaboration

### CO01 — Threads/inbox list/detail and mutable metadata
**Politika:** FORM+LIST · **Důkaz:** DOC_CURRENT/PERIMETER_OPEN · **Vlna:** R6

**UI entry:** `src/UI/CanDoItAll.Collaboration.UI`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Collaboration/CollaborationService.Commands.cs`; `src/Modules/CanDoItAll.Modules.Collaboration/CollaborationModels.cs`.
**Rozsah konfliktu:** mutable thread metadata separate unread state.
**Změna:** Quiet list; draft pinned selection; current metadata original conditions audit.
**Merge:** Title/status typed; read/unread desired-state intent, not entire thread replacement.
**Testy:** Reply stays target leaving filter, unread changed concurrently, metadata form stale.
**Zdroje:** S37, S38.

### CO02 — New thread/reply composer/message stream
**Politika:** STREAM+FORM · **Důkaz:** DOC_CURRENT · **Vlna:** R6

**UI entry:** `src/Modules/CanDoItAll.Modules.Collaboration`.
**Owner entry:** `src/Modules/CanDoItAll.Modules.Collaboration/CollaborationService.Commands.cs`.
**Rozsah konfliktu:** append identity + independent create/reply draft.
**Změna:** Retain unsent text/unknown submission; avoid automatic selection reset; explicit navigation discard now guard required.
**Merge:** Append existing semantics; no transcript merge.
**Testy:** Unknown append locked, duplicate submit, pending reply during quiet list refresh.
**Zdroje:** S37.

## App / shared Components

### SH01 — Notification host, modal/floating layers, navigation guard
**Politika:** OVERLAY · **Důkaz:** DIRECT · **Vlna:** PILOT

**UI entry:** `src/Modules/CanDoItAll.Modules.Prompts/Pages/PromptGalleryPage.razor`.
**Owner entry:** `src/CanDoItAll.Components.BaseLib/Components/Feedback/Notification.razor`; `src/CanDoItAll.Components.BaseLib/Components/Modals/Dialog.razor`.
**Rozsah konfliktu:** per circuit modal stack and single logical notification lifetime.
**Změna:** Sibling shared fix; one active outlet; no simple z-index-only patch; preserve raw form when closing denied.
**Merge:** Human-friendly merge renderer in existing floating primitive.
**Testy:** Real pointer/focus/top-layer nested browser tests, single expiry clock.
**Zdroje:** S12, S42, S43.

### SH02 — FileTools editors exposed in main UI and managed files
**Politika:** FORM+COMMAND · **Důkaz:** TREE_DISCOVERY · **Vlna:** R7

**UI entry:** `src/UI/CanDoItAll.AppComponents`.
**Owner entry:** `Sibling CanDoItAll.FileTools + main owner integration`.
**Rozsah konfliktu:** file content/version/ETag vs app metadata and storage placement.
**Změna:** P0 census must identify actual editable main-UI file paths; use existing file conflict semantics; do not invent module-level JSON merge of file bytes.
**Merge:** Text file diff only through file owner version; binary needs choose/copy, no auto merge.
**Testy:** External file change, stale save, placement recovery, dirty close with nested file editor.
**Zdroje:** S05, S06, S34.

### SH03 — Read-only audit/history, immutable versions, run telemetry
**Politika:** EXCLUDED_FROM_MERGE · **Důkaz:** DOC_CURRENT · **Vlna:** ALL

**UI entry:** `src/App/CanDoItAll.Web`.
**Owner entry:** `Existing owner-specific append/history services`.
**Rozsah konfliktu:** append sequence / retention / runtime claim.
**Změna:** Keep current streams and bounded refresh; do not count as editable entity closure or lock entire history.
**Merge:** Not applicable; editable annotations are separate scoped forms.
**Testy:** No lost append from config save, no dispatch from refresh, retained immutable exact version.
**Zdroje:** S25, S26, S28, S38.
