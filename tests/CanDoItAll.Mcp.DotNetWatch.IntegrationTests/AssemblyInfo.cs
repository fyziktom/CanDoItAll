using Xunit;

[assembly: AssemblyTrait("Category", "LiveProcess")]
[assembly: AssemblyTrait("Category", "LongRunning")]
[assembly: CollectionBehavior(DisableTestParallelization = true)]
