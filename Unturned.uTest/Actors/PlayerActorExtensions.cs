using System;
using uTest.Compat.Tests;
using uTest.Module;

namespace uTest.Actors;

public static class PlayerActorExtensions
{
    extension(IUnitTestExecution ex)
    {
        /// <summary>
        /// Gets a list of all allocated serverside players ('dummies').
        /// </summary>
        public IReadOnlyList<IServersideTestPlayer> GetAllocatedServersidePlayers()
        {
            if (ex is not UnturnedTestInstanceData test)
                return Array.Empty<IServersideTestPlayer>();

            IServersideTestPlayer[]? arr = test.AllocatedDummies;
            if (arr == null || arr.Length == 0)
                return Array.Empty<IServersideTestPlayer>();

            return arr;
        }
    }
}
