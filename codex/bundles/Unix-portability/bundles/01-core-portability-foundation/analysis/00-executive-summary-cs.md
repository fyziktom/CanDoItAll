# Shrnutí core bundle

Tento bundle má za úkol dostat základní aplikaci do stavu, kdy se korektně sestaví, spustí a restartuje na Windows, Linuxu a macOS bez závislosti na desktopových nebo runtime-node funkcích.

Pořadí je záměrně konzervativní:

1. přesný anchor a úplný inventář;
2. logické cesty, slash syntax a portable konfigurace;
3. filesystem case/link/atomicity/permissions;
4. storage a control-plane migrace;
5. secure secret providers, Data Protection a legacy migrace;
6. composition a truthful capabilities;
7. headless hosting, publish a aktivní Windows/Linux/macOS CI.

Secrets jsou až po storage/filesystem základu, protože jejich key ring, vault roots, atomic writes, locking a Unix permissions na tomto základu přímo závisí.

C4 je tvrdá hranice. Runtime bundle se nesmí spustit pouze proto, že core změny „vypadají hotově“. Musí existovat přesný passing commit, restart/migration evidence a aktivní tříplatformní CI.
