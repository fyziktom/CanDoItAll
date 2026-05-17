import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const bundleRoot = path.resolve(scriptDir, "..", "..");
const stagedRoot = path.join(bundleRoot, "sample-data", "staged-sources");
const trackerDir = path.join(bundleRoot, "sample-data", "trackers");

const stages = [
  {
    id: "S01",
    dir: "stage-01-baseline-detail",
    name: "Baseline detail",
    cycle: "Initial ingestion and first forced consolidation cycle",
    purpose: "Seed durable project context, actors, architecture, risks, and expected source-backed memories."
  },
  {
    id: "S02",
    dir: "stage-02-operational-updates",
    name: "Operational updates",
    cycle: "Second forced ingestion and consolidation cycle",
    purpose: "Add realistic updates that should extend existing memories instead of creating unrelated duplicates."
  },
  {
    id: "S03",
    dir: "stage-03-contradictions-and-decisions",
    name: "Contradictions and decisions",
    cycle: "Third forced ingestion and consolidation cycle",
    purpose: "Introduce conflicts, replacements, and explicit decisions so review items and duplicate handling can be evaluated."
  },
  {
    id: "S04",
    dir: "stage-04-email-and-instructions",
    name: "Emails and instructions",
    cycle: "Fourth forced ingestion and consolidation cycle plus chat validation",
    purpose: "Load email-style Markdown assets and operating instructions to test source attribution, chunking, and recall."
  }
];

