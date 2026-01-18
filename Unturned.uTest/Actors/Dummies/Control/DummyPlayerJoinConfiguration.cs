using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Unturned.SystemEx;
using uTest.Util;
using Random = System.Random;

namespace uTest.Dummies;

/// <summary>
/// Configures the information the client uses to join the server.
/// </summary>
/// <remarks>Plugins can change some of these during the <see cref="Provider.onCheckValidWithExplanation"/> event.</remarks>
public class DummyPlayerJoinConfiguration
{
    private int _customizationUserSetMask;
    private string _characterName;
    private string _nickName;
    private byte[][] _reportedHardwareIds;

    /// <summary>
    /// The zero-based index of the current dummy.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// The randomized Steam ID of the current dummy.
    /// </summary>
    public CSteamID SteamId { get; }

    /// <summary>
    /// The player's Steam name.
    /// </summary>
    /// <remarks>Defaults to a name generated from the player's randomly assigned Steam64 ID.</remarks>
    public string PlayerName { get; }

    /// <summary>
    /// Whether or not the joining player should use the correct password.
    /// This is ignored if the server doesn't have a password.
    /// </summary>
    /// <remarks>Defaults to <see langword="true"/>.</remarks>
    public bool UseCorrectPassword { get; set; } = true;

    /// <summary>
    /// Whether or not the joining player should report the correct level hash to the server.
    /// </summary>
    /// <remarks>Defaults to <see langword="true"/>.</remarks>
    public bool UseCorrectLevelHash { get; set; } = true;

    /// <summary>
    /// Whether or not the joining player should report the correct Assembly-CSharp.dll hash to the server.
    /// </summary>
    /// <remarks>Defaults to <see langword="true"/>.</remarks>
    public bool UseCorrectAssemblyHash { get; set; } = true;

    /// <summary>
    /// Whether or not the joining player should report the correct Unity resource hash to the server.
    /// </summary>
    /// <remarks>Defaults to <see langword="true"/>.</remarks>
    public bool UseCorrectResourceHash { get; set; } = true;

    /// <summary>
    /// Whether or not the joining player should report the correct Steam economy data hash to the server.
    /// </summary>
    /// <remarks>Defaults to <see langword="true"/>.</remarks>
    public bool UseCorrectEconHash { get; set; } = true;

    /// <summary>
    /// Whether or not the joining player should report the correct Unturned version to the server.
    /// </summary>
    /// <remarks>Defaults to <see langword="true"/>.</remarks>
    public bool UseCorrectGameVersion { get; set; } = true;

    /// <summary>
    /// Whether or not the joining player should report the correct map version to the server.
    /// </summary>
    /// <remarks>Defaults to <see langword="true"/>.</remarks>
    public bool UseCorrectMapVersion { get; set; } = true;

    /// <summary>
    /// The version of the game reported to the server. Takes priority over <see cref="UseCorrectGameVersion"/> if set.
    /// </summary>
    /// <remarks>Defaults to the currently installed game version on the client.</remarks>
    public string? ReportedGameVersion { get; set; }

    /// <summary>
    /// The version of the level reported to the server. Takes priority over <see cref="UseCorrectMapVersion"/> if set.
    /// </summary>
    /// <remarks>Defaults to the currently installed map version on the client.</remarks>
    public string? ReportedMapVersion { get; set; }

    /// <summary>
    /// The connection ping reported to the server in milliseconds.
    /// If this value is over <see cref="ServerConfigData.Max_Ping_Milliseconds"/> the player will be kicked.
    /// </summary>
    /// <remarks>Defaults to <c>50</c>.</remarks>
    public ushort ReportedPing { get; set; } = 50;

    /// <summary>
    /// The present required modules this player will report to the server.
    /// If <see langword="null"/>, the reported modules are unchanged from whatever is installed in the client at the time of running the tests.
    /// </summary>
    /// <remarks>Defaults to <see langword="null"/>.</remarks>
    public RequiredModule[]? ReportedRequiredModules { get; set; }

    /// <summary>
    /// The player's spawn location and angle.
    /// </summary>
    /// <remarks>Defaults to a random primary player spawn.</remarks>
    public PlayerTransform Transform { get; set; }

    #region Client -> Server @ ReadyToConnect

