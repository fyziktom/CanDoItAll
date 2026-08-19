# Shrnutí runtime bundle

Tento bundle je připravený a B00 je způsobilý přes výslovně schválený provizorní core handoff. Core Gate C4, hosted CI a skutečné macOS ověření zůstávají odložené a nejsou prohlášeny za splněné.

Aktuální runtime situace není „všechno je Windows-only“. Centrální `LocalWorkspaceProcessHost` už používá typed arguments a je dobrým základem. Windows-first problémy jsou hlavně kolem:

- environment allowlistu a case sensitivity;
- executable resolution;
- Workbench runtime plans reprezentovaných jako PowerShell text;
- terminal/elevation presentation;
- Manager process ownership a WMI/Unix discovery;
- MCP command policy, Playwright npx cache a secret environment bindings;
- duplicated process runners;
- Docker pluginu, který si vytváří vlastní host;
- FileTools balíčku bez aktuální tříplatformní compatibility evidence;
- process-domain drivers, které musí spotřebovat host capabilities, ale nesmí převzít obecné OS služby.

Cílem není vytvořit nový univerzální process framework. Cílem je sjednotit low-level execution primitives, zachovat jejich správného vlastníka a potom přes ně postupně adaptovat jednotlivé integrační vrstvy.

B00 je povinný rebase a ownership checkpoint. Pokud po core změnách scope překročí split triggers, runtime plán se před implementací rozdělí na menší child bundles.
