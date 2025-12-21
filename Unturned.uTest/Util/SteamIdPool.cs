using uTest.Module;
using Random = System.Random;

namespace uTest;

/// <summary>
/// Used to create unique Steam64 IDs within the 'Dev' universe.
/// </summary>
internal sealed class SteamIdPool
{
    private readonly SteamIdGenerationStyle _style;

    private int _nextAccountNumber;

    public SteamIdPool(SteamIdGenerationStyle style)
    {
        int min, max;
        if (style != SteamIdGenerationStyle.DevUniverse)
        {
            // note:
            // seems like theres a bunch of steam accounts from
            // march 2020 allocated near the very high numbers
            min = 0b01111111000000000000111111111111;
            max = 0b01111111000000111111111111111111;
        }
        else
        {
            min = 9999;
            max = 999999;
        }

        _nextAccountNumber = new Random().Next(min, max);
        _style = style;
    }

    internal CSteamID GetUniqueCSteamID()
    {
        uint acctNum = unchecked ( (uint)Interlocked.Increment(ref _nextAccountNumber) );
        if (_style == SteamIdGenerationStyle.Random)
        {
            acctNum |= 0b10000000000000000000000000000000u;
        }

        return new CSteamID(
            new AccountID_t(acctNum),
            _style == SteamIdGenerationStyle.Instance
                ? 0b00000000000000000010u
                : 1u,
            _style == SteamIdGenerationStyle.DevUniverse
                ? EUniverse.k_EUniverseDev
                : EUniverse.k_EUniversePublic,
            EAccountType.k_EAccountTypeIndividual
        );
    }
}