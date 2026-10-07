using System.Globalization;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.TestLab;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.TestLab.UI;

public sealed class TestLabDraft {
    private readonly Dictionary<TestRunEditorModel, string> timestamps = new(ReferenceEqualityComparer.Instance);
    private readonly ValidationMessageStore validation;
    public TestPlanEditorModel Model { get; }
    public EditContext Context { get; }
    public long TargetVersion { get; private set; }
    public TestLabSubmission? Pending { get; private set; }
    public TestLabSubmission? LastCommitted { get; set; }
    public TestLabSubmission? UncertainSubmission { get; private set; }
    public TestLabSaveState SaveState { get; set; }
    public string Message { get; set; } = "Ready";
    public bool CanSave => Pending is null && SaveState != TestLabSaveState.Unknown;

    public TestLabDraft(TestPlanEditorModel model) {
        Model = model;
        Context = new(model);
        validation = new(Context);
        Context.OnValidationRequested += (_, _) => Validate();
    }

    public void ChangeProject(Guid? id, ProjectWriteAdmission? admission) {
        TargetVersion++;
        Model.ProjectId = id;
        Model.ExpectedProjectAdmission = admission;
        Pending = null;
        LastCommitted = null;
        if (SaveState != TestLabSaveState.Unknown) {
            UncertainSubmission = null;
            SaveState = TestLabSaveState.Ready;
            Message = "Project changed; review and save this draft.";
        }
        Context.NotifyFieldChanged(new(Model, nameof(Model.ProjectId)));
    }

    public TestLabSubmission? BeginSave() {
        if (!CanSave || !Context.Validate()) {
            return null;
        }
        LastCommitted = null;
        Pending = new(this);
        SaveState = TestLabSaveState.Pending;
        Message = "Saving plan…";
        return Pending;
    }

    public void Finish(TestLabSubmission submission, TestLabSaveState state, string message) {
        if (!ReferenceEquals(Pending, submission) || !submission.BelongsTo(this)) {
            return;
        }
        Pending = null;
        UncertainSubmission = state == TestLabSaveState.Unknown ? submission : null;
        SaveState = state;
        Message = message;
    }

    public void Input(object model, string field, Action<string> assign, string? value) {
        assign(value ?? string.Empty);
        var identifier = new FieldIdentifier(model, field);
        if (ReferenceEquals(model, Model) && field == nameof(Model.Title)) {
            validation.Clear(identifier);
        }
        Context.NotifyFieldChanged(identifier);
        Context.NotifyValidationStateChanged();
    }

    public string Timestamp(TestRunEditorModel run) => timestamps.GetValueOrDefault(run) ?? run.ExecutedAtUtc.ToString("O", CultureInfo.InvariantCulture);
    public bool HasRawTimestamp(TestRunEditorModel run) => timestamps.ContainsKey(run);

    public void InputTimestamp(TestRunEditorModel run, string? value) {
        timestamps[run] = value ?? string.Empty;
        ValidateTimestamp(run);
        Context.NotifyFieldChanged(new(run, nameof(run.ExecutedAtUtc)));
        Context.NotifyValidationStateChanged();
    }

    private void Validate() {
        validation.Clear();
        if (string.IsNullOrWhiteSpace(Model.Title)) {
            validation.Add(new FieldIdentifier(Model, nameof(Model.Title)), "Title is required.");
        }
        foreach (var run in Model.Runs.Where(timestamps.ContainsKey)) {
            ValidateTimestamp(run);
        }
        Context.NotifyValidationStateChanged();
    }

    private void ValidateTimestamp(TestRunEditorModel run) {
        var field = new FieldIdentifier(run, nameof(run.ExecutedAtUtc));
        validation.Clear(field);
        if (DateTimeOffset.TryParseExact(timestamps[run], "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)) {
            run.ExecutedAtUtc = value.ToUniversalTime();
        } else {
            validation.Add(field, "Enter a complete ISO 8601 timestamp including fractional seconds and offset.");
        }
    }
}
