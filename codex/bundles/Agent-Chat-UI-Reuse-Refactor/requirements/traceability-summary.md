# Traceability summary

The authoritative machine-readable requirement ownership is in:

- `requirements/requirements.json`
- `traceability/requirements-to-subbundles.json`
- `manifest.json`

Rules:

1. a subbundle may close only its owned requirements;
2. a requirement shared by multiple subbundles closes only after its final owner and checkpoint pass;
3. a later failure reopens every earlier requirement whose proof it invalidates;
4. UIR-003, UIR-062, and UIR-063 are negative phase constraints and require source/diff evidence, not only narrative confirmation;
5. UIR-080 and UIR-081 prevent automatic progression into the Simple Chat UI phase.
