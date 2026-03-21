# Checklist implementace

- [ ] Řešení buildí na `net10.0`.
- [ ] Všechny komentáře ve zdrojových kódech jsou anglicky.
- [ ] Všechny veřejné tool DTO mají validaci.
- [ ] Každý tool vrací standardní response envelope.
- [ ] Každý mutující tool vrací `correlationId`.
- [ ] Dlouhé operace vrací `operationId`.
- [ ] `operation_status`, `operation_wait`, `operation_logs` fungují konzistentně.
- [ ] Upload bundle probíhá atomicky.
- [ ] Backup a restore mají audit trail.
- [ ] Compose apply nejprve dělá validate/checkpoint.
- [ ] Probe tooly umí timeout a retry.
- [ ] Logy procházejí redakcí.
- [ ] Exceptions se mapují na stabilní error codes.
- [ ] Žádný tool nevyžaduje interaktivní shell.
- [ ] Žádný tool nepředává neomezený shell string bez policy vrstvy.
- [ ] Konfigurační klíče a env refs jsou zdokumentované.
- [ ] README / docs v repu odkazují na tento balík.
