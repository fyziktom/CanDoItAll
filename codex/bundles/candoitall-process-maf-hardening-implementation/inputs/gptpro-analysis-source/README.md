# CanDoItAll process/MAF hardening analysis

Tento balíček shrnuje statickou analýzu větve `memory-providers` a přiloženého výstupu `calculator-output.zip` se zaměřením na procesní runtime, nested subprocessy, artefact lifecycle a MAF wrapper.

Nejdůležitější závěr: incident u `prepare-solution-skeleton` nevypadá primárně jako problém, že by agent neuměl vytvořit .NET projekt. Přiložený `calculator-output.zip` obsahuje produktovou kostru, ale neobsahuje managed process artefakty ani handoff evidence. To odpovídá slabému místu mezi child procesem `dotnet-solution-setup` a parent slotem `solution-skeleton-evidence`.

## Doporučené pořadí oprav

1. **Observability first** – opravit korelaci AgentFramework execution runs podle přesného `(processRunId, stepInstanceId)` a zobrazit runtime receipt diagnostics, i když chybí AF `ResultSummary`.
2. **Runtime-owned subprocess bridge** – rozhodnout, že `StepKind=Subprocess` spouští, čeká a překlenuje runtime, ne běžný agent.
3. **Deterministický child-to-parent artifact bridge** – parent output slot se musí vyrobit z přesně validovaného child handoff artefaktu, ne z obecného seznamu child step souborů.
4. **Semantic artifact descriptors** – slot GUID nestačí; prompt, rework a diagnostika musí uvádět expectation key, title, primary managed ref, accepted child mapping a completion gate.
5. **Tool/capability preflight** – před claim/dispatch ověřit přesný composed runtime tool, ne jen obecné allowed operations.
6. **Template hardening** – prose-only kontrakty převést do typed `SubprocessContract`, `CompletionGates`, `RequiredReceipts`, `BranchRules`.

## Struktura balíčku

- `analysis/` – hlavní architektonická a kódová analýza.
- `codex/` – instrukce a sub-bundles pro Codex v angličtině.
- `data/findings.json` – strojově čitelný seznam nálezů.
- `data/source-map.csv` – mapování nálezů na soubory a řádky.
- `checklists/hardening-checklist.csv` – implementační checklist.
- `evidence/` – poznámky k přiloženému calculator výstupu a limitům kontroly.
- `mermaid/` – aktuální a cílový flow model.

## Omezení kontroly

V tomto prostředí nebylo dostupné `dotnet`, takže jsem neprovedla build ani testy. Analýza je statická nad zdrojovým kódem a přiloženými artefakty. Návrhy jsou proto formulované tak, aby Codex musel doplnit regression testy a ověřit je v běžném vývojovém prostředí.
