using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
public sealed partial class PromptLibraryVerificationTests
{
    private readonly PlaywrightAppFixture fixture;

    public PromptLibraryVerificationTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

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
        await page.WaitForTimeoutAsync(150);
        await page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });
    }

    private async Task PreparePromptFactoryAsync(IPage page)
        => await LoadPromptFactoryAsync(page);

    private async Task LoadPromptFactoryAsync(IPage page)
    {
        var response = await page.GotoAsync($"{fixture.BaseUrl}/prompt-factory");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /prompt-factory to return 2xx, got {(int)response.Status}.");

        await page.WaitForSelectorAsync("text=Prompt session workbench");
        await DismissStartupModalIfPresentAsync(page);
        await page.WaitForSelectorAsync(".cw-canvas-host");
        await WaitForNodeAsync(page, "session-root");
        await WaitForNodeAsync(page, "selection:setup");
        await FocusCanvasNodeAsync(page, "session-root");
    }

    private async Task<VerificationResult> VerifyComponentAsync(IPage page, VerificationManifest manifest, VerificationComponent component, int index, string artifactsRoot)
    {
        var slug = $"{index:000}-{component.Key}";
        var entryPath = Path.Combine(artifactsRoot, $"{slug}-{(component.TemplateTokens.Count > 0 ? "specify" : "menu")}.png");
        var resultPath = Path.Combine(artifactsRoot, $"{slug}-added.png");

        await FocusCanvasNodeAsync(page, "session-root");
        await OpenComponentCatalogAsync(page, component);

        IReadOnlyList<TokenValue> tokenValues = [];
        await CaptureCanvasStageAsync(page, entryPath);
        await ClickToolboxItemAsync(page, component);
        if (component.TemplateTokens.Count > 0)
        {
            await WaitForComposerAsync(page);
            tokenValues = await FillComponentComposerAsync(page, component);
            await SubmitComposerAsync(page);
        }

        var nodeId = $"selection:component:{component.Key}";
        await WaitForNodeAsync(page, nodeId);
        await FocusCanvasNodeAsync(page, nodeId);
        var nodeText = await ReadNodeTextAsync(page, nodeId);

        Assert.Contains(component.Name, nodeText, StringComparison.OrdinalIgnoreCase);
        if (tokenValues.Count > 0)
        {
            var matchedToken = tokenValues.FirstOrDefault(item =>
                !string.IsNullOrWhiteSpace(item.Value) &&
                nodeText.Contains(item.Value, StringComparison.OrdinalIgnoreCase));
            Assert.True(
                matchedToken is not null,
                $"Expected component node '{nodeId}' to contain at least one configured token value.{Environment.NewLine}" +
                $"Configured values: {string.Join(", ", tokenValues.Select(item => $"{item.Key}='{item.Value}'"))}{Environment.NewLine}" +
                $"Actual text:{Environment.NewLine}{nodeText}");
        }

        await CaptureCanvasStageAsync(page, resultPath);

        return new VerificationResult
        {
            Category = "component",
            Key = component.Key,
            Name = component.Name,
            Passed = true,
            UsedModal = component.TemplateTokens.Count > 0,
            MenuPath = $"components-toolbox > search > prompt-factory-component-{component.Key}",
            EntryScreenshotPath = entryPath,
            ResultScreenshotPath = resultPath,
            Notes = tokenValues.Count > 0
                ? $"Configured {tokenValues.Count} token value(s) from the toolbox composer and verified the node text."
                : "Verified the node was added from the floating components toolbox."
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
        await AssertCanvasActionAvailableAsync(page, menuPath[^1]);
        await CaptureCanvasStageAsync(page, entryPath);
        await InvokeCanvasContextActionAsync(page, "session-root", menuPath[^1]);
        await ConfirmPromptImpactDialogIfPresentAsync(page, "Use flow");

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
        await AssertCanvasActionAvailableAsync(page, menuPath[^1]);
        await CaptureCanvasStageAsync(page, entryPath);
        await InvokeCanvasContextActionAsync(page, "session-root", menuPath[^1]);
        await ConfirmPromptImpactDialogIfPresentAsync(page, "Use blueprint");

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
        await CaptureCanvasStageAsync(page, entryPath);
        await AssertCanvasActionAvailableAsync(page, menuPath[^1]);
        await InvokeCanvasInputCreateActionAsync(page, input);

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
            var input = fields.Nth(index).Locator("input, textarea").First;
            await SetComposerFieldValueAsync(input, value);

            values.Add(new TokenValue(token, value));
        }

        await page.WaitForTimeoutAsync(100);
        return values;
    }

    private static async Task SetComposerFieldValueAsync(ILocator input, string value)
    {
        await input.FillAsync(value);
        var actualValue = await input.InputValueAsync();
        if (string.Equals(actualValue, value, StringComparison.Ordinal))
        {
            return;
        }

        await input.EvaluateAsync(
            @"(element, requestedValue) => {
                element.value = requestedValue;
                element.dispatchEvent(new Event('input', { bubbles: true }));
                element.dispatchEvent(new Event('change', { bubbles: true }));
            }",
            value);

        actualValue = await input.InputValueAsync();
        Assert.Equal(value, actualValue);
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
        var submitButton = page.Locator(".cw-canvas-composer__actions button[data-tone='accent']");
        try
        {
            await page.WaitForFunctionAsync(
                @"() => {
                    const button = document.querySelector('.cw-canvas-composer__actions button[data-tone=""accent""]');
                    return !!button && !button.disabled;
                }",
                null,
                new PageWaitForFunctionOptions
                {
                    Timeout = 15_000
                });
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            var snapshot = await CaptureComposerSnapshotAsync(page);
            var fieldSummary = string.Join(
                Environment.NewLine,
                snapshot.Fields.Select(field => $"- {field.Label} [{field.TagName}] = '{field.Value}'"));
            Assert.Fail(
                $"Composer submit button remained disabled.{Environment.NewLine}" +
                $"Button text: {snapshot.SubmitText}{Environment.NewLine}" +
                $"Field count: {snapshot.Fields.Count}{Environment.NewLine}" +
                $"Fields:{Environment.NewLine}{fieldSummary}");
        }

        await submitButton.ClickAsync();
        await page.WaitForFunctionAsync("() => !document.querySelector('.cw-canvas-composer')");
    }

    private static async Task<ComposerSnapshot> CaptureComposerSnapshotAsync(IPage page)
    {
        var json = await page.EvaluateAsync<string>(
            @"() => {
                const composer = document.querySelector('.cw-canvas-composer');
                const submitButton = composer?.querySelector('.cw-canvas-composer__actions button[data-tone=""accent""]');
                const fields = Array.from(composer?.querySelectorAll('.cw-canvas-composer__field') || []).map(field => {
                    const input = field.querySelector('input, textarea, select');
                    const label = field.querySelector('span')?.textContent?.trim() || '';
                    const tagName = input?.tagName?.toLowerCase?.() || '';
                    const value = input?.value ?? '';
                    return { label, tagName, value };
                });

                return JSON.stringify({
                    submitText: submitButton?.textContent?.trim() || '',
                    fields
                });
            }");

        return JsonSerializer.Deserialize<ComposerSnapshot>(json, JsonOptions)
            ?? new ComposerSnapshot(string.Empty, []);
    }

    private static async Task WaitForComposerAsync(IPage page)
        => await page.Locator(".cw-canvas-composer.is-dialog").WaitForAsync();

    private static async Task WaitForNodeAsync(IPage page, string nodeId)
        => await page.WaitForFunctionAsync(
            @"requestedNodeId => {
                const nodes = document.querySelector('.cw-canvas-host')?.__canvasWorkbenchState?.surface?.nodes;
                return Array.isArray(nodes) && nodes.some(node => node?.id === requestedNodeId);
            }",
            nodeId);

    private static async Task<string> WaitForSingleInputNodeAsync(IPage page)
    {
        await page.WaitForFunctionAsync(
            @"() => {
                const nodes = document.querySelector('.cw-canvas-host')?.__canvasWorkbenchState?.surface?.nodes;
                return Array.isArray(nodes) && nodes.some(node => typeof node?.id === 'string' && node.id.startsWith('selection:input:'));
            }");

        var nodeId = await page.EvaluateAsync<string>(
            @"() => {
                const nodes = document.querySelector('.cw-canvas-host')?.__canvasWorkbenchState?.surface?.nodes;
                if (!Array.isArray(nodes)) {
                    return '';
                }

                const matches = nodes.filter(node => typeof node?.id === 'string' && node.id.startsWith('selection:input:'));
                const node = matches.length > 0 ? matches[matches.length - 1] : null;
                return node?.id || '';
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
        => await page.EvaluateAsync<string>(
            @"requestedNodeId => {
                const nodes = document.querySelector('.cw-canvas-host')?.__canvasWorkbenchState?.surface?.nodes;
                if (!Array.isArray(nodes)) {
                    return '';
                }

                const node = nodes.find(candidate => candidate?.id === requestedNodeId);
                if (!node) {
                    return '';
                }

                const parts = [
                    node.title,
                    node.subtitle,
                    node.inlineText,
                    node.leadText,
                    node.notes,
                    node.compactPath?.displayText,
                    node.compactPath?.promotedText,
                    node.compactPath?.fullPath,
                    node.objectSubtype
                ];
                if (Array.isArray(node.chips)) {
                    parts.push(...node.chips.map(chip => chip?.text || ''));
                }

                if (Array.isArray(node.footerChips)) {
                    parts.push(...node.footerChips.map(chip => chip?.text || ''));
                }

                if (Array.isArray(node.annotations)) {
                    parts.push(...node.annotations.map(annotation => annotation?.label || annotation?.text || ''));
                }

                return parts
                    .filter(part => typeof part === 'string' && part.trim().length > 0)
                    .join('\n');
            }",
            nodeId);

    private static Task<bool> NodeExistsAsync(IPage page, string nodeId)
        => page.EvaluateAsync<bool>(
            @"requestedNodeId => {
                const nodes = document.querySelector('.cw-canvas-host')?.__canvasWorkbenchState?.surface?.nodes;
                return Array.isArray(nodes) && nodes.some(node => node?.id === requestedNodeId);
            }",
            nodeId);

    private static Task<int> ReadNodeCountByPrefixAsync(IPage page, string prefix)
        => page.EvaluateAsync<int>(
            @"requestedPrefix => {
                const nodes = document.querySelector('.cw-canvas-host')?.__canvasWorkbenchState?.surface?.nodes;
                if (!Array.isArray(nodes)) {
                    return 0;
                }

                return nodes.filter(node => typeof node?.id === 'string' && node.id.startsWith(requestedPrefix)).length;
            }",
            prefix);

    private static async Task CaptureCanvasStageAsync(IPage page, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await page.Locator(".cw-stage-surface").ScreenshotAsync(new() { Path = path });
    }

    private static async Task CaptureWorkspaceAsync(IPage page, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await page.Locator(".cw-workbench-shell").ScreenshotAsync(new() { Path = path });
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

    private static async Task AssertCanvasActionAvailableAsync(IPage page, string actionId)
    {
        var isAvailable = await page.EvaluateAsync<bool>(
            @"requestedActionId => {
                const host = document.querySelector('.cw-canvas-host');
                const state = host?.__canvasWorkbenchState;
                const pending = [...(state?.surface?.chrome?.quickCreateActions || [])];
                while (pending.length > 0) {
                    const action = pending.pop();
                    if (!action) {
                        continue;
                    }

                    if (action.actionId === requestedActionId) {
                        return true;
                    }

                    if (Array.isArray(action.children)) {
                        pending.push(...action.children);
                    }
                }

                return false;
            }",
            actionId);
        Assert.True(isAvailable, $"Expected canvas quick-create actions to include '{actionId}'.");
    }

    private static async Task InvokeCanvasContextActionAsync(IPage page, string nodeId, string actionId)
    {
        var invoked = await page.EvaluateAsync<bool>(
            @"async payload => {
                const host = document.querySelector('.cw-canvas-host');
                const state = host?.__canvasWorkbenchState;
                if (!state?.dotNetRef?.invokeMethodAsync) {
                    return false;
                }

                await state.dotNetRef.invokeMethodAsync('OnContextAction', payload.nodeId, payload.actionId, 0, 0);
                return true;
            }",
            new { nodeId, actionId });
        Assert.True(invoked, $"Expected canvas context action '{actionId}' to be invokable.");
    }

    private static async Task InvokeCanvasCreateActionAsync(IPage page, string actionId, IReadOnlyList<TokenValue> tokenValues)
    {
        var invoked = await page.EvaluateAsync<bool>(
            @"async payload => {
                const host = document.querySelector('.cw-canvas-host');
                const state = host?.__canvasWorkbenchState;
                if (!state?.dotNetRef?.invokeMethodAsync) {
                    return false;
                }

                await state.dotNetRef.invokeMethodAsync('OnCreateAction', JSON.stringify({
                    actionId: payload.actionId,
                    sourceNodeId: 'session-root',
                    x: 0,
                    y: 0,
                    parentNodeId: 'session-root',
                    title: '',
                    subtitle: '',
                    notes: '',
                    placementKind: 'child',
                    createMode: payload.tokenValues.length > 0 ? 'dialog' : 'create',
                    objectSubtype: '',
                    uploadedFile: null,
                    inputValues: payload.tokenValues
                }));
                return true;
            }",
            new
            {
                actionId,
                tokenValues = tokenValues.Select(item => new { key = item.Key, value = item.Value }).ToList()
            });
        Assert.True(invoked, $"Expected canvas create action '{actionId}' to be invokable.");
    }

    private static async Task InvokeCanvasInputCreateActionAsync(IPage page, VerificationInputDefinition input)
    {
        var uploadedFile = input.SampleFilePath is null
            ? null
            : new
            {
                fileName = Path.GetFileName(input.SampleFilePath),
                contentType = ResolveContentType(input.SampleFilePath),
                base64Data = Convert.ToBase64String(await File.ReadAllBytesAsync(input.SampleFilePath))
            };

        var invoked = await page.EvaluateAsync<bool>(
            @"async payload => {
                const host = document.querySelector('.cw-canvas-host');
                const state = host?.__canvasWorkbenchState;
                if (!state?.dotNetRef?.invokeMethodAsync) {
                    return false;
                }

                await state.dotNetRef.invokeMethodAsync('OnCreateAction', JSON.stringify({
                    actionId: payload.actionId,
                    sourceNodeId: 'session-root',
                    x: 0,
                    y: 0,
                    parentNodeId: 'session-root',
                    title: payload.title,
                    subtitle: payload.subtitle,
                    notes: payload.notes,
                    placementKind: 'child',
                    createMode: 'dialog',
                    objectSubtype: payload.objectSubtype,
                    uploadedFile: payload.uploadedFile,
                    inputValues: []
                }));
                return true;
            }",
            new
            {
                actionId = $"input:add:{input.Key}",
                title = input.Title,
                subtitle = input.Subtitle,
                notes = input.Notes,
                objectSubtype = input.Key,
                uploadedFile
            });
        Assert.True(invoked, $"Expected canvas input action '{input.Key}' to be invokable.");
    }

    private static async Task ConfirmPromptImpactDialogIfPresentAsync(IPage page, string confirmLabel)
    {
        var confirmDialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Confirm prompt-factory action" });
        if (!await WaitForLocatorAsync(confirmDialog, 1_000))
        {
            return;
        }

        await confirmDialog.Locator("button[data-tone='warn']").ClickAsync();
    }

    private static async Task OpenComponentCatalogAsync(IPage page, VerificationComponent component)
    {
        var searchLabel = ResolveComponentCatalogLabel(component.Name);
        var toolboxWindow = page.GetByTestId("prompt-factory-components-toolbox-window");
        if (!await WaitForLocatorAsync(toolboxWindow, 1_000))
        {
            await page.GetByTestId("prompt-factory-components-toolbox-toggle").ClickAsync();
            await toolboxWindow.WaitForAsync();
        }

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var search = page.Locator("[data-testid='prompt-factory-components-toolbox'] .cw-context-toolbox__search");
                await search.WaitForAsync();
                await search.FillAsync(string.Empty);
                await search.FillAsync(searchLabel);
                await page.WaitForFunctionAsync(
                    @"testId => !!document.querySelector(`[data-testid=""${testId}""]`)",
                    $"prompt-factory-component-{component.Key}");
                return;
            }
            catch when (attempt < 3)
            {
                var search = page.Locator("[data-testid='prompt-factory-components-toolbox'] .cw-context-toolbox__search");
                if (await search.CountAsync() > 0)
                {
                    await search.FillAsync(string.Empty);
                }
                await page.WaitForTimeoutAsync(120);
            }
        }

        throw new InvalidOperationException($"Could not open the component catalog entry for '{component.Key}'.");
    }

    private static async Task ExpandQuickCreateMenuPathAsync(IPage page, IReadOnlyList<string> actionIds)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                await CloseTransientUiAsync(page);
                await OpenQuickCreateMenuAsync(page);

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

        throw new InvalidOperationException($"Could not expand quick-create menu path '{string.Join(" > ", actionIds)}'.");
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

    private static async Task ClickToolboxItemAsync(IPage page, VerificationComponent component)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var button = page.GetByTestId($"prompt-factory-component-{component.Key}");
                await button.WaitForAsync();
                await button.ClickAsync();
                return;
            }
            catch when (attempt < 3)
            {
                await page.WaitForTimeoutAsync(120);
            }
        }

        throw new InvalidOperationException($"Could not click the toolbox item for '{component.Key}'.");
    }

    private static async Task HoverComponentPreviewAsync(IPage page, string componentKey)
    {
        var previewToggle = page.GetByTestId($"prompt-factory-component-{componentKey}")
            .Locator("xpath=ancestor::div[contains(@class,'pf-components-toolbox__item-row')]")
            .Locator(".pf-components-toolbox__preview-toggle");
        await previewToggle.WaitForAsync();
        await previewToggle.HoverAsync();
        await page.GetByTestId("prompt-factory-component-preview-popover").WaitForAsync();
    }

    private static async Task AssertComponentPreviewPlacementAsync(IPage page, string placement)
    {
        await page.WaitForFunctionAsync(
            @"expectedPlacement => {
                const popover = document.querySelector('[data-testid=""prompt-factory-component-preview-popover""]');
                return popover?.getAttribute('data-placement') === expectedPlacement;
            }",
            placement);
    }

    private static async Task DragFloatingWindowAsync(IPage page, string testId, float targetX, float targetY)
    {
        var dragHandle = page.GetByTestId(testId).Locator(".cw-floating-window__drag");
        await dragHandle.WaitForAsync();

        var bounds = await dragHandle.BoundingBoxAsync();
        Assert.NotNull(bounds);

        await page.Mouse.MoveAsync((float)(bounds!.X + (bounds.Width / 2)), (float)(bounds.Y + (bounds.Height / 2)));
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(targetX, targetY, new MouseMoveOptions { Steps = 14 });
        await page.Mouse.UpAsync();
        await page.WaitForTimeoutAsync(220);
    }

    private static async Task OpenQuickCreateMenuAsync(IPage page)
    {
        await CloseTransientUiAsync(page);
        await page.EvaluateAsync(
            @"() => {
                const host = document.querySelector('.cw-canvas-host');
                const button = document.querySelector('button[aria-label=""Open quick create actions""]');
                if (host && button) {
                    window.CanDoItAll.canvasWorkbench.openQuickCreateMenu(host, button);
                }
            }");
        var menuVisible = await WaitForMenuActionAsync(page, "catalog-components", 1_500);
        if (!menuVisible)
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "Open quick create actions" }).ClickAsync();
            menuVisible = await WaitForMenuActionAsync(page, "catalog-components", 1_500);
        }

        Assert.True(menuVisible, "Expected the quick create menu to open.");
    }

    private static async Task<bool> WaitForMenuActionAsync(IPage page, string actionId, float timeoutMs)
    {
        try
        {
            await page.Locator(ActionSelector(actionId)).Last.WaitForAsync(new() { Timeout = timeoutMs });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
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
        Assert.True(
            await WaitForMenuActionAsync(page, actionId, 1_500),
            $"Expected menu action '{actionId}' to be visible.");
        var selector = ActionSelector(actionId);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                var activated = await page.EvaluateAsync<bool>(
                    @"actionSelector => {
                        const matches = Array.from(document.querySelectorAll(actionSelector));
                        const node = matches.length > 0 ? matches[matches.length - 1] : null;
                        if (!(node instanceof HTMLElement)) {
                            return false;
                        }

                        const rect = node.getBoundingClientRect();
                        const payload = {
                            bubbles: true,
                            cancelable: true,
                            clientX: rect.left + (rect.width / 2),
                            clientY: rect.top + (rect.height / 2),
                            pointerId: 1,
                            pointerType: 'mouse'
                        };

                        for (const type of ['pointerenter', 'pointermove']) {
                            const pointerEvent = typeof PointerEvent === 'function'
                                ? new PointerEvent(type, payload)
                                : new MouseEvent(type, payload);
                            node.dispatchEvent(pointerEvent);
                        }

                        for (const type of ['mouseenter', 'mouseover', 'mousemove']) {
                            node.dispatchEvent(new MouseEvent(type, payload));
                        }

                        node.focus({ preventScroll: true });
                        return true;
                    }",
                    selector);
                if (!activated)
                {
                    await page.WaitForTimeoutAsync(120);
                    continue;
                }

                await page.WaitForTimeoutAsync(180);
                return;
            }
            catch (PlaywrightException)
            {
                await page.WaitForTimeoutAsync(120);
            }
        }

        throw new InvalidOperationException($"Could not activate menu action '{actionId}' after repeated rerenders.");
    }

    private static string ActionSelector(string actionId)
        => $".cw-context-menu__action[data-action-id=\"{actionId}\"]";

    private static Task<string?> ResolveCanvasNodeIdAsync(IPage page, string selector)
    {
        var exactMatch = Regex.Match(selector, @"data-node-id='(?<id>[^']+)'", RegexOptions.IgnoreCase);
        if (exactMatch.Success)
        {
            return Task.FromResult<string?>(exactMatch.Groups["id"].Value);
        }

        var prefixMatch = Regex.Match(selector, @"data-node-id\^='(?<prefix>[^']+)'", RegexOptions.IgnoreCase);
        if (prefixMatch.Success)
        {
            return page.EvaluateAsync<string?>(
                @"prefix => {
                    const nodes = document.querySelector('.cw-canvas-host')?.__canvasWorkbenchState?.surface?.nodes;
                    if (!Array.isArray(nodes)) {
                        return null;
                    }

                    const match = nodes.find(node => typeof node?.id === 'string' && node.id.startsWith(prefix));
                    return match?.id || null;
                }",
                prefixMatch.Groups["prefix"].Value);
        }

        return Task.FromResult<string?>(null);
    }

    private static async Task<(float X, float Y)> ReadCanvasNodeCenterAsync(IPage page, string selector)
    {
        var nodeId = await ResolveCanvasNodeIdAsync(page, selector);
        Assert.False(string.IsNullOrWhiteSpace(nodeId), $"Could not resolve a canvas node id for selector '{selector}'.");

        var center = await page.EvaluateAsync<CanvasPoint?>(
            @"requestedNodeId => {
                const workbench = window.CanDoItAll?.canvasWorkbench;
                const host = document.querySelector('.cw-canvas-host');
                if (!(host instanceof HTMLElement) || !workbench?.getSceneSnapshot) {
                    return null;
                }

                const snapshot = workbench.getSceneSnapshot(host);
                const node = snapshot?.nodes?.find(candidate => candidate?.id === requestedNodeId);
                if (!node) {
                    return null;
                }

                return {
                    x: node.left + (node.width / 2),
                    y: node.top + (node.height / 2)
                };
            }",
            nodeId);
        Assert.NotNull(center);
        return ((float)center!.X, (float)center.Y);
    }

    private static async Task OpenCanvasContextMenuAsync(IPage page, string selector)
    {
        var nodeId = await ResolveCanvasNodeIdAsync(page, selector);
        Assert.False(string.IsNullOrWhiteSpace(nodeId), $"Canvas node '{selector}' was not found.");

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await CloseTransientUiAsync(page);

            await RightClickCanvasNodeAsync(page, selector);
            if (await WaitForContextMenuAsync(page, 1_500))
            {
                return;
            }

            await DispatchContextMenuAsync(page, selector);
            if (await WaitForContextMenuAsync(page, 1_500))
            {
                return;
            }
        }

        throw new InvalidOperationException($"Could not open the canvas context menu for '{selector}'.");
    }

    private static async Task RightClickCanvasNodeAsync(IPage page, string selector)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var center = await ReadCanvasNodeCenterAsync(page, selector);

            try
            {
                await page.Mouse.ClickAsync(
                    center.X,
                    center.Y,
                    new() { Button = MouseButton.Right });
                return;
            }
            catch (PlaywrightException exception) when (exception.Message.Contains("attached", StringComparison.OrdinalIgnoreCase) || exception.Message.Contains("target closed", StringComparison.OrdinalIgnoreCase))
            {
                await page.WaitForTimeoutAsync(120);
            }
        }

        throw new InvalidOperationException($"Could not right-click canvas node '{selector}' after repeated rerenders.");
    }

    private static async Task<bool> WaitForContextMenuAsync(IPage page, float timeoutMs)
    {
        try
        {
            await page.Locator(".cw-context-menu__action").First.WaitForAsync(new() { Timeout = timeoutMs });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static async Task DispatchContextMenuAsync(IPage page, string selector)
    {
        var center = await ReadCanvasNodeCenterAsync(page, selector);
        await page.EvaluateAsync(
            @"request => {
                const host = document.querySelector('.cw-canvas-host');
                if (!(host instanceof HTMLElement)) {
                    return;
                }

                host.dispatchEvent(new MouseEvent('contextmenu', {
                    bubbles: true,
                    cancelable: true,
                    button: 2,
                    buttons: 2,
                    clientX: request.x,
                    clientY: request.y
                }));
            }",
            new { x = center.X, y = center.Y });
    }

    private static async Task CloseTransientUiAsync(IPage page)
    {
        for (var index = 0; index < 2; index++)
        {
            await page.Keyboard.PressAsync("Escape");
            await page.WaitForTimeoutAsync(80);
        }
    }

    private async Task ResetSessionAsync(IPage page)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await LoadPromptFactoryAsync(page);
            await CloseTransientUiAsync(page);
            await FocusCanvasNodeAsync(page, "session-root");
            await InvokeCanvasContextActionAsync(page, "session-root", "reset:session");
            var confirmDialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Confirm prompt-factory action" });
            if (await WaitForLocatorAsync(confirmDialog, 1_500))
            {
                await confirmDialog.Locator("button[data-tone='warn']").ClickAsync();
            }

            if (await WaitForResetStateAsync(page, 5_000))
            {
                await FocusCanvasNodeAsync(page, "session-root");
                return;
            }
        }

        throw new InvalidOperationException("Prompt Factory reset did not converge after repeated retries.");
    }

    private static async Task<bool> WaitForLocatorAsync(ILocator locator, float timeoutMs)
    {
        try
        {
            await locator.WaitForAsync(new() { Timeout = timeoutMs });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static async Task DismissStartupModalIfPresentAsync(IPage page, float timeoutMs = 1_500)
    {
        var startupDialog = page.GetByTestId("database-startup-modal");
        if (!await WaitForLocatorAsync(startupDialog, timeoutMs))
        {
            return;
        }

        await page.GetByTestId("database-startup-continue").ClickAsync();
        await startupDialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }

    private static async Task<bool> WaitForResetStateAsync(IPage page, float timeoutMs)
    {
        try
        {
            await page.WaitForFunctionAsync(
                @"() => {
                    const nodes = document.querySelector('.cw-canvas-host')?.__canvasWorkbenchState?.surface?.nodes || [];
                    const has = id => Array.isArray(nodes) && nodes.some(node => node?.id === id);
                    return !has('selection:blueprint')
                        && !has('selection:flow')
                        && !has('selection:components')
                        && !has('selection:inputs')
                        && has('selection:setup');
                }",
                null,
                new() { Timeout = timeoutMs });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static async Task ClearComponentsAsync(IPage page)
    {
        if (!await NodeExistsAsync(page, "selection:components"))
        {
            return;
        }

        await FocusCanvasNodeAsync(page, "selection:components");
        await TriggerNodeContextActionAsync(page, "selection:components", "clear:components");
        await page.WaitForFunctionAsync(
            @"() => {
                const nodes = document.querySelector('.cw-canvas-host')?.__canvasWorkbenchState?.surface?.nodes;
                return !Array.isArray(nodes) || !nodes.some(node => node?.id === 'selection:components');
            }");
    }

    private static async Task ClearInputsAsync(IPage page)
    {
        if (!await NodeExistsAsync(page, "selection:inputs"))
        {
            return;
        }

        await FocusCanvasNodeAsync(page, "selection:inputs");
        await TriggerNodeContextActionAsync(page, "selection:inputs", "clear:inputs");
        await page.WaitForFunctionAsync(
            @"() => {
                const nodes = document.querySelector('.cw-canvas-host')?.__canvasWorkbenchState?.surface?.nodes;
                return !Array.isArray(nodes) || !nodes.some(node => node?.id === 'selection:inputs');
            }");
    }

    private static async Task ClearFlowSelectionAsync(IPage page)
    {
        if (!await NodeExistsAsync(page, "selection:flow"))
        {
            return;
        }

        await FocusCanvasNodeAsync(page, "selection:flow");
        await TriggerNodeContextActionAsync(page, "selection:flow", "clear:flow");
        await page.WaitForFunctionAsync(
            @"() => {
                const nodes = document.querySelector('.cw-canvas-host')?.__canvasWorkbenchState?.surface?.nodes;
                return !Array.isArray(nodes) || !nodes.some(node => node?.id === 'selection:flow');
            }");
    }

    private static async Task ClearBlueprintSelectionAsync(IPage page)
    {
        if (!await NodeExistsAsync(page, "selection:blueprint"))
        {
            return;
        }

        await FocusCanvasNodeAsync(page, "selection:blueprint");
        await TriggerNodeContextActionAsync(page, "selection:blueprint", "clear:blueprint");
        await page.WaitForFunctionAsync(
            @"() => {
                const nodes = document.querySelector('.cw-canvas-host')?.__canvasWorkbenchState?.surface?.nodes;
                return !Array.isArray(nodes) || !nodes.some(node => node?.id === 'selection:blueprint');
            }");
    }

    private static async Task TriggerNodeContextActionAsync(IPage page, string nodeId, string actionId)
    {
        await CloseTransientUiAsync(page);
        await InvokeCanvasContextActionAsync(page, nodeId, actionId);
    }

    private static string ResolveComponentSection(string groupKey)
        => ComponentSectionByGroup.TryGetValue(groupKey, out var sectionKey) ? sectionKey : "foundation";

    private static string ResolveFlowSection(string flowKey)
        => FlowSectionByKey.TryGetValue(flowKey, out var sectionKey) ? sectionKey : "core-delivery";

    private static string ResolveBlueprintSection(string blueprintKey)
        => BlueprintSectionByKey.TryGetValue(blueprintKey, out var sectionKey) ? sectionKey : "foundation";

    private static string ResolveComponentCatalogLabel(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Component";
        }

        var separatorIndex = name.IndexOf(": ", StringComparison.Ordinal);
        return separatorIndex >= 0 ? name[(separatorIndex + 2)..] : name;
    }

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

    private static string ResolveContentType(string path)
        => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".md" => "text/markdown",
            ".txt" => "text/plain",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".svg" => "image/svg+xml",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            _ => "application/octet-stream"
        };

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
                "output/mcp-review/02-projects-wizard.png",
                "Short demo clip attached as session evidence.",
                Path.Combine(repoRoot, "output", "mcp-review", "02-projects-wizard.png")),
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

private sealed record ComposerSnapshot(
    string SubmitText,
    IReadOnlyList<ComposerFieldSnapshot> Fields);

private sealed record ComposerFieldSnapshot(
    string Label,
    string TagName,
    string Value);

private sealed class CanvasPoint
{
    public double X { get; set; }

    public double Y { get; set; }
}

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

    private sealed class MenuActionBounds
    {
        public float X { get; set; }

        public float Y { get; set; }
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
