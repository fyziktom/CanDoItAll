using System.Globalization;
using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessDecisionMaterialTests {
    private static readonly DateTimeOffset Captured = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly ProcessDecisionMaterialId MaterialId = new(Guid.Parse("66666666-7777-8888-9999-aaaaaaaaaaaa"));
    private static readonly ProcessDecisionExecutionContract Contract = ProcessDecisionPolicyFixture.Contract(ProcessDecisionChildOwner.Agent);
    private const string Payload = "{\"tasks\":[]}";

    [Theory]
    [InlineData(false, "0049203A0515566FE649958CF9EF42328D0123E985B6FB5D742C95B3B830D6C1")]
    [InlineData(true, "F1114CA41EA2BCB8F6A8103B2EF5CE982DF7F046C3E5797C0B343DC8A24A6BB3")]
    public void Original_material_has_a_culture_independent_protocol_fingerprint(bool step, string expected) {
        var previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            var material = Material(Scope(step));
            Assert.Equal(expected, material.Fingerprint);
            Assert.Equal(step ? ReadBasisMode.StepSnapshot : ReadBasisMode.RunSnapshot, material.Scope.ReadBasisMode);
            Assert.Equal(Captured, material.CapturedAtUtc);
            Assert.Equal(Payload, material.PayloadJson);
            Assert.Equal(12, material.PayloadUtf8Bytes);
            material.RequireOriginal(Material(Scope(step)));
        } finally {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Original_capture_controls_age_including_the_exact_boundary() {
        var original = Material();
        var maximum = TimeSpan.FromMinutes(10);
        original.RequireFresh(Captured + maximum, maximum);
        var failure = Assert.Throws<ProcessDecisionMaterialExpiredException>(() => original.RequireFresh(Captured + maximum + TimeSpan.FromTicks(1), maximum));
        Assert.Equal(original.Reference, failure.Reference);
    }

    [Fact]
    public void Delayed_child_cannot_renew_material_age_by_restamping_identical_content() {
        var original = Material();
        var childStarted = Captured.AddMinutes(20);
        var restamped = Material(captured: childStarted);
        Assert.Throws<ProcessDecisionMaterialExpiredException>(() => original.RequireFresh(childStarted, TimeSpan.FromMinutes(10)));
        Assert.Throws<InvalidDataException>(() => original.RequireOriginal(restamped));
        Assert.NotEqual(original.Fingerprint, restamped.Fingerprint);
        Assert.Equal(Captured, original.CapturedAtUtc);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 60)]
    [InlineData(0, -60)]
    public void Clock_reversal_and_non_UTC_observations_fail_explicitly(int minutes, int offsetMinutes) {
        var now = Captured.AddMinutes(minutes).ToOffset(TimeSpan.FromMinutes(offsetMinutes));
        Assert.Throws<InvalidDataException>(() => Material().RequireFresh(now, TimeSpan.FromHours(1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Missing_or_negative_age_bound_is_not_unlimited(long ticks) {
        Assert.Throws<InvalidDataException>(() => Material().RequireFresh(Captured, TimeSpan.FromTicks(ticks)));
    }

    [Fact]
    public void Every_original_scope_material_and_contract_field_prevents_substitution() {
        var original = Material();
        var scope = original.Scope;
        var replacements = new[] {
            Material(scope with { Id = new(Guid.NewGuid()) }),
            Material(scope with { DatabaseProfileId = Guid.NewGuid() }),
            Material(scope with { WorkspaceNamespaceId = Guid.NewGuid() }),
            Material(scope with { RootRunId = new(Guid.NewGuid()) }),
            Material(scope with { OwnerRunId = new(Guid.NewGuid()) }),
            Material(scope with { StepInstanceId = new(Guid.NewGuid()) }),
            Material(generation: 2),
            Material(materialId: new(Guid.NewGuid())),
            Material(contract: Contract with { ContractId = Guid.NewGuid() }),
            Material(contract: Contract with { Version = 2 }),
            Material(contract: Contract with { Executor = ProcessDecisionChildOwner.Workflow }),
            Material(captured: Captured.AddTicks(1)),
            Material(payload: "{\"tasks\":[{}]}")
        };
        Assert.Equal(replacements.Length, replacements.Select(item => item.Fingerprint).Distinct().Count());
        foreach (var replacement in replacements) {
            Assert.NotEqual(original.Fingerprint, replacement.Fingerprint);
            Assert.Throws<InvalidDataException>(() => original.RequireOriginal(replacement));
        }
    }

    [Theory]
    [InlineData('x', 1)]
    [InlineData('漢', 3)]
    public void Payload_limit_measures_UTF8_bytes_before_retaining_material(char character, int width) {
        var count = (ProcessDecisionMaterial.MaximumPayloadUtf8Bytes - 8) / width;
        var prefix = "{\"x\":\"";
        var suffix = "\"}";
        var admitted = Material(payload: prefix + new string(character, count) + suffix);
        Assert.Equal(count * width + 8, admitted.PayloadUtf8Bytes);
        Assert.Throws<InvalidDataException>(() => Material(payload: prefix + new string(character, count + 1) + suffix));
    }

    [Fact]
    public void Invalid_Unicode_cannot_be_replaced_silently_during_hashing() {
        Assert.Throws<InvalidDataException>(() => Material(payload: "{\"x\":\"\ud800\"}"));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("1")]
    [InlineData("\"value\"")]
    public void Material_requires_an_owner_encoded_object(string payload) {
        Assert.Throws<InvalidDataException>(() => Material(payload: payload));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"x\":}")]
    public void Malformed_owner_payload_is_not_retained(string payload) {
        Assert.ThrowsAny<JsonException>(() => Material(payload: payload));
    }

    [Fact]
    public void Empty_scope_fields_cannot_turn_a_step_into_a_run_scope() {
        var scope = Scope();
        foreach (var invalid in new[] { scope with { Id = default }, scope with { DatabaseProfileId = Guid.Empty },
            scope with { WorkspaceNamespaceId = Guid.Empty }, scope with { RootRunId = default }, scope with { OwnerRunId = default },
            scope with { StepInstanceId = default(ProcessStepInstanceId) } }) {
            Assert.Throws<InvalidDataException>(() => Material(invalid));
        }
    }

    [Fact]
    public void Material_reference_requires_its_exact_scope_and_bounded_generation() {
        var scope = Scope();
        Assert.Throws<InvalidDataException>(() => new ProcessDecisionMaterial(scope, new(new(Guid.NewGuid()), 1, MaterialId), Contract, Captured, Payload));
        Assert.Throws<InvalidDataException>(() => Material(materialId: default(ProcessDecisionMaterialId)));
        Assert.Throws<InvalidDataException>(() => Material(generation: 0));
        Assert.Throws<InvalidDataException>(() => Material(generation: ProcessDecisionGeneration.MaximumGenerations + 1));
        Material(generation: ProcessDecisionGeneration.MaximumGenerations).RequireFresh(Captured, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Capture_requires_a_real_UTC_time_and_valid_execution_contract() {
        Assert.Throws<InvalidDataException>(() => Material(captured: default(DateTimeOffset)));
        Assert.Throws<InvalidDataException>(() => Material(captured: Captured.ToOffset(TimeSpan.FromHours(1))));
        Assert.Throws<InvalidDataException>(() => Material(contract: Contract with { Version = 0 }));
    }

    private static ProcessDecisionMaterialScope Scope(bool step = false) => new(
        new(Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")), Guid.Parse("11111111-2222-3333-4444-555555555555"),
        Guid.Parse("22222222-3333-4444-5555-666666666666"), new(Guid.Parse("33333333-4444-5555-6666-777777777777")),
        new(Guid.Parse("44444444-5555-6666-7777-888888888888")), step ? new(Guid.Parse("55555555-6666-7777-8888-999999999999")) : null);

    private static ProcessDecisionMaterial Material(ProcessDecisionMaterialScope? scope = null, int generation = 1,
        ProcessDecisionMaterialId? materialId = null, ProcessDecisionExecutionContract? contract = null,
        DateTimeOffset? captured = null, string payload = Payload) {
        scope ??= Scope();
        return new(scope, new(scope.Id, generation, materialId ?? MaterialId), contract ?? Contract, captured ?? Captured, payload);
    }
}
