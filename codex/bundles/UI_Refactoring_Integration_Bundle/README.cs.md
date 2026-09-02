# Český přehled bundle

Tento bundle řeší pouze původní větev `CanDoItAll/ui-refactoring`. Větev
`ui-refactoring-v2` je samostatná další generace úprav a nesmí být mergována,
cherry-pickována ani používána jako zdroj pro řešení konfliktů.

## Hlavní závěry analýzy

1. Původní aplikační větev má pouze pět vlastních commitů. Aktuální
   `development` je proti ní výrazně napřed, proto se zachovává dnešní
   implementace a doplňují se jen původní záměry.
2. `CanDoItAll.Components/main` je po merge UI větve červený ve třech
   approval/asset testech. Nejdříve se musí stabilizovat upstream.
3. Components nově necommituje BaseLib `output.css`, ale hlavní aplikace používá
   sibling source references. Čistý checkout by tak neměl garantované styly.
   Bundle předepisuje commitovat pouze distribuovaný BaseLib CSS výstup a v CI
   ověřovat jeho deterministickou regeneraci. Sandboxové výstupy zůstávají
   generované.
4. FileTools dnes nemá přímou závislost na Components a jeho validace ji výslovně
   zakazuje. V FileTools se neočekává UI refaktor; provede se kontrola, sjednocení
   verzí, balení a sandboxový test.
5. V hlavním repu je potřeba přepnout hostitelský asset na
   `material-symbols.css`, odstranit přímou vazbu na `.material-icons` a používat
   stabilní `.cda-material-icon` nebo komponentu `<Icon>`.
6. Starý root `PODMAN.md` se nesmí převzít beze změny. Užitečný obsah se přesune
   do dnešní struktury `docs/operations/` a opraví se požadavek na sibling
   repozitáře.
7. Doporučené společné číslo balíčků je `0.3.0`, pouze pokud ještě není použité
   na žádném relevantním feedu. Jinak Codex vybere další nepoužitou stabilní
   verzi a zaznamená rozhodnutí.

## Pořadí

Codex nejprve zafixuje scope a ochranu proti v2, potom opraví Components, sjednotí
verze, ověří FileTools, provede merge `development` do `ui-refactoring`, upraví
hlavní aplikaci, obnoví CI source piny a dokumentaci, provede cross-repo proof a
nakonec připraví report pro merge `ui-refactoring -> development -> main`.

Remote merge nebo publikace NuGetů nejsou automaticky povolené; Codex je provede
jen po výslovném pokynu vlastníka.
