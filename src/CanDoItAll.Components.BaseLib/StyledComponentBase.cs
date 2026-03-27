using CanDoItAll.Components.Common;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Components.BaseLib;

public abstract class StyledComponentBase : ComponentBase
{
    [Parameter]
    public string? Class { get; set; }

    [Parameter]
    public string? Style { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    protected IReadOnlyDictionary<string, object>? BuildAttributes(string? baseClass = null, string? baseStyle = null)
    {
        return ComponentAttributeExtensions.WithClassAndStyle(
            AdditionalAttributes,
            CssClassBuilder.Join(baseClass, Class),
            CssClassBuilder.JoinStyles(baseStyle, Style));
    }

    protected string BuildClass(params string?[] values)
    {
        return CssClassBuilder.Join(values.Concat([Class]).ToArray());
    }

    protected string? BuildStyle(params string?[] values)
    {
        return CssClassBuilder.JoinStyles(values.Concat([Style]).ToArray());
    }
}





