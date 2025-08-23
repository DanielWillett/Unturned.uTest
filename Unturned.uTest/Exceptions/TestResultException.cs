using System;
using System.Runtime.Serialization;

namespace uTest;

/// <summary>
/// An exception thrown with a <see cref="TestResult"/>, ending a test immediately.
/// </summary>
public class TestResultException : Exception, ITestResultException
{
    /// <summary>
    /// The result of this <see cref="TestResultException"/>.
    /// </summary>
    public virtual TestResult Result { get; }

    /// <summary>
    /// Create a new <see cref="TestResultException"/>.
    /// </summary>
    public TestResultException(TestResult result)
        : this(result, GetDefaultMessage(result))
    { }

    /// <summary>
    /// Create a new <see cref="TestResultException"/> with a custom message.
    /// </summary>
    public TestResultException(TestResult result, string? message)
        : base(message ?? GetDefaultMessage(result))
    {
        if (result is not TestResult.Pass and not TestResult.Fail and not TestResult.Inconclusive)
            throw new ArgumentOutOfRangeException(nameof(result));

        Result = result;
    }

    /// <summary>
    /// Create a new <see cref="TestResultException"/> with a custom message and inner exception.
    /// </summary>
    public TestResultException(TestResult result, string? message, Exception? inner)
        : base(message ?? GetDefaultMessage(result), inner)
    {
        if (result is not TestResult.Pass and not TestResult.Fail and not TestResult.Inconclusive)
            throw new ArgumentOutOfRangeException(nameof(result));

        Result = result;
    }

    /// <summary>
    /// Create a new <see cref="TestResultException"/> with an inner exception.
    /// </summary>
    public TestResultException(TestResult result, Exception? inner)
        : base(GetDefaultMessage(result), inner)
    {
        if (result is not TestResult.Pass and not TestResult.Fail and not TestResult.Inconclusive)
            throw new ArgumentOutOfRangeException(nameof(result));

        Result = result;
    }

    /// <inheritdoc />
    protected TestResultException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        TestResult r = (TestResult)info.GetByte("TestResult");
        Result = r is TestResult.Pass or TestResult.Fail ? r : TestResult.Inconclusive;
    }

    /// <inheritdoc />
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        TestResult res = Result;
        info.AddValue(
            "TestResult",
            res is not TestResult.Pass and not TestResult.Fail and not TestResult.Inconclusive
            ? (byte)0
            : (byte)res
        );
    }

    /// <summary>
    /// Gets the default message for a <see cref="TestResultException"/> given a <paramref name="result"/>.
    /// </summary>
    protected static string GetDefaultMessage(TestResult result)
    {
        return string.Format(
            Properties.Resources.TestResultExceptionDefaultMessage,
            result switch
            {
                TestResult.Pass => Properties.Resources.TestResultPass,
                TestResult.Fail => Properties.Resources.TestResultFail,
                _ => Properties.Resources.TestResultInconclusive
            }
        );
    }
}

/// <summary>
/// Can be implemented by any exception that should influence the result of a test.
/// </summary>
public interface ITestResultException
{
    /// <summary>
    /// The result of this <see cref="ITestResultException"/>.
    /// </summary>
    TestResult Result { get; }
}