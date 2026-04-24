# Acceptance Metrics

## Build metrics

- Firmware default build: pass.
- Firmware ADC build: pass.
- Host build: pass.
- Host tests: pass.

## Protocol metrics

- v1.03 valid fixture parses: pass.
- v1.03 corrupted CRC rejects: pass.
- v1.04 valid fixture parses if implemented: pass.
- Unknown source value handled safely: pass.

## Navigation metrics

| Metric | Target |
|---|---:|
| Pan total delta, 50 Hz vs 100 Hz synthetic 1-second input | Difference < 10% |
| Zoom final factor, 50 Hz vs 100 Hz synthetic 1-second input | Difference < 10% |
| Orbit total delta, 50 Hz vs 100 Hz synthetic 1-second input | Difference < 10% |
| Idle jitter pan equivalent | < 1 px/sec |
| Idle jitter zoom equivalent | No visible zoom drift |
| Settings profile switch | Filter resets safely |

## Firmware serial metrics

| Metric | Target |
|---|---:|
| `get json` command | Returns valid JSON-like line |
| `set orientation game` | Active source becomes no-mag game RV if supported |
| `set orientation rotation` | Active source becomes mag-assisted RV |
| Unsupported mode | Clear error, no crash |
| 5-minute idle queue drops | Near zero; investigate if > 1% of emitted samples |
| Watchdog resets | 0 |
