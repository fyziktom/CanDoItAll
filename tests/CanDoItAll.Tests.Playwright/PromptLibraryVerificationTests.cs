using System.Text;
using System.Text.Json;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed class PromptLibraryVerificationTests(PlaywrightAppFixture fixture) : IClassFixture<PlaywrightAppFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly IReadOnlyDictionary<string, string> ComponentSectionByGroup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["session-framing"] = "foundation",
        ["mission-scope"] = "foundation",
        ["context-discovery"] = "foundation",
        ["guardrails"] = "foundation",
        ["workflow-orchestration"] = "delivery",
        ["architecture-analysis"] = "delivery",
        ["planning-checklists"] = "delivery",
        ["implementation-execution"] = "delivery",
        ["validation-review"] = "validation",
        ["output-handoff"] = "validation",
        ["stack-profiles"] = "environment",
        ["toolbox-snippets"] = "environment"
    };

    private static readonly IReadOnlyDictionary<string, int> ComponentSectionOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["foundation"] = 0,
        ["delivery"] = 1,
        ["validation"] = 2,
        ["environment"] = 3
    };

    private static readonly IReadOnlyDictionary<string, string> FlowSectionByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["architecture-review-plan-implement-validate"] = "core-delivery",
        ["audit-plan-refactor-review"] = "core-delivery",
        ["bugfix-regression-proof"] = "core-delivery",
        ["release-hardening-final-audit"] = "core-delivery",
        ["ui-canvas-feature-delivery"] = "ui-data",
        ["fullstack-offline-feature"] = "ui-data",
        ["data-layer-change-crossdb"] = "ui-data",
        ["playwright-automation-upgrade"] = "specialized",
        ["php-canvas-migration"] = "specialized",
        ["embedded-midi-firmware-tuning"] = "specialized"
    };

    private static readonly IReadOnlyDictionary<string, int> FlowSectionOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["core-delivery"] = 0,
        ["ui-data"] = 1,
        ["specialized"] = 2
    };

    private static readonly IReadOnlyDictionary<string, string> BlueprintSectionByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["architecture-spec"] = "foundation",
        ["repository-audit"] = "foundation",
        ["implementation-plan"] = "foundation",
        ["feature-implementation"] = "foundation",
        ["safe-refactor"] = "foundation",
        ["bugfix-with-regression-lock"] = "foundation",
        ["senior-code-review"] = "assurance",
        ["test-strategy-and-automation"] = "assurance",
        ["validation-audit"] = "assurance",
        ["performance-hardening"] = "assurance",
        ["security-hardening"] = "assurance",
        ["ui-ux-delivery"] = "experience-and-embedded",
        ["embedded-firmware-iteration"] = "experience-and-embedded"
    };

    private static readonly IReadOnlyDictionary<string, int> BlueprintSectionOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["foundation"] = 0,
        ["assurance"] = 1,
        ["experience-and-embedded"] = 2
    };

    [Fact]
    public async Task Prompt_library_catalog_is_exhaustively_available_from_prompt_gallery_and_factory_canvas()
    {
        var repoRoot = GetRepoRoot();
        var artifactsRoot = Path.Combine(repoRoot, "output", "playwright", "prompt-library-verification");
        ResetDirectory(artifactsRoot);

        var componentsRoot = Path.Combine(artifactsRoot, "components");
        var flowsRoot = Path.Combine(artifactsRoot, "flows");
        var blueprintsRoot = Path.Combine(artifactsRoot, "blueprints");
        var inputsRoot = Path.Combine(artifactsRoot, "inputs");
        Directory.CreateDirectory(componentsRoot);
        Directory.CreateDirectory(flowsRoot);
        Directory.CreateDirectory(blueprintsRoot);
        Directory.CreateDirectory(inputsRoot);

        var manifest = LoadManifest(repoRoot);
        Assert.Equal(112, manifest.Components.Count);
        Assert.Equal(10, manifest.Flows.Count);
        Assert.Equal(13, manifest.Blueprints.Count);
        Assert.Equal(5, manifest.Inputs.Count);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1900,
                Height = 1200
            }
        });

        var page = await context.NewPageAsync();
        var results = new List<VerificationResult>();

        await VerifyPromptGalleryAsync(page, manifest, artifactsRoot);
        await PreparePromptFactoryAsync(page);

        await ResetSessionAsync(page);

        var componentIndex = 1;
        foreach (var component in OrderComponents(manifest))
        {
            await ResetSessionAsync(page);
            results.Add(await VerifyComponentAsync(page, manifest, component, componentIndex++, componentsRoot));
        }

        await ResetSessionAsync(page);

        var flowIndex = 1;
        foreach (var flow in OrderFlows(manifest))
        {
            await ResetSessionAsync(page);
            results.Add(await VerifyFlowAsync(page, flow, flowIndex++, flowsRoot));
        }

        await ResetSessionAsync(page);

        var blueprintIndex = 1;
        foreach (var blueprint in OrderBlueprints(manifest))
        {
            await ResetSessionAsync(page);
            results.Add(await VerifyBlueprintAsync(page, blueprint, blueprintIndex++, blueprintsRoot));
        }

        await ResetSessionAsync(page);

        var inputIndex = 1;
        foreach (var input in manifest.Inputs)
        {
            await ResetSessionAsync(page);
            results.Add(await VerifyInputAsync(page, input, inputIndex++, inputsRoot));
        }

        WriteReports(repoRoot, artifactsRoot, manifest, results);

        var failures = results.Where(item => !item.Passed).ToList();
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Select(item => $"{item.Category}/{item.Key}: {item.Notes}")));
        Assert.Equal(manifest.TotalItemCount, results.Count);
    }

    private async Task VerifyPromptGalleryAsync(IPage page, VerificationManifest manifest, string artifactsRoot)
    {
        var screenshotPath = Path.Combine(artifactsRoot, "prompt-gallery-imported-catalog.png");
        var response = await page.GotoAsync($"{fixture.BaseUrl}/prompt-gallery");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /prompt-gallery to return 2xx, got {(int)response.Status}.");

        await page.WaitForSelectorAsync("text=Prompt gallery and versions");
        await page.WaitForSelectorAsync("text=Library groups");
        await page.WaitForSelectorAsync("text=Prompt flow templates");
        await page.WaitForSelectorAsync("text=Prompt blueprints");
        await page.WaitForSelectorAsync($"text={manifest.Components.Count}");
        await page.WaitForSelectorAsync($"text={manifest.Flows.Count}");
        await page.WaitForSelectorAsync($"text={manifest.Blueprints.Count}");
        await page.Locator("main").ScreenshotAsync(new() { Path = screenshotPath });
    }

    private async Task PreparePromptFactoryAsync(IPage page)
    {
        var response = await page.GotoAsync($"{fixture.BaseUrl}/prompt-factory");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /prompt-factory to return 2xx, got {(int)response.Status}.");

        await page.WaitForSelectorAsync("text=Prompt session workbench");
        await page.WaitForSelectorAsync(".cw-node[data-node-id='session-root']");
        await page.WaitForSelectorAsync(".cw-canvas-host");
        await FocusCanvasNodeAsync(page, "session-root");
    }

    private async Task<VerificationResult> VerifyComponentAsync(IPage page, VerificationManifest manifest, VerificationComponent component, int index, string artifactsRoot)
    {
        var slug = $"{index:000}-{component.Key}";
        var entryPath = Path.Combine(artifactsRoot, $"{slug}-{(component.TemplateTokens.Count > 0 ? "specify" : "menu")}.png");
        var resultPath = Path.Combine(artifactsRoot, $"{slug}-added.png");
        var sectionKey = ResolveComponentSection(component.GroupKey);
        var menuPath = new[]
        {
            "catalog-components",
            $"catalog-components:{sectionKey}",
            $"catalog-group:{component.GroupKey}",
            $"component:add:{component.Key}"
        };

        await FocusCanvasNodeAsync(page, "session-root");
        await ExpandMenuPathAsync(page, ".cw-node[data-node-id='session-root']", menuPath);

        IReadOnlyList<TokenValue> tokenValues = [];
        if (component.TemplateTokens.Count > 0)
        {
            await ClickMenuActionAsync(page, menuPath[^1]);
            await WaitForComposerAsync(page);
            tokenValues = await FillComponentComposerAsync(page, component);
            await CaptureCanvasStageAsync(page, entryPath);
            await SubmitComposerAsync(page);
        }
        else
        {
            await CaptureCanvasStageAsync(page, entryPath);
            await ClickMenuActionAsync(page, menuPath[^1]);
        }

        var nodeId = $"selection:component:{component.Key}";
        await WaitForNodeAsync(page, nodeId);
        await FocusCanvasNodeAsync(page, nodeId);
        var nodeText = await ReadNodeTextAsync(page, nodeId);

        Assert.Contains(component.Name, nodeText, StringComparison.OrdinalIgnoreCase);
        if (tokenValues.Count > 0)
        {
            Assert.Contains(tokenValues[0].Value, nodeText, StringComparison.Ordinal);
        }

        await CaptureCanvasStageAsync(page, resultPath);

        return new VerificationResult
        {
            Category = "component",
            Key = component.Key,
            Name = component.Name,
            Passed = true,
            UsedModal = component.TemplateTokens.Count > 0,
            MenuPath = string.Join(" > ", menuPath),
            EntryScreenshotPath = entryPath,
            ResultScreenshotPath = resultPath,
            Notes = tokenValues.Count > 0
                ? $"Configured {tokenValues.Count} token value(s) and verified the node text."
                : "Verified the node was added from the canvas menu."
        };
    }

    private async Task<VerificationResult> VerifyFlowAsync(IPage page, VerificationFlow flow, int index, string artifactsRoot)
    {
        var slug = $"{index:000}-{flow.Key}";
        var entryPath = Path.Combine(artifactsRoot, $"{slug}-menu.png");
        var resultPath = Path.Combine(artifactsRoot, $"{slug}-selected.png");
        var menuPath = new[]
        {
            "catalog-flows",
            $"catalog-flows:{ResolveFlowSection(flow.Key)}",
            $"flow:set:{flow.Key}"
        };

        await FocusCanvasNodeAsync(page, "session-root");
        await ExpandMenuPathAsync(page, ".cw-node[data-node-id='session-root']", menuPath);
        await CaptureCanvasStageAsync(page, entryPath);
        await ClickMenuActionAsync(page, menuPath[^1]);

        await WaitForNodeAsync(page, "selection:flow");
        await FocusCanvasNodeAsync(page, "selection:flow");
        var nodeText = await ReadNodeTextAsync(page, "selection:flow");
        Assert.Contains(flow.Name, nodeText, StringComparison.OrdinalIgnoreCase);

        await CaptureCanvasStageAsync(page, resultPath);

        return new VerificationResult
        {
            Category = "flow",
            Key = flow.Key,
            Name = flow.Name,
            Passed = true,
            UsedModal = false,
            MenuPath = string.Join(" > ", menuPath),
            EntryScreenshotPath = entryPath,
            ResultScreenshotPath = resultPath,
            Notes = "Verified the flow selection node updated from the canvas menu."
        };
    }

    private async Task<VerificationResult> VerifyBlueprintAsync(IPage page, VerificationBlueprint blueprint, int index, string artifactsRoot)
    {
        var slug = $"{index:000}-{blueprint.Key}";
        var entryPath = Path.Combine(artifactsRoot, $"{slug}-menu.png");
        var resultPath = Path.Combine(artifactsRoot, $"{slug}-selected.png");
        var menuPath = new[]
        {
            "catalog-blueprints",
            $"catalog-blueprints:{ResolveBlueprintSection(blueprint.Key)}",
            $"blueprint:set:{blueprint.Key}"
        };

        await FocusCanvasNodeAsync(page, "session-root");
        await ExpandMenuPathAsync(page, ".cw-node[data-node-id='session-root']", menuPath);
        await CaptureCanvasStageAsync(page, entryPath);
        await ClickMenuActionAsync(page, menuPath[^1]);

        await WaitForNodeAsync(page, "selection:blueprint");
        await FocusCanvasNodeAsync(page, "selection:blueprint");
        var nodeText = await ReadNodeTextAsync(page, "selection:blueprint");
        Assert.Contains(blueprint.Name, nodeText, StringComparison.OrdinalIgnoreCase);

        await CaptureCanvasStageAsync(page, resultPath);

        return new VerificationResult
        {
            Category = "blueprint",
            Key = blueprint.Key,
            Name = blueprint.Name,
            Passed = true,
            UsedModal = false,
            MenuPath = string.Join(" > ", menuPath),
            EntryScreenshotPath = entryPath,
            ResultScreenshotPath = resultPath,
            Notes = "Verified the blueprint selection node updated from the canvas menu."
        };
    }

    private async Task<VerificationResult> VerifyInputAsync(IPage page, VerificationInputDefinition input, int index, string artifactsRoot)
    {
        var slug = $"{index:000}-{input.Key}";
        var entryPath = Path.Combine(artifactsRoot, $"{slug}-composer.png");
        var resultPath = Path.Combine(artifactsRoot, $"{slug}-added.png");
        var menuPath = new[]
        {
            "catalog-inputs",
            $"input:add:{input.Key}"
        };

        await FocusCanvasNodeAsync(page, "session-root");
        await ExpandMenuPathAsync(page, ".cw-node[data-node-id='session-root']", menuPath);
        await ClickMenuActionAsync(page, menuPath[^1]);
        await WaitForComposerAsync(page);
        await FillInputComposerAsync(page, input);
        await CaptureCanvasStageAsync(page, entryPath);
        await SubmitComposerAsync(page);

        var inputNodeId = await WaitForSingleInputNodeAsync(page);
        await FocusCanvasNodeAsync(page, inputNodeId);
        var nodeText = await ReadNodeTextAsync(page, inputNodeId);
        Assert.True(
            ResolveInputExpectationMarkers(input)
                .Any(marker => !string.IsNullOrWhiteSpace(marker) && nodeText.Contains(marker, StringComparison.OrdinalIgnoreCase)),
            $"Expected input node '{inputNodeId}' to contain one of the markers: {string.Join(", ", ResolveInputExpectationMarkers(input))}{Environment.NewLine}Actual text:{Environment.NewLine}{nodeText}");

        await CaptureCanvasStageAsync(page, resultPath);

        return new VerificationResult
        {
            Category = "input",
            Key = input.Key,
            Name = input.Label,
            Passed = true,
            UsedModal = true,
            MenuPath = string.Join(" > ", menuPath),
            EntryScreenshotPath = entryPath,
            ResultScreenshotPath = resultPath,
            Notes = "Verified the prompt-session input node was added from the right-click composer."
        };
    }

    private static async Task<IReadOnlyList<TokenValue>> FillComponentComposerAsync(IPage page, VerificationComponent component)
    {
        var values = new List<TokenValue>();
        var fields = page.Locator(".cw-canvas-composer__field");
        var count = await fields.CountAsync();
        Assert.Equal(component.TemplateTokens.Count, count);

        for (var index = 0; index < component.TemplateTokens.Count; index++)
        {
            var token = component.TemplateTokens[index];
            var value = BuildTokenValue(component.Key, token);
            await fields.Nth(index).Locator("input, textarea").First.FillAsync(value);
            values.Add(new TokenValue(token, value));
        }

        await page.WaitForTimeoutAsync(100);
        return values;
    }

    private static async Task FillInputComposerAsync(IPage page, VerificationInputDefinition input)
    {
        var fields = page.Locator(".cw-canvas-composer__field");
        Assert.True(await fields.CountAsync() >= 3, $"Expected at least 3 input fields for '{input.Key}'.");

        await fields.Nth(0).Locator("input, textarea").First.FillAsync(input.Title);
        await fields.Nth(1).Locator("input, textarea").First.FillAsync(input.Subtitle);
        await fields.Nth(2).Locator("input, textarea").First.FillAsync(input.Notes);

        if (!string.IsNullOrWhiteSpace(input.SampleFilePath))
        {
            await page.Locator(".cw-canvas-composer__file-input").SetInputFilesAsync(input.SampleFilePath);
            await page.WaitForFunctionAsync("() => !!document.querySelector('.cw-canvas-composer__upload-summary')?.textContent?.includes('ready')");
        }

        await page.WaitForTimeoutAsync(100);
    }

    private static async Task SubmitComposerAsync(IPage page)
    {
        await page.Locator(".cw-canvas-composer__actions button[data-tone='accent']").ClickAsync();
        await page.WaitForFunctionAsync("() => !document.querySelector('.cw-canvas-composer')");
    }

    private static async Task WaitForComposerAsync(IPage page)
        => await page.Locator(".cw-canvas-composer.is-dialog").WaitForAsync();

    private static async Task WaitForNodeAsync(IPage page, string nodeId)
        => await page.Locator($".cw-node[data-node-id=\"{nodeId}\"]").WaitForAsync();

    private static async Task<string> WaitForSingleInputNodeAsync(IPage page)
    {
        await page.WaitForFunctionAsync(
            @"() => document.querySelectorAll('.cw-node[data-node-id^=""selection:input:""]').length > 0");

        var nodeId = await page.EvaluateAsync<string>(
            @"() => {
                const matches = Array.from(document.querySelectorAll('.cw-node[data-node-id^=""selection:input:""]'));
                const node = matches.length > 0 ? matches[matches.length - 1] : null;
                return node?.getAttribute('data-node-id') || '';
            }");

        Assert.False(string.IsNullOrWhiteSpace(nodeId));
        return nodeId;
    }

    private static IReadOnlyList<string> ResolveInputExpectationMarkers(VerificationInputDefinition input)
    {
        var markers = new List<string>
        {
            input.Title,
            input.Subtitle,
            input.Label,
            input.Key
        };

        if (!string.IsNullOrWhiteSpace(input.SampleFilePath))
        {
            markers.Add(Path.GetFileName(input.SampleFilePath));
        }

        return markers
            .Where(marker => !string.IsNullOrWhiteSpace(marker))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<string> ReadNodeTextAsync(IPage page, string nodeId)
        => await page.Locator($".cw-node[data-node-id=\"{nodeId}\"]").InnerTextAsync();

    private static async Task CaptureCanvasStageAsync(IPage page, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await page.Locator(".cw-stage-surface").ScreenshotAsync(new() { Path = path });
    }

    private static async Task FocusCanvasNodeAsync(IPage page, string nodeId, int zoomPercent = 100)
    {
        await page.EvaluateAsync(
            @"args => {
                const host = document.querySelector('.cw-canvas-host');
                if (!host || !window.CanDoItAll?.canvasWorkbench) {
                    return;
                }

                window.CanDoItAll.canvasWorkbench.focusNode(host, args.nodeId);
                window.CanDoItAll.canvasWorkbench.setZoomPercent(host, args.zoomPercent);
            }",
            new { nodeId, zoomPercent });
        await page.WaitForTimeoutAsync(250);
    }

    private static async Task ExpandMenuPathAsync(IPage page, string nodeSelector, IReadOnlyList<string> actionIds)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                await CloseTransientUiAsync(page);
                await OpenCanvasContextMenuAsync(page, nodeSelector);

                foreach (var actionId in actionIds.Take(actionIds.Count - 1))
                {
                    await ActivateMenuActionAsync(page, actionId);
                }

                await page.WaitForFunctionAsync(
                    "selector => document.querySelectorAll(selector).length > 0",
                    ActionSelector(actionIds[^1]));
                return;
            }
            catch when (attempt < 3)
            {
                await CloseTransientUiAsync(page);
                await page.WaitForTimeoutAsync(120);
            }
        }

        throw new InvalidOperationException($"Could not expand menu path '{string.Join(" > ", actionIds)}'.");
    }

    private static async Task ClickMenuActionAsync(IPage page, string actionId)
    {
        var selector = ActionSelector(actionId);
        await page.WaitForFunctionAsync("selector => document.querySelectorAll(selector).length > 0", selector);
        await page.EvaluateAsync(
            @"selector => {
                const matches = Array.from(document.querySelectorAll(selector));
                const action = matches.length > 0 ? matches[matches.length - 1] : null;
                if (!action) {
                    return false;
                }

                action.click();
                return true;
            }",
            selector);
    }

    private static async Task ActivateMenuActionAsync(IPage page, string actionId)
    {
        var selector = ActionSelector(actionId);
        await page.WaitForFunctionAsync("selector => document.querySelectorAll(selector).length > 0", selector);
        await page.EvaluateAsync(
            @"selector => {
                const matches = Array.from(document.querySelectorAll(selector));
                const action = matches.length > 0 ? matches[matches.length - 1] : null;
                if (!action) {
                    return false;
                }

                action.scrollIntoView({ block: 'nearest', inline: 'nearest' });
                for (const type of ['pointerenter', 'mouseenter', 'mouseover', 'mousemove']) {
                    action.dispatchEvent(new MouseEvent(type, {
                        bubbles: true,
                        cancelable: true,
                        view: window
                    }));
                }

                return true;
            }",
            selector);
        await page.WaitForTimeoutAsync(180);
    }

    private static string ActionSelector(string actionId)
        => $".cw-context-menu__action[data-action-id=\"{actionId}\"]";

    private static async Task OpenCanvasContextMenuAsync(IPage page, string selector)
    {
        var locator = page.Locator(selector);
        var exists = await locator.CountAsync() > 0;
        Assert.True(exists, $"Canvas node '{selector}' was not found.");

        await locator.ClickAsync(new() { Button = MouseButton.Right, Force = true });
        await page.Locator(".cw-context-menu__action").First.WaitForAsync();
    }

    private static async Task CloseTransientUiAsync(IPage page)
    {
        for (var index = 0; index < 2; index++)
        {
            await page.Keyboard.PressAsync("Escape");
            await page.WaitForTimeoutAsync(80);
        }
    }

    private static async Task ResetSessionAsync(IPage page)
    {
        await page.Locator("[data-testid='prompt-factory-reset-session']").ClickAsync();
        await page.WaitForFunctionAsync(
            @"() => !document.querySelector('.cw-node[data-node-id=""selection:blueprint""]')
                && !document.querySelector('.cw-node[data-node-id=""selection:flow""]')
                && !document.querySelector('.cw-node[data-node-id=""selection:components""]')
                && !document.querySelector('.cw-node[data-node-id=""selection:inputs""]')");
    }

    private static async Task ClearComponentsAsync(IPage page)
    {
        if (await page.Locator(".cw-node[data-node-id='selection:components']").CountAsync() == 0)
        {
            return;
        }

        await FocusCanvasNodeAsync(page, "selection:components");
        await TriggerNodeContextActionAsync(page, ".cw-node[data-node-id='selection:components']", "clear:components");
        await page.WaitForFunctionAsync("() => !document.querySelector('.cw-node[data-node-id=\"selection:components\"]')");
    }

    private static async Task ClearInputsAsync(IPage page)
    {
        if (await page.Locator(".cw-node[data-node-id='selection:inputs']").CountAsync() == 0)
        {
            return;
        }

        await FocusCanvasNodeAsync(page, "selection:inputs");
        await TriggerNodeContextActionAsync(page, ".cw-node[data-node-id='selection:inputs']", "clear:inputs");
        await page.WaitForFunctionAsync("() => !document.querySelector('.cw-node[data-node-id=\"selection:inputs\"]')");
    }

    private static async Task ClearFlowSelectionAsync(IPage page)
    {
        if (await page.Locator(".cw-node[data-node-id='selection:flow']").CountAsync() == 0)
        {
            return;
        }

        await FocusCanvasNodeAsync(page, "selection:flow");
        await TriggerNodeContextActionAsync(page, ".cw-node[data-node-id='selection:flow']", "clear:flow");
        await page.WaitForFunctionAsync("() => !document.querySelector('.cw-node[data-node-id=\"selection:flow\"]')");
    }

    private static async Task ClearBlueprintSelectionAsync(IPage page)
    {
        if (await page.Locator(".cw-node[data-node-id='selection:blueprint']").CountAsync() == 0)
        {
            return;
        }

        await FocusCanvasNodeAsync(page, "selection:blueprint");
        await TriggerNodeContextActionAsync(page, ".cw-node[data-node-id='selection:blueprint']", "clear:blueprint");
        await page.WaitForFunctionAsync("() => !document.querySelector('.cw-node[data-node-id=\"selection:blueprint\"]')");
    }

    private static async Task TriggerNodeContextActionAsync(IPage page, string nodeSelector, string actionId)
    {
        await CloseTransientUiAsync(page);
        await OpenCanvasContextMenuAsync(page, nodeSelector);
        await ClickMenuActionAsync(page, actionId);
    }

    private static string ResolveComponentSection(string groupKey)
        => ComponentSectionByGroup.TryGetValue(groupKey, out var sectionKey) ? sectionKey : "foundation";

    private static string ResolveFlowSection(string flowKey)
        => FlowSectionByKey.TryGetValue(flowKey, out var sectionKey) ? sectionKey : "core-delivery";

    private static string ResolveBlueprintSection(string blueprintKey)
        => BlueprintSectionByKey.TryGetValue(blueprintKey, out var sectionKey) ? sectionKey : "foundation";

    private static string BuildTokenValue(string itemKey, string token)
    {
        var normalized = token.Trim();
        var slug = itemKey.Replace('_', '-');
        if (normalized.Contains("url", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("link", StringComparison.OrdinalIgnoreCase))
        {
            return $"https://example.com/{slug}/{normalized.Replace('_', '-')}";
        }

        if (normalized.Contains("file", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("path", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("module", StringComparison.OrdinalIgnoreCase))
        {
            return $"src/{slug}/{normalized.Replace('_', '-')}.txt";
        }

        if (normalized.Contains("branch", StringComparison.OrdinalIgnoreCase))
        {
            return $"feature/{slug}";
        }

        if (normalized.Contains("commit", StringComparison.OrdinalIgnoreCase))
        {
            return "abc1234";
        }

        if (normalized.Contains("notes", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("summary", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("description", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("context", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("plan", StringComparison.OrdinalIgnoreCase))
        {
            return $"{HumanizeToken(normalized)} for {slug}.";
        }

        return $"{HumanizeToken(normalized)} sample";
    }

    private static string HumanizeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return "Value";
        }

        var words = token
            .Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..])
            .ToList();

        return string.Join(' ', words);
    }

    private static VerificationManifest LoadManifest(string repoRoot)
    {
        var libraryRoot = Path.Combine(repoRoot, "output", "prompt-library");
        var groups = ReadJson<List<GroupRecord>>(Path.Combine(libraryRoot, "group-catalog.json"));
        var components = ReadJson<List<ComponentRecord>>(Path.Combine(libraryRoot, "prompt-component-library.json"));
        var flows = ReadJson<List<FlowRecord>>(Path.Combine(libraryRoot, "factory-prompt-flow-templates.seed.json"));
        var blueprints = ReadJson<List<BlueprintRecord>>(Path.Combine(libraryRoot, "factory-prompt-blueprints.seed.json"));

        return new VerificationManifest(
            groups.Select(item => new VerificationGroup(item.Key, item.Name, item.Purpose, item.Order)).ToList(),
            components.Select(item => new VerificationComponent(item.Key, item.Name, item.Group, item.TemplateTokens ?? [])).ToList(),
            flows.Select(item => new VerificationFlow(item.Key, item.Name, item.Summary, item.OrderIndex)).ToList(),
            blueprints.Select(item => new VerificationBlueprint(item.Key, item.Name, item.Summary, item.OrderIndex)).ToList(),
            BuildInputDefinitions(repoRoot));
    }

    private static IReadOnlyList<VerificationComponent> OrderComponents(VerificationManifest manifest)
    {
        var groupOrderLookup = manifest.Groups.ToDictionary(item => item.Key, item => item.Order, StringComparer.OrdinalIgnoreCase);
        return manifest.Components
            .OrderBy(item => ComponentSectionOrder.TryGetValue(ResolveComponentSection(item.GroupKey), out var order) ? order : int.MaxValue)
            .ThenBy(item => groupOrderLookup.TryGetValue(item.GroupKey, out var groupOrder) ? groupOrder : int.MaxValue)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<VerificationFlow> OrderFlows(VerificationManifest manifest)
        => manifest.Flows
            .OrderBy(item => FlowSectionOrder.TryGetValue(ResolveFlowSection(item.Key), out var order) ? order : int.MaxValue)
            .ThenBy(item => item.OrderIndex)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IReadOnlyList<VerificationBlueprint> OrderBlueprints(VerificationManifest manifest)
        => manifest.Blueprints
            .OrderBy(item => BlueprintSectionOrder.TryGetValue(ResolveBlueprintSection(item.Key), out var order) ? order : int.MaxValue)
            .ThenBy(item => item.OrderIndex)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IReadOnlyList<VerificationInputDefinition> BuildInputDefinitions(string repoRoot)
        =>
        [
            new VerificationInputDefinition(
                "file",
                "File",
                "Prompt file input",
                "docs/prompt-library/README.md",
                "Reference document for the prompt session.",
                Path.Combine(repoRoot, "output", "prompt-library", "README.md")),
            new VerificationInputDefinition(
                "image",
                "Image",
                "Prompt image input",
                "docs/mcp-review/01-dashboard.png",
                "Dashboard screenshot attached as evidence.",
                Path.Combine(repoRoot, "output", "mcp-review", "01-dashboard.png")),
            new VerificationInputDefinition(
                "video",
                "Video",
                "Prompt video input",
                "output/playwright/qa-sample-video.mp4",
                "Short demo clip attached as session evidence.",
                Path.Combine(repoRoot, "output", "playwright", "qa-sample-video.mp4")),
            new VerificationInputDefinition(
                "link",
                "Link",
                "Prompt link input",
                "https://example.com/prompt-library/reference",
                "External reference preserved in the prompt session.",
                null),
            new VerificationInputDefinition(
                "note",
                "Note",
                "Prompt note input",
                "Canvas wizard note",
                "Free-form context captured in the prompt session.",
                null)
        ];

    private static T ReadJson<T>(string path)
        => JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)
           ?? throw new InvalidOperationException($"Could not deserialize '{path}'.");

    private static void WriteReports(string repoRoot, string artifactsRoot, VerificationManifest manifest, IReadOnlyList<VerificationResult> results)
    {
        var markdownPath = Path.Combine(artifactsRoot, "verification-report.md");
        var jsonPath = Path.Combine(artifactsRoot, "verification-report.json");

        var builder = new StringBuilder();
        builder.AppendLine("# Prompt Library Verification");
        builder.AppendLine();
        builder.AppendLine($"- Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        builder.AppendLine($"- Components expected/verified: {manifest.Components.Count}/{results.Count(item => item.Category == "component")}");
        builder.AppendLine($"- Flows expected/verified: {manifest.Flows.Count}/{results.Count(item => item.Category == "flow")}");
        builder.AppendLine($"- Blueprints expected/verified: {manifest.Blueprints.Count}/{results.Count(item => item.Category == "blueprint")}");
        builder.AppendLine($"- Inputs expected/verified: {manifest.Inputs.Count}/{results.Count(item => item.Category == "input")}");
        builder.AppendLine();
        builder.AppendLine("| Category | Key | Name | Modal | Entry Screenshot | Result Screenshot | Notes |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");

        foreach (var result in results)
        {
            var entryRelative = Path.GetRelativePath(repoRoot, result.EntryScreenshotPath).Replace('\\', '/');
            var resultRelative = Path.GetRelativePath(repoRoot, result.ResultScreenshotPath).Replace('\\', '/');
            builder.AppendLine($"| {result.Category} | {result.Key} | {result.Name} | {(result.UsedModal ? "Yes" : "No")} | `{entryRelative}` | `{resultRelative}` | {result.Notes} |");
        }

        File.WriteAllText(markdownPath, builder.ToString(), Encoding.UTF8);

        var jsonPayload = new
        {
            generatedAt = DateTimeOffset.Now,
            expected = new
            {
                components = manifest.Components.Count,
                flows = manifest.Flows.Count,
                blueprints = manifest.Blueprints.Count,
                inputs = manifest.Inputs.Count,
                total = manifest.TotalItemCount
            },
            verified = new
            {
                components = results.Count(item => item.Category == "component"),
                flows = results.Count(item => item.Category == "flow"),
                blueprints = results.Count(item => item.Category == "blueprint"),
                inputs = results.Count(item => item.Category == "input"),
                total = results.Count
            },
            results
        };

        File.WriteAllText(jsonPath, JsonSerializer.Serialize(jsonPayload, JsonOptions), Encoding.UTF8);
    }

    private static void ResetDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Directory.CreateDirectory(path);
    }

    private static string GetRepoRoot()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private sealed record VerificationManifest(
        IReadOnlyList<VerificationGroup> Groups,
        IReadOnlyList<VerificationComponent> Components,
        IReadOnlyList<VerificationFlow> Flows,
        IReadOnlyList<VerificationBlueprint> Blueprints,
        IReadOnlyList<VerificationInputDefinition> Inputs)
    {
        public int TotalItemCount => Components.Count + Flows.Count + Blueprints.Count + Inputs.Count;
    }

    private sealed record VerificationGroup(string Key, string Name, string Purpose, int Order);

    private sealed record VerificationComponent(string Key, string Name, string GroupKey, IReadOnlyList<string> TemplateTokens);

    private sealed record VerificationFlow(string Key, string Name, string Summary, int OrderIndex);

    private sealed record VerificationBlueprint(string Key, string Name, string Summary, int OrderIndex);

    private sealed record VerificationInputDefinition(
        string Key,
        string Label,
        string Title,
        string Subtitle,
        string Notes,
        string? SampleFilePath);

    private sealed record TokenValue(string Key, string Value);

    private sealed class VerificationResult
    {
        public string Category { get; set; } = string.Empty;

        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public bool Passed { get; set; }

        public bool UsedModal { get; set; }

        public string MenuPath { get; set; } = string.Empty;

        public string EntryScreenshotPath { get; set; } = string.Empty;

        public string ResultScreenshotPath { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;
    }

    private sealed class GroupRecord
    {
        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Purpose { get; set; } = string.Empty;

        public int Order { get; set; }
    }

    private sealed class ComponentRecord
    {
        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Group { get; set; } = string.Empty;

        public List<string>? TemplateTokens { get; set; }
    }

    private sealed class FlowRecord
    {
        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;

        public int OrderIndex { get; set; }
    }

    private sealed class BlueprintRecord
    {
        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;

        public int OrderIndex { get; set; }
    }
}
