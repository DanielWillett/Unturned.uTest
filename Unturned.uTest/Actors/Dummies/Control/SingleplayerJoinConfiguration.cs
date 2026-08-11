using System;

namespace uTest.Dummies;

/// <summary>
/// Configures the information the client uses to create a singleplayer world.
/// </summary>
/// <remarks>Plugins can change some of these during the <see cref="Provider.onCheckValidWithExplanation"/> event.</remarks>
public class SingleplayerJoinConfiguration : DummyPlayerJoinConfiguration
{
    public delegate void ConfigureConfig(ConfigData config, ModeConfigData modeConfig);

    private const string TutorialMapName = "Tutorial";

    internal ConfigureConfig? LevelConfigurer;

    /// <summary>
    /// The difficulty to use for the world. If you want to load the tutorial level, use <see cref="UseTutorial"/> instead.
    /// </summary>
    /// <remarks>Defaults to a <see cref="EGameMode.NORMAL"/>.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">Only <see cref="EGameMode.EASY"/>, <see cref="EGameMode.NORMAL"/>, and <see cref="EGameMode.HARD"/> can be used here.</exception>
    public EGameMode Difficulty
    {
        get;
        set
        {
            if (value is not EGameMode.EASY and not EGameMode.NORMAL and not EGameMode.HARD)
            {
                if (value != EGameMode.TUTORIAL || Map != TutorialMapName)
                    throw new ArgumentOutOfRangeException(nameof(value));
            }

            field = value;
        }
    } = EGameMode.NORMAL;

    /// <summary>
    /// Whether or not the player has the ability to use vanilla commands.
    /// </summary>
    /// <remarks>Defaults to <see langword="true"/>.</remarks>
    public bool HasCheats { get; set; } = true;

    /// <summary>
    /// The name of the map to load for this singleplayer instance.
    /// </summary>
    /// <remarks>Defaults to whatever map is configured using the <see cref="RequiredMapAttribute"/>.</remarks>
    public string? Map { get; set; }

    internal SingleplayerJoinConfiguration(CSteamID steamId, string name) : base(0, steamId, name, false)
    {
    }

    /// <summary>
    /// Changes the <see cref="Map"/> and <see cref="Difficulty"/> settings so that the tutorial level is loaded.
    /// </summary>
    public SingleplayerJoinConfiguration UseTutorial()
    {
        Map = TutorialMapName;
        Difficulty = EGameMode.TUTORIAL;
        return this;
    }

    /// <summary>
    /// Enables cheats for the player.
    /// </summary>
    public SingleplayerJoinConfiguration WithCheats()
    {
        HasCheats = true;
        return this;
    }

    /// <summary>
    /// Disables cheats for the player.
    /// </summary>
    public SingleplayerJoinConfiguration WithNoCheats()
    {
        HasCheats = false;
        return this;
    }

    /// <summary>
    /// Sets the <see cref="Map"/> to load for this test.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    public SingleplayerJoinConfiguration WithMap(string levelName)
    {
        Map = levelName ?? throw new ArgumentNullException(nameof(levelName));
        return this;
    }

    /// <summary>
    /// Applies a modification to the level's <see cref="ConfigData"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    public SingleplayerJoinConfiguration ConfigureLevelConfig(ConfigureConfig configurer)
    {
        if (configurer == null)
            throw new ArgumentNullException(nameof(configurer));

        if (LevelConfigurer == null)
        {
            LevelConfigurer = configurer;
        }
        else
        {
            ConfigureConfig old = LevelConfigurer;
            LevelConfigurer = (c, m) =>
            {
                old(c, m);
                configurer(c, m);
            };
        }

        return this;
    }
}