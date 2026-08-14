# Shrnutí návrhu pro člověka

## Hlavní rozhodnutí

Původní bundle jsem nepoužila jen jako šablonu. Přestavěla jsem jeho pořadí a rozdělila implementaci na dva navazující bundly uvnitř jednoho ZIPu.

První bundle řeší pouze základ, který musí být stabilní dříve, než se začne měnit spouštění procesů a agentní runtime:

1. aktuální anchor a úplný inventář;
2. logické cesty, lomítka a portable konfigurace;
3. chování filesystemu, case sensitivity, symlinky, atomicitu, locking a Unix permissions;
4. storage, control plane a migraci host-bound cest;
5. secrets, Data Protection, macOS Keychain, Linux Secret Service a headless provider;
6. composition, capabilities a readiness;
7. headless hosting, publish, CI a Gate C4.

Druhý bundle začíná až po úspěšném C4 a řeší:

1. společné process primitives;
2. Workbench runtime nodes a oddělení PowerShell command textu od typed execution planu;
3. Manager a bezpečné vlastnictví procesů;
4. MCP, local stdio a external tools;
5. Docker, FileTools a desktop integrace;
6. napojení host capabilities na process-domain drivers bez přesunu procesní sémantiky do MAF;
7. tříplatformní runtime E2E a finální Gate R4.

## Nejzávažnější aktuální nálezy

Aktuální `development` má dobrý základ: projekty cílí na neutrální `net10.0`, centrální workspace process host už používá `ProcessStartInfo.ArgumentList` a poslední refaktoring výrazně zpřesnil hranice MAF a Processes.

Současně však zůstávají zásadní blokery:

- Development konfigurace stále používá `%LOCALAPPDATA%\...`.
- Infrastructure a MAF mají několik rozdílných path-policy implementací. Na Unixu může mít zpětné lomítko jiný význam podle toho, která vrstva cestu čte.
- `MafRuntimePathResolver` používá case-insensitive containment na všech OS.
- Absolutní workspace roots a cesty k preferovaným aplikacím se ukládají bez host/platform affinity.
- `Auto` secret provider na Linuxu a macOS vybere provider, který je zatím implementovaný jako unsupported.
- Souborový vault ukládá Base64 master key vedle šifrovaných dat.
- Data Protection key ring se ukládá do filesystemu bez explicitní produkční ochrany at rest.
- Workbench runtime launcher je stále čistě Windows/PowerShell/`runas` a Python venv předpokládá `Scripts/Activate.ps1`.
- Manager má WMI na Windows a na Unixu pouze nedostatečný name-only fallback.
- MCP a workspace tools mají odlišné executable resolvery.
- Docker plugin si vytváří vlastní `LocalWorkspaceProcessHost`.
- Jediný nalezený CI workflow je v `workflows-disabled` a hlavní application gate byl Windows-only.

## Důležité architektonické pravidlo

Nedoporučuji vytvářet jeden velký `IPlatformService`. OS rozdíly mají být izolované v malých, účelově vlastněných adaptérech:

- root/path defaults v Infrastructure/composition;
- secret providers v Security;
- process primitives v existující workspace runtime vrstvě;
- terminal presentation ve Workbench;
- process discovery v Manageru;
- process-domain rozhodování a recovery v `Processes`.

Samotný `ProcessDriverLayer.Platform` neznamená, že se do process drivers mají přesunout obecné filesystem, secret nebo process-host služby. Znamená pouze process strategy package, který spotřebovává deklarované host capabilities.

## Proč je runtime samostatný bundle

Nejde jen o velikost práce. Core část mění persistentní data a klíče, zatímco runtime část mění vlastnictví procesů a integrační hranice po čerstvém MAF refaktoringu. Oddělení umožní:

- bezpečně dokončit a stabilizovat migrace;
- získat Linux/macOS headless build/start dříve;
- znovu ukotvit runtime plán na skutečný commit po core změnách;
- vyhnout se tomu, že Codex vytvoří paralelní process stack nebo vrátí procesní sémantiku do MAF;
- případně runtime bundle po B00 ještě rozdělit, pokud lokální inventář překročí definované split triggers.

## Stav přípravy

Bundle je připravený pro Codex 5.6 Sol xhigh. Portable validace je součástí ZIPu. Protože během přípravy nebyl dostupný lokální checkout, první subbundle povinně provede build/test a opraví source references proti přesnému aktuálnímu commitu.
