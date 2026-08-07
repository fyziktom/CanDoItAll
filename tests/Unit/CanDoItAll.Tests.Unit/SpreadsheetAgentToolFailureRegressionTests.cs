using System.Runtime.CompilerServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tools.Documents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Unit;

public sealed class SpreadsheetAgentToolFailureRegressionTests
{
    [Theory]
    [InlineData(SpreadsheetWriteInputFailureKind.UnsupportedInputWorkbookFormat)]
    [InlineData(SpreadsheetWriteInputFailureKind.UnsupportedOutputWorkbookFormat)]
    [InlineData(SpreadsheetWriteInputFailureKind.InvalidWorksheetName)]
    [InlineData(SpreadsheetWriteInputFailureKind.MissingCellWrites)]
    [InlineData(SpreadsheetWriteInputFailureKind.MissingCellWrite)]
    [InlineData(SpreadsheetWriteInputFailureKind.InvalidCellAddress)]
    [InlineData(SpreadsheetWriteInputFailureKind.MissingRangeWrites)]
    [InlineData(SpreadsheetWriteInputFailureKind.MissingRangeWrite)]
    [InlineData(SpreadsheetWriteInputFailureKind.InvalidRangeAddress)]
    [InlineData(SpreadsheetWriteInputFailureKind.MissingRangeValues)]
    [InlineData(SpreadsheetWriteInputFailureKind.MissingRangeRow)]
    [InlineData(SpreadsheetWriteInputFailureKind.InputWorkbookMissing)]
    public void Predictable_write_input_failure_throws_typed_document_exception(
        SpreadsheetWriteInputFailureKind expectedKind)
    {
        using var temp = new SpreadsheetRegressionTempDirectory();
        var workbookPath = Path.Combine(temp.Path, "invalid-input.xlsx");
        var service = new ClosedXmlSpreadsheetDocumentService();

        var exception = Assert.Throws<SpreadsheetWriteInputException>(() =>
            service.Write(CreateInvalidWriteRequest(workbookPath, expectedKind)));

        Assert.Equal(expectedKind, exception.Kind);
        Assert.False(File.Exists(workbookPath));
    }

    [Fact]
    public void Existing_input_workbook_can_be_updated_in_place_without_overwrite()
    {
        using var temp = new SpreadsheetRegressionTempDirectory();
        var workbookPath = Path.Combine(temp.Path, "in-place-update.xlsx");
        var service = new ClosedXmlSpreadsheetDocumentService();
        service.Write(CreateValidWriteRequest(workbookPath, overwrite: false));

        var result = service.Write(new SpreadsheetWriteRequest(
            workbookPath,
            workbookPath,
            "Calculations",
            [new SpreadsheetCellWrite("A1", "=1+1")],
            [],
            CreateWorkbookIfMissing: true,
            Overwrite: false));

        Assert.Equal(workbookPath, result.WorkbookPath);
        Assert.Equal("Created", service.ReadCell(workbookPath, "Summary", "A1").Value);
        Assert.Equal("=1+1", service.ReadCell(workbookPath, "Calculations", "A1").Value);
    }