const projects = [
  {
    key: "fieldops-mobile",
    name: "FieldOps Mobile App",
    domain: "offline-first field service application",
    owner: "Marta Novak, Field Operations Director",
    baseline: [
      "Technicians work in cellars, construction sites, and rural customer locations where connectivity is intermittent.",
      "The backend work-order API is the canonical source for work-order state; the mobile client owns a local mutation queue until sync succeeds.",
      "Every queued mutation must carry an idempotency key, device id, technician id, local timestamp, server receipt timestamp, and conflict policy.",
      "Photo evidence is part of the inspection record and must remain retryable until the backend confirms object-storage persistence.",
      "Supervisor review requires a visible audit trail of changed checklist answers, rejected photos, and conflict decisions."
    ],
    update: [
      "The pilot team added barcode scanning for asset identity because manual asset selection caused six wrong-equipment inspections in dry runs.",
      "Two technicians asked for a compact route-day view that groups work orders by customer site, not only by scheduled time.",
      "The sync queue must show retry backoff and the exact item blocking upload because hidden retries made the first pilot look frozen.",
      "Logs must mask customer names, addresses, and photo filenames while preserving queue age, retry count, and endpoint failure category."
    ],
    contradiction: [
      "Product originally suggested last-write-wins for conflicts, but operations now rejects it for safety checklist answers.",
      "A vendor proposed storing original photos only on the device for bandwidth savings; compliance rejected this because evidence retention must be centralized.",
      "The route-day view is useful, but it cannot become the canonical work-order sequence because dispatch remains responsible for priorities."
    ],
    decision: "Use server-reviewed conflict resolution for safety fields, keep idempotent client mutation envelopes, and persist confirmed photos centrally with retry-visible uploads.",
    emails: [
      {
        subject: "Pilot incident: missing photo after cellar inspection",
        from: "marta.novak@fieldops.example",
        body: "Technician R-17 completed the pump inspection offline. The checklist synced, but two photos stayed local and the supervisor did not see the failure until the next morning. Treat photo upload status as a first-class task state, not as a hidden background detail."
      },
      {
        subject: "Instruction: conflict review wording",
        from: "qa.lead@fieldops.example",
        body: "When the app detects conflicting safety answers, show both technician values, the server value, timestamps, and the supervisor decision. Do not summarize a conflict as a generic sync warning."
      }
    ]
  },
  {
    key: "knowledgeops-dashboard",
    name: "KnowledgeOps Dashboard",
    domain: "support engineering incident knowledge dashboard",
    owner: "Irena Malik, Support Engineering Manager",
    baseline: [
      "The dashboard combines incident intake, customer impact, runbook lookup, timeline notes, action tracking, and postmortem learning proposals.",
      "Incident status is canonical; runbook recommendations and memory-backed suggestions are advisory until an operator acts.",
      "Operators need dense lists, keyboard navigation, status badges, source citations, and visible staleness warnings.",
      "Official runbooks, generated summaries, recent incidents, and postmortems must be visually distinct.",
      "Every memory-backed suggestion must cite the runbook, incident, email, or postmortem source that caused it."
    ],
    update: [
      "The on-call group added a severity downgrade path for incidents where customer impact is confirmed lower than initially reported.",
      "Runbook rank must penalize stale runbooks when the service version in the incident differs from the version mentioned in the runbook.",
      "Operators want a single-key action to copy the suggested mitigation with citations into the incident timeline.",
      "Postmortem lessons should create review-gated learning proposals rather than directly mutating official runbooks."
    ],
    contradiction: [
      "One team wants generated summaries to appear above official runbooks; support leadership rejected that ordering for high severity incidents.",
      "An old design treated repeated customer reports as duplicates, but the current rule keeps them separate when regions or product tiers differ.",
      "A proposed auto-close action conflicts with the requirement that incident ownership stays human-controlled."
    ],
    decision: "Rank official current-version runbooks first for high severity incidents, keep generated summaries advisory, and record postmortem learning as review-gated proposals.",
    emails: [
      {
        subject: "Runbook staleness caused wrong mitigation",
        from: "irena.malik@support.example",
        body: "The dashboard suggested a restart sequence for API Gateway 3.1 during an API Gateway 4.0 incident. The recommendation was plausible but stale. Memory must preserve version context and the source date."
      },
      {
        subject: "Instruction: timeline citation copy",
        from: "ops.tooling@support.example",
        body: "When copying a suggested mitigation into the incident timeline, include source title, runbook version, and confidence label. Do not paste uncited memory text."
      }
    ]
  },
  {
    key: "clinicflow-saas",
    name: "ClinicFlow SaaS Business Plan",
    domain: "SaaS plan for small clinics",
    owner: "Elena Ruiz, Founder",
    baseline: [
      "The product targets clinics with 3 to 15 providers that struggle with waitlist triage, appointment reminders, and insurance document collection.",
      "The MVP includes patient intake links, waitlist ranking, staff task queue, reminder templates, and pilot reporting.",
      "The first market is private outpatient physiotherapy and occupational therapy practices, not hospitals.",
      "Pricing starts with a low per-location platform fee plus per-provider seats; enterprise contracting is explicitly out of MVP scope.",
      "Compliance posture requires consent tracking, minimum necessary data display, audit logs, and clear deletion workflow."
    ],
    update: [
      "Pilot interviews showed front-desk staff care more about reducing phone calls than optimizing clinical capacity metrics.",
      "The waitlist ranking model must be explainable with staff-editable factors such as urgency, provider fit, insurance readiness, and last contact date.",
      "The landing page should not promise automated clinical prioritization; the system supports administrative triage only.",
      "A partner clinic requested exportable monthly metrics for no-show reduction, reminder completion, and insurance-document readiness."
    ],
    contradiction: [
      "The first business plan mentioned automated clinical priority scoring, but legal review says this is not allowed in the MVP messaging.",
      "A pricing note suggested per-patient fees; pilots disliked that because it feels punitive when marketing succeeds.",
      "A hospital network inbound lead is tempting but conflicts with the small-clinic implementation focus."
    ],
    decision: "Position ClinicFlow as administrative intake and waitlist operations for small outpatient clinics, with explainable staff-controlled ranking and no clinical-priority automation claim.",
    emails: [
      {
        subject: "Pilot feedback: phone calls are the pain",
        from: "elena.ruiz@clinicflow.example",
        body: "The two pilot clinics both said capacity optimization sounds nice, but the actual buying trigger is fewer phone calls and fewer incomplete insurance packets. Memory should keep that buyer priority separate from long-term analytics ideas."
      },
      {
        subject: "Instruction: do not say clinical prioritization",
        from: "legal@clinicflow.example",
        body: "Replace clinical prioritization wording with administrative waitlist ranking. Staff must remain responsible for final decisions, and every ranking explanation must show editable administrative factors."
      }
    ]
  },
  {
    key: "docker-platform",
    name: "Docker Development Platform Analysis",
    domain: "developer platform and container workflow analysis",
    owner: "Tomas Krivan, Platform Lead",
    baseline: [
      "The platform standardizes local development with Docker Compose profiles for app, database, cache, object storage, and background workers.",
      "Production parity matters for dependencies and environment variables, but not for exact replica counts or production-scale resource limits.",
      "Build caching must be explicit: shared base images, locked package restore layers, and separate app build layers.",
      "CI evidence must include compose config validation, container health checks, migration dry run, and smoke request against the web app.",
      "Developers must be able to run only the dependencies they need instead of starting the entire product stack."
    ],
    update: [
      "The team added a lightweight profile for documentation and static analysis that does not start PostgreSQL or workers.",
      "Windows developers reported path-volume inconsistencies, so the platform now prefers named volumes for database and object-store state.",
      "The agent test profile must disable external email delivery and replace it with a local capture service.",
      "A build-cache benchmark showed dependency restore dominates cold starts, so the next optimization target is package-layer reuse."
    ],
    contradiction: [
      "A proposal to mirror production replica counts locally conflicts with laptop resource constraints and does not improve most debugging.",
      "A Dockerfile draft copies the full repository before package restore, which defeats restore-layer caching.",
      "One doc says SQLite is acceptable for agent workflow tests; current PostgreSQL-first policy rejects that for this memory validation path."
    ],
    decision: "Keep Compose profiles narrow, use PostgreSQL for agent and memory validation, prefer named volumes for stateful dependencies, and optimize restore-layer caching before app-layer tweaks.",
    emails: [
      {
        subject: "Compose profile scope",
        from: "tomas.krivan@platform.example",
        body: "Do not make the default profile start everything. The default should start app plus required dependencies. Workers, email capture, object storage, and observability are opt-in profiles."
      },
      {
        subject: "Instruction: PostgreSQL for agent-memory validation",
        from: "qa.platform@example",
        body: "All agent automation and cognitive-memory behavior tests must run on PostgreSQL. SQLite compatibility can be tested separately, but it is not the proof path for this bundle."
      }
    ]
  },
  {
    key: "regional-economy",
    name: "Regional Inflation And Small Business Economy Analysis",
    domain: "non-programming economic analysis",
    owner: "Nadia Patel, Regional Policy Analyst",
    baseline: [
      "The analysis separates observed indicators from scenarios and policy recommendations.",
      "Observed indicators include consumer prices, producer input costs, wage growth, rent, credit spreads, default rates, and business formation.",
      "Sectors must be analyzed separately: food service, retail, construction trades, healthcare services, and local logistics.",
      "Scenarios include base, persistent inflation, credit crunch, wage catch-up, and demand rebound.",
      "Policy options must state tradeoffs, eligible sectors, expected lag, and evidence quality."
    ],
    update: [
      "Local interviews indicate restaurants are reducing menu breadth while keeping headline prices stable to avoid customer churn.",
      "Construction trades report backlog softness, but repair and maintenance demand remains more resilient than new builds.",
      "Credit unions report more cautious underwriting for small-business equipment loans.",
      "Healthcare service providers report wage pressure but steadier demand than retail."
    ],
    contradiction: [
      "A draft conclusion said inflation is uniformly hurting all sectors; the source interviews show sector-specific effects.",
      "One scenario assumed immediate demand rebound after rate cuts, but lenders expect credit standards to loosen slowly.",
      "A policy memo proposed blanket grants, while the analysis favors targeted support tied to sector-specific constraints."
    ],
    decision: "Keep the analysis scenario-based and sector-specific; do not collapse interviews into a single inflation story or mix observed indicators with forecast assumptions.",
    emails: [
      {
        subject: "Interview note: restaurant menu breadth",
        from: "nadia.patel@region.example",
        body: "Owners say they are dropping low-margin items rather than raising every price. Memory should preserve this as a food-service-specific response, not as a general small-business rule."
      },
      {
        subject: "Instruction: separate observed facts from scenarios",
        from: "review.board@region.example",
        body: "When summarizing the economy analysis, label observed indicators, interview evidence, scenario assumptions, and recommendations separately. Do not present a scenario as a measured fact."
      }
    ]
  },
  {
    key: "community-learning",
    name: "Community Learning Program",
    domain: "non-programming adult education program",
    owner: "Samuel Brooks, Program Coordinator",
    baseline: [
      "The program offers short community sessions for budgeting basics, resume refresh, interview practice, account safety, and benefits navigation.",
      "Learners have uneven schedules, varied digital comfort, privacy concerns, and different confidence levels discussing money or job history.",
      "Partners include libraries, local employers, nonprofits, workforce offices, and volunteer facilitators.",
      "Delivery must be modular, plain-language, repeatable, and supported by printed handouts plus phone-friendly reminders.",
      "Evaluation measures attendance, confidence surveys, completed resumes, budget plans, referral follow-through, and consent boundaries."
    ],
    update: [
      "Library partners requested shorter 45-minute sessions because evening room availability is limited.",
      "Participants prefer examples using cash envelopes, prepaid cards, and shared family phones instead of bank-app-only examples.",
      "Employer partners want an optional mock interview station after resume sessions.",
      "The program added a privacy script explaining that facilitators do not need to see account balances or full job histories."
    ],
    contradiction: [
      "The first curriculum plan assumed 90-minute classes, but partner locations can reliably support 45 minutes.",
      "A donor suggested tracking individual income changes; program leadership rejected this as too invasive for the current trust model.",
      "A digital-only reminder plan conflicts with learners who share phones or prefer printed calendars."
    ],
    decision: "Use 45-minute modules, privacy-preserving evaluation, printed plus phone-friendly materials, and optional partner stations for resume and interview support.",
    emails: [
      {
        subject: "Library room schedule",
        from: "samuel.brooks@community.example",
        body: "The library can host us twice a month, but only for 45 minutes after closing setup. Please keep the curriculum modular and do not require a 90-minute sequence for a learner to benefit."
      },
      {
        subject: "Instruction: privacy script",
        from: "participant.support@community.example",
        body: "Facilitators must say that learners can discuss budgeting patterns without showing balances, account numbers, or complete job histories. Memory summaries should not imply that personal financial disclosure is required."
      }
    ]
  }
];

