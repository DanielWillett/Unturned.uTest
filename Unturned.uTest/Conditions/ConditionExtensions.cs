using System;

namespace uTest;

public static class ConditionExtensions
{
    extension(ICondition @this)
    {
        /// <summary>
        /// Evaluates whether or not a condition is met, returning the resulting <see cref="TestResult"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException"/>
        public TestResult Check()
        {
            try
            {
                @this.Evaluate();
            }
            catch (TestResultException result)
            {
                TestResult res = result.Result;
                return res is TestResult.Pass or TestResult.Fail ? res : TestResult.Inconclusive;
            }

            return TestResult.Pass;
        }
    }
}