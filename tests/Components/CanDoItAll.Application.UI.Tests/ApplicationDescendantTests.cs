using System.Reflection;
using System.Reflection.Emit;
using Bunit;
using CanDoItAll.Application.UiSandbox.Components;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ApplicationUI;

public sealed class ApplicationDescendantTests : BunitContext {
    public ApplicationDescendantTests() {
        Services.AddCanDoItAllBaseLib();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task Actual_dashboard_readiness_and_open_profile_shell_children_are_light() {
        var dashboard = Render<DashboardPage>();
        var runtime = Render<RuntimePage>();
        var profiles = Render<ProfilePage>();
        await profiles.Find("[data-testid='profile-scenario-open']").ClickAsync(new());
        ApplicationBoundaryTests.RequirePresentationDescendants(dashboard.FindComponents<ComponentBase>()
            .Concat(runtime.FindComponents<ComponentBase>()).Concat(profiles.FindComponents<ComponentBase>())
            .Select(component => component.Instance.GetType()));
        Assert.NotEmpty(profiles.FindComponents<SelectionListItem>());
        Assert.NotEmpty(profiles.FindComponents<CopyButton>());
    }

    [Fact]
    public void A_native_public_parameter_is_rejected_even_without_a_static_project_reference() {
        var parameterProbe = typeof(ParameterProbe<>).MakeGenericType(CreateNativeComponent());
        Assert.Throws<InvalidOperationException>(() => ApplicationBoundaryTests.RequirePresentationContract(parameterProbe));
        Assert.Throws<InvalidOperationException>(() => ApplicationBoundaryTests.RequirePresentationContract(typeof(ParameterProbe<IServiceProvider>)));
    }

    [Fact]
    public void A_native_visual_supplied_only_through_DynamicComponent_is_rejected() {
        var native = CreateNativeComponent();
        var cut = Render<DynamicComponent>(parameters => parameters.Add(component => component.Type, native));
        var actualChildren = cut.FindComponents<ComponentBase>().Select(component => component.Instance.GetType()).ToArray();
        Assert.Contains(native, actualChildren);
        Assert.Throws<InvalidOperationException>(() => ApplicationBoundaryTests.RequirePresentationDescendants(actualChildren));
    }

    private static Type CreateNativeComponent() {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("CanDoItAll.Web"), AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("NativeFixture").DefineType("NativeVisual", TypeAttributes.Public, typeof(ComponentBase));
        type.DefineDefaultConstructor(MethodAttributes.Public);
        return type.CreateType()!;
    }

    public sealed class ParameterProbe<T> {
        [Parameter]
        public T? Value { get; set; }
    }
}