function bulletLines(items) {
  return items.map((item) => `- ${item}`).join("\n");
}

function mindmap(project, stage) {
  return [
    "```mermaid",
    "mindmap",
    `  root((${project.name}))`,
    `    ${stage.name}`,
    `      Domain: ${project.domain}`,
    `      Owner: ${project.owner}`,
    "      Durable facts",
    ...project.baseline.slice(0, 3).map((item) => `        ${item.replaceAll(":", " -")}`),
    "      Updates",
    ...project.update.slice(0, 2).map((item) => `        ${item.replaceAll(":", " -")}`),
    "      Decisions",
    `        ${project.decision.replaceAll(":", " -")}`,
    "```"
  ].join("\n");
}

function stageMarkdown(project, stage) {
  if (stage.id === "S01") {
    return `# ${stage.name}: ${project.name}

Source package: ${project.key}-${stage.id.toLowerCase()}
Project domain: ${project.domain}
Named owner: ${project.owner}
Intended ingestion: external Markdown file plus Markdown asset node in project structure
Expected consolidation behavior: create source-backed candidate memories for durable context, actors, risks, and boundaries.

## Project Context

${project.name} is a demo project used to evaluate whether Cognitive Memory stores source-grounded, useful memories rather than shallow or duplicated chunks. The source should be treated as a project-scoped document. It is not a generic article, and it should not be recalled for unrelated demo projects.

## Durable Facts To Preserve

${bulletLines(project.baseline)}

## Initial Validation Questions

- What is the canonical source of truth or governing boundary for this project?
- Which risks should be remembered as durable project risks?
- Which details should be summarized as project-specific context instead of global knowledge?
- Which facts must be attached to this source file and not to another project?

## Mindmap

${mindmap(project, stage)}

## Expected Memory Behavior

The first memory cycle should create a small set of focused memories: one project overview, two to four specific operational memories, and any high-risk boundary that should require review. It should not create one memory per sentence, and it should not merge this project with similarly named sources from other projects.
`;
  }

  if (stage.id === "S02") {
    return `# ${stage.name}: ${project.name}

Source package: ${project.key}-${stage.id.toLowerCase()}
Project domain: ${project.domain}
Named owner: ${project.owner}
Intended ingestion: external Markdown file plus project-structure update node
Expected consolidation behavior: update or extend existing memories where topics match, and create new candidates only for materially new facts.

## Operational Updates

${bulletLines(project.update)}

## How These Updates Relate To Stage 01

The updates refine the baseline. They should not erase the original context. A good memory cycle should connect these facts to the existing project memories by topic: product scope, risks, operations, architecture, evidence, or evaluation. Duplicates should be detected when an update restates a Stage 01 fact with only wording changes.

## Expected Duplicate And Merge Checks

- If an update repeats a Stage 01 source fact, the review queue should show it as duplicate, reinforcement, or low-priority update rather than a new independent memory.
- If an update narrows scope, the resulting memory should mention the narrowed boundary and cite both the baseline and update source where useful.
- If the system cannot decide between update and new memory, the review item should expose enough source text for a human decision.

## Mindmap

${mindmap(project, stage)}
`;
  }

  if (stage.id === "S03") {
    return `# ${stage.name}: ${project.name}

Source package: ${project.key}-${stage.id.toLowerCase()}
Project domain: ${project.domain}
Named owner: ${project.owner}
Intended ingestion: conflict/decision Markdown file, then forced consolidation and review.
Expected consolidation behavior: create reviewable contradiction or decision candidates and keep obsolete claims distinguishable from accepted decisions.

## Conflicts Introduced

${bulletLines(project.contradiction)}

## Resolution Decision

${project.decision}

## Review Expectations

- The contradiction candidates must show the old claim, the new conflicting claim, and the deciding source.
- The review queue should not silently overwrite earlier memory.
- After approval, recall should prefer the resolved decision while still being able to explain that an older source was superseded.
- If the system produces near-duplicate candidates for the same contradiction, record them in the duplicate analysis sheet and approve only the best source-backed candidate.

## Mindmap

${mindmap(project, stage)}
`;
  }

  const emailBlocks = project.emails.map((email, index) => `## Email ${index + 1}: ${email.subject}

From: ${email.from}
To: ${project.owner.toLowerCase().replaceAll(" ", ".")}@demo.example
Project: ${project.name}
Message:

${email.body}`).join("\n\n");

  return `# ${stage.name}: ${project.name}

Source package: ${project.key}-${stage.id.toLowerCase()}
Project domain: ${project.domain}
Named owner: ${project.owner}
Intended ingestion: Markdown email bundle as a project asset node and as an external file.
Expected consolidation behavior: preserve email-specific facts with source attribution and do not turn instructions into unsupported project facts.

${emailBlocks}

## Operator Instruction For Memory Review

- Treat email messages as source evidence with sender, subject, and stage.
- Approve durable facts only when they are useful for later project work.
- Reject or mark needs-changes for vague reminders, one-off scheduling chatter, or facts that duplicate a stronger source.
- During chat validation, ask one question that requires this email packet and one question that should ignore this email packet.

## Mindmap

${mindmap(project, stage)}
`;
}