    [Fact]
    public void Existing_distinct_output_without_overwrite_throws_typed_document_conflict()
    {
        using var temp = new SpreadsheetRegressionTempDirectory();
        var inputPath = Path.Combine(temp.Path, "input.xlsx");
        var outputPath = Path.Combine(temp.Path, "existing-output.xlsx");
        var service = new ClosedXmlSpreadsheetDocumentService();
        service.Write(CreateValidWriteRequest(inputPath, overwrite: false));
        service.Write(CreateValidWriteRequest(outputPath, overwrite: false));

        var exception = Assert.Throws<SpreadsheetWriteConflictException>(() =>
            service.Write(new SpreadsheetWriteRequest(
                inputPath,
                outputPath,
                "Copy",
                [new SpreadsheetCellWrite("A1", "Changed")],
                [],
                CreateWorkbookIfMissing: true,
                Overwrite: false)));

        Assert.DoesNotContain(outputPath, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Created", service.ReadCell(outputPath, "Summary", "A1").Value);
    }

    [Fact]
    public void Range_write_with_more_values_than_columns_throws_typed_capacity_failure()
    {
        using var temp = new SpreadsheetRegressionTempDirectory();
        var workbookPath = Path.Combine(temp.Path, "range-capacity.xlsx");
        var service = new ClosedXmlSpreadsheetDocumentService();

        var exception = Assert.Throws<SpreadsheetRangeCapacityExceededException>(() =>
            service.Write(CreateMalformedWriteRequest(workbookPath)));

        Assert.Equal("A1:B12", exception.RangeAddress);
        Assert.Equal(SpreadsheetRangeCapacityDimension.Columns, exception.Dimension);
        Assert.Equal(2, exception.Capacity);
        Assert.Equal(3, exception.SuppliedCount);
        Assert.Equal(6, exception.ValuesRowNumber);
        Assert.Contains("A1:B12", exception.Message, StringComparison.Ordinal);
        Assert.Contains("2 column", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("row 6", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("3 value", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(workbookPath));
    }

    [Fact]
    public void Range_write_with_more_value_rows_than_range_throws_typed_capacity_failure()
    {
        using var temp = new SpreadsheetRegressionTempDirectory();
        var workbookPath = Path.Combine(temp.Path, "row-capacity.xlsx");
        var service = new ClosedXmlSpreadsheetDocumentService();
        var request = new SpreadsheetWriteRequest(
            workbookPath,
            workbookPath,
            "Summary",
            [],
            [new SpreadsheetRangeWrite("A1:B2", [["A", "B"], ["C", "D"], ["E", "F"]])],
            CreateWorkbookIfMissing: true,
            Overwrite: true);

        var exception = Assert.Throws<SpreadsheetRangeCapacityExceededException>(() =>
            service.Write(request));

        Assert.Equal("A1:B2", exception.RangeAddress);
        Assert.Equal(SpreadsheetRangeCapacityDimension.Rows, exception.Dimension);
        Assert.Equal(2, exception.Capacity);
        Assert.Equal(3, exception.SuppliedCount);
        Assert.Null(exception.ValuesRowNumber);
        Assert.False(File.Exists(workbookPath));
    }

    [Fact]
    public void Spreadsheet_runtime_maps_row_capacity_failure_to_actionable_safe_tool_input_failure()
    {
        using var temp = new SpreadsheetRegressionTempDirectory();
        var plugin = CreatePlugin(
            temp.Path,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            });

        var exception = Assert.Throws<AgentToolInputValidationException>(() =>
            plugin.WriteSpreadsheetWorkbook(
                "row-capacity.xlsx",
                "Summary",
                rangeWrites:
                [
                    new SpreadsheetRangeWrite(
                        "A1:B2",
                        [["A", "B"], ["C", "D"], ["E", "F"]])
                ],
                createWorkbookIfMissing: true,
                overwrite: true));
        var mapped = MafAgentToolFailureMapper.TryMap(exception, out var result);

        Assert.True(mapped);
        Assert.False(result.Succeeded);
        Assert.Equal(AgentToolInputValidationException.FailureCode, result.ErrorCode);
        Assert.Contains("A1:B2", result.Message, StringComparison.Ordinal);
        Assert.Contains("2 row", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("3 row", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.CanRetryWithCorrectedInput);
        Assert.DoesNotContain(temp.Path, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(temp.Path, "row-capacity.xlsx")));
    }

    [Fact]
    public void Spreadsheet_runtime_maps_range_capacity_failure_to_actionable_safe_tool_input_failure()
    {
        using var temp = new SpreadsheetRegressionTempDirectory();
        var plugin = CreatePlugin(
            temp.Path,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            });

        var exception = Assert.Throws<AgentToolInputValidationException>(() =>
            plugin.WriteSpreadsheetWorkbook(
                "range-capacity.xlsx",
                "Summary",
                rangeWrites: [new SpreadsheetRangeWrite("A1:B12", CreateMalformedRows())],
                createWorkbookIfMissing: true,
                overwrite: true));
        var mapped = MafAgentToolFailureMapper.TryMap(exception, out var result);

        Assert.True(mapped);
        Assert.False(result.Succeeded);
        Assert.Equal("InvalidToolInput", result.ErrorCode);
        Assert.Contains("A1:B12", result.Message, StringComparison.Ordinal);
        Assert.Contains("2 column", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("row 6", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("3 value", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.CanRetryWithCorrectedInput);
        Assert.DoesNotContain(temp.Path, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(temp.Path, "range-capacity.xlsx")));
    }

    [Fact]
    public async Task Instrumented_agent_returns_actionable_failure_and_executes_corrected_retry()
    {
        using var temp = new SpreadsheetRegressionTempDirectory();
        await using var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var plugin = CreatePlugin(
            temp.Path,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            });
        var function = AIFunctionFactory.Create(
            plugin.WriteSpreadsheetWorkbook,
            ToolContractCatalog.WorkspaceWriteSpreadsheet,
            "Writes an XLSX workbook.");
        var capabilityState = new RuntimeCapabilityState();
        capabilityState.Tools.Add(function);
        var client = new SpreadsheetRetryScriptChatClient();
        var uninstrumentedAgent = new ChatClientAgent(
            client,
            new ChatClientAgentOptions
            {
                ChatOptions = new ChatOptions
                {
                    Tools = [function]
                },
                UseProvidedChatClientAsIs = false
            });
        var runtimeFactory = new MafRuntimeAgentFactory(
            temp.Path,
            WorkspaceScopeDescriptor.Sandbox,
            new UnusedProviderCredentialService(),
            new UnusedProviderAgentFactory(),
            new UnusedRuntimeCapabilityComposer(),
            services.GetRequiredService<ILoggerFactory>());
        var agentDefinition = CreateToolEnabledAgent();
        var instrumentedAgent = runtimeFactory.CreateInstrumentedAgent(
            uninstrumentedAgent,
            CreateProviderProfile(),
            agentDefinition,
            capabilityState,
            suppressApprovalRequirements: true,
            toolInvocationTraceRecorder: new ToolInvocationTraceRecorder(),
            finalizerPolicy: null,
            finalizerMode: AgentFinalizerMode.Disabled);
        var session = await instrumentedAgent.CreateSessionAsync();

        var response = await instrumentedAgent.RunAsync(
            [new ChatMessage(ChatRole.User, "Create the workbook and correct retryable input errors.")],
            session);

        Assert.Equal("completed", response.Text);
        var failure = Assert.IsType<AgentToolFailureResult>(client.FirstToolResult);
        Assert.Equal(AgentToolInputValidationException.FailureCode, failure.ErrorCode);
        Assert.Contains("A1:B1", failure.Message, StringComparison.Ordinal);
        Assert.Contains("3 column", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(failure.CanRetryWithCorrectedInput);
        var workbookPath = Path.Combine(temp.Path, "instrumented-retry.xlsx");
        Assert.True(File.Exists(workbookPath));
        var written = new ClosedXmlSpreadsheetDocumentService().ReadRange(
            workbookPath,
            "Summary",
            "A1:C1",
            maxRows: 1,
            maxColumns: 3);
        Assert.Equal(["Metric", "Value", "Unit"], written.Values[0]);
    }

    [Fact]
    public async Task Instrumented_agent_can_add_worksheets_to_same_workbook_without_overwrite()
    {
        using var temp = new SpreadsheetRegressionTempDirectory();
        await using var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var plugin = CreatePlugin(
            temp.Path,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            });
        var function = AIFunctionFactory.Create(
            plugin.WriteSpreadsheetWorkbook,
            ToolContractCatalog.WorkspaceWriteSpreadsheet,
            "Writes an XLSX workbook.");
        var capabilityState = new RuntimeCapabilityState();
        capabilityState.Tools.Add(function);
        var client = new SpreadsheetInPlaceUpdateScriptChatClient();
        var uninstrumentedAgent = new ChatClientAgent(
            client,
            new ChatClientAgentOptions
            {
                ChatOptions = new ChatOptions
                {
                    Tools = [function]
                },
                UseProvidedChatClientAsIs = false
            });
        var runtimeFactory = new MafRuntimeAgentFactory(
            temp.Path,
            WorkspaceScopeDescriptor.Sandbox,
            new UnusedProviderCredentialService(),
            new UnusedProviderAgentFactory(),
            new UnusedRuntimeCapabilityComposer(),
            services.GetRequiredService<ILoggerFactory>());
        var instrumentedAgent = runtimeFactory.CreateInstrumentedAgent(
            uninstrumentedAgent,
            CreateProviderProfile(),
            CreateToolEnabledAgent(),
            capabilityState,
            suppressApprovalRequirements: true,
            toolInvocationTraceRecorder: new ToolInvocationTraceRecorder(),
            finalizerPolicy: null,
            finalizerMode: AgentFinalizerMode.Disabled);
        var session = await instrumentedAgent.CreateSessionAsync();

        var response = await instrumentedAgent.RunAsync(
            [new ChatMessage(ChatRole.User, "Create one workbook with summary and calculation worksheets.")],
            session);

        Assert.Equal("completed", response.Text);
        Assert.Equal(2, client.SuccessfulToolResults.Count);
        Assert.All(
            client.SuccessfulToolResults,
            result => Assert.True(Assert.IsType<JsonElement>(result).GetProperty("succeeded").GetBoolean()));
        var workbookPath = Path.Combine(temp.Path, "instrumented-in-place.xlsx");
        var spreadsheets = new ClosedXmlSpreadsheetDocumentService();
        Assert.Equal("Summary", spreadsheets.ReadCell(workbookPath, "Summary", "A1").Value);
        Assert.Equal("=1+1", spreadsheets.ReadCell(workbookPath, "Calculations", "A1").Value);
    }

    [Theory]
    [InlineData(SpreadsheetWriteInputFailureKind.UnsupportedInputWorkbookFormat, "workbookPath must end with .xlsx")]
    [InlineData(SpreadsheetWriteInputFailureKind.UnsupportedOutputWorkbookFormat, "outputWorkbookPath must end with .xlsx")]
    [InlineData(SpreadsheetWriteInputFailureKind.InvalidWorksheetName, "worksheetName")]
    [InlineData(SpreadsheetWriteInputFailureKind.MissingCellWrite, "cellWrites item 1")]
    [InlineData(SpreadsheetWriteInputFailureKind.InvalidCellAddress, "cellAddress")]
    [InlineData(SpreadsheetWriteInputFailureKind.MissingRangeWrite, "rangeWrites item 1")]
    [InlineData(SpreadsheetWriteInputFailureKind.InvalidRangeAddress, "rangeAddress")]
    [InlineData(SpreadsheetWriteInputFailureKind.MissingRangeValues, "values array")]
    [InlineData(SpreadsheetWriteInputFailureKind.MissingRangeRow, "values row 1")]
    [InlineData(SpreadsheetWriteInputFailureKind.InputWorkbookMissing, "createWorkbookIfMissing")]
    public void Spreadsheet_runtime_maps_known_write_input_failure_to_safe_retryable_result(
        SpreadsheetWriteInputFailureKind failureKind,
        string expectedMessageFragment)
    {
        using var temp = new SpreadsheetRegressionTempDirectory();
        var plugin = CreatePlugin(
            temp.Path,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            });

        var exception = Assert.Throws<AgentToolInputValidationException>(() =>
            InvokeInvalidPluginWrite(plugin, failureKind));
        var mapped = MafAgentToolFailureMapper.TryMap(exception, out var result);

        Assert.True(mapped);
        Assert.False(result.Succeeded);
        Assert.Equal(AgentToolInputValidationException.FailureCode, result.ErrorCode);
        Assert.Contains(expectedMessageFragment, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.CanRetryWithCorrectedInput);
        Assert.DoesNotContain(temp.Path, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(temp.Path, "invalid-input.xlsx")));
    }

    [Fact]
    public void Spreadsheet_runtime_maps_existing_output_to_safe_retryable_conflict()
    {
        using var temp = new SpreadsheetRegressionTempDirectory();
        var plugin = CreatePlugin(
            temp.Path,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            });
        plugin.WriteSpreadsheetWorkbook(
            "input.xlsx",
            "Summary",
            cellWrites: [new SpreadsheetCellWrite("A1", "Created")],
            createWorkbookIfMissing: true,
            overwrite: false);
        plugin.WriteSpreadsheetWorkbook(
            "existing-output.xlsx",
            "Summary",
            cellWrites: [new SpreadsheetCellWrite("A1", "Existing")],
            createWorkbookIfMissing: true,
            overwrite: false);

        var exception = Assert.Throws<AgentToolConflictException>(() =>
            plugin.WriteSpreadsheetWorkbook(
                "input.xlsx",
                "Summary",
                outputWorkbookPath: "existing-output.xlsx",
                cellWrites: [new SpreadsheetCellWrite("A1", "Changed")],
                createWorkbookIfMissing: true,
                overwrite: false));
        var mapped = MafAgentToolFailureMapper.TryMap(exception, out var result);

        Assert.True(mapped);
        Assert.False(result.Succeeded);
        Assert.Equal(AgentToolConflictException.FailureCode, result.ErrorCode);
        Assert.Contains("already exists", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("overwrite", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.CanRetryWithCorrectedInput);
        Assert.DoesNotContain(temp.Path, result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Spreadsheet_runtime_updates_implicit_input_output_in_place_without_overwrite()
    {
        using var temp = new SpreadsheetRegressionTempDirectory();
        var plugin = CreatePlugin(
            temp.Path,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            });
        plugin.WriteSpreadsheetWorkbook(
            "in-place.xlsx",
            "Summary",
            cellWrites: [new SpreadsheetCellWrite("A1", "Summary")],
            createWorkbookIfMissing: true,
            overwrite: false);

        var result = plugin.WriteSpreadsheetWorkbook(
            "in-place.xlsx",
            "Calculations",
            cellWrites: [new SpreadsheetCellWrite("A1", "=1+1")],
            createWorkbookIfMissing: true,
            overwrite: false);

        Assert.True(result.Succeeded);
        Assert.Equal("in-place.xlsx", result.WorkbookPath);
        Assert.Equal("Summary", plugin.ReadSpreadsheetCell("in-place.xlsx", "Summary", "A1").Value);
        Assert.Equal("=1+1", plugin.ReadSpreadsheetCell("in-place.xlsx", "Calculations", "A1").Value);
    }

    [Fact]
    public void Spreadsheet_runtime_maps_corrupt_existing_write_input_to_safe_retryable_failure()
    {
        using var temp = new SpreadsheetRegressionTempDirectory();
        var workbookPath = Path.Combine(temp.Path, "corrupt-existing.xlsx");
        File.WriteAllText(workbookPath, "not an xlsx workbook");
        var plugin = CreatePlugin(
            temp.Path,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            });

        var exception = Assert.Throws<AgentToolInputValidationException>(() =>
            plugin.WriteSpreadsheetWorkbook(
                "corrupt-existing.xlsx",
                "Summary",
                createWorkbookIfMissing: false,
                overwrite: true));
        var mapped = MafAgentToolFailureMapper.TryMap(exception, out var result);

        Assert.True(mapped);
        Assert.Equal(AgentToolInputValidationException.FailureCode, result.ErrorCode);
        Assert.Contains("invalid or corrupt", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.CanRetryWithCorrectedInput);
        Assert.DoesNotContain(temp.Path, result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(UnexpectedWriteFailureKind.Argument)]
    [InlineData(UnexpectedWriteFailureKind.InvalidOperation)]
    [InlineData(UnexpectedWriteFailureKind.Io)]
    public void Spreadsheet_runtime_does_not_expose_untyped_document_failure(
        UnexpectedWriteFailureKind failureKind)
    {
        using var temp = new SpreadsheetRegressionTempDirectory();
        var expected = CreateUnexpectedFailure(failureKind, temp.Path);
        var plugin = CreatePlugin(
            temp.Path,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            },
            new ThrowingSpreadsheetDocumentService(expected));

        var exception = Record.Exception(() =>
            plugin.WriteSpreadsheetWorkbook(
                "unexpected.xlsx",
                "Summary",
                createWorkbookIfMissing: true,
                overwrite: true));

        Assert.Same(expected, exception);
        Assert.False(MafAgentToolFailureMapper.TryMap(exception!, out _));
    }

    [Theory]
    [InlineData(SpreadsheetReadFailureScenario.WorkbookMissing, "existing .xlsx workbook")]
    [InlineData(SpreadsheetReadFailureScenario.UnsupportedWorkbookFormat, "must identify an .xlsx workbook")]
    [InlineData(SpreadsheetReadFailureScenario.InvalidWorkbook, "invalid or corrupt")]
    [InlineData(SpreadsheetReadFailureScenario.WorksheetNotFound, "workspace_spreadsheet_summary")]
    [InlineData(SpreadsheetReadFailureScenario.InvalidCellAddress, "cellAddress")]
    [InlineData(SpreadsheetReadFailureScenario.InvalidRangeAddress, "rangeAddress")]
    [InlineData(SpreadsheetReadFailureScenario.PreviewLimitOutOfRange, "maxWorksheets")]
    [InlineData(SpreadsheetReadFailureScenario.ReadLimitOutOfRange, "maxRows")]
    public void Spreadsheet_runtime_maps_known_read_failure_to_safe_retryable_result(
        SpreadsheetReadFailureScenario scenario,
        string expectedMessageFragment)
    {
        using var temp = new SpreadsheetRegressionTempDirectory();
        var service = new ClosedXmlSpreadsheetDocumentService();
        var plugin = CreatePlugin(
            temp.Path,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            },
            service);
        PrepareReadFailureScenario(temp.Path, service, scenario);

        var exception = Assert.Throws<AgentToolInputValidationException>(() =>
            InvokeInvalidPluginRead(plugin, scenario));
        var mapped = MafAgentToolFailureMapper.TryMap(exception, out var result);

        Assert.True(mapped);
        Assert.False(result.Succeeded);
        Assert.Equal(AgentToolInputValidationException.FailureCode, result.ErrorCode);
        Assert.Contains(expectedMessageFragment, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.CanRetryWithCorrectedInput);
        Assert.DoesNotContain(temp.Path, result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(".", "workbookPath", "directory")]
    [InlineData("../outside.xlsx", "workbookPath", "accessible workspace file path")]
    [InlineData("external-target/C/../outside.xlsx", "workbookPath", "accessible workspace file path")]
    public void Spreadsheet_runtime_maps_predictable_read_path_failure_to_safe_retryable_result(
        string workbookPath,
        string expectedArgumentName,
        string expectedMessageFragment)
    {
        using var temp = new SpreadsheetRegressionTempDirectory();
        var plugin = CreatePlugin(
            temp.Path,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            });

        var exception = Assert.Throws<AgentToolInputValidationException>(() =>
            plugin.InspectWorkbook(workbookPath));
        var mapped = MafAgentToolFailureMapper.TryMap(exception, out var result);

        Assert.True(mapped);
        Assert.Equal(AgentToolInputValidationException.FailureCode, result.ErrorCode);
        Assert.Contains(expectedArgumentName, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedMessageFragment, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.CanRetryWithCorrectedInput);
        Assert.DoesNotContain(temp.Path, result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(".", null, "workbookPath", "directory")]
    [InlineData("input.xlsx", "../outside.xlsx", "outputWorkbookPath", "accessible workspace file path")]
    public void Spreadsheet_runtime_maps_predictable_write_path_failure_to_safe_retryable_result(
        string workbookPath,
        string? outputWorkbookPath,
        string expectedArgumentName,
        string expectedMessageFragment)
    {
        using var temp = new SpreadsheetRegressionTempDirectory();
        var plugin = CreatePlugin(
            temp.Path,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            });

        var exception = Assert.Throws<AgentToolInputValidationException>(() =>
            plugin.WriteSpreadsheetWorkbook(
                workbookPath,
                "Summary",
                outputWorkbookPath,
                createWorkbookIfMissing: true,
                overwrite: true));
        var mapped = MafAgentToolFailureMapper.TryMap(exception, out var result);

        Assert.True(mapped);
        Assert.Equal(AgentToolInputValidationException.FailureCode, result.ErrorCode);
        Assert.Contains(expectedArgumentName, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedMessageFragment, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.CanRetryWithCorrectedInput);
        Assert.DoesNotContain(temp.Path, result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(UnexpectedWriteFailureKind.Argument)]
    [InlineData(UnexpectedWriteFailureKind.InvalidOperation)]
    [InlineData(UnexpectedWriteFailureKind.Io)]
    public void Spreadsheet_runtime_does_not_expose_untyped_read_failure(
        UnexpectedWriteFailureKind failureKind)
    {
        using var temp = new SpreadsheetRegressionTempDirectory();
        File.WriteAllBytes(Path.Combine(temp.Path, "unexpected.xlsx"), [0]);
        var expected = CreateUnexpectedFailure(failureKind, temp.Path);
        var plugin = CreatePlugin(
            temp.Path,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            },
            new ThrowingSpreadsheetDocumentService(expected));

        var exception = Record.Exception(() =>
            plugin.InspectWorkbook("unexpected.xlsx"));

        Assert.Same(expected, exception);
        Assert.False(MafAgentToolFailureMapper.TryMap(exception!, out _));
    }

    [Fact]
    public void Spreadsheet_runtime_read_access_denial_maps_to_typed_safe_failure()
    {
        using var temp = new SpreadsheetRegressionTempDirectory();
        var plugin = CreatePlugin(
            temp.Path,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = false,
                CanWriteFiles = false
            });

        var exception = Assert.Throws<WorkspaceToolAccessDeniedException>(() =>
            plugin.InspectWorkbook("private-report.xlsx"));
        var mapped = MafAgentToolFailureMapper.TryMap(exception, out var result);

        Assert.True(mapped);
        Assert.False(result.Succeeded);
        Assert.Equal(WorkspaceToolAccessDeniedException.FailureCode, result.ErrorCode);
        Assert.Equal(exception.SafeMessage, result.Message);
        Assert.Contains("not allowed to read workspace files", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.CanRetryWithCorrectedInput);
        Assert.DoesNotContain(temp.Path, result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Spreadsheet_runtime_write_access_denial_maps_to_typed_safe_failure()
    {
        using var temp = new SpreadsheetRegressionTempDirectory();
        var plugin = CreatePlugin(
            temp.Path,
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true
            });

        var exception = Assert.Throws<WorkspaceToolAccessDeniedException>(() =>
            plugin.WriteSpreadsheetWorkbook(
                "private-report.xlsx",
                "Summary",
                createWorkbookIfMissing: true,
                overwrite: true));
        var mapped = MafAgentToolFailureMapper.TryMap(exception, out var result);

        Assert.True(mapped);
        Assert.False(result.Succeeded);
        Assert.Equal(WorkspaceToolAccessDeniedException.FailureCode, result.ErrorCode);
        Assert.Equal(exception.SafeMessage, result.Message);
        Assert.Contains("not allowed to write workspace files", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.CanRetryWithCorrectedInput);
        Assert.DoesNotContain(temp.Path, result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static SpreadsheetWriteRequest CreateMalformedWriteRequest(string workbookPath)
        => new(
            workbookPath,
            workbookPath,
            "Summary",
            [],
            [new SpreadsheetRangeWrite("A1:B12", CreateMalformedRows())],
            CreateWorkbookIfMissing: true,
            Overwrite: true);

    private static void PrepareReadFailureScenario(
        string workspaceRoot,
        ISpreadsheetDocumentService service,
        SpreadsheetReadFailureScenario scenario)
    {
        switch (scenario)
        {
            case SpreadsheetReadFailureScenario.WorkbookMissing:
                return;
            case SpreadsheetReadFailureScenario.UnsupportedWorkbookFormat:
                File.WriteAllText(Path.Combine(workspaceRoot, "unsupported.xls"), "not used");
                return;
            case SpreadsheetReadFailureScenario.InvalidWorkbook:
                File.WriteAllText(Path.Combine(workspaceRoot, "invalid.xlsx"), "not an xlsx workbook");
                return;
            case SpreadsheetReadFailureScenario.WorksheetNotFound:
            case SpreadsheetReadFailureScenario.InvalidCellAddress:
            case SpreadsheetReadFailureScenario.InvalidRangeAddress:
            case SpreadsheetReadFailureScenario.PreviewLimitOutOfRange:
            case SpreadsheetReadFailureScenario.ReadLimitOutOfRange:
                service.Write(CreateValidWriteRequest(
                    Path.Combine(workspaceRoot, "valid.xlsx"),
                    overwrite: true));
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(scenario),
                    scenario,
                    "Unknown spreadsheet read failure scenario.");
        }
    }

    private static void InvokeInvalidPluginRead(
        WorkspaceSpreadsheetRuntimePlugin plugin,
        SpreadsheetReadFailureScenario scenario)
    {
        switch (scenario)
        {
            case SpreadsheetReadFailureScenario.WorkbookMissing:
                plugin.InspectWorkbook("missing.xlsx");
                return;
            case SpreadsheetReadFailureScenario.UnsupportedWorkbookFormat:
                plugin.InspectWorkbook("unsupported.xls");
                return;
            case SpreadsheetReadFailureScenario.InvalidWorkbook:
                plugin.InspectWorkbook("invalid.xlsx");
                return;
            case SpreadsheetReadFailureScenario.WorksheetNotFound:
                plugin.ReadSpreadsheetCell("valid.xlsx", "Missing", "A1");
                return;
            case SpreadsheetReadFailureScenario.InvalidCellAddress:
                plugin.ReadSpreadsheetCell("valid.xlsx", "Summary", "A0");
                return;
            case SpreadsheetReadFailureScenario.InvalidRangeAddress:
                plugin.ReadSpreadsheetRange("valid.xlsx", "Summary", "A1:B0");
                return;
            case SpreadsheetReadFailureScenario.PreviewLimitOutOfRange:
                plugin.PreviewWorkbook("valid.xlsx", maxWorksheets: 0);
                return;
            case SpreadsheetReadFailureScenario.ReadLimitOutOfRange:
                plugin.ReadSpreadsheetRange(
                    "valid.xlsx",
                    "Summary",
                    "A1",
                    maxRows: 0);
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(scenario),
                    scenario,
                    "Unknown spreadsheet read failure scenario.");
        }
    }

    private static SpreadsheetWriteRequest CreateValidWriteRequest(
        string workbookPath,
        bool overwrite)
        => new(
            workbookPath,
            workbookPath,
            "Summary",
            [new SpreadsheetCellWrite("A1", "Created")],
            [],
            CreateWorkbookIfMissing: true,
            Overwrite: overwrite);

    private static SpreadsheetWriteRequest CreateInvalidWriteRequest(
        string workbookPath,
        SpreadsheetWriteInputFailureKind failureKind)
    {
        var inputPath = failureKind is SpreadsheetWriteInputFailureKind.UnsupportedInputWorkbookFormat
            ? Path.ChangeExtension(workbookPath, ".csv")
            : workbookPath;
        var outputPath = failureKind is SpreadsheetWriteInputFailureKind.UnsupportedOutputWorkbookFormat
            ? Path.ChangeExtension(workbookPath, ".csv")
            : workbookPath;
        var worksheetName = failureKind is SpreadsheetWriteInputFailureKind.InvalidWorksheetName
            ? "Invalid/Worksheet"
            : "Summary";
        IReadOnlyList<SpreadsheetCellWrite> cellWrites = failureKind switch
        {
            SpreadsheetWriteInputFailureKind.MissingCellWrites => null!,
            SpreadsheetWriteInputFailureKind.MissingCellWrite => new SpreadsheetCellWrite[] { null! },
            SpreadsheetWriteInputFailureKind.InvalidCellAddress => [new SpreadsheetCellWrite("A0", "value")],
            _ => []
        };
        IReadOnlyList<SpreadsheetRangeWrite> rangeWrites = failureKind switch
        {
            SpreadsheetWriteInputFailureKind.MissingRangeWrites => null!,
            SpreadsheetWriteInputFailureKind.MissingRangeWrite => new SpreadsheetRangeWrite[] { null! },
            SpreadsheetWriteInputFailureKind.InvalidRangeAddress =>
                [new SpreadsheetRangeWrite("A1:B0", [["value"]])],
            SpreadsheetWriteInputFailureKind.MissingRangeValues =>
                [new SpreadsheetRangeWrite("A1:B2", null!)],
            SpreadsheetWriteInputFailureKind.MissingRangeRow =>
                [new SpreadsheetRangeWrite("A1:B2", new IReadOnlyList<string>[] { null! })],
            _ => []
        };

        return new SpreadsheetWriteRequest(
            inputPath,
            outputPath,
            worksheetName,
            cellWrites,
            rangeWrites,
            CreateWorkbookIfMissing: failureKind is not SpreadsheetWriteInputFailureKind.InputWorkbookMissing,
            Overwrite: true);
    }

    private static void InvokeInvalidPluginWrite(
        WorkspaceSpreadsheetRuntimePlugin plugin,
        SpreadsheetWriteInputFailureKind failureKind)
    {
        switch (failureKind)
        {
            case SpreadsheetWriteInputFailureKind.UnsupportedInputWorkbookFormat:
                plugin.WriteSpreadsheetWorkbook(
                    "invalid-input.csv",
                    "Summary",
                    outputWorkbookPath: "invalid-output.xlsx",
                    createWorkbookIfMissing: true,
                    overwrite: true);
                return;
            case SpreadsheetWriteInputFailureKind.UnsupportedOutputWorkbookFormat:
                plugin.WriteSpreadsheetWorkbook(
                    "invalid-input.xlsx",
                    "Summary",
                    outputWorkbookPath: "invalid-output.csv",
                    createWorkbookIfMissing: true,
                    overwrite: true);
                return;
            case SpreadsheetWriteInputFailureKind.InvalidWorksheetName:
                plugin.WriteSpreadsheetWorkbook(
                    "invalid-input.xlsx",
                    "Invalid/Worksheet",
                    createWorkbookIfMissing: true,
                    overwrite: true);
                return;
            case SpreadsheetWriteInputFailureKind.MissingCellWrite:
                plugin.WriteSpreadsheetWorkbook(
                    "invalid-input.xlsx",
                    "Summary",
                    cellWrites: new SpreadsheetCellWrite[] { null! },
                    createWorkbookIfMissing: true,
                    overwrite: true);
                return;
            case SpreadsheetWriteInputFailureKind.InvalidCellAddress:
                plugin.WriteSpreadsheetWorkbook(
                    "invalid-input.xlsx",
                    "Summary",
                    cellWrites: [new SpreadsheetCellWrite("A0", "value")],
                    createWorkbookIfMissing: true,
                    overwrite: true);
                return;
            case SpreadsheetWriteInputFailureKind.MissingRangeWrite:
                plugin.WriteSpreadsheetWorkbook(
                    "invalid-input.xlsx",
                    "Summary",
                    rangeWrites: new SpreadsheetRangeWrite[] { null! },
                    createWorkbookIfMissing: true,
                    overwrite: true);
                return;
            case SpreadsheetWriteInputFailureKind.InvalidRangeAddress:
                plugin.WriteSpreadsheetWorkbook(
                    "invalid-input.xlsx",
                    "Summary",
                    rangeWrites: [new SpreadsheetRangeWrite("A1:B0", [["value"]])],
                    createWorkbookIfMissing: true,
                    overwrite: true);
                return;
            case SpreadsheetWriteInputFailureKind.MissingRangeValues:
                plugin.WriteSpreadsheetWorkbook(
                    "invalid-input.xlsx",
                    "Summary",
                    rangeWrites: [new SpreadsheetRangeWrite("A1:B2", null!)],
                    createWorkbookIfMissing: true,
                    overwrite: true);
                return;
            case SpreadsheetWriteInputFailureKind.MissingRangeRow:
                plugin.WriteSpreadsheetWorkbook(
                    "invalid-input.xlsx",
                    "Summary",
                    rangeWrites:
                    [
                        new SpreadsheetRangeWrite(
                            "A1:B2",
                            new IReadOnlyList<string>[] { null! })
                    ],
                    createWorkbookIfMissing: true,
                    overwrite: true);
                return;
            case SpreadsheetWriteInputFailureKind.InputWorkbookMissing:
                plugin.WriteSpreadsheetWorkbook(
                    "invalid-input.xlsx",
                    "Summary",
                    createWorkbookIfMissing: false,
                    overwrite: true);
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(failureKind),
                    failureKind,
                    "The failure kind is not reachable through the runtime plugin contract.");
        }
    }

    private static Exception CreateUnexpectedFailure(
        UnexpectedWriteFailureKind failureKind,
        string sensitivePath)
        => failureKind switch
        {
            UnexpectedWriteFailureKind.Argument =>
                new ArgumentException($"Sensitive argument failure at '{sensitivePath}'."),
            UnexpectedWriteFailureKind.InvalidOperation =>
                new InvalidOperationException($"Sensitive operation failure at '{sensitivePath}'."),
            UnexpectedWriteFailureKind.Io =>
                new IOException($"Sensitive I/O failure at '{sensitivePath}'."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(failureKind),
                failureKind,
                "Unknown unexpected spreadsheet failure kind.")
        };

    private static IReadOnlyList<IReadOnlyList<string>> CreateMalformedRows()
        =>
        [
            ["Metric", "Value"],
            ["Area", "120"],
            ["Width", "10"],
            ["Length", "12"],
            ["Spacing", "0.5"],
            ["", "", ""],
            ["Quantity", "24"],
            ["Unit cost", "3"],
            ["Subtotal", "=B7*B8"],
            ["Tax", "0.2"],
            ["Total", "=B9*(1+B10)"],
            ["Status", "Ready"]
        ];

    private static WorkspaceSpreadsheetRuntimePlugin CreatePlugin(
        string workspaceRoot,
        AgentWorkspaceToolAccessSettings access,
        ISpreadsheetDocumentService? spreadsheets = null)
        => new(
            spreadsheets ?? new ClosedXmlSpreadsheetDocumentService(),
            workspaceRoot,
            WorkspaceScopeDescriptor.Sandbox,
            access);

    private static AgentDefinition CreateToolEnabledAgent()
        => new(
            Id: Guid.NewGuid(),
            Name: "Spreadsheet Retry Agent",
            RoleTitle: "Tester",
            Summary: "Tests spreadsheet retry behavior.",
            Instructions: "Use the supplied spreadsheet tool and correct retryable failures.",
            Status: AgentLifecycleStatus.Active,
            ProviderProfileId: Guid.NewGuid(),
            Model: string.Empty,
            Workload: AgentWorkloadKind.Programming,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: AgentWorkspaceToolAccessMetadata.Write(
                "{}",
                new AgentWorkspaceToolAccessSettings
                {
                    CanReadFiles = true,
                    CanWriteFiles = true
                }),
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default with
            {
                CanUseTools = true,
                RequiresApprovalForExternalCalls = false
            },
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow);

    private static ProviderProfile CreateProviderProfile()
        => new(
            Guid.NewGuid(),
            "Spreadsheet Test Provider",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "OPENAI_API_KEY",
            "gpt-4.1",
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: []);

    public enum UnexpectedWriteFailureKind
    {
        Argument,
        InvalidOperation,
        Io
    }

    public enum SpreadsheetReadFailureScenario
    {
        WorkbookMissing,
        UnsupportedWorkbookFormat,
        InvalidWorkbook,
        WorksheetNotFound,
        InvalidCellAddress,
        InvalidRangeAddress,
        PreviewLimitOutOfRange,
        ReadLimitOutOfRange
    }

    private sealed class ThrowingSpreadsheetDocumentService(Exception exception)
        : ISpreadsheetDocumentService
    {
        public SpreadsheetWorkbookSummary InspectWorkbook(string workbookPath)
            => throw exception;

        public SpreadsheetWorkbookPreviewResult PreviewWorkbook(
            SpreadsheetWorkbookPreviewRequest request)
            => throw exception;

        public SpreadsheetWorkbookContentPreviewResult PreviewWorkbook(
            SpreadsheetWorkbookContentPreviewRequest request)
            => throw new NotSupportedException();

        public SpreadsheetCellValue ReadCell(
            string workbookPath,
            string worksheetName,
            string cellAddress)
            => throw exception;

        public SpreadsheetRangeReadResult ReadRange(
            string workbookPath,
            string worksheetName,
            string rangeAddress,
            int maxRows,
            int maxColumns)
            => throw exception;

        public SpreadsheetWriteResult Write(SpreadsheetWriteRequest request)
            => throw exception;
    }

    private sealed class SpreadsheetRetryScriptChatClient : IChatClient
    {
        private const string ToolName = ToolContractCatalog.WorkspaceWriteSpreadsheet;
        private int responseCount;

        public object? FirstToolResult { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = responseCount++ switch
            {
                0 => CreateToolCallResponse(
                    "call-malformed",
                    "A1:B1"),
                1 => CreateCorrectedToolCallResponse(messages),
                _ => new ChatResponse(
                    new ChatMessage(ChatRole.Assistant, "completed"))
            };
            return Task.FromResult(response);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var update in response.ToChatResponseUpdates())
            {
                yield return update;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this)
                ? this
                : null;

        public void Dispose()
        {
        }

        private ChatResponse CreateCorrectedToolCallResponse(
            IEnumerable<ChatMessage> messages)
        {
            FirstToolResult = messages
                .SelectMany(message => message.Contents)
                .OfType<FunctionResultContent>()
                .Last()
                .Result;
            return CreateToolCallResponse(
                "call-corrected",
                "A1:C1");
        }

        private static ChatResponse CreateToolCallResponse(
            string callId,
            string rangeAddress)
            => new(
                new ChatMessage(
                    ChatRole.Assistant,
                    [
                        new FunctionCallContent(
                            callId,
                            ToolName,
                            new Dictionary<string, object?>
                            {
                                ["workbookPath"] = "instrumented-retry.xlsx",
                                ["worksheetName"] = "Summary",
                                ["rangeWrites"] = new[]
                                {
                                    new SpreadsheetRangeWrite(
                                        rangeAddress,
                                        [["Metric", "Value", "Unit"]])
                                },
                                ["createWorkbookIfMissing"] = true,
                                ["overwrite"] = true
                            })
                    ]));
    }

    private sealed class SpreadsheetInPlaceUpdateScriptChatClient : IChatClient
    {
        private const string ToolName = ToolContractCatalog.WorkspaceWriteSpreadsheet;
        private int responseCount;

        public List<object?> SuccessfulToolResults { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = responseCount++ switch
            {
                0 => CreateToolCallResponse(
                    "call-summary",
                    "Summary",
                    "Summary"),
                1 => CreateFollowUpResponse(
                    messages,
                    "call-calculations",
                    "Calculations",
                    "=1+1"),
                _ => CreateCompletedResponse(messages)
            };
            return Task.FromResult(response);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var update in response.ToChatResponseUpdates())
            {
                yield return update;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this)
                ? this
                : null;

        public void Dispose()
        {
        }

        private ChatResponse CreateFollowUpResponse(
            IEnumerable<ChatMessage> messages,
            string callId,
            string worksheetName,
            string value)
        {
            RecordLatestToolResult(messages);
            return CreateToolCallResponse(callId, worksheetName, value);
        }

        private ChatResponse CreateCompletedResponse(IEnumerable<ChatMessage> messages)
        {
            RecordLatestToolResult(messages);
            return new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "completed"));
        }

        private void RecordLatestToolResult(IEnumerable<ChatMessage> messages)
        {
            SuccessfulToolResults.Add(messages
                .SelectMany(message => message.Contents)
                .OfType<FunctionResultContent>()
                .Last()
                .Result);
        }

        private static ChatResponse CreateToolCallResponse(
            string callId,
            string worksheetName,
            string value)
            => new(
                new ChatMessage(
                    ChatRole.Assistant,
                    [
                        new FunctionCallContent(
                            callId,
                            ToolName,
                            new Dictionary<string, object?>
                            {
                                ["workbookPath"] = "instrumented-in-place.xlsx",
                                ["worksheetName"] = worksheetName,
                                ["cellWrites"] = new[]
                                {
                                    new SpreadsheetCellWrite("A1", value)
                                },
                                ["createWorkbookIfMissing"] = true,
                                ["overwrite"] = false
                            })
                    ]));
    }

    private sealed class UnusedProviderCredentialService : IMafProviderCredentialService
    {
        public ProviderCredentialResolution Resolve(ProviderProfile provider)
            => throw new NotSupportedException();

        public string ResolveOpenAiCredentialOverride(ProviderProfile provider)
            => throw new NotSupportedException();
    }

    private sealed class UnusedProviderAgentFactory : IMafProviderAgentFactory
    {
        public AIAgent CreateFrameworkAgent(
            ProviderProfile provider,
            string model,
            ChatClientAgentOptions options,
            bool frameworkManagedHistory,
            bool allowBackgroundResponses)
            => throw new NotSupportedException();
    }

    private sealed class UnusedRuntimeCapabilityComposer : IRuntimeCapabilityComposer
    {
        public Task<RuntimeCapabilityState> CreateCapabilityStateAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            WorkspaceRuntimeServices workspaceRuntimeServices,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken,
            bool suppressApprovalRequirements = false)
            => throw new NotSupportedException();

        public Task<RuntimeCapabilityState> CreateCapabilityStateCoreAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            string model,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken,
            bool suppressApprovalRequirements,
            WorkspaceScopeDescriptor contextWorkspaceScope,
            AgentRuntimeContextIntent contextIntent,
            WorkspaceRuntimeServices workspaceRuntimeServices,
            string runtimeSessionKey = "",
            IReadOnlyList<AgentChatContextAttachmentEnvelope>? contextAttachments = null)
            => throw new NotSupportedException();
    }
}

internal sealed class SpreadsheetRegressionTempDirectory : IDisposable
{
    public SpreadsheetRegressionTempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            nameof(SpreadsheetRegressionTempDirectory),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
