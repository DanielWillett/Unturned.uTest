using System;
using uTest.Environment;

namespace uTest;

/// <summary>
/// Default extensions for <see cref="ITestConfigurationBuilder"/>.
/// </summary>
public static class TestConfigurationBuilderExtensions
{
    extension(ITestConfigurationBuilder builder)
    {
        /// <summary>
        /// Spawns a given amount of dummy players.
        /// </summary>
        /// <remarks>All other players will be kicked prior to the test beginning.</remarks>
        public ITestConfigurationBuilder WithPlayers(int playerCount)
        {
            if (playerCount is < 0 or > byte.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(playerCount));

            builder.TestEnvironment.PlayerCount = playerCount;
            return builder;
        }

        /// <summary>
        /// Configures the time of day in HH:MM:SS (24 hour time).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Invalid time values.</exception>
        public ITestConfigurationBuilder WithTime(int hours, int minutes = 0, int seconds = 0, bool timeProgression = false)
        {
            builder.TestEnvironment.Time = new DateTime(1970, 1, 1, hours, minutes, seconds, 0, DateTimeKind.Unspecified);
            builder.TestEnvironment.ShouldTimeProgress = timeProgression;
            return builder;
        }

        /// <summary>
        /// Configures the phase of the moon in-game.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Invalid <paramref name="moonPhase"/> value.</exception>
        public ITestConfigurationBuilder WithMoonPhase(MoonPhase moonPhase)
        {
            if (moonPhase is < MoonPhase.WaxingCrescent or > MoonPhase.WaningCresent)
                throw new ArgumentOutOfRangeException(nameof(moonPhase));

            builder.TestEnvironment.MoonPhase = moonPhase;
            return builder;
        }
    }
}