    /// <summary>
    /// The player's character name visible only to group members.
    /// </summary>
    /// <remarks>Defaults to <see cref="PlayerName"/>.</remarks>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentOutOfRangeException">Name doesn't pass Unturned's name filter rules.</exception>
    public string NickName
    {
        get => _nickName;
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (value.Length is 0 or > 32
                || NameTool.containsRichText(value)
                || value.ContainsNewLine()
                || long.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out long _)
                || double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double _))
            {
                throw new ArgumentOutOfRangeException(nameof(value), Properties.Resources.ArgumentOutOfRangeExceptionInvalidNickName);
            }

            _nickName = value;
        }
    }

    /// <summary>
    /// The player's character name visible to all players.
    /// </summary>
    /// <remarks>Defaults to <see cref="PlayerName"/>.</remarks>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentOutOfRangeException">Name doesn't pass Unturned's name filter rules.</exception>
    public string CharacterName
    {
        get => _characterName;
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (value.Length is < 2 or > 32
                || NameTool.containsRichText(value)
                || value.ContainsNewLine()
                || long.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out long _)
                || double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double _))
            {
                throw new ArgumentOutOfRangeException(nameof(value), Properties.Resources.ArgumentOutOfRangeExceptionInvalidNickName);
            }

            _characterName = value;
        }
    }

    /// <summary>
    /// The player's operating system.
    /// </summary>
    /// <remarks>Defaults to whatever the tests are running on.</remarks>
    /// <exception cref="InvalidEnumArgumentException"/>
    public EClientPlatform Platform
    {
        get;
        set
        {
            if (value is < EClientPlatform.Windows or > EClientPlatform.Linux)
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(EClientPlatform));

            field = value;
            if ((_customizationUserSetMask & 256) != 0 || _reportedHardwareIds == null)
                return;

            int expectedHwidLength = value == EClientPlatform.Windows ? 3 : 2;
            if (_reportedHardwareIds.Length != expectedHwidLength)
                _reportedHardwareIds = GenerateRandomHWIDs(expectedHwidLength);
        }
    }

    /// <summary>
    /// Whether or not the player owns Unturned Gold (Pro).
    /// </summary>
    /// <remarks>Defaults to <see langword="false"/>.</remarks>
    public bool HasGold
    {
        get;
        set
        {
            field = value;
            UpdateCustomizationAfterGoldUpdated();
        }
    }

    /// <summary>
    /// The Steam ID of the player's group.
    /// </summary>
    /// <remarks>Defaults to <see cref="CSteamID.Nil"/>.</remarks>
    public CSteamID SteamGroupId { get; set; }

    /// <summary>
    /// The Steam ID of the player's lobby.
    /// </summary>
    /// <remarks>Defaults to <see cref="CSteamID.Nil"/>.</remarks>
    public CSteamID SteamLobbyId { get; set; }

    /// <summary>
    /// The index of the face to use for the player's character.
    /// </summary>
    /// <remarks>Defaults to a face based on the player's <see cref="Index"/> and <see cref="HasGold"/>.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">Given face does not exist in-game.</exception>
    public byte FaceIndex
    {
        get;
        set
        {
            if (value >= Customization.FACES_FREE + Customization.FACES_PRO)
                throw new ArgumentOutOfRangeException(nameof(value));
            field = value;
            _customizationUserSetMask |= 1;
        }
    }

    /// <summary>
    /// The index of the hairstyle to use for the player's character.
    /// </summary>
    /// <remarks>Defaults to a hairstyle based on the player's <see cref="Index"/> and <see cref="HasGold"/>.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">Given hairstyle does not exist in-game.</exception>
    public byte HairIndex
    {
        get;
        set
        {
            if (value >= Customization.HAIRS_FREE + Customization.HAIRS_PRO)
                throw new ArgumentOutOfRangeException(nameof(value));
            field = value;
            _customizationUserSetMask |= 2;
        }
    }

    /// <summary>
    /// The index of the beard style to use for the player's character.
    /// </summary>
    /// <remarks>Defaults to a beard based on the player's <see cref="Index"/> and <see cref="HasGold"/>.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">Given beard style does not exist in-game.</exception>
    public byte BeardIndex
    {
        get;
        set
        {
            if (value >= Customization.BEARDS_FREE + Customization.BEARDS_PRO)
                throw new ArgumentOutOfRangeException(nameof(value));
            field = value;
            _customizationUserSetMask |= 4;
        }
    }

    /// <summary>
    /// The skin color to use for the player's character.
    /// </summary>
    /// <remarks>Defaults to a color based on the player's <see cref="Index"/>.</remarks>
    public Color32 SkinColor
    {
        get;
        set
        {
            field = value with { a = 255 };
            _customizationUserSetMask |= 8;
        }
    }

    /// <summary>
    /// The hair color to use for the player's character.
    /// </summary>
    /// <remarks>Defaults to a color based on the player's <see cref="Index"/>.</remarks>
    public Color32 HairColor
    {
        get;
        set
        {
            field = value with { a = 255 };
            _customizationUserSetMask |= 16;
        }
    }

    /// <summary>
    /// The beard color to use for the player's character.
    /// </summary>
    /// <remarks>Defaults to a color based on the player's <see cref="Index"/>.</remarks>
    public Color32 BeardColor
    {
        get;
        set
        {
            field = value with { a = 255 };
            _customizationUserSetMask |= 32;
        }
    }

    /// <summary>
    /// The waypoint marker color to use for the player's character.
    /// </summary>
    /// <remarks>Defaults to a color based on the player's <see cref="Index"/>.</remarks>
    public Color32 MarkerColor
    {
        get;
        set
        {
            field = value with { a = 255 };
            _customizationUserSetMask |= 64;
        }
    }

    /// <summary>
    /// Whether or not the player holds items in their left hand instead of their right.
    /// </summary>
    /// <remarks>Defaults to <see langword="false"/>.</remarks>
    public bool IsLeftHanded { get; set; }

    /// <summary>
    /// The steam inventory item equipped as a shirt for this player's character.
    /// </summary>
    /// <remarks>Defaults to <c>0</c>.</remarks>
    public SteamItemInstanceID_t ShirtItem { get; set; }

    /// <summary>
    /// The steam inventory item equipped as pants for this player's character.
    /// </summary>
    /// <remarks>Defaults to <c>0</c>.</remarks>
    public SteamItemInstanceID_t PantsItem { get; set; }

    /// <summary>
    /// The steam inventory item equipped as a hat for this player's character.
    /// </summary>
    /// <remarks>Defaults to <c>0</c>.</remarks>
    public SteamItemInstanceID_t HatItem { get; set; }

    /// <summary>
    /// The steam inventory item equipped as a backpack for this player's character.
    /// </summary>
    /// <remarks>Defaults to <c>0</c>.</remarks>
    public SteamItemInstanceID_t BackpackItem { get; set; }

    /// <summary>
    /// The steam inventory item equipped as a vest for this player's character.
    /// </summary>
    /// <remarks>Defaults to <c>0</c>.</remarks>
    public SteamItemInstanceID_t VestItem { get; set; }

    /// <summary>
    /// The steam inventory item equipped as a mask for this player's character.
    /// </summary>
    /// <remarks>Defaults to <c>0</c>.</remarks>
    public SteamItemInstanceID_t MaskItem { get; set; }

    /// <summary>
    /// The steam inventory item equipped as glasses for this player's character.
    /// </summary>
    /// <remarks>Defaults to <c>0</c>.</remarks>
    public SteamItemInstanceID_t GlassesItem { get; set; }

    /// <summary>
    /// List of equipped steam inventory items for skins and mythics.
    /// </summary>
    /// <remarks>Defaults to an empty list.</remarks>
    public IList<SteamItemInstanceID_t> EquippedSkins { get; }

    /// <summary>
    /// The skillset of the player's character.
    /// </summary>
    /// <remarks>Defaults to a skillset based on the player's <see cref="Index"/>.</remarks>
    /// <exception cref="InvalidEnumArgumentException"/>
    public EPlayerSkillset Skillset
    {
        get;
        set
        {
            if ((int)value < 0 || (int)value >= Customization.SKILLSETS)
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(EPlayerSkillset));
            field = value;
            _customizationUserSetMask |= 128;
        }
    }

    /// <summary>
    /// The language of the player's client as reported to the server.
    /// </summary>
    /// <remarks>Defaults to the current server language, usually 'English'.</remarks>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentOutOfRangeException">Language name is empty.</exception>
    public string Language
    {
        get;
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (value.Length == 0)
                throw new ArgumentOutOfRangeException(nameof(value), Properties.Resources.ArgumentOutOfRangeExceptionEmpty);
            field = value;
        }
    }

    /// <summary>
    /// The list of hardware IDs reported by the client.
    /// </summary>
    /// <remarks>Defaults to a deterministic random HWID based on the player's <see cref="Index"/> and <see cref="Platform"/>.</remarks>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentOutOfRangeException">Array is either empty or contains HWIDs that aren't exactly 20 bytes.</exception>
    public byte[][] ReportedHardwareIds
    {
        get => _reportedHardwareIds;
        set
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (value.Length == 0)
                throw new ArgumentOutOfRangeException(nameof(value), Properties.Resources.ArgumentOutOfRangeExceptionEmpty);
            for (int i = 0; i < value.Length; ++i)
            {
                if (value[i] is not { Length: 20 })
                    throw new ArgumentOutOfRangeException(nameof(value), Properties.Resources.ArgumentOutOfRangeExceptionInvalidHWIDInList);
            }
            _customizationUserSetMask |= 256;
            _reportedHardwareIds = value;
        }
    }

    /// <summary>
    /// The index of the player's selected character.
    /// </summary>
    /// <remarks>Defaults to a character slot based on the player's <see cref="Index"/> and <see cref="HasGold"/>.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">Given character slot does not exist in-game.</exception>
    public byte CharacterIndex
    {
        get;
        set
        {
            if (value >= Customization.FREE_CHARACTERS + Customization.PRO_CHARACTERS)
                throw new ArgumentOutOfRangeException(nameof(value));

            field = value;
            _customizationUserSetMask |= 512;
        }
    }

    #endregion

    public DummyPlayerJoinConfiguration(int index, CSteamID steamId, string name)
    {
        Random r = new Random(Index * 4129);

        Transform = LevelPlayers.spawns.Count <= 0
            ? PlayerTransform.DefaultSpawn
            : PlayerTransform.FromPlayerSpawn(LevelPlayers.spawns[r.Next(0, LevelPlayers.spawns.Count)]);

        EquippedSkins = new List<SteamItemInstanceID_t>();
        Index = index;
        SteamId = steamId;
        _nickName = name;
        _characterName = name;
        PlayerName = name;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Platform = EClientPlatform.Windows;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Platform = EClientPlatform.Mac;
        else
            Platform = EClientPlatform.Linux;

        HasGold = false;
        SteamGroupId = CSteamID.Nil;
        Language = Provider.language;
        _reportedHardwareIds = GenerateRandomHWIDs(Platform == EClientPlatform.Windows ? 3 : 2);
        UpdateCustomizationAfterGoldUpdated();
    }

    internal void ApplyConfigurationToFiles()
    {
        Block b = new Block();
        PlayerTransform t = Transform;
        b.writeSingleVector3(t.Position);
        b.writeByte(MeasurementTool.angleToByte(t.Yaw));
        if (PlayerSavedata.hasSync)
        {
            ReadWrite.writeBlock($"/Sync/{SteamId.m_SteamID.ToString()}_{CharacterIndex.ToString()}/{Level.info.name}/Player/Player.dat", false, b);
        }
        else
        {
            ServerSavedata.writeBlock($"/Players/{SteamId.m_SteamID.ToString()}_{CharacterIndex.ToString()}/{Level.info.name}/Player/Player.dat", b);
        }
    }

    internal string? GetRequiredModulesString()
    {
        RequiredModule[]? modules = ReportedRequiredModules;
        if (modules == null)
            return null;

        if (modules.Length == 0)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < modules.Length; i++)
        {
            if (i != 0)
                sb.Append(';');

            ref DummyPlayerJoinConfiguration.RequiredModule module = ref modules[i];
            sb.Append(module.Name).Append(',').Append(module.Version);
        }

        return sb.ToString();
    }

    private byte[][] GenerateRandomHWIDs(int amt)
    {
        byte[][] outArr = new byte[amt][];
        Random r = new Random(Index * 397);
        for (int i = 0; i < amt; ++i)
        {
            byte[] hash = new byte[20];
            r.NextBytes(hash);
            outArr[i] = hash;
        }

        return outArr;
    }

    private void UpdateCustomizationAfterGoldUpdated()
    {
        int mask = _customizationUserSetMask;
        if ((mask & 1) == 0)
        {
            FaceIndex = (byte)(Index % (HasGold ? Customization.FACES_FREE + Customization.FACES_PRO : Customization.FACES_FREE));
        }
        if ((mask & 2) == 0)
        {
            HairIndex = (byte)(Index % (HasGold ? Customization.HAIRS_FREE + Customization.HAIRS_PRO : Customization.HAIRS_FREE));
        }
        if ((mask & 4) == 0)
        {
            BeardIndex = (byte)(Index % (HasGold ? Customization.BEARDS_FREE + Customization.BEARDS_PRO : Customization.BEARDS_FREE));
        }
        if ((mask & 8) == 0)
        {
            SkinColor = Customization.SKINS[Index % Customization.SKINS.Length];
        }
        if ((mask & 16) == 0)
        {
            HairColor = Customization.COLORS[Index % Customization.COLORS.Length];
        }
        if ((mask & 32) == 0)
        {
            BeardColor = Customization.COLORS[Index % Customization.COLORS.Length];
        }
        if ((mask & 64) == 0)
        {
            MarkerColor = Customization.MARKER_COLORS[Index % Customization.MARKER_COLORS.Length];
        }
        if ((mask & 128) == 0)
        {
            Skillset = (EPlayerSkillset)(1 + Index % (Customization.SKILLSETS - 1));
        }
        if ((mask & 512) == 0)
        {
            CharacterIndex = (byte)(Index % (HasGold ? Customization.FREE_CHARACTERS + Customization.PRO_CHARACTERS : Customization.FREE_CHARACTERS));
        }

        _customizationUserSetMask = mask;
    }

    internal byte[] GetHwidPacked()
    {
        byte[][] hwids = _reportedHardwareIds;
        byte[] newArray = new byte[hwids.Length * 20];
        for (int i = 0; i < hwids.Length; ++i)
        {
            Buffer.BlockCopy(hwids[i], 0, newArray, i * 20, 20);
        }

        return newArray;
    }

    /// <summary>
    /// Sets he player's spawn location and angle.
    /// </summary>
    public DummyPlayerJoinConfiguration WithSpawnLocation(PlayerTransform value)
    {
        Transform = value;
        return this;
    }

    /// <summary>
    /// Sets the player's character name which is visible only to group members.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentOutOfRangeException">Name doesn't pass Unturned's name filter rules.</exception>
    public DummyPlayerJoinConfiguration WithNickName(string value)
    {
        NickName = value;
        return this;
    }

    /// <summary>
    /// Sets the player's character name which is visible to all players.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentOutOfRangeException">Name doesn't pass Unturned's name filter rules.</exception>
    public DummyPlayerJoinConfiguration WithCharacterName(string value)
    {
        CharacterName = value;
        return this;
    }

    /// <summary>
    /// Sets the player's operating system.
    /// </summary>
    /// <exception cref="InvalidEnumArgumentException"/>
    public DummyPlayerJoinConfiguration WithPlatform(EClientPlatform value)
    {
        Platform = value;
        return this;
    }

    /// <summary>
    /// Sets whether or not the player owns Unturned Gold (Pro).
    /// </summary>
    public DummyPlayerJoinConfiguration WithUnturnedGold(bool hasGold = true)
    {
        HasGold = hasGold;
        return this;
    }

    /// <summary>
    /// Sets the Steam ID of the player's group.
    /// </summary>
    public DummyPlayerJoinConfiguration WithSteamGroup(CSteamID value)
    {
        SteamGroupId = value;
        return this;
    }

    /// <summary>
    /// Sets the Steam ID of the player's lobby.
    /// </summary>
    public DummyPlayerJoinConfiguration WithSteamLobby(CSteamID value)
    {
        SteamLobbyId = value;
        return this;
    }

    /// <summary>
    /// Sets the index of the face to use for the player's character.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Given face does not exist in-game.</exception>
    public DummyPlayerJoinConfiguration WithFace(byte value)
    {
        FaceIndex = value;
        return this;
    }

    /// <summary>
    /// Sets the index of the hairstyle to use for the player's character.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Given hairstyle does not exist in-game.</exception>
    public DummyPlayerJoinConfiguration WithHair(byte value)
    {
        HairIndex = value;
        return this;
    }

    /// <summary>
    /// Sets the index of the beard style to use for the player's character.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Given beard style does not exist in-game.</exception>
    public DummyPlayerJoinConfiguration WithBeard(byte value)
    {
        BeardIndex = value;
        return this;
    }

    /// <summary>
    /// Sets the skin color to use for the player's character.
    /// </summary>
    public DummyPlayerJoinConfiguration WithSkinColor(Color32 value)
    {
        SkinColor = value;
        return this;
    }

    /// <summary>
    /// Sets the skin color to use for the player's character.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="FormatException">Failed to parse <paramref name="colorHex"/> as a <see cref="Color32"/>.</exception>
    public DummyPlayerJoinConfiguration WithSkinColor(string colorHex)
    {
        SkinColor = HexStringHelper.ParseColor32(colorHex ?? throw new ArgumentNullException(nameof(colorHex)));
        return this;
    }

    /// <summary>
    /// Sets the hair color to use for the player's character.
    /// </summary>
    public DummyPlayerJoinConfiguration WithHairColor(Color32 value)
    {
        HairColor = value;
        return this;
    }

    /// <summary>
    /// Sets the hair color to use for the player's character.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="FormatException">Failed to parse <paramref name="colorHex"/> as a <see cref="Color32"/>.</exception>
    public DummyPlayerJoinConfiguration WithHairColor(string colorHex)
    {
        HairColor = HexStringHelper.ParseColor32(colorHex ?? throw new ArgumentNullException(nameof(colorHex)));
        return this;
    }

    /// <summary>
    /// Sets the beard color to use for the player's character.
    /// </summary>
    public DummyPlayerJoinConfiguration WithBeardColor(Color32 value)
    {
        BeardColor = value;
        return this;
    }

    /// <summary>
    /// Sets the beard color to use for the player's character.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="FormatException">Failed to parse <paramref name="colorHex"/> as a <see cref="Color32"/>.</exception>
    public DummyPlayerJoinConfiguration WithBeardColor(string colorHex)
    {
        BeardColor = HexStringHelper.ParseColor32(colorHex ?? throw new ArgumentNullException(nameof(colorHex)));
        return this;
    }

    /// <summary>
    /// Sets the waypoint marker color to use for the player's character.
    /// </summary>
    public DummyPlayerJoinConfiguration WithMarkerColor(Color32 value)
    {
        MarkerColor = value;
        return this;
    }

    /// <summary>
    /// Sets the waypoint marker color to use for the player's character.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="FormatException">Failed to parse <paramref name="colorHex"/> as a <see cref="Color32"/>.</exception>
    public DummyPlayerJoinConfiguration WithMarkerColor(string colorHex)
    {
        MarkerColor = HexStringHelper.ParseColor32(colorHex ?? throw new ArgumentNullException(nameof(colorHex)));
        return this;
    }

    /// <summary>
    /// Sets whether or not the player holds items in their left hand instead of their right.
    /// </summary>
    public DummyPlayerJoinConfiguration WithLeftHanded(bool isLeftHanded = true)
    {
        IsLeftHanded = isLeftHanded;
        return this;
    }

    /// <summary>
    /// Sets the steam inventory item equipped as a shirt for this player's character.
    /// </summary>
    public DummyPlayerJoinConfiguration WithShirtItem(ulong itemInstanceId) => WithShirtItem(new SteamItemInstanceID_t(itemInstanceId));

    /// <summary>
    /// Sets the steam inventory item equipped as a shirt for this player's character.
    /// </summary>
    public DummyPlayerJoinConfiguration WithShirtItem(SteamItemInstanceID_t value)
    {
        ShirtItem = value;
        return this;
    }

    /// <summary>
    /// Sets the steam inventory item equipped as pants for this player's character.
    /// </summary>
    public DummyPlayerJoinConfiguration WithPantsItem(ulong itemInstanceId) => WithPantsItem(new SteamItemInstanceID_t(itemInstanceId));

    /// <summary>
    /// Sets the steam inventory item equipped as pants for this player's character.
    /// </summary>
    public DummyPlayerJoinConfiguration WithPantsItem(SteamItemInstanceID_t value)
    {
        PantsItem = value;
        return this;
    }

    /// <summary>
    /// Sets the steam inventory item equipped as a hat for this player's character.
    /// </summary>
    public DummyPlayerJoinConfiguration WithHatItem(ulong itemInstanceId) => WithHatItem(new SteamItemInstanceID_t(itemInstanceId));

    /// <summary>
    /// Sets the steam inventory item equipped as a hat for this player's character.
    /// </summary>
    public DummyPlayerJoinConfiguration WithHatItem(SteamItemInstanceID_t value)
    {
        HatItem = value;
        return this;
    }

    /// <summary>
    /// Sets the steam inventory item equipped as a backpack for this player's character.
    /// </summary>
    public DummyPlayerJoinConfiguration WithBackpackItem(ulong itemInstanceId) => WithBackpackItem(new SteamItemInstanceID_t(itemInstanceId));

    /// <summary>
    /// Sets the steam inventory item equipped as a backpack for this player's character.
    /// </summary>
    public DummyPlayerJoinConfiguration WithBackpackItem(SteamItemInstanceID_t value)
    {
        BackpackItem = value;
        return this;
    }

    /// <summary>
    /// Sets the steam inventory item equipped as a vest for this player's character.
    /// </summary>
    public DummyPlayerJoinConfiguration WithVestItem(ulong itemInstanceId) => WithVestItem(new SteamItemInstanceID_t(itemInstanceId));

    /// <summary>
    /// Sets the steam inventory item equipped as a vest for this player's character.
    /// </summary>
    public DummyPlayerJoinConfiguration WithVestItem(SteamItemInstanceID_t value)
    {
        VestItem = value;
        return this;
    }

    /// <summary>
    /// Sets the steam inventory item equipped as a mask for this player's character.
    /// </summary>
    public DummyPlayerJoinConfiguration WithMaskItem(ulong itemInstanceId) => WithMaskItem(new SteamItemInstanceID_t(itemInstanceId));

    /// <summary>
    /// Sets the steam inventory item equipped as a mask for this player's character.
    /// </summary>
    public DummyPlayerJoinConfiguration WithMaskItem(SteamItemInstanceID_t value)
    {
        MaskItem = value;
        return this;
    }

    /// <summary>
    /// Sets the steam inventory item equipped as glasses for this player's character.
    /// </summary>
    public DummyPlayerJoinConfiguration WithGlassesItem(ulong itemInstanceId) => WithGlassesItem(new SteamItemInstanceID_t(itemInstanceId));

    /// <summary>
    /// Sets the steam inventory item equipped as glasses for this player's character.
    /// </summary>
    public DummyPlayerJoinConfiguration WithGlassesItem(SteamItemInstanceID_t value)
    {
        GlassesItem = value;
        return this;
    }

    /// <summary>
    /// Adds an equipped steam inventory item for skins or mythics.
    /// </summary>
    /// <exception cref="ArgumentException">Duplicate item.</exception>
    public DummyPlayerJoinConfiguration WithEquippedSkin(ulong itemInstanceId) => WithEquippedSkin(new SteamItemInstanceID_t(itemInstanceId));

    /// <summary>
    /// Adds an equipped steam inventory item for skins or mythics.
    /// </summary>
    /// <exception cref="ArgumentException">Duplicate item.</exception>
    public DummyPlayerJoinConfiguration WithEquippedSkin(SteamItemInstanceID_t item)
    {
        if (EquippedSkins.Contains(item))
        {
            throw new ArgumentException(Properties.Resources.ArgumentExceptionDuplicateItem, nameof(item));
        }

        EquippedSkins.Add(item);
        return this;
    }

    /// <summary>
    /// Sets the skillset of the player's character.
    /// </summary>
    /// <exception cref="InvalidEnumArgumentException"/>
    public DummyPlayerJoinConfiguration WithSkillset(EPlayerSkillset value)
    {
        Skillset = value;
        return this;
    }

    /// <summary>
    /// Sets the language of the player's client as reported to the server.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentOutOfRangeException">Language name is empty.</exception>
    public DummyPlayerJoinConfiguration WithLanguage(string value)
    {
        Language = value;
        return this;
    }

    /// <summary>
    /// The list of hardware IDs reported by the client.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentOutOfRangeException">Array is either empty or contains HWIDs that aren't exactly 20 bytes.</exception>
    public DummyPlayerJoinConfiguration WithHardwareIds(byte[][] value)
    {
        ReportedHardwareIds = value;
        return this;
    }

    /// <summary>
    /// The index of the player's selected character.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Given character slot does not exist in-game.</exception>
    public DummyPlayerJoinConfiguration WithCharacter(byte value)
    {
        CharacterIndex = value;
        return this;
    }

    /// <summary>
    /// Defines a required module.
    /// </summary>
    public struct RequiredModule
    {
        public string Name;
        public uint Version;

        public RequiredModule(string name, string version)
        {
            Name = name;
            Version = Parser.getUInt32FromIP(version);
        }

        public RequiredModule(string name, uint version)
        {
            Name = name;
            Version = version;
        }
    }
}