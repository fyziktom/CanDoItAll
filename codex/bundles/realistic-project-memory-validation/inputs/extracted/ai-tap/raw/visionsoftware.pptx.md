# Extracted Source: VisionSoftware.pptx

- Source path: `C:\repositories\CanDoItAll\codex\bundles\input\AI kohoutek\VisionSoftware.pptx`
- Source kind: `pptx`
- Project: `AI Tap Intelligent Water Faucet`

## Slide 1
- Poptávka vývoje SW
- Systém pro rozpoznání nádobí a rukou
- Důvěrné

## Slide 2
- Cíle vývoje
- Vytvořit software pro rozpoznání základních druhů nádobí, v první etapě musí umět rozeznat:
- Talíř malý
- Talíř velký
- Miska
- Sklenice
- Hrnek
- Lžička/lžíce
- Příbor ostatní (vidlička, nůž, apod.)
- Dále systém musí umět rozpoznat lidské ruce
- Kontrola bude probíhat v intervalech 0,25s (pokud je nereálné, tak max 0,5s)
- Při každé kontrole pošle zařízení
- info
- do MQTT
- topicu
- .
- Payload
- bude JSON s identifikací předmětu, jeho pozicí a relativní velikostí (nebo velikostí v
- px
- ).

## Slide 3
- Další požadavky
- Musí jít spustit na Linux (ideálně i na ARM CPU)
- Jako zdroj obrazových dat bude použita web kamera (můžeme dodat, jinak jakákoliv standardní
- webcam
- )
- Obraz nemusí být barevný
- Kód by měl být finálně vytvořen v C# (.NET
- Core
- )
- Pro zjednodušení lze nyní použít i Python (pokud by však rozdíl času nebyl markantní, tak je preferován C#)
- Nesmí být použity knihovny pod licencí GNU/GPL
- Lze použít jen knihovny licencované free licencemi typu MIT, BSD, apod.
