using Newtonsoft.Json;
using System;
using uTest.Module;

namespace uTest;

public sealed class TestExecutionSummary
{
    [JsonRequired]
    public required string Uid { get; set; }

    [JsonRequired]
    public required string SessionUid { get; set; }

    public List<TestTimingStep>? TimingSteps { get; set; }
    public List<TestOutputMessage>? OutputMessages { get; set; }
    public List<TestArtifact>? Artifacts { get; set; }

    public string? StackTrace { get; set; }
    public string? ExceptionMessage { get; set; }
    public string? ExceptionType { get; set; }

    public string? StandardOutput { get; set; }
    public string? StandardError { get; set; }

    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public TimeSpan Duration { get; set; }
}

public sealed class TestTimingStep
{
    [JsonIgnore]
    internal TestRunStopwatchStage Stage { get; set; }

    [JsonRequired]
    public string Id { get; }
    public string? Description { get; }
    public DateTimeOffset StartTime { get; }
    public DateTimeOffset EndTime { get; }
    public TimeSpan Duration { get; }

    [JsonConstructor]
    public TestTimingStep(string id, string? description, DateTimeOffset startTime, DateTimeOffset endTime, TimeSpan duration)
    {
        Id = id;
        Description = description;
        StartTime = startTime;
        EndTime = endTime;
        Duration = duration;
    }
}

public readonly struct TestOutputMessage
{
    // LogSeverity enum: Error, Information, or Debug
    public int Severity { get; }
    public string Message { get; }

    [JsonConstructor]
    public TestOutputMessage(int severity, string message)
    {
        Severity = severity;
        Message = message;
    }
}

public readonly struct TestArtifact
{
    public string FileName { get; }
    public string DisplayName { get; }
    public string? Description { get; }

    [JsonConstructor]
    public TestArtifact(string fileName, string displayName, string? description)
    {
        FileName = fileName;
        DisplayName = displayName;
        Description = description;
    }
}