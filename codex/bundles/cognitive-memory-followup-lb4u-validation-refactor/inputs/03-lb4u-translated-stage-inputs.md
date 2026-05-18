# LB4U Translated Stage Inputs

These stages are prepared from the final LB4U files and translated into English summaries. Execution must keep links to the original source artifacts and must not overwrite or modify the LB4U folder.

## Stage 1 Product Discovery

- Source: `LB4U-BP.docx`
- Source: `2020-06-09-prezentace LB4U.pdf`
- Ingest as: project concept notes and opportunity framing.
- Summary: Existing nurse-call and care-call systems are described as outdated, expensive, and slow to deploy. LB4U is positioned as a fast, low-cost button system that saves staff time, protects health, and can be checked remotely. The core user need is reliable request signaling from a patient or bed to care staff devices.
- Expected memories: project identity, target users, pain points, intended value proposition, healthcare/care-facility context, and early assumptions.

## Stage 2 Basic Workflow And UX

- Source: `2020-06-09-prezentace LB4U.pptx`
- Source: `LB4U-BP.docx`
- Ingest as: workflow and user interface plan.
- Summary: A button press sends a signal to the server. Logged-in devices receive a notification and display which room/module triggered it and how many presses occurred. Staff can acknowledge the call. The web UI supports a floor-plan background, placed device icons, module naming, and operation from PC, tablet, or mobile browser. An Android app is also considered.
- Expected memories: user workflow, UI responsibilities, acknowledgement behavior, floor-plan asset handling, device/module mapping, and mobile/browser expectations.

## Stage 3 Technical Architecture And Installation

- Source: `LB4U-BP.docx`
- Source: `2020-06-09-prezentace LB4U.pdf`
- Ingest as: architecture, deployment, and runbook-like procedure.
- Summary: The button uses an M5Stack M5StickC/ESP32 module, Dual Button Unit, battery HAT, WiFi, and firmware. A local server is based on Raspberry Pi 3 B+ with Ubuntu Mate, a C# backend DLL, HTML/JavaScript Vue+Quasar frontend, MQTT broker, and Node-RED. Router hardware is Tenda AC10U. Installation steps include unpacking, plugging in router/server/modules, opening the server page, uploading floor images, placing modules, and naming them.
- Expected memories: hardware components, software stack, local network assumptions, installation procedure, deployment constraints, and maintenance facts.

## Stage 4 Procurement And Device Alternatives

- Source: `Alza nabídka Brano 21.4.xlsx`
- Source: `Alza nabídka Brano 27.4.xlsx`
- Ingest as: procurement evidence and planning assumptions.
- Summary: The spreadsheets include purchase quotations and device alternatives: USB-C and micro USB cables, USB hubs, chargers, Raspberry Pi cases, Tenda AC10U routers, Raspberry Pi 3 B+ units, tablets, phones, and rugged Android devices. These are evidence for BOM, pilot hardware, and client device planning.
- Expected memories: procurement items, candidate client devices, infrastructure components, quantities, cost-planning source references, and unresolved cost assumptions.

## Stage 5 Custom Button Engineering

- Source: `LB4U Vývoj vlastního tlačítka.pdf`
- Source: `LB4U Vývoj vlastního tlačítka.pptx`
- Source: `eagle5-11_lb4u_v02_doc\zadani_vyroby.txt`
- Ingest as: engineering requirements, design constraints, and manufacturing notes.
- Summary: Existing hardware is purchased, but the patient-facing button needs a custom safer design. Requirements include one button, possible switch/failure detection, piezo speaker for acknowledgement feedback, flexible cable, connector to the module, waterproof/disinfectable construction, cable strain relief, white medical appearance, and PCB manufacturing constraints.
- Expected memories: safety requirements, hardware design decisions, manufacturing parameters, waterproofing/disinfection constraints, feedback signal requirement, and unresolved engineering tests.

## Stage 6 Field Testing And Release

- Source: `2020-06-09-prezentace LB4U.pdf`
- Source: `LB4U-BP.docx`
- Ingest as: release plan and validation plan.
- Summary: The project planned testing in Opava hospital and FN Ostrava, final button development, serial production, and release. The presentation describes quick replacement, remote update, low-power server operation, and possible extensions.
- Expected memories: pilot customers, release milestones, extension ideas, maintenance expectations, risks, and open validation questions.

## Stage 7 Business Plan And Reusable Planning Knowledge

- Source: `LB4U-BP.docx`
- Ingest as: business plan artifact and consolidation trigger.
- Summary: The business plan combines product description, target customers, technical parameters, first release scope, development tasks, team, staging, timeline, and planned activities. Execution must let cognitive memory derive reusable knowledge about business plans, marketing planning, staffing, cost planning, procurement, and release staging through normal consolidation and review cycles.
- Expected memories: LB4U-specific plan facts plus candidate cross-project knowledge, such as what sections a business plan should contain, how marketing activity planning is represented, how expense and salary assumptions should be modeled, and how project release plans link to procurement and staffing.

## Stage 8 Probing And Study Loop

- Source: cognitive memory API probe, recall, consolidation, review, and epistemic-drive endpoints.
- Ingest as: conversational probing and review decisions.
- Summary: Ask memory what it knows, inspect gaps, request deeper study, approve useful generated recommendations, reject weak or ungrounded proposals, and rerun consolidation. This stage must record whether the system improves without manually injecting the desired answer.
- Expected memories: improved canonical facts, provenance-backed recalls, candidate cross-project knowledge, gap proposals, review decisions, and regression evidence.
