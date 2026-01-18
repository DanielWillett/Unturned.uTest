using System;
using uTest;
using Resources = uTest.Properties.Resources;

#pragma warning disable IDE0130

namespace Xunit;

#pragma warning restore IDE0130

public static class ServersidePlayerAssertions
{
    extension(Assert)
    {
        /// <summary>
        /// Asserts that the given player is connected to the server.
        /// </summary>
        public static void Connected(IServersideTestPlayer player)
        {
            Assert.True(player.IsOnline, string.Format(Resources.Assertion_Message_PlayerConnected, player.DisplayName));
        }

        /// <summary>
        /// Asserts that the given player was rejected by the server for a specific reason.
        /// </summary>
        /// <param name="player">The player to check.</param>
        /// <param name="reason">The expected reason for the rejection.</param>
        public static void Rejected(IServersideTestPlayer player, ESteamConnectionFailureInfo reason)
        {
            Assert.True(
                player.TryGetRejectionInfo(out ESteamConnectionFailureInfo actualReason, out _, out _),
                string.Format(Resources.Assertion_Message_PlayerRejected, player.DisplayName)
            );

            Assert.Equal(reason, actualReason);
        }

        /// <summary>
        /// Asserts that the given player was rejected by the server for a specific reason.
        /// </summary>
        /// <param name="player">The player to check.</param>
        /// <param name="reason">The expected reason for the rejection.</param>
        /// <param name="reasonString">The expected reason string for the rejection.</param>
        public static void Rejected(IServersideTestPlayer player, ESteamConnectionFailureInfo reason, string reasonString)
        {
            Assert.True(
                player.TryGetRejectionInfo(out ESteamConnectionFailureInfo actualReason, out string? actualReasonString, out _),
                string.Format(Resources.Assertion_Message_PlayerRejected, player.DisplayName)
            );

            Assert.Equal(reason, actualReason);
            Assert.Equal(reasonString, actualReasonString);
        }

        /// <summary>
        /// Asserts that the given player was rejected by the server for a specific reason.
        /// </summary>
        /// <param name="player">The player to check.</param>
        public static void Banned(IServersideTestPlayer player)
        {
            Assert.True(
                player.TryGetRejectionInfo(out ESteamConnectionFailureInfo actualReason, out _, out _),
                string.Format(Resources.Assertion_Message_PlayerRejected, player.DisplayName)
            );

            Assert.Equal(ESteamConnectionFailureInfo.BANNED, actualReason);
        }

        /// <summary>
        /// Asserts that the given player was rejected by the server for a specific reason.
        /// </summary>
        /// <param name="player">The player to check.</param>
        /// <param name="reasonString">The expected reason string for the rejection.</param>
        public static void Banned(IServersideTestPlayer player, string reasonString)
        {
            Assert.True(
                player.TryGetRejectionInfo(out ESteamConnectionFailureInfo actualReason, out string? actualReasonString, out _),
                string.Format(Resources.Assertion_Message_PlayerRejected, player.DisplayName)
            );

            Assert.Equal(ESteamConnectionFailureInfo.BANNED, actualReason);
            Assert.Equal(reasonString, actualReasonString);
        }

        /// <summary>
        /// Asserts that the given player was rejected by the server for a specific reason.
        /// </summary>
        /// <param name="player">The player to check.</param>
        /// <param name="duration">The expected duration of the ban. Use <see cref="Timeout.Infinite"/> to check for an 'infinite' ban (&gt;= <see cref="SteamBlacklist.PERMANENT"/> which is 1yr).</param>
        public static void Banned(IServersideTestPlayer player, TimeSpan duration)
        {
            bool inf = duration.Ticks < 0;

            Assert.True(
                player.TryGetRejectionInfo(out ESteamConnectionFailureInfo actualReason, out _, out TimeSpan? actualDuration),
                string.Format(Resources.Assertion_Message_PlayerRejected, player.DisplayName)
            );

            Assert.Equal(ESteamConnectionFailureInfo.BANNED, actualReason);

            Assert.NotNull(actualDuration);

            if (inf)
                Assert.True(actualDuration.Value >= TimeSpan.FromSeconds(SteamBlacklist.PERMANENT), "actualDuration >= SteamBlacklist.PERMANENT");
            else
                Assert.Equal(duration, actualDuration);
        }

        /// <summary>
        /// Asserts that the given player was rejected by the server for a specific reason.
        /// </summary>
        /// <param name="player">The player to check.</param>
        /// <param name="duration">The expected duration of the ban. Use <see cref="Timeout.Infinite"/> to check for an 'infinite' ban (&gt;= <see cref="SteamBlacklist.PERMANENT"/> which is 1yr).</param>
        /// <param name="reasonString">The expected reason string for the rejection.</param>
        public static void Banned(IServersideTestPlayer player, string reasonString, TimeSpan duration)
        {
            bool inf = duration.Ticks < 0;

            Assert.True(
                player.TryGetRejectionInfo(out ESteamConnectionFailureInfo actualReason, out string? actualReasonString, out TimeSpan? actualDuration),
                string.Format(Resources.Assertion_Message_PlayerRejected, player.DisplayName)
            );

            Assert.Equal(ESteamConnectionFailureInfo.BANNED, actualReason);
            Assert.Equal(reasonString, actualReasonString);

            Assert.NotNull(actualDuration);

            if (inf)
                Assert.True(actualDuration.Value >= TimeSpan.FromSeconds(SteamBlacklist.PERMANENT), "actualDuration >= SteamBlacklist.PERMANENT");
            else
                Assert.Equal(duration, actualDuration);
        }
    }
}
