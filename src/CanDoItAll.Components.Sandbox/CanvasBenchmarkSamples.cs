using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Components.Sandbox;

public sealed record CanvasBenchmarkSurfaceDefinition(
    string Key,
    string Label,
    string Summary,
    int NodeCount,
    int LinkCount,
    CanvasWorkbenchSurface Surface);

public static class CanvasBenchmarkSamples
{
    public const string DefaultTierKey = "review";

    private static readonly string[] PaletteKeys =
    [
        "info",
        "success",
        "warning",
        "accent",
        "neutral"
    ];

    private static readonly IReadOnlyList<CanvasBenchmarkSurfaceDefinition> Definitions =
    [
        CreateDefinition(
            "compact",
            "Compact",
            "Small proof graph for baseline scene materialization.",
            laneCount: 3,
            rowsPerLane: 3,
            cardsPerRow: 4),
        CreateDefinition(
            "review",
            "Review",
            "Mid-sized review graph that is close to dense day-to-day authoring.",
            laneCount: 4,
            rowsPerLane: 4,
            cardsPerRow: 5),
        CreateDefinition(
            "stress",
            "Stress",
            "Large graph intended to pressure the initial scene materialization path without replacing shipped behavior.",
            laneCount: 5,
            rowsPerLane: 5,
            cardsPerRow: 6)
    ];

    public static IReadOnlyList<CanvasBenchmarkSurfaceDefinition> CreateDefinitions()
    {
        return Definitions;
    }

    public static CanvasBenchmarkSurfaceDefinition GetDefinition(string? key)
    {
        return Definitions.FirstOrDefault(
                   definition => string.Equals(definition.Key, key, StringComparison.OrdinalIgnoreCase))
               ?? Definitions.First(definition => string.Equals(definition.Key, DefaultTierKey, StringComparison.Ordinal));
    }

    private static CanvasBenchmarkSurfaceDefinition CreateDefinition(
        string key,
        string label,
        string summary,
        int laneCount,
        int rowsPerLane,
        int cardsPerRow)
    {
        var surface = BuildSurface(key, label, summary, laneCount, rowsPerLane, cardsPerRow);
        return new CanvasBenchmarkSurfaceDefinition(
            key,
            label,
            summary,
            surface.Nodes.Count,
            surface.Links.Count,
            surface);
    }

