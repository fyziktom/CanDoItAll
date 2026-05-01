using CanDoItAll.Mcp.Core.Contracts;

namespace CanDoItAll.Mcp.Mermaid.Catalog;

public sealed class MermaidSyntaxCatalogService
{
    private const string MermaidVersion = "11.14.0";
    private const string SourceBasis = "Official Mermaid 11.14.0 syntax docs and the local mermaid parser grammar snapshot, including architecture-beta grammar.";

    private static readonly IReadOnlyList<string> GlobalRules =
    [
        "Start each diagram with the exact diagram keyword expected by that parser, such as `flowchart`, `sequenceDiagram`, `classDiagram`, or `architecture-beta`.",
        "Keep structural ids simple unless a diagram type explicitly allows richer names. Prefer letters, digits, underscores, and hyphens.",
        "When labels contain punctuation, operators, brackets, commas, or colons, quote the label or use the diagram type's label syntax instead of putting punctuation into ids.",
        "Mermaid parser errors are often caused by punctuation being placed in an id slot where Mermaid expects an identifier token."
    ];

    private static readonly IReadOnlyList<MermaidDiagramSyntaxDocument> Diagrams =
    [
        new(
            "flowchart",
            "Flowchart",
            "stable",
            "Directed graph syntax for process and dependency diagrams.",
            ["graph"],
            ["flowchart LR", "flowchart TD", "graph TB"],
            [
                "Use `flowchart` or `graph`, followed by an orientation such as `TD`, `TB`, `BT`, `LR`, or `RL`.",
                "Use simple node ids and put display text in brackets, parentheses, braces, or quoted labels.",
                "Common links include `-->`, `---`, `-.->`, `==>`, and edge labels such as `A -- label --> B`.",
                "Use `subgraph id[Title]` and close it with `end`."
            ],
            [
                "Quoted labels are the safest place for punctuation: `A[\"API: v2\"]`.",
                "Lowercase `end` is parsed as a block terminator; use another id or quote it as label text."
            ],
            [
                new("node id", "spaces, `.`, `/`, `:`, `?`, `#`, raw brackets", "Flowchart ids are parsed as identifiers before label syntax is applied.", "Use `api_gateway[\"API gateway: v2\"]` instead of `api.gateway: v2`."),
                new("reserved word", "end", "Lowercase `end` closes a subgraph block.", "Use `EndNode[End]`, `finish[End]`, or quoted label text."),
                new("edge target", "leading `o` or `x` immediately after an arrow", "`A---oB` and `A---xB` create circle or cross edge heads rather than ids named `oB` or `xB`.", "Insert a space or capitalize the id: `A --- OB`.")
            ],
            [
                new("Clickable process flow", "Safe labels with punctuation in quoted label text.", """
                    flowchart LR
                        intake([Request])
                        api["API: validate"]
                        worker[Worker]
                        done([Done])
                        intake --> api --> worker --> done
                    """)
            ]),
        new(
            "sequence",
            "Sequence Diagram",
            "stable",
            "Participant-to-participant message timelines.",
            ["sequenceDiagram"],
            ["sequenceDiagram"],
            [
                "Start with `sequenceDiagram`.",
                "Declare actors with `participant alias as Display Name` or let Mermaid infer participants from messages.",
                "Messages use arrows such as `->>`, `-->>`, `-)`, `--x`, and labels after `:`.",
                "Blocks such as `loop`, `alt`, `opt`, `par`, `critical`, and `break` must close with `end`."
            ],
            [
                "Use aliases when display names contain spaces or punctuation.",
                "Use `activate`/`deactivate` or arrow suffixes `+` and `-` for lifeline activation."
            ],
            [
                new("participant alias", "spaces, `.`, `/`, `:`, `-` as punctuation-heavy names", "Aliases are referenced by messages and should stay identifier-like.", "Use `participant api as API: Gateway` then send messages to `api`."),
                new("message line", "missing `:`", "The parser expects message text after an actor-to-actor arrow to be separated by a colon.", "Use `Client->>API: request`."),
                new("blocks", "unbalanced `end`", "Sequence control blocks must be balanced.", "Close each `loop`, `alt`, `opt`, `par`, `critical`, and `break` block with `end`.")
            ],
            [
                new("Request round trip", "Participant aliases keep display text rich without breaking references.", """
                    sequenceDiagram
                        participant user as User
                        participant api as API: Gateway
                        user->>api: Submit request
                        api-->>user: Accepted
                    """)
            ]),
        new(
            "class",
            "Class Diagram",
            "stable",
            "Class, member, annotation, and relationship diagrams.",
            ["classDiagram"],
            ["classDiagram"],
            [
                "Start with `classDiagram`.",
                "Declare classes with `class Name` or `class Name { ... }`.",
                "Use relations such as `<|--`, `*--`, `o--`, `-->`, `..>`, and labels after `:`.",
                "Use `~T~` for generic placeholders in class names or members."
            ],
            [
                "Class names should remain identifier-like; use relation labels for punctuation-heavy text.",
                "Member visibility markers include `+`, `-`, `#`, and `~` at the start of member lines."
            ],
            [
                new("class name", "spaces, `.`, `/`, `:`, raw `<T>`", "Class names are references used by relation lines.", "Use `Repository~T~` or `Repository_T` rather than `Repository<T>`."),
                new("member block", "unbalanced `{` or `}`", "Braces delimit class members.", "Keep braces paired and move explanation into comments or relation labels."),
                new("relation", "free-form punctuation before relation operator", "The parser expects `ClassA <|-- ClassB`-style relation tokens.", "Use simple class ids and put text after `:`.")
            ],
            [
                new("Component contract", "Generic marker and relation label.", """
                    classDiagram
                        class MermaidDiagram
                        class MermaidDiagramOptions
                        MermaidDiagram --> MermaidDiagramOptions : uses
                    """)
            ]),
        new(
            "state",
            "State Diagram",
            "stable",
            "State-machine syntax with transitions, composites, and start/end markers.",
            ["stateDiagram", "stateDiagram-v2"],
            ["stateDiagram-v2"],
            [
                "Prefer `stateDiagram-v2` for current diagrams.",
                "Use transitions such as `Idle --> Running : Start`.",
                "Use `[*]` for start and final states.",
                "Use `state \"Display text\" as id` when a state label needs spaces or punctuation."
            ],
            [
                "Composite states use braces and must be balanced.",
                "Choice and fork/join nodes use explicit `<<choice>>`, `<<fork>>`, or `<<join>>` annotations."
            ],
            [
                new("state id", "spaces, `.`, `/`, `:`, raw brackets except `[*]`", "State ids are transition endpoints.", "Use `state \"Waiting: approval\" as waitingApproval`."),
                new("reserved marker", "[*]", "`[*]` is a start/final marker, not a reusable state id.", "Use a named state for ordinary nodes."),
                new("composite state", "unbalanced `{` or `}`", "Braces delimit nested state bodies.", "Close each composite state block before declaring the next top-level state.")
            ],
            [
                new("Approval state machine", "Quoted display text plus simple ids.", """
                    stateDiagram-v2
                        [*] --> waiting
                        state "Waiting: approval" as waiting
                        waiting --> running : approved
                        running --> [*]
                    """)
            ]),
        new(
            "er",
            "ER Diagram",
            "stable",
            "Entity relationship syntax with cardinality markers and attributes.",
            ["erDiagram"],
            ["erDiagram"],
            [
                "Start with `erDiagram`.",
                "Relationships use cardinality operators such as `||--o{`, `}|..|{`, and labels after `:`.",
                "Entity attributes live inside `ENTITY { type name }` blocks.",
                "Use simple uppercase entity ids and simple attribute names."
            ],
            [
                "Relationship labels are quoted strings after `:`.",
                "Attribute keys or comments should not replace entity ids."
            ],
            [
                new("entity id", "spaces, `.`, `/`, `:`, quotes", "Entity ids are relation endpoints.", "Use `MERMAID_DIAGRAM` instead of `Mermaid Diagram`."),
                new("attribute declaration", "commas between type/name fields", "Attributes are whitespace-delimited inside entity blocks.", "Use `string diagram_id` not `string, diagram_id`."),
                new("relationship", "missing quoted label after `:`", "Relationship labels with spaces must be quoted.", "Use `USER ||--o{ DIAGRAM : \"creates\"`.")
            ],
            [
                new("Diagram ownership", "Entity ids stay simple; relation text is quoted.", """
                    erDiagram
                        USER ||--o{ DIAGRAM : "creates"
                        DIAGRAM {
                            string diagram_id
                            string source
                        }
                    """)
            ]),
        new(
            "architecture-beta",
            "Architecture Beta",
            "beta",
            "Cloud and deployment architecture diagrams introduced in Mermaid v11.1.0+ with deterministic layout controls in v11.14.0.",
            ["architecture", "architectureBeta"],
            ["architecture-beta"],
            [
                "Start with the exact keyword `architecture-beta`.",
                "Declare groups as `group id(icon)[Title] in parent?`.",
                "Declare services as `service id(icon)[Title] in parent?`.",
                "Declare junctions as `junction id in parent?`.",
                "Connect services or junctions with ported edges such as `api:R --> L:db`."
            ],
            [
                "Architecture ids follow `[\u005cw]([-\u005cw]*\u005cw)?`: letters, digits, underscores, and inner hyphens; no spaces or punctuation.",
                "Icons use parentheses with `[\u005cw-:]+`, for example `(cloud)` or `(logos:azure)`.",
                "Titles use brackets. Plain bracket text allows word characters and spaces; quote titles when punctuation is needed: `[\"API: v2\"]`.",
                "Ports are only `L`, `R`, `T`, and `B`.",
                "Edges cannot use group ids as endpoints. Use service or junction ids, and add `{group}` only to a service inside a group when the edge should attach to the group boundary.",
                "Mermaid 11.14.0 adds `architecture.randomize`; keep it false for deterministic screenshots unless exploration needs varied layouts."
            ],
            [
                new("id", "spaces, `.`, `/`, `:`, `?`, `#`, leading or trailing `-`", "The architecture parser's ID token is identifier-like and allows hyphens only inside the id.", "Use `public_api`, `api-v2`, or `db1`; put punctuation in the title."),
                new("title", "unquoted punctuation inside `[]` such as `:`, `/`, `.`, `,`, `#`", "Plain architecture titles allow word characters and spaces. Punctuation needs quoted title content.", "Use `[\"API: v2\"]` instead of `[API: v2]`."),
                new("port", "anything except `L`, `R`, `T`, `B`", "Architecture edge ports are fixed side tokens.", "Use `api:R --> L:db`."),
                new("edge endpoint", "group id as endpoint", "Edges reference services or junctions, not group declarations.", "Connect from a service in the group, or use `serviceId{group}` for boundary attachment."),
                new("group modifier", "`{group}` on an id that is not a service inside a group", "The modifier attaches an edge to the containing group boundary adjacent to that service.", "Use it only as `server{group}:B --> T:database{group}` when both services belong to groups.")
            ],
            [
                new("Grouped service architecture", "Groups, services, junctions, and directional ports.", """
                    architecture-beta
                        group api(cloud)[API]
                        service db(database)[Database] in api
                        service disk(disk)[Storage] in api
                        service server(server)[Server] in api
                        junction split in api

                        server:R --> L:split
                        split:R --> L:db
                        split:B --> T:disk
                    """)
            ]),
        new(
            "gantt",
            "Gantt",
            "stable",
            "Project timeline syntax with sections, task states, dates, and durations.",
            ["gantt"],
            ["gantt"],
            [
                "Start with `gantt`.",
                "Use header directives such as `dateFormat`, `axisFormat`, and `title`.",
                "Use `section Name` followed by task lines shaped like `Task name :state, id, after other, 3d`.",
                "Task fields are comma-separated after the first colon."
            ],
            [
                "Task descriptions may be display text, but the structured fields after `:` have strict comma-separated meaning.",
                "Use ids for dependencies and keep ids identifier-like."
            ],
            [
                new("task field separator", "extra `:` in a task declaration", "The first colon separates display text from task metadata.", "Move punctuation into the task title before the first colon or simplify the title."),
                new("task id", "spaces, `.`, `/`, `:`, `,`", "Task ids are dependency references.", "Use `design_api` rather than `design/api`."),
                new("date/duration", "free-form dates outside configured `dateFormat`", "Gantt dates must match the declared date format.", "Use `dateFormat YYYY-MM-DD` with dates like `2026-05-01`.")
            ],
            [
                new("Release timeline", "Simple ids and after-dependencies.", """
                    gantt
                        dateFormat  YYYY-MM-DD
                        section Mermaid
                        Wrapper package :done, wrapper, 2026-05-01, 2d
                        Sandbox proof :active, sandbox, after wrapper, 1d
                    """)
            ])
    ];

