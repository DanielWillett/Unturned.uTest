namespace uTest.Module;

public record TestExecutionResult(
    TestResult Result,
    TestExecutionSummary? Summary
)
{
    public static implicit operator TestExecutionResult(TestResult result) => new TestExecutionResult(result, null);
}