function expectedSignals(project, stage) {
  if (stage.id === "S01") {
    return `overview; durable facts; source-scoped project context; ${project.baseline.slice(0, 2).join("; ")}`;
  }

  if (stage.id === "S02") {
    return `topic extension; merge/duplicate check; operational update; ${project.update.slice(0, 2).join("; ")}`;
  }

  if (stage.id === "S03") {
    return `contradiction; superseded claim; accepted decision; ${project.decision}`;
  }

  return `email source attribution; instruction handling; sender/subject preservation; ${project.emails.map((email) => email.subject).join("; ")}`;
}

function chatQuestion(project, stage) {
  if (stage.id === "S01") {
    return `For ${project.name}, summarize the core source-of-truth boundary and two durable risks.`;
  }

  if (stage.id === "S02") {
    return `What changed for ${project.name} after the operational update, and which existing memory should it update rather than duplicate?`;
  }

  if (stage.id === "S03") {
    return `Which earlier assumption for ${project.name} was contradicted, and what decision should the memory prefer now?`;
  }

  return `Which email-specific instruction for ${project.name} should affect future work, and what should not be overgeneralized?`;
}

const manifestRows = [];

await fs.mkdir(stagedRoot, { recursive: true });
await fs.mkdir(trackerDir, { recursive: true });

