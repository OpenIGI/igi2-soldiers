using OpenIGI.Core.Diagnostics;
using OpenIGI.Core.Numerics;
using OpenIGI.Core.Titles;
using OpenIGI.Game.Ai;
using OpenIGI.Game.Hosting;
using OpenIGI.Game.Player;
using OpenIGI.Modding;
using OpenIGI.Modding.Presentation;
using OpenIGI.Mods.Igi2Soldiers.Collision;
using OpenIGI.Mods.Igi2Soldiers.Mapping;
using OpenIGI.Mods.Igi2Soldiers.Scripts;
using OpenIGI.Presentation;

namespace OpenIGI.Mods.Igi2Soldiers;

/// <summary>Project I.G.I.'s missions spawn I.G.I. 2's soldiers, which run I.G.I. 2's AI.</summary>
/// <remarks>
/// <para>
/// Two systems join Project I.G.I.'s mission build. <see cref="ClaimSystem"/> runs before the game's soldiers are given their
/// bodies and minds, and claims those the table maps; <see cref="SpawnSystem"/> runs once the player stands, and spawns the
/// I.G.I. 2 soldiers that stand for them, puts them in the level and has its walk run them.
/// </para>
/// <para>
/// The table is <c>igi2soldiers.json</c> beside the mod's assembly when there is one, else the one the mod ships. Without
/// I.G.I. 2 installed nothing is claimed, and the missions play as they ship.
/// </para>
/// </remarks>
[OpenIgiMod]
public sealed partial class Igi2SoldiersMod : IOpenIgiPresentationMod
{
    /// <summary>The system that claims the soldiers.</summary>
    public const string ClaimSystem = "Igi2Soldiers.Claim";

    /// <summary>The system that spawns them.</summary>
    public const string SpawnSystem = "Igi2Soldiers.Spawn";

    private const string LogCategory = "Mods";

    private IModContext? _session;
    private ITitleAudio? _audio;
    private Igi2SoldierKit? _kit;
    private Igi2SoldierLevel? _level;

    /// <summary>Gets the table that says which Project I.G.I. soldiers become which.</summary>
    public SoldierTable Table { get; private set; } = SoldierTable.Shipped;

    /// <inheritdoc/>
    /// <remarks>The soldiers are drawn by one presenter for the level, which skins every body in one batch.</remarks>
    public void ConfigurePresentation(GameTitle title, PresenterRegistry presenters)
    {
        ArgumentNullException.ThrowIfNull(presenters);
        if (title != GameTitle.Igi1)
        {
            return;
        }

        presenters.ForLevel(request => _kit is { } kit && _level is { } level
            ? new Presentation.Igi2SoldierPresenter(request.Device, kit, level, request.Logger)
            : null);
    }

    partial void OnConfigure(IModBuilder builder) => Table = SoldierTable.Load(builder.Directory);

    // What the mod reads of the session from then on: its tables, where the games are installed, and its log.
    [OnModLoaded(Title = "igi1")]
    private void Loaded(IModContext context) => _session = context;

    [AddsLevelSystem<MissionLevelBuild>(Before = MissionSystemNames.Soldiers, Name = ClaimSystem, Title = "igi1")]
    private void Claim(MissionLevelBuild build)
    {
        _level?.Leave();
        _level = null;
        if (DataRoot(GameTitle.Igi2) is null)
        {
            Logger.Warning(LogCategory, "I.G.I. 2 is not installed: the mission's soldiers stay Project I.G.I.'s.");
            return;
        }

        Igi2SoldierLevel.Claim(build.LevelRoot, Table);
    }

    [AddsLevelSystem<MissionLevelBuild>(After = MissionSystemNames.Player, Name = SpawnSystem, Title = "igi1")]
    private void Spawn(MissionLevelBuild build)
    {
        if (DataRoot(GameTitle.Igi2) is not { } igi2 || DataRoot(GameTitle.Igi1) is not { } igi1)
        {
            return;
        }

        if (build.Scene is not { } scene || PlayerSpawn.FindGameplayTask(build.LevelRoot) is not { } player)
        {
            Logger.Warning(LogCategory, "The mission has no scene or no player: its I.G.I. 2 soldiers are not spawned.");
            return;
        }

        _kit ??= Igi2SoldierKit.Load(igi2, Table, Logger);
        string missionDirectory = Path.Combine(igi1, build.Loaded.Descriptor.MissionDirectory);
        _kit.Guest.FileSystem.RegisterDevice(new Igi1MissionDevice(missionDirectory, _kit.Scripts));
        Game.World.TerrainData? terrain = build.Loaded.Terrain is { IsUsable: true } usable ? usable : null;

        // The models' solids follow Project I.G.I.'s collision of them as they move, and what rides them goes along.
        Dictionary<Game.World.WorldPlacement, Game.Igi2.Collision.Igi2RigidObject> solids = [];
        Game.Igi2.Collision.Igi2CollisionWorld world =
            Igi1CollisionWorld.Build(scene.Placements, terrain, static material => material, (object?)terrain ?? scene, solids);
        Game.World.ObjectCollisionIndex? moving = build.Solids;
        Igi1Carriers carriers = new(
            solids,
            moving is null ? null : placement => (moving.TryGetPose(placement, out Vector3d at, out Matrix3f turn), at, turn));
        _level = Igi2SoldierLevel.Spawn(
            _kit,
            Table,
            build.LevelRoot,
            build.Graphs ?? [],
            world,
            player,
            (AiDifficulty)Math.Clamp(build.Mission.PlayerDifficulty, (int)AiDifficulty.Easy, (int)AiDifficulty.Hard),
            new IgiRandom(),
            Logger,
            carriers,
            // Their sounds are I.G.I. 2's, heard through the game's own device beside its sounds.
            _audio ??= _session is not null && _session.Services.TryGet(out ITitleAudioMixer? mixer)
                ? mixer.Open(_kit.Guest.FileSystem, _kit.SoundDefinitions, _kit.SoundBanks)
                : null,
            // The light the level bakes into its buildings, which the soldiers see the player by indoors.
            Detection.Igi1ObjectLight.Of(scene.Placements, LightmapArchive(missionDirectory)));
        _level.JoinTo(build.Mission, build.LevelRoot);
        _level.BindTo(build.Walker);
    }

    private IOpenIgiLogger Logger => _session?.Logger ?? NullLogger.Instance;

    // A level's object lightmaps, lightmaps/lightmaps.res in its mission directory; none where it bakes none.
    private static Formats.Ilff.IresArchive? LightmapArchive(string missionDirectory)
    {
        string path = Path.Combine(missionDirectory, "lightmaps", "lightmaps.res");
        return File.Exists(path) ? Formats.Ilff.IresArchive.Read(File.ReadAllBytes(path)) : null;
    }

    private string? DataRoot(GameTitle title) => _session is not null && _session.TryGetDataRoot(title, out string? root) ? root : null;
}
