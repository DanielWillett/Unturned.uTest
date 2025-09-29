using System;

namespace uTest;

internal static class DefaultAssets
{
    internal static void Add(HashSet<Guid> guids)
    {
        // relied on by vanilla code
        guids.Add(new Guid("cea791255ba74b43a20e511a52ebcbec"));
        guids.Add(new Guid("67a4addd45174d7e9ca5c8ec24f8010f"));
        guids.Add(new Guid("229440c249dc490ba26ce71e8a59d5c6"));
        guids.Add(new Guid("b7d53965bc6545c28e029175af35de30"));
        guids.Add(new Guid("bc41e0feaebe4e788a3612811b8722d3"));
        guids.Add(new Guid("704906b407fe4cb9b4a193ab7447d784"));
        guids.Add(new Guid("ab5f0056b54545c8a051159659da8bea"));
        guids.Add(new Guid("288b98b718084699ba3653c592e57803"));
        guids.Add(new Guid("498ca625072d443a876b2a4f11896018"));
        guids.Add(new Guid("12dc9fdbe9974022afd21158ad54b76a"));
        guids.Add(new Guid("bed12ffc45694cd69217924d75e96fe9"));
        guids.Add(new Guid("805bb3b0752749d1b5cf9959d17e104e"));
        guids.Add(new Guid("f515fcbe1b5241e39217b52317e68d72"));
        guids.Add(new Guid("663158e0a71346068947b29978818ef7"));
        guids.Add(new Guid("47258d0dcad14cb8be26e24c1ef3449e"));
        guids.Add(new Guid("6b91a94f01b6472eaca31d9420ec2367"));
        guids.Add(new Guid("bb9f9f0204c4462ca7d976b87d1336d4"));
        guids.Add(new Guid("93a47d6d40454335b4784e803628ac54"));
        guids.Add(new Guid("903577da2ecd4f5784b2f7aed8c300c1"));
        guids.Add(new Guid("d73923f4416c43dfa5bc8b6234cf0257"));
        guids.Add(new Guid("a87c5007b22542dcbf3599ee3faceadd"));
        guids.Add(new Guid("5436f56485c841a7bbec8e79a163ad19"));
        guids.Add(new Guid("b7acfd045ceb40c1b84788cb9159d0f2"));
        guids.Add(new Guid("000f550dc3d44586b7fc0f6e5b2530d9"));
        guids.Add(new Guid("f2f0d31897024317b32b58c00c1f78dd"));
        guids.Add(new Guid("469414f0a1b047c58732bb6076b0c035"));
        guids.Add(new Guid("ae477aac40b64d3c8ce8e538daffecf5"));
        guids.Add(new Guid("9fd759eda4b746dfb9f2599bf8f27219"));
        guids.Add(new Guid("50872061be8e411ea28780fcb7aa7cef"));
        guids.Add(new Guid("23363b069ad740819f1d7131656f8ca7"));
        guids.Add(new Guid("36b272f5be8c4427b0fdd0625f361c15"));
        guids.Add(new Guid("c17b00f2a58646c8a9ea728f6d72e54e"));

        // may add more in the future
        string mapName = Provider.map;
        LevelInfo lvlInfo = Level.getLevel(mapName);
        if (lvlInfo == null)
            return;

        LevelInfoConfigData config = lvlInfo.configData;
        TryAdd(guids, config.Asset.GUID);

        static void TryAdd(HashSet<Guid> guids, Guid guid)
        {
            if (guid != Guid.Empty) guids.Add(guid);
        }
    }
}
