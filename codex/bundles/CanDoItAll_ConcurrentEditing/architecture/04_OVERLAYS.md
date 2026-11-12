# Floating merge, modal lifecycle a notifikace

## Příčina, nikoli kosmetické číslo

Pinned shared `Notification.razor` renderuje běžný fixed div s inline z-index 900 a vlastní expiry timery. `Dialog.razor` používá native `<dialog>`; repository UI seam popisuje `showModal` a nested dialog re-raising. Native modal vytváří top-layer/inertness pravidla, která obyčejný vysoký z-index body prvku neobchází. Pouhý `z-index:2147483647` není řešení. S42–S43, S06, W03–W04.

Cílem je, aby aplikační notifikace byla viditelná a její ovládání použitelné nad **nejvyšším právě aktivním aplikačním modálem nebo floating merge**. Nejde o překrytí browser/OS bezpečnostních dialogů.

## Preferované řešení

Použít existující dialog/overlay registry nebo ji minimálně doplnit ve sibling Components. Jeden logický NotificationService/message lifecycle obsluhuje jeden **aktivní prezentační outlet**: normálně root shell; při modal stacku outlet ve flat-tree subtree nejvyššího modálu. Změna outletu nesmí restartovat timeout, duplikovat message nebo oznámení pro screen reader. Renderer může být přemountovaný, ale message expiry nesmí být nově vypočítané „od začátku“ pokaždé.

Není předepsaná konkrétní implementace portálu. Codex nejprve prohlédne BaseLib DialogInterop, existující overlay registry, AppShell host a Components MCP. Podmínky:

- nemoveovat ručně Blazor-owned DOM tak, aby se renderer po další aktualizaci rozešel se stromem;
- nevkládat Notification komponentu s vlastními timery do každého dialogu;
- nepoužívat `showModal()` pro toast, který by učinil formulář inertním;
- samostatný body-level popover není sám důkaz použitelnosti uvnitř modal režimu; kromě paint order se testuje inertness, pointer a keyboard interakce;
- neřešit problém CSS override v každém modulu;
- registry musí být circuit/host-local, ne statický cross-user modal stack.

U outletu uvnitř dialogu hlídat clipping scroll containerů, transforms a viewport positioning. Může být vhodné místo přímo pod kořenem dialogu mimo scrollable body, případně podporovaný top-layer mechanismus uvnitř jeho subtree. Rozhodnutí musí mít browser důkaz; nezaměňovat screenshot s funkčním tlačítkem.

## Floating merge window

Použít existující floating overlay primitives, ne nový window manager. U modal parentu musí být merge window skutečně v interaktivním subtree/dialog stacku. Preferovat host-owned child overlay a pouze tam, kde to existující knihovna umí, přepnout do správné nested modal vrstvy. Zobrazí se nad formulářem, ale formulář zůstane zachovaný. Implementační forma musí splnit uživatelovo očekávání floating review okna, nikoli přesměrování na jinou stránku.

Obsah: název záznamu, kdo/kdy/co se změnilo (jen dostupné důvěryhodné údaje), počet automatic/unresolved položek, řádkové/typed porovnání a výsledný návrh. Sticky footer: `Zrušit`, `Použít sloučení do formuláře`; samostatně nebezpečná volba `Zahodit moje změny a načíst aktuální`. Žádné implicitní Save. U velkého dokumentu skupiny a virtualizace, ne roztažená jedna stránka JSON.

### Zavírání a draft retention

Všechny cesty k zavření jdou přes jeden per-opening guard: X, Cancel, Escape, backdrop, programmatic dialog close, změna route/query recordu, výběr jiného záznamu, parent dispose vyvolaný navigací. Backdrop může zůstat disabled podle dnešního pilotu; přesto ostatní cesty potřebují guard.

Guard nabídne `Pokračovat v úpravách` a explicitní `Zahodit rozpracované změny`. Tlačítko „Save a zavřít“ lze nabídnout jen při běžné bezpečné save admission; ne při konfliktu či Unknown. Zrušení guardu nezmění URL tak, aby se dialog pak zavřel opožděným OnParametersSet. Po odmítnuté navigaci musí souhlasit route i skutečný target.

`NavigationLock`/equivalent interní navigační guard doplnit do hosta. `beforeunload` je pouze best-effort varování; **není zárukou přežití reloadu, crash nebo zániku circuitu**. Pilot garantuje zachování draftu ve stále žijícím editoru a řízené zavření. Durable draft autosave do browser storage není automatická součást; pro osobní a secret data vyžaduje samostatnou bezpečnostní politiku. Existující retained operation stores Projects/Agent policy se nesmí odstranit.

Neznámá běžící operace při close není obyčejný dirty text. Zachovat native retained submission/recovery nebo výslovně znemožnit destructive close; cancellation UI nevrací potvrzený DB commit zpět. Zůstává rozdíl mezi zavřením okna a dokončením admitted command.

## Notifikace jako doplněk, nikoli jediný stav

Konflikt/Unknown/Read failure zůstává inline v editoru. Konfliktní toast může být persistent nebo mít dostatečný lifecycle, ale zmizení toastu nesmí odstranit vysvětlení blokovaného Save. Obvyklé success toasty nesmí ukrást focus z textového pole. Kritická chyba může použít appropriate live announcement; neduplikovat stejnou zprávu několika aktivními regiony.

## Povinný browser důkaz

Ověřit root, otevřený modal, nested modal, merge nad modalem, toast existující před otevřením dalšího modálu, nový toast za běhu, změnu stacku při close a rychlé dispose/import race. `elementFromPoint`/hit-test a reálný click na dismiss musí zasáhnout toast, nikoli backdrop. Tab/Escape vracejí focus do správného pole/parentu; při zavření merge se nezavře rodič. Proveď desktop běh v Chromium a alespoň kontrolu podporovaných dalších browser engines podle existující infrastruktury. Neoznačovat neprovedené cross-browser scénáře za prošlé.
