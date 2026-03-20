# Ops runbook checklist

## Bootstrap
- [ ] Je nainstalovaný kompatibilní .NET 10 SDK.
- [ ] V workspace existuje platná konfigurace serveru.
- [ ] Dev certifikát pro localhost HTTPS je důvěryhodný, pokud se používá HTTPS health.
- [ ] `.mcp-state` a další pomocné složky jsou excluded z watch.

## Běžný provoz
- [ ] Server lze spustit bez stdout noise.
- [ ] `workspace_info` ukazuje správný startup project.
- [ ] App session jde startnout a stopnout.
- [ ] Logy jsou dohledatelné v souboru i přes MCP tools.
- [ ] Cleanup stale procesů funguje.

## Troubleshooting
- [ ] Je zdokumentovaný postup pro port conflict.
- [ ] Je zdokumentovaný postup pro health timeout.
- [ ] Je zdokumentovaný postup pro missing SDK.
- [ ] Je zdokumentovaný postup pro locky binárek.
- [ ] Je zdokumentovaný postup pro watch nereagující na změny.

## Maintenance
- [ ] Logy mají rozumnou retenční politiku.
- [ ] Process registry je periodicky nebo bootstrapem čištěná.
- [ ] Tool contracts a prompts odpovídají aktuální implementaci.
