using Unturned.SystemEx;

namespace uTest.Dummies;

internal class DummyConnectionParameters
{
    public required CSteamID SteamId { get; init; }
    public required CSteamID SteamGroupId { get; init; }
    public required string PlayerName { get; init; }
    public required string CharacterName { get; init; }
    public required string NickName { get; init; }

    public required IPv4Address ConnectionIPv4 { get; init; }
    public required ushort ConnectionPort { get; init; }

    public byte CharacterId { get; set; }
    public byte[][]? HWIDs { get; set; }
    public byte FaceIndex { get; set; }
    public byte HairIndex { get; set; }
    public byte BeardIndex { get; set; }
    public Color SkinColor { get; set; } = Customization.SKINS[0];
    public Color HairColor { get; set; } = Customization.COLORS[0];
    public Color MarkerColor { get; set; } = Customization.MARKER_COLORS[0];
    public bool IsLeftHanded { get; set; }
    public int ShirtItem { get; set; }
    public int PantsItem { get; set; }
    public int HatItem { get; set; }
    public int BackpackItem { get; set; }
    public int VestItem { get; set; }
    public int MaskItem { get; set; }
    public int GlassesItem { get; set; }
    public int[]? SkinItems { get; set; }
    public string[]? SkinTags { get; set; }
    public string[]? SkinDynamicProperties { get; set; }
    public EPlayerSkillset Skillset { get; set; } = EPlayerSkillset.FIRE;
    public string Language { get; set; } = "English";
    public CSteamID LobbyId { get; set; }
}