    private static CanvasWorkbenchSurface BuildSurface(
        string key,
        string label,
        string summary,
        int laneCount,
        int rowsPerLane,
        int cardsPerRow)
    {
        var nodes = new List<CanvasWorkbenchNode>();
        var links = new List<CanvasWorkbenchLink>();
        var groupFrames = new List<CanvasWorkbenchGroupFrame>();
        const string rootId = "benchmark-root";

        nodes.Add(new CanvasWorkbenchNode
        {
            Id = rootId,
            Family = "project",
            Kind = "project",
            Title = $"{label} renderer benchmark",
            Subtitle = "Typed synthetic workbench surface",
            LeadText = summary,
            Status = "Ready",
            StatusPill = "Benchmark",
            PaletteKey = "accent",
            AccentColor = "#2563eb",
            X = 180,
            Y = 96,
            Chips =
            [
                new CanvasWorkbenchChip
                {
                    Text = $"{laneCount * rowsPerLane * cardsPerRow} cards",
                    Tone = "info"
                },
                new CanvasWorkbenchChip
                {
                    Text = $"{laneCount} lanes",
                    Tone = "success"
                }
            ]
        });

        var itemCounter = 0;
        var laneSpacing = cardsPerRow switch
        {
            >= 6 => 540,
            5 => 500,
            _ => 460
        };

        for (var laneIndex = 0; laneIndex < laneCount; laneIndex++)
        {
            var laneId = $"lane-{laneIndex + 1}";
            var lanePalette = PaletteKeys[laneIndex % PaletteKeys.Length];
            var laneX = 420 + (laneIndex * laneSpacing);
            var laneY = 140d;

            nodes.Add(new CanvasWorkbenchNode
            {
                Id = laneId,
                ParentId = rootId,
                Family = "feature",
                Kind = "feature",
                Title = $"Benchmark lane {laneIndex + 1}",
                Subtitle = "Initial scene materialization",
                LeadText = "Compare retained DOM-SVG create cost against a narrow true-canvas draw pass.",
                Status = laneIndex % 2 == 0 ? "Active" : "Ready",
                StatusPill = laneIndex % 2 == 0 ? "Hot path" : "Stable",
                PaletteKey = lanePalette,
                AccentColor = laneIndex % 2 == 0 ? "#0f766e" : "#7c3aed",
                X = laneX,
                Y = laneY,
                Chips =
                [
                    new CanvasWorkbenchChip
                    {
                        Text = $"{rowsPerLane * cardsPerRow} nodes",
                        Tone = "info"
                    }
                ]
            });

            links.Add(new CanvasWorkbenchLink
            {
                SourceId = rootId,
                TargetId = laneId,
                Kind = "contains"
            });

            var laneAnchorNodeIds = new List<string> { laneId };
            var previousRowNodeIds = new string[cardsPerRow];

            for (var rowIndex = 0; rowIndex < rowsPerLane; rowIndex++)
            {
                for (var cardIndex = 0; cardIndex < cardsPerRow; cardIndex++)
                {
                    itemCounter++;

                    var nodeId = $"{laneId}-card-{itemCounter:D3}";
                    var x = laneX + (cardIndex * 188) - ((cardsPerRow - 1) * 94);
                    var y = 312 + (rowIndex * 166) + ((cardIndex % 2) * 18);
                    var paletteKey = PaletteKeys[(laneIndex + rowIndex + cardIndex) % PaletteKeys.Length];
                    var kind = (cardIndex % 3) switch
                    {
                        0 => "task",
                        1 => "evidence",
                        _ => "note"
                    };

                    nodes.Add(new CanvasWorkbenchNode
                    {
                        Id = nodeId,
                        ParentId = laneId,
                        Family = "work-item",
                        Kind = kind,
                        Title = $"{label} node {itemCounter:D3}",
                        Subtitle = rowIndex % 2 == 0 ? "Scene materialization" : "Feature parity risk",
                        LeadText = "Synthetic benchmark item for renderer comparison.",
                        Status = rowIndex == rowsPerLane - 1 ? "Review" : "Ready",
                        StatusPill = cardIndex % 2 == 0 ? "Retained" : "Canvas",
                        PaletteKey = paletteKey,
                        AccentColor = cardIndex % 2 == 0 ? "#2563eb" : "#9333ea",
                        ProgressMode = "determinate",
                        ProgressPercent = 25 + ((itemCounter * 7) % 65),
                        Priority = (cardIndex % 3) + 1,
                        X = x,
                        Y = y,
                        Chips =
                        [
                            new CanvasWorkbenchChip
                            {
                                Text = rowIndex % 2 == 0 ? "Hot path" : "Parity",
                                Tone = rowIndex % 2 == 0 ? "warning" : "neutral"
                            }
                        ],
                        FooterChips =
                        [
                            new CanvasWorkbenchChip
                            {
                                Text = kind,
                                Tone = "info"
                            }
                        ]
                    });

                    links.Add(new CanvasWorkbenchLink
                    {
                        SourceId = rowIndex == 0 ? laneId : previousRowNodeIds[cardIndex],
                        TargetId = nodeId,
                        Kind = "contains"
                    });

                    if (cardIndex > 0 && rowIndex % 2 == 0)
                    {
                        links.Add(new CanvasWorkbenchLink
                        {
                            SourceId = $"{laneId}-card-{(itemCounter - 1):D3}",
                            TargetId = nodeId,
                            Kind = "supports"
                        });
                    }

                    previousRowNodeIds[cardIndex] = nodeId;
                    laneAnchorNodeIds.Add(nodeId);
                }
            }

            groupFrames.Add(new CanvasWorkbenchGroupFrame
            {
                Id = $"frame-{laneId}",
                Label = $"Delivery lane {laneIndex + 1}",
                Tone = laneIndex % 2 == 0 ? "info" : "warning",
                AnchorNodeIds = laneAnchorNodeIds
            });
        }

        var chrome = new CanvasWorkbenchChrome
        {
            HintText = "Benchmark surfaces stay isolated in the sandbox. The shipped retained renderer remains the production path until a benchmark-backed decision changes that.",
            EmptyStateKicker = "Canvas benchmark",
            EmptyStateTitle = "Benchmark tier missing",
            EmptyStateDescription = "Choose a tier to compare the retained DOM-SVG path against the narrow true-canvas prototype.",
            ShowFocusAction = false,
            ShowQuickCreateRail = false,
            Diagnostics = new CanvasWorkbenchDiagnosticsOptions
            {
                IsEnabled = false
            },
            Minimap = new CanvasWorkbenchMinimapOptions
            {
                IsEnabled = false,
                Title = "Benchmark overview"
            },
            Clipboard = new CanvasWorkbenchClipboardOptions
            {
                IsEnabled = false,
                AllowCopy = false,
                AllowPaste = false,
                AllowDuplicate = false
            },
            TooltipPopover = new CanvasWorkbenchTooltipPopoverOptions
            {
                IsEnabled = false
            },
            MarqueeSelection = new CanvasWorkbenchMarqueeOptions
            {
                IsEnabled = false
            },
            SnapGuides = new CanvasWorkbenchSnapGuideOptions
            {
                IsEnabled = false
            },
            ConnectorAnchors = new CanvasWorkbenchConnectorAnchorOptions
            {
                IsEnabled = false
            },
            TransformHandles = new CanvasWorkbenchTransformHandleOptions
            {
                IsEnabled = false
            }
        };

        var uiState = new CanvasWorkbenchUiState
        {
            SelectedNodeIds = [rootId],
            GroupFrames = groupFrames,
            Zoom = cardsPerRow >= 6 ? 0.28 : cardsPerRow == 5 ? 0.34 : 0.44,
            PanX = 54,
            PanY = 72,
            ShowDiagnostics = false,
            ShowMinimap = false,
            ActiveInspectorTab = "overview"
        };

        return new CanvasWorkbenchSurface
        {
            SurfaceId = $"canvas-benchmark:{key}",
            Mode = "benchmark",
            Nodes = nodes,
            Links = links,
            UiState = uiState,
            Chrome = chrome
        };
    }
}
