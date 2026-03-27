
# Specification

## Item identity

- **Item ID:** I10
- **Title:** Script nodes and terminal execution surface
- **Origin:** docx
- **Dependencies:** I01, I09

## Objective

Model executable scripts cleanly and provide a realistic terminal experience inside the web app instead of assuming native terminal launch from a browser.

## Normalized scope

Add PowerShell script and console script nodes plus an in-app, manager-backed terminal surface rooted to the working directory.

### In scope

- PowerShell and console script node families.
- Open terminal action rooted to the working directory.
- Integration points to runtime or manager-backed execution services.

### Out of scope

- A fully featured shell emulator with every terminal capability.

## Key implementation decisions

- Interpret Open terminal as an in-app terminal or command session, not a native OS terminal launch from the browser.
- Store working directory and command metadata explicitly on script nodes.
- Reuse manager/runtime helpers where possible for execution orchestration.

## Implementation tasks

- Add script node types and metadata.
- Create the open-terminal action and connect it to a safe runtime surface.
- Ensure the working directory and command path are visible in details.
- Provide enough execution feedback that users know what ran and where.

## Risks to control

- A fake Open terminal action that cannot work in-browser will destroy trust quickly.

## Covered original notes

- N075 — Scripts
- N076 — Add PS script
- N077 — Console script
- N078 — All with button to “Open terminal” (automatically in work folder)
