# Pomocné skripty

Oba skripty vyžadují Python 3.10+ a používají pouze standard library. Nemění produkční kód, nespouštějí build ani aplikační testy. Komentáře ve zdrojích jsou anglicky.

## Kontrola dodaného balíku

Z kořene rozbaleného balíku:

```text
python scripts/validate_package.py
```

Kontroluje lokální odkazy, JSON, evidence ID, mapování požadavků, inventář, stop-gate a SHA-256 manifest. Provede referenční skalární pravdivostní tabulku na dodaných příkladech a konečné sadě kombinací. **Nekontroluje produkční C# implementaci merge a neprovádí aplikační testy.** Složitější vektory jsou jen popsané scénáře určené k následným C#/browser testům.

`--write-report` je autorská volba před vytvořením hash manifestu. Sealed balík validuj bez ní, aby se nezměnil soubor zahrnutý v kontrolních součtech. Uložený report vzniká před zapečetěním; následné spuštění bez zápisu ověřuje i hotový manifest.

## Lokální inventář skutečného checkoutu pro Codex

```text
python scripts/audit_repository.py C:/src/CanDoItAll --components-repo C:/src/CanDoItAll.Components --out C:/temp/cda-concurrent-editing-census.json
```

Cesty jsou příklady. Použij skutečný checkout a novou output cestu; existující report skript nepřepíše. Potřebuje lokální Git. Neprovádí fetch, reset, checkout, migraci ani test. Do výstupu zapisuje souborové cesty, čísla řádků a názvy nalezených kategorií, nikoli obsah konfigurace či fragmenty uživatelských dat.

Výstup hledá kandidáty na route, formuláře, inputy, modal/floating plochy, mutace, dynamické renderery a odběry. Kontroluje také existenci zdrojových cest z evidence manifestu. Jde o **heuristický census**, nikoli úplný Roslyn call graph nebo důkaz concurrency. Regex může zachytit false positive i něco vynechat; každý kandidát vyžaduje spojení s CodeAnalytics, skutečným volajícím a autoritativním ownerem. Chybějící baseline Git objekt může znamenat pouze shallow clone.

Mapuj nálezy na [inventář](../inventory/01_SURFACES.md) a pro každou vlnu uzavři [zápisový obvod](../inventory/02_WRITE_PERIMETER.md). Soubor bez `EditForm` není automaticky read-only; dynamické a inline editory vyžadují ruční kontrolu. Sibling bez předané cesty je označen jako neověřený, nikoli chybějící.

Self-test nástroje pracuje jen s dočasným syntetickým Git repozitářem:

```text
python scripts/audit_repository.py --self-test
```

Ani úspěch self-testu neznamená, že byl tento census spuštěn proti skutečnému CanDoItAll. V tomto analytickém úkolu skutečný lokální checkout ani runtime nebyly dostupné; zdrojové důkazy pocházejí z GitHub connectoru.
