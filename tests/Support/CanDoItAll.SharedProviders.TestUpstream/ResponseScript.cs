using System.Text;
using System.Text.Json;

namespace CanDoItAll.SharedProviders.TestUpstream;

public enum FixtureResponseStepKind { Text, ToolCalls, EchoToolContent, IncompleteText }

public sealed record FixtureScriptToolCall(string Name, JsonElement Arguments);

public sealed record FixtureResponseStep(
    FixtureResponseStepKind Kind,
    string? Text = null,
    IReadOnlyList<FixtureScriptToolCall>? ToolCalls = null);

public sealed record FixtureResponseScript(string Model, string Marker, IReadOnlyList<FixtureResponseStep> Steps);

public sealed record FixtureResponseScriptProgress(bool Active, int Consumed, int Total);

internal sealed class ResponseScriptState {
    private readonly object sync = new();
    private FixtureResponseScript? script;
    private int consumed;

    public FixtureResponseScriptProgress Set(FixtureResponseScript? value) {
        lock (sync) {
            script = value;
            consumed = 0;
            return Progress();
        }
    }

    public FixtureResponseScriptProgress Get() {
        lock (sync) {
            return Progress();
        }
    }

    public ResponsesResponse? Respond(ResponsesRequest request) {
        lock (sync) {
            if (script is null) {
                return null;
            }
            if (request.Model != script.Model ||
                !(request.Instructions ?? string.Empty).Contains(script.Marker, StringComparison.Ordinal) &&
                !request.Input.GetRawText().Contains(script.Marker, StringComparison.Ordinal)) {
                throw new InvalidOperationException("The active response script does not match this model and marker.");
            }
            if (consumed >= script.Steps.Count) {
                throw new InvalidOperationException("The bounded response script is exhausted.");
            }
            var step = script.Steps[consumed];
            var ordinal = consumed + 1;
            var status = step.Kind == FixtureResponseStepKind.IncompleteText ? "incomplete" : "completed";
            IReadOnlyList<ResponseOutputItem> output;
            if (step.Kind == FixtureResponseStepKind.ToolCalls) {
                output = step.ToolCalls!.Select((call, index) => {
                    if (request.Tools?.Any(tool => tool.Type == "function" && tool.Name == call.Name) != true) {
                        throw new InvalidOperationException("The scripted tool was not offered by the native consumer.");
                    }
                    return new ResponseOutputItem($"fc_script_{ordinal}_{index}", "function_call", "completed",
                        CallId: $"call_script_{ordinal}_{index}", Name: call.Name, Arguments: call.Arguments.GetRawText());
                }).ToArray();
            } else {
                var text = step.Kind == FixtureResponseStepKind.EchoToolContent ? ReadToolContent(request.Input) : step.Text!;
                output = [new($"msg_script_{ordinal}", "message", status, "assistant", [new("output_text", text, [])])];
            }
            consumed++;
            return new($"resp_script_{ordinal}", "response", 1_700_000_000, status, request.Model, output,
                new(11, 7, 18), status == "incomplete" ? new("max_output_tokens") : null);
        }
    }

    private FixtureResponseScriptProgress Progress() => new(script is not null, consumed, script?.Steps.Count ?? 0);

    private static string ReadToolContent(JsonElement input) {
        if (input.ValueKind != JsonValueKind.Array) {
            throw new InvalidOperationException("The native tool output is missing.");
        }
        foreach (var item in input.EnumerateArray().Reverse()) {
            if (!item.TryGetProperty("type", out var type) || type.GetString() != "function_call_output" ||
                !item.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.String) {
                continue;
            }
            using var result = JsonDocument.Parse(output.GetString()!);
            var encoded = FindContent(result.RootElement);
            if (encoded is not null) {
                return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            }
        }
        throw new InvalidOperationException("The native tool output did not contain file content.");
    }

    private static string? FindContent(JsonElement value) {
        if (value.ValueKind == JsonValueKind.Object) {
            foreach (var property in value.EnumerateObject()) {
                if (property.Name.Equals("base64Data", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String) {
                    return property.Value.GetString();
                }
                if (FindContent(property.Value) is { } found) {
                    return found;
                }
            }
        } else if (value.ValueKind == JsonValueKind.Array) {
            foreach (var item in value.EnumerateArray()) {
                if (FindContent(item) is { } found) {
                    return found;
                }
            }
        }
        return null;
    }
}

internal static class ResponseScriptEndpoints {
    public static void MapResponseScript(this RouteGroupBuilder group) {
        group.MapGet("/response-script", (ResponseScriptState state) => TypedResults.Ok(state.Get()));
        group.MapDelete("/response-script", (ResponseScriptState state) => TypedResults.Ok(state.Set(null)));
        group.MapPut("/response-script", (FixtureResponseScript script, ResponseScriptState state) => {
            if (string.IsNullOrWhiteSpace(script.Model) || script.Model.Length > 512 ||
                string.IsNullOrWhiteSpace(script.Marker) || script.Marker.Length is < 8 or > 128 ||
                script.Steps is not { Count: > 0 and <= 16 } || script.Steps.Any(step => !Valid(step))) {
                return Results.BadRequest(new FixtureErrorEnvelope(new("Invalid bounded response script.", "fixture_validation_error", "fixture_validation_error")));
            }
            return Results.Ok(state.Set(script));
        });
    }

    private static bool Valid(FixtureResponseStep step) => step.Kind switch {
        FixtureResponseStepKind.Text or FixtureResponseStepKind.IncompleteText =>
            step.Text is { Length: > 0 and <= 16_384 } && step.ToolCalls is null,
        FixtureResponseStepKind.EchoToolContent => step.Text is null && step.ToolCalls is null,
        FixtureResponseStepKind.ToolCalls => step.Text is null && step.ToolCalls is { Count: > 0 and <= 4 } &&
            step.ToolCalls.All(call => !string.IsNullOrWhiteSpace(call.Name) && call.Name.Length <= 128 &&
                call.Arguments.ValueKind == JsonValueKind.Object && call.Arguments.GetRawText().Length <= 16_384),
        _ => false
    };
}
