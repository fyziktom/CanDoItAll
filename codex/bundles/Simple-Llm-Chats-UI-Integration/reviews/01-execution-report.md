# Execution Report

## Subbundle Gate Results

| Subbundle | Tier | Entry | Closure | Progression |
|---|---|---|---|---|
| SB01 | Standard | Pass | Pass | CP0 passed; SB02 unlocked |
| SB02 | Behavioral | Pass | Pass | SB03 unlocked; CP1 remains locked |
| SB03 | Behavioral | Pass | Pass | SB04 unlocked; CP1 remains locked |
| SB04 | Governed | Pass | Pass | SB05 unlocked; CP1 remains locked |
| SB05 | Governed | Pass | Pass | CP1 passed; Simple Chat UI activation and SB06 unlocked |
| SB06 | Behavioral | Pass | Pass | SB07 unlocked; route remains unadvertised |

## Browser Analytics

| Subbundle | Viewport | Normal state | Open overlays | First viewport | Scroll owner | Result |
|---|---|---|---|---|---|---|
| SB01 | N/A | N/A | N/A | N/A | N/A | No visible change |
| SB02 | N/A | N/A | N/A | N/A | N/A | No browser-visible change owned |
| SB03 | N/A | N/A | N/A | N/A | N/A | Browser activation forbidden |
| SB04 | N/A | N/A | N/A | N/A | N/A | No browser-visible change owned |
| SB05 | 1600x1000 | Agent catalog and main chat pass | Settings, floating chat, close decision, and contextual catalog pass | Primary actions and composers visible | Transcript owns inner scroll | Pass |
| SB06 | N/A | N/A | N/A | N/A | N/A | No user-visible activation |

## Current Decision

CP1 passed. SB06 established the Simple Chat UI boundary without route activation. Execution is active at SB07; floating Simple Chat integration remains locked until CP2.
