# Pokyn vlastníka

Projekt potřebuje rychlejší UI iterace, ale Components a FileTools mají během vývoje
zůstat připojené živě jako sibling repozitáře. První praktický krok proto není lokální
NuGet režim ani úprava Manageru, ale postupné rozplétání komponent s příliš širokou
závislostí na aplikační logiku a služby.

Pro tento první implementační bundle platí:

- začít Agents modulem;
- komponenty zatím fyzicky nepřesouvat;
- zachovat stávající funkčnost jako opěrný bod;
- vytvořit jasné state, intent a I/O hranice připravené pro pozdější sandbox a routing;
- nedělat mechanické wrappery a interface pro všechno;
- `AppComponents` ponechat pro skutečně aplikačně obecné komponenty, feature význam
  ponechat modulu;
- odstranit nebo přepsat nesmyslné testy a testovací techniky, které fixují private/source
  shape, například exact partial counts, private reflection a uninitialized services;
- obecná pravidla jsou definována v `CDA-UI-SEAMS-BASE-v1` a tento bundle je musí použít;
- konkrétní routing, fyzické `.UI` projekty, sandbox, Manager a dotnet-watch optimalizace
  přijdou v dalších bundlích.
