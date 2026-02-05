using Microsoft.Extensions.Logging;
using OpenMod.API.Eventing;
using OpenMod.API.Persistence;
using OpenMod.API.Users;
using OpenMod.Core.Users;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using uTest.Compat.Lifetime;
using uTest.Compat.OpenMod.Events;
using uTest.Compat.Tests;

namespace uTest.Compat.OpenMod.Lifetime;

internal class OpenModTestLifetimeIntegration : ITestLifetimeIntegration, IDisposable
{
    private readonly IUserDataStore _userDataStore;
    private readonly IDataStore _dataStore;
    private readonly IEventBus _eventBus;
    private readonly OpenModCompatPlugin _plugin;
    private readonly ILogger<OpenModTestLifetimeIntegration> _logger;

    public static ITestLifetimeIntegration? Instance;

    // time to wait for user data to update after writing to the file.
    private const double WaitForUpdateTimeout = 2.0;

    /// <inheritdoc />
    public int Priority => 10;

    public OpenModTestLifetimeIntegration(
        IUserDataStore userDataStore,
        IDataStore dataStore,
        IEventBus eventBus,
        ILogger<OpenModTestLifetimeIntegration> logger,
        OpenModCompatPlugin plugin)
    {
        _userDataStore = userDataStore;
        _dataStore = dataStore;
        _eventBus = eventBus;
        _logger = logger;
        _plugin = plugin;
        Interlocked.CompareExchange(ref Instance, this, null);
    }

    /// <inheritdoc />
    public async Task<bool> BeginTestAsync(IUnitTestExecution test, CancellationToken token = default)
    {
        TestBeginningEvent e = new TestBeginningEvent
        {
            Test = test
        };

        await _eventBus.EmitAsync(_plugin, this, e).ConfigureAwait(false);

        if (e.IsCancelled)
        {
            return false;
        }

        // remove allocated users' data from data store
        if (_userDataStore is UserDataStore
            && test.GetAllocatedServersidePlayerIds().Any(
                   x => _userDataStore.GetUserDataAsync(x.ToString(), KnownActorTypes.Player) != null
               )
            )
        {
            await RemoveAllocatedPlayersFromUserDataStore(test, token);
        }

        return true;
    }

    /// <inheritdoc />
    public async Task EndTestAsync(IUnitTestExecution test, TestResult? result, CancellationToken token = default)
    {
        TestEndedEvent e = new TestEndedEvent
        {
            Test = test,
            Result = result
        };

        await _eventBus.EmitAsync(_plugin, this, e).ConfigureAwait(false);

        if (_userDataStore is not UserDataStore)
        {
            _logger.LogWarning("uTest does not support the current IUserDataStore: {0}. Dummy user data will not be wiped.", _userDataStore.GetType());
            return;
        }

        await RemoveAllocatedPlayersFromUserDataStore(test, token);
    }

    private async Task RemoveAllocatedPlayersFromUserDataStore(IUnitTestExecution test, CancellationToken token)
    {
        // remove dummies from user data store
        UsersData? data = await _dataStore.LoadAsync<UsersData>(UserDataStore.UsersKey).ConfigureAwait(false);

        token.ThrowIfCancellationRequested();

        if (data?.Users == null || data.Users.Count == 0)
        {
            return;
        }

        IReadOnlyList<CSteamID> idsToRemove = test.GetAllocatedServersidePlayerIds();
        if (idsToRemove.Count == 0)
            return;

        string? someUserInDataStore = null;
        foreach (CSteamID serversidePlayer in idsToRemove)
        {
            string idToString = serversidePlayer.ToString();

            int index = data.Users.FindIndex(x =>
                string.Equals(x.Id, idToString, StringComparison.Ordinal) &&
                string.Equals(KnownActorTypes.Player, x.Type, StringComparison.Ordinal)
            );

            if (index < 0)
                continue;

            data.Users.RemoveAt(index);
            someUserInDataStore = idToString;
        }

        if (someUserInDataStore == null)
            return;

        await _dataStore.SaveAsync(UserDataStore.UsersKey, data).ConfigureAwait(false);

        // wait for user data to be updated with a timeout
        DateTime start = DateTime.UtcNow;
        while (await _userDataStore.GetUserDataAsync(someUserInDataStore, KnownActorTypes.Player) != null)
        {
            TimeSpan elapsed = DateTime.UtcNow - start;
            if (elapsed.TotalSeconds > WaitForUpdateTimeout)
            {
                _logger.LogError("Timed out trying to remove dummies from user data store after {0}.", elapsed);
                break;
            }

            token.ThrowIfCancellationRequested();
            await Task.Delay(100, token);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Interlocked.CompareExchange(ref Instance, null, this);
    }
}