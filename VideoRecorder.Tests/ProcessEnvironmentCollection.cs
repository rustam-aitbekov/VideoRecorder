namespace VideoRecorder.Tests;

/// <summary>
/// Tests that change process-wide state (PATH) or start processes run one at a time.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessEnvironmentCollection
{
    public const string Name = "Process environment";
}
