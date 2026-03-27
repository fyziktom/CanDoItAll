
# Specification

## Item identity

- **Item ID:** I03
- **Title:** Meeting nodes for online and onsite work
- **Origin:** docx
- **Dependencies:** I01, I02

## Objective

Add meeting nodes with real metadata for online and onsite scenarios and surface them naturally in the canvas and calendar flows.

## Normalized scope

Implement meeting blocks, meeting nodes, channel/address/repeating metadata, meeting-specific actions, and calendar integration for online and onsite meetings.

### In scope

- Meeting block and meeting node creation.
- Online meeting details including channel enum and repeat rules.
- Onsite meeting details including address and map link behavior.
- Meeting actions such as Add blocks, Add Tasks, Add progress, Add priority, Add Recording.

### Out of scope

- Full meeting synchronization with external calendar providers.

## Key implementation decisions

- Use one Meeting node family with a mode subtype or metadata field instead of separate disconnected models.
- Leverage StartUtc and EndUtc integration with the existing project calendar where possible.
- Represent repeating cadence as normalized metadata rather than free-form text.

## Implementation tasks

- Add meeting-specific metadata fields and editors.
- Expose online channel options such as MSTeams, Google Meet, Zoom, WhatsApp, and Telegram.
- Implement onsite address behavior with map link support.
- Integrate repeating metadata into meeting details and any schedule-related views.
- Ensure meeting actions appear only where they make sense.

## Risks to control

- Inconsistent time handling if repeat rules and calendar projection are implemented separately.

## Covered original notes

- N009 — Meetings
- N010 — Meetings
- N011 — Meeting block
- N012 — Online
- N013 — Channel (enum MSTeams, Google Meet, Zoom, WhatsApp, Telegram)
- N014 — Date
- N015 — Repeating (enum per day, per week, per 2 weeks, per month)
- N016 — RightClick Menu Options
- N017 — Add blocks
- N018 — Add Tasks
- N019 — Add progress
- N020 — Add priority
- N021 — Add Recording
- N022 — Onsite
- N023 — Address
- N024 — Click to google maps
- N025 — Date
- N026 — Repeating and right click same as online
