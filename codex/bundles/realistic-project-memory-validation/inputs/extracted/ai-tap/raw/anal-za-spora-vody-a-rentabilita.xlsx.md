# Extracted Source: Analýza\úspora vody a rentabilita.xlsx

- Source path: `C:\repositories\CanDoItAll\codex\bundles\input\AI kohoutek\Analýza\úspora vody a rentabilita.xlsx`
- Source kind: `xlsx`
- Project: `AI Tap Intelligent Water Faucet`

### Sheet: List1
- Used range: 22 rows x 11 columns; non-empty cells: 83; formulas: 19.

| Cell | Value | Formula | Format |
| --- | --- | --- | --- |
| D4 | úspora při mytí nádobí |  | General |
| E4 | 10 |  | General |
| F4 | l/os |  | General |
| H4 | odběr proudu ve sleepu |  | General |
| I4 | 0.05 |  | General |
| J4 | A |  | General |
| D5 | počet dní mytí v roce |  | General |
| E5 | 365 | =365 | General |
| F5 | dní |  | General |
| H5 | napětí |  | General |
| I5 | 24 |  | General |
| J5 | V |  | General |
| D6 | počet litrů za rok |  | General |
| E6 | 3650 | =E5*E4 | General |
| F6 | /os |  | General |
| H6 | příkon |  | General |
| I6 | 1.2000000000000002 | =I5*(I4) | General |
| J6 | W |  | General |
| D8 | 3 členná rodina |  | General |
| E8 | 10950 | =E6*3 | #,##0 |
| F8 | l/rodina/rok |  | General |
| H8 | spotřeba za den |  | General |
| I8 | 27.960000000000004 | =I6*23.3 | General |
| J8 | Wh/den |  | General |
| H9 | spotřeba za rok |  | General |
| I9 | 10205.400000000001 | =I8*365 | General |
| J9 | Wh/rok |  | General |
| D10 | cena vody |  | General |
| E10 | 120 |  | General |
| F10 | Kč/1000l |  | General |
| H10 | spotřeba za rok |  | General |
| I10 | 10.205400000000001 | =I9/1000 | General |
| J10 | kWh/rok |  | General |
| D11 | úspora |  | General |
| E11 | 1314 | =E8/1000*E10 | General |
| F11 | Kč/rok |  | General |
| H11 | cena za kWh |  | General |
| I11 | 5 |  | General |
| J11 | kč/kWh |  | General |
| H12 | cena za elektriku ročně |  | General |
| I12 | 51.027 | =I10*I11 | General |
| J12 | Kč/rok |  | General |
| D13 | cena baterie bez dotace |  | General |
| E13 | 4600 | =4600 | General |
| F13 | Kč/ks |  | General |
| D14 | návratnost |  | General |
| E14 | 3.8280601804347842 | =E13/(E11-I12-I22) | 0.00 |
| F14 | let |  | General |
| H14 | odběr proudu při mytí |  | General |
| I14 | 2 |  | General |
| J14 | A |  | General |
| H15 | napětí |  | General |
| I15 | 24 |  | General |
| J15 | V |  | General |
| H16 | příkon |  | General |
| I16 | 48 | =I15*(I14) | General |
| J16 | W |  | General |
| H18 | spotřeba za mytí |  | General |
| I18 | 33.599999999999994 | =I16*0.7 | General |
| J18 | Wh/den |  | General |
| D19 | počet domáctností |  | General |
| E19 | 1000000 |  | #,##0 |
| H19 | spotřeba za rok |  | General |
| I19 | 12263.999999999998 | =I18*365 | General |
| J19 | Wh/rok |  | General |
| D20 | roční úspora vody |  | General |
| E20 | 10950000000 | =E8*E19 | #,##0 |
| F20 | l vody za rok |  | General |
| H20 | spotřeba za rok |  | General |
| I20 | 12.263999999999998 | =I19/1000 | General |
| J20 | kWh/rok |  | General |
| D21 | roční úspora vody |  | General |
| E21 | 10950000 | =E20/1000 | #,##0 |
| F21 | m3 vody za rok |  | General |
| H21 | cena za kWh |  | General |
| I21 | 5 |  | General |
| J21 | kč/kWh |  | General |
| D22 | roční úspora cena |  | General |
| E22 | 1201653000 | =E10*E21-(I12+I22)*E19 | _-* #,##0\ "Kč"_-;\-* #,##0\ "Kč"_-;_-* "-"??\ "Kč"_-;_-@_- |
| F22 | Kč/rok/1mil.domácností |  | General |
| H22 | cena za elektriku ročně |  | General |
| I22 | 61.319999999999986 | =I20*I21 | General |
| J22 | Kč/rok |  | General |