for (const stage of stages) {
  const stageDir = path.join(stagedRoot, stage.dir);
  await fs.mkdir(stageDir, { recursive: true });

  for (const project of projects) {
    const fileName = `${project.key}-${stage.id.toLowerCase()}.md`;
    const absolutePath = path.join(stageDir, fileName);
    await fs.writeFile(absolutePath, stageMarkdown(project, stage), "utf8");
    manifestRows.push({
      sourceId: `${project.key}-${stage.id}`,
      projectKey: project.key,
      projectName: project.name,
      stageId: stage.id,
      stageName: stage.name,
      cycle: stage.cycle,
      relativePath: path.relative(bundleRoot, absolutePath).replaceAll("\\", "/"),
      absolutePath,
      intendedLoad: stage.id === "S04" ? "External file and Markdown asset node" : "External file and project structure Markdown asset node",
      expectedSignals: expectedSignals(project, stage),
      expectedChatQuestion: chatQuestion(project, stage),
      approvalGuidance: stage.id === "S03" ? "Approve the resolved decision; mark obsolete or duplicate candidates explicitly." : "Approve durable, source-backed memories; reject vague or duplicate candidates.",
      qualityChecks: "Correct project scope; correct source locator; no cross-project merge; useful summary; duplicate handling recorded"
    });
  }
}

