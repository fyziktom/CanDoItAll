# Release readiness checklist

## Build & packages
- [ ] Server buildí na čistém checkoutu.
- [ ] Použité package verze jsou zafixované nebo řízené solution policy.
- [ ] Nejsou přítomné zbytečné preview dependency bez důvodu.
- [ ] Projekt je přidaný do solution.

## Functionality
- [ ] Všechny MVP tooly jsou implementované.
- [ ] `workspace_info` dává klientovi použitelná metadata.
- [ ] `app_start` / `app_stop` / `app_wait` fungují na reálném CanDoItAll projektu.
- [ ] build/test workflow nevyžadují ruční stop/start.
- [ ] recovery scénáře jsou ověřené.

## Safety
- [ ] stdout discipline test je green.
- [ ] Path guard test je green.
- [ ] log redaction test je green.
- [ ] stale cleanup test je green.
- [ ] `dotnet watch test` se nikde nepoužívá.

## Docs
- [ ] Tool contracts odpovídají implementaci.
- [ ] Konfigurace odpovídá implementaci.
- [ ] Runbook je aktualizovaný.
- [ ] Prompts odpovídají finálním tool names a flow.
- [ ] Known risks jsou aktualizované.

## Operational readiness
- [ ] Výchozí nastavení dává smysl pro CanDoItAll.
- [ ] Log folder a registry path jsou excluded z watch.
- [ ] Cleanup on startup je otestovaný.
- [ ] Health endpoint a URL konfigurace jsou ověřené.
- [ ] Build/test timeouty jsou realistické.

## Review gate
- [ ] Prošla architektura checklist.
- [ ] Prošla implementation checklist.
- [ ] Prošla testing checklist.
- [ ] Nezbývá žádný otevřený blocker P0/P1.
