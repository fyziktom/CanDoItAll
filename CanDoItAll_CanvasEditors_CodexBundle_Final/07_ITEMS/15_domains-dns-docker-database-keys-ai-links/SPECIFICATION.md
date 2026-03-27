
# Specification

## Item identity

- **Item ID:** I15
- **Title:** Domains, DNS, Docker, database, keys, and AI links
- **Origin:** docx
- **Dependencies:** I01, I14, I09

## Objective

Complete the infrastructure subtree with typed child nodes for domains, DNS, containers, databases, deployment folders, keys, and AI-related references.

## Normalized scope

Add infrastructure-adjacent child nodes for connected domains, DNS records, docker mode, proxy provider, database info, deployment folder, keys, and AI links including ChatGPT, Codex, and local LLM references.

### In scope

- Domain name and owner nodes or metadata.
- DNS record nodes.
- Docker type and proxy provider representation.
- Database, deployment folder, and key references.
- AI links including ChatGPT conversation link, Codex thread link, and local LLM reference.

### Out of scope

- Automated DNS management or container orchestration execution.

## Key implementation decisions

- Prefer typed child nodes beneath infrastructure roots over squeezing everything into one giant server card.
- Reuse resource-like concepts such as DockerCompose, Ssh, SecretLink, or PromptLink where that reduces duplication.
- AI links are references and context anchors, not embedded conversations.

## Implementation tasks

- Define typed child nodes or metadata shapes for the requested infrastructure concepts.
- Ensure the server subtree stays readable and navigable.
- Add node editors and concise card summaries for the new infrastructure children.

## Risks to control

- Server nodes turn into unmaintainable mega-forms if child decomposition is skipped.

## Covered original notes

- N111 — Connected Domains
- N112 — Domain name
- N113 — Owner
- N114 — DNS Records
- N115 — Docker type (compose vs swarm)
- N116 — Proxy provider
- N117 — Nginx, traefik, etc.
- N118 — Database
- N119 — Type, connection
- N120 — Deployment folder
- N121 — Keys
- N122 — AI
- N123 — ChatGPT conversation link
- N124 — Codex thread link
- N125 — Local LLM