await fs.writeFile(
  path.join(bundleRoot, "sample-data", "source-manifest.json"),
  JSON.stringify({ generatedAtUtc: new Date().toISOString(), stages, projects: projects.map(({ key, name, domain, owner }) => ({ key, name, domain, owner })), sources: manifestRows }, null, 2),
  "utf8"
);

const workbook = Workbook.create();
const manifestSheet = workbook.worksheets.add("Source Manifest");
const cycleSheet = workbook.worksheets.add("Cycle Plan");
const chatSheet = workbook.worksheets.add("Chat Probes");
const analysisSheet = workbook.worksheets.add("Memory Analysis");
const repairSheet = workbook.worksheets.add("Repair Log");

const manifestHeaders = [
  "Source ID",
  "Project Key",
  "Project Name",
  "Stage",
  "Stage Name",
  "Cycle",
  "Relative Path",
  "Intended Load",
  "Expected Memory Signals",
  "Expected Chat Question",
  "Approval Guidance",
  "Quality Checks"
];

manifestSheet.getRange(`A1:L1`).values = [manifestHeaders];
manifestSheet.getRange(`A2:L${manifestRows.length + 1}`).values = manifestRows.map((row) => [
  row.sourceId,
  row.projectKey,
  row.projectName,
  row.stageId,
  row.stageName,
  row.cycle,
  row.relativePath,
  row.intendedLoad,
  row.expectedSignals,
  row.expectedChatQuestion,
  row.approvalGuidance,
  row.qualityChecks
]);

cycleSheet.getRange("A1:H1").values = [[
  "Cycle",
  "Stage",
  "Inputs",
  "Forced Actions",
  "Observe Before Approval",
  "Review Actions",
  "Observe After Approval",
  "Exit Criteria"
]];
cycleSheet.getRange(`A2:H${stages.length + 1}`).values = stages.map((stage) => [
  stage.cycle,
  stage.id,
  `${stage.dir}/*.md`,
  "Upload stage files through Cognitive Memory APIs; create/update project Markdown asset nodes; force project/process ingestion; force consolidation/dreaming cycle.",
  "Snapshot candidate counts, source locators, duplicates, contradictions, and pending review items.",
  "Approve useful recommendations; reject or mark duplicates/needs-changes; record every decision.",
  "Run recall and snapshot again; compare changed memory records to the source manifest.",
  "No cross-project chunks, source locators correct, useful memories retained, duplicates controlled."
]);

chatSheet.getRange("A1:G1").values = [[
  "Project Key",
  "Project Name",
  "Stage",
  "Probe Question",
  "Expected Evidence",
  "Pass Criteria",
  "Observed Result"
]];
chatSheet.getRange(`A2:G${manifestRows.length + 1}`).values = manifestRows.map((row) => [
  row.projectKey,
  row.projectName,
  row.stageId,
  row.expectedChatQuestion,
  row.expectedSignals,
  "Agent answer cites correct project memories/sources and avoids unrelated project facts.",
  "Pending execution"
]);

analysisSheet.getRange("A1:K1").values = [[
  "Cycle",
  "Project Key",
  "Source ID",
  "Candidate Title",
  "Memory Record ID",
  "Source Locator",
  "Useful Summary?",
  "Duplicate?",
  "Cross-Project Leakage?",
  "Decision",
  "Notes"
]];
analysisSheet.getRange("A2:K25").values = Array.from({ length: 24 }, () => [
  "",
  "",
  "",
  "",
  "",
  "",
  "Pending",
  "Pending",
  "Pending",
  "Pending",
  ""
]);

repairSheet.getRange("A1:H1").values = [[
  "Discovered Issue",
  "Cycle",
  "Project",
  "Evidence",
  "Severity",
  "Repair Subbundle Created?",
  "Repair Bundle Path",
  "Closure Status"
]];
repairSheet.getRange("A2:H9").values = Array.from({ length: 8 }, () => [
  "",
  "",
  "",
  "",
  "",
  "No",
  "",
  "Open"
]);

const trackerPath = path.join(trackerDir, "cognitive-memory-demo-source-tracker.xlsx");
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(trackerPath);

console.log(JSON.stringify({
  sourceCount: manifestRows.length,
  trackerPath,
  manifestPath: path.join(bundleRoot, "sample-data", "source-manifest.json")
}, null, 2));
