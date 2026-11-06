using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Prompts.UI.Editor;

public sealed class DraftNumber<TValue> : InputNumber<TValue> {
    public string RawValue => CurrentValueAsString ?? string.Empty;
    public void SetRaw(string value) => CurrentValueAsString = value;
}