    public MermaidSyntaxIndex GetIndex()
    {
        return new MermaidSyntaxIndex(MermaidVersion, SourceBasis, GlobalRules, Diagrams.Select(ToSummary).ToArray());
    }

    public MermaidSyntaxListData ListTypes(string? query = null)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        var results = Diagrams
            .Where(diagram => string.IsNullOrWhiteSpace(normalizedQuery) || Matches(diagram, normalizedQuery))
            .Select(ToSummary)
            .OrderBy(diagram => diagram.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new MermaidSyntaxListData(normalizedQuery, results);
    }

    public MermaidDiagramSyntaxDocument GetSyntax(string diagramType)
    {
        return Resolve(diagramType);
    }

    public MermaidForbiddenSymbolsData GetForbiddenSymbols(string diagramType)
    {
        var diagram = Resolve(diagramType);
        return new MermaidForbiddenSymbolsData(diagram.Key, diagram.ForbiddenSymbols);
    }

    public MermaidExamplesData GetExamples(string diagramType)
    {
        var diagram = Resolve(diagramType);
        return new MermaidExamplesData(diagram.Key, diagram.Examples);
    }

    private static MermaidDiagramSyntaxDocument Resolve(string diagramType)
    {
        if (string.IsNullOrWhiteSpace(diagramType))
        {
            throw new ToolInvocationException("ValidationFailed", "A Mermaid diagram type is required.");
        }

        var normalized = Normalize(diagramType);
        var matches = Diagrams
            .Where(diagram =>
                string.Equals(Normalize(diagram.Key), normalized, StringComparison.OrdinalIgnoreCase) ||
                diagram.Aliases.Any(alias => string.Equals(Normalize(alias), normalized, StringComparison.OrdinalIgnoreCase)) ||
                diagram.StartsWith.Any(start => string.Equals(Normalize(start.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]), normalized, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new ToolInvocationException("DiagramTypeNotFound", $"No Mermaid syntax entry matched '{diagramType}'.", Diagrams.Select(diagram => diagram.Key).ToArray()),
            _ => throw new ToolInvocationException("AmbiguousDiagramType", $"Mermaid diagram type '{diagramType}' matched multiple entries.", matches.Select(diagram => diagram.Key).ToArray())
        };
    }

    private static bool Matches(MermaidDiagramSyntaxDocument diagram, string query)
    {
        var haystack = string.Join(
            ' ',
            [
                diagram.Key,
                diagram.Title,
                diagram.Status,
                diagram.Summary,
                "forbidden symbols",
                .. diagram.Aliases,
                .. diagram.StartsWith,
                .. diagram.MainRules,
                .. diagram.AdvancedRules,
                .. diagram.ForbiddenSymbols.SelectMany(rule => new[] { rule.Scope, rule.Symbols, rule.Reason, rule.SaferForm }),
                .. diagram.Examples.SelectMany(example => new[] { example.Title, example.Description, example.Source })
            ]);

        return ScoreText(haystack, query);
    }

    private static bool ScoreText(string text, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static MermaidDiagramTypeSummary ToSummary(MermaidDiagramSyntaxDocument diagram)
    {
        return new MermaidDiagramTypeSummary(diagram.Key, diagram.Title, diagram.Status, diagram.Summary, diagram.Aliases, diagram.StartsWith);
    }

    private static string Normalize(string value)
    {
        return value.Trim().Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }
}
