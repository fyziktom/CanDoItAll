
# Specification

## Item identity

- **Item ID:** I14
- **Title:** Remote server core model
- **Origin:** docx
- **Dependencies:** I01, I10

## Objective

Model remote server infrastructure as a structured canvas node with technical, commercial, and access-related metadata.

## Normalized scope

Add remote server nodes with capacity, price, address, provider links, login links, SSH, secret references, and account identity.

### In scope

- Remote server node metadata and editor.
- Capacity and business metadata.
- Provider and login links.
- SSH and secret-link metadata.

### Out of scope

- Direct secret value editing inside the canvas.

## Key implementation decisions

- Treat remote server as a structured infrastructure node family.
- Reference secrets through secure links or secret references instead of storing credentials inline.
- Keep provider website and login links separate from SSH connection metadata.

## Implementation tasks

- Add remote server node family and structured metadata fields.
- Support provider website and login links as explicit properties or linked child nodes.
- Link SSH connection details and secret references safely.
- Expose concise capacity and business information on the card or details view.

## Risks to control

- Infrastructure nodes become unreadable if field grouping is not designed carefully.

## Covered original notes

- N100 — Remote Server (common block)
- N101 — Parameters
- N102 — CPU, RAM, HDD/SSD cap, etc.
- N103 — Price and business related info
- N104 — Address
- N105 — Provider
- N106 — Link to provider website
- N107 — Link to login
- N108 — SSH connection (we need terminal component)
- N109 — Connection to secret for login
- N110 — Account name
