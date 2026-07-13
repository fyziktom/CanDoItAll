# Root causes

## RC1: Required receipt gates nejsou branch-aware

`qa-validation` v software-delivery procesu má dvě branch outcomes: `quality-accepted` a `repair-required`. Browser/runtime receipts jsou nutné pro acceptance, ale nemají blokovat repair branch, pokud je defect prokázaný jinou deterministickou evidence.

Aktuální stav:

- `ProductCompletionRequiredToolReceiptsByStep` pro `qa-validation` obsahuje validation receipts i browser runtime proof receipts.
- `CapabilityScope.RequiredReceipts` pro `qa-validation` obsahuje stejný browser chain.
- `ValidateRequiredProductToolReceipts` a `ValidateRequiredProcessToolReceipts` neberou ohled na `output.BranchOutcomeKey`.

Důsledek:

- `repair-required` je blokované stejnými receipts jako `quality-accepted`.
- Validní defect-routing decision se změní na same-step retry nebo manager escalation.

## RC2: Completion gate failures neumí routovat branch

`product_required_file_content_missing` při `quality-accepted` znamená: „acceptance branch není pravdivá, existuje product defect“. Pokud má step repair branch, správné generické chování je route to repair branch.

Aktuální stav:

- Adapter vrací `NeedsManagerForCompletionIssues` před vytvořením branch signalů.
- `ProcessRuntimeBranchSignalApplicationService` umí pracovat až s manager signals ve `Succeeded` resultu.
- `ProcessRecoveryClassifier` vidí jen safe/idempotent diagnostic a spálí current-step retry budget.

Důsledek:

- Attempt 3 měl být automaticky převeden z `quality-accepted` na `repair-required`.
- Místo toho spotřeboval poslední automatic retry.

## RC3: Duplikovaný receipt contract

Stejné runtime/browser tools jsou dnes vynucované dvěma mechanismy:

1. product completion required receipts,
2. `CapabilityScope.RequiredReceipts`.

To způsobuje duplicitní diagnostics:

- `process.adapter.product_required_tool_receipt_missing`,
- `process.adapter.required_tool_receipt_missing`.

Duplicitní gates komplikují ordering, recovery, prompt a retry fingerprinty. Capability scope by měla řešit capability/policy/availability a product completion rules by měly řešit branch-specific evidence obligations. Pokud oba mechanismy zůstanou, musí být deduplikované a musí sdílet stejný branch applicability model.

## RC4: Safe retry policy nerozlišuje retryable vs branch-routable

`SafeToRetry` dnes znamená prakticky „zkus stejný step znovu“. Jenže deterministic product defect v acceptance branch není chyba stejného QA kroku. Je to výsledek QA, který má aktivovat repair branch.

Důsledek:

- `product_required_file_content_missing` spálil retry budget.
- Následný správný `repair-required` attempt už skončil v `ManagerRequired`.

## RC5: Recovery builder supluje runtime chování a obsahuje domain leakage

`ProcessStepRecoveryInstructionBuilder` obsahuje hardcoded hodnoty:

- `qa-validation`,
- `qa-recheck`,
- `quality-accepted`,
- `repair-required`,
- `repair-escalation`,
- `workspace_dotnet_run`,
- `workspace_dotnet_new`,
- `workspace_pwsh_run_script`.

To je špatná separace. Generic process application vrstva nemá znát .NET ani konkrétní software-delivery step keys. Tato znalost patří do:

- template metadata,
- software-delivery process pack,
- .NET workbench contributoru,
- optional `IProcessRecoveryAdviceProvider` registrovaného pro daný process/template/domain.

## RC6: Template stále matoucím způsobem spojuje proof gap a product defect

`qa-validation.md` už obsahuje mnoho správných vět, ale agenti s menšími modely pořád zaměňují:

- „neprovedl jsem browser proof“
- za „implementace musí repair“.

Template musí mít explicitní evidence matrix pro každou branch:

- `quality-accepted`: plná current-run validation + UI/browser proof + žádné deterministic gate failures.
- `repair-required`: konkrétní failed validation, deterministic product defect nebo browserem prokázaný product defect.
- `Blocked`: tool/policy/access/environment failure.
- Missing proof caused by QA omission: same-step retry / incomplete QA, ne repair branch.

## RC7: Není acceptance matrix z project structure

Project structure pro Tetris obsahuje jasná pravidla chování, ale runtime gates kontrolují hlavně technické artefakty a default scaffold. To je slabé pro jakoukoliv složitější .NET aplikaci.

Důsledek:

- Agent může akceptovat shell obrazovku, i když nesplňuje product behavior.
- QA se opírá o screenshot a build/test, ne o požadavky typu game loop, keyboard input, persistence, next piece UI.

Toto není Tetris-specific fix. Multi-team development process musí umět převést libovolné project-structure požadavky do acceptance criteria matrix a dokazovat je testy, static checks a UI proof.

## RC8: Testy kódují lokální gates, ne reálné kombinace

Existující test `Read_only_qa_acceptance_enforces_branch_specific_product_file_content_checks` správně ověřuje, že `quality-accepted` se scaffoldem spadne. Ale dnes očekává `NeedsManager`. Pro reálný enterprise proces je lepší expectation: pokud je nakonfigurovaný repair branch mapping, má to vyústit v branch route, ne manager.

Chybí regression test přes přesnou kombinaci:

- QA `quality-accepted` + full receipts + deterministic scaffold defect,
- QA `repair-required` + deterministic defect + missing acceptance-only browser receipts,
- retry budget se nemá spotřebovat pro branch-routable defect.

## RC9: MAF wrapper sám receipt capture zvládá, ale adapter mapping je příliš binární

Attempt 3 ukazuje, že MAF uměl posbírat browser/runtime receipts. Problém není „MAF ztratil receipts“. Problém je v tom, že adapter bere required receipts jako unconditional completion contract a neumí je aplikovat podle branch outcome/purpose.

MAF wrapper má ale zůstat pod kontrolou:

- receipt index musí být přesně current execution-run,
- evidence refs v textu nejsou náhrada receipt records,
- runtime-owned issue routing musí být jasně traceované v resultu a UI.
