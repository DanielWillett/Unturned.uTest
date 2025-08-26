namespace uTest.Module;

public record TestExecutionResult(
    TestResult Result,
    string? ExecutionInfoFile
)
{
    public static implicit operator TestExecutionResult(TestResult result) => new TestExecutionResult(result, null);
}