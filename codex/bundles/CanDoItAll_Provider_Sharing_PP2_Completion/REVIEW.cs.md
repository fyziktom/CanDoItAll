# Revize PP2 a další dokončovací bundle

## Výsledek

PP2 ještě není dokončené. Aktuální produktový commit
`9052b2a443a4fdce1ac24dd0ccfce236fd35ab74` obsahuje opravy S0, kontejnerový harness a jeho testy.
Předchozí `df9b7c8e1b563d56fef762c38aad2a14089bd332` přidal historické zadání. Dokumentace
výslovně ponechává otevřenou samotnou extrakci Sharing/Connections, nezávislý sandbox a závěrečné
consumer testy. Nejdu tedy na History ani jiný modul; nový bundle dokončuje již zadané PP2.

## Co zachovat

Oprava tooltipu používá název modelu, nikoli interní route ID. Importovaný editor se přijímá vůči
nativní revizi stejného katalogu; smíchané snapshoty se odmítnou. Lokální PP1 draft se při běžném
refreshi zbytečně nenahrazuje. Relay používá kanonický konfigurační mapper; nativní test jde přes
uložení, publikaci a skutečný driver/dispatch. Testovací runner umí izolovaný název, porty a kontrolu
vlastnictví. Nezadávám přestavbu tohoto základu.

## Nový nález PP2C-R1

Zachování podřízeného formuláře importu podle ImportId odstranilo ztrátu rozepsaného aliasu při
změně vzdálených metadat. Současně však formulář při stejném ImportId nikdy nepřevezme nové lokální
hodnoty. Rodič již drží čerstvý snapshot a při Save kombinuje starý alias/enabled s jeho nejnovějšími
tokeny. Když jiný editor mezitím lokální nastavení změní, následný Refresh + Save může jeho změnu
přepsat bez konfliktu. Vlastník přitom správně kontroluje tokeny, které mu UI samo dodalo.

Příklad: A načte alias „Team model“, enabled=true; B uloží „Operations model“, enabled=false;
A provede Refresh, ale vidí původní lokální hodnoty. Save je odešle s novými tokeny B. Nutná oprava
je oddělit původní lokální baseline a draft od nových vzdálených údajů a bezpečně rozhodnout o
sloučení nebo konfliktu. Nestačí znovu resetovat celý formulář při každé revizi.

Toto je nález z toku zdrojového kódu, nikoli zde spuštěná reprodukce nebo prokázaný incident v datech.
Nejde o doložené obejití autentizace. Bundle vyžaduje regresní test přes skutečný formulář a dva
nativní vlastníky, zachování čistých/špinavých draftů i aktualizací pouze vzdálených metadat.

## Testování S0

Implementační zpráva uvádí 19/19 protokolových scénářů a 1/1 rozsáhlý browserový test na třech
aplikacích se čtyřmi circuity. Dále uvádí 9/9 native/terminal, 2/2 absence, 24/24 komponentových,
62/62 snapshot/read, 39/39 původních Sharing consumer případů a 6/6 isolation kontrol.
Výběry nesčítám jako unikátní testy. Jde o výsledky Codexu; raw TRX/logy/image manifesty nebyly
předané a zde jsem testy neopakovala.

Nativní browserový kód opravdu zachycuje původní modely před úpravami a používá UI publikace/importu.
Zpráva zaznamenává 10/10/3/5 modelů v příslušných čtyřech katalozích a dvanáct default/non-default
chatových volání. Tyto počty nejsou nové pevné očekávání budoucích verzí. Názvy se mají porovnávat
s nezávislým snapshotem, zatímco interní sp1 identifikátory musí dál určovat správné směrování.
To však ještě neuzavírá Agent/Simple Chat/Workflow selectory, image/vision obsah, rotaci credentials,
History ani obraz po budoucí extrakci. Dokumentace tyto mezery správně přiznává.

## Další práce

Nové zadání nejprve ověří návaznost a opraví PP2C-R1, potom vyčlení skutečné Sharing a Sources
renderery a reusable Refresh. Přidá samostatný sandbox se stejnými komponentami a nakonec znovu
provede kontejnerové a aplikační cesty nad finálním image. Celou nezměněnou S0 kampaň není potřeba
zopakovat jen kvůli dalšímu vstupnímu reportu. Závěrečná kampaň po extrakci zůstává povinná.

Request History a další provozní dialogy se zachovají jako integrace a připraví až pro další řez.
Dokončené Workspace, Projects a Agent Editor se nemají znovu plošně refaktorovat.

## Omezení a commity

Pouze large desktop 1920×1080, žádné mobilní/tabletové ladění. Podpis existujícím PGP klíčem přes
native pinentry, stejný gpg-agent/prostředí pro větší checkpointy, lokální ověření každého podpisu.
Žádné heslo/klíč v chatu či logu, unsigned fallback, push/merge nebo úklid archivních bundles.
Původní placený limit 40/40 se neresetuje; externí modely lze simulovat jen za skutečnou produkční
cestou nástrojů, oprávnění a persistence.

V prostředí revize nebyl dostupný dotnet ani Docker. Provedla jsem zdrojovou kontrolu a kontrolu
předávacího balíčku; neprovedla jsem nové produktové, browserové ani watch testy. Seznam zdrojů
rozlišuje celé soubory a čtené rozsahy. Pomocné validátory kontrolují pouze strukturu a konzistenci
dodaných údajů, nikoli pravdivost výsledků aplikace.
