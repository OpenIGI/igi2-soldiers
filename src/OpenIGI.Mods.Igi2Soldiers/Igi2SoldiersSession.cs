using OpenIGI.Core.Diagnostics;
using OpenIGI.Core.Numerics;
using OpenIGI.Core.Titles;
using OpenIGI.Game.Ai;
using OpenIGI.Game.Hosting;
using OpenIGI.Game.Igi1.World;
using OpenIGI.Game.Igi2.Collision;
using OpenIGI.Game.Igi2.Scripting;
using OpenIGI.Game.Igi2.Systems;
using OpenIGI.Game.Player;
using OpenIGI.Modding;
using OpenIGI.Mods.Igi2Soldiers.Collision;
using OpenIGI.Mods.Igi2Soldiers.Mapping;
using OpenIGI.Presentation;
using OpenIGI.Presentation.Igi2.World;

namespace OpenIGI.Mods.Igi2Soldiers;

/// <summary>
/// The mod in a run: the session it reads, the I.G.I. 2 content its soldiers are made of, and the mission's soldiers while
/// one is played.
/// </summary>
/// <param name="table">Which Project I.G.I. soldiers become which.</param>
internal sealed class Igi2SoldiersSession(SoldierTable table)
{
    private const string LogCategory = "Mods";

    private readonly SoldierTable _table = table;
    private IModContext? _session;
    private ITitleAudio? _audio;
    private Igi2GuestContent? _content;
    private Igi2HostedLevel? _level;

    /// <summary>Keeps what the mod reads of a session from then on: its tables, where the games are installed, and its log.</summary>
    /// <param name="session">The session.</param>
    public void Start(IModContext session) => _session = session;

    /// <summary>Draws the mission's soldiers, one presenter for the level that skins every body in one batch.</summary>
    /// <param name="request">What the presenter draws with.</param>
    /// <returns>The presenter, or <see langword="null"/> while no mission's soldiers stand.</returns>
    public ITaskPresenter? Presenter(TaskPresenterRequest request) =>
        _content is { } content && _level is { } level
            ? new Igi2HostedLevelPresenter(request.Device, content, level, request.Logger)
            : null;

    /// <summary>Claims the soldiers the table maps, before Project I.G.I. gives its own their bodies and minds.</summary>
    /// <param name="build">The mission's build.</param>
    public void Claim(MissionLevelBuild build)
    {
        _level?.Leave();
        _level = null;
        if (DataRoot(GameTitle.Igi2) is null)
        {
            Logger.Warning(LogCategory, "I.G.I. 2 is not installed: the mission's soldiers stay Project I.G.I.'s.");
            return;
        }

        Igi2SoldierLevel.Claim(build.LevelRoot, _table);
    }

    /// <summary>Spawns the I.G.I. 2 soldiers that stand for those claimed, once the player stands, and has the level run them.</summary>
    /// <param name="build">The mission's build.</param>
    public void Spawn(MissionLevelBuild build)
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

        _content ??= Igi2GuestContent.Load(igi2, _table.Models, Logger);
        string missionDirectory = Path.Combine(igi1, build.Loaded.Descriptor.MissionDirectory);
        Igi2HostMissionDevice scripts = new(missionDirectory, _content.Scripts);
        _content.Guest.FileSystem.RegisterDevice(scripts);
        Game.World.TerrainData? terrain = build.Loaded.Terrain is { IsUsable: true } usable ? usable : null;

        // The models' solids follow Project I.G.I.'s collision of them as they move, and what rides them goes along.
        Dictionary<Game.World.WorldPlacement, Igi2RigidObject> solids = [];
        Igi2CollisionWorld world =
            Igi1CollisionWorld.Build(scene.Placements, terrain, static material => material, (object?)terrain ?? scene, solids);
        Game.World.ObjectCollisionIndex? moving = build.Solids;
        Igi2PlacedCarriers carriers = new(
            solids,
            moving is null ? null : placement => (moving.TryGetPose(placement, out Vector3d at, out Matrix3f turn), at, turn));
        _level = Igi2SoldierLevel.Spawn(
            _content,
            _table,
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
                ? mixer.Open(_content.Guest.FileSystem, _content.SoundDefinitions, _content.SoundBanks)
                : null,
            // The light the level bakes into its buildings, which the soldiers see the player by indoors.
            Igi1ObjectLight.InMission(scene.Placements, missionDirectory),
            // Each guard's own orders, its patrol among them, written into the script its I.G.I. 2 mind reads.
            scripts,
            missionDirectory,
            // The mission's own guards, whose alarms its soldiers raise and hear.
            (build.Mission as MissionWorld)?.Soldiers is { } guards ? new Igi1Alarms(guards.IsAlarmOn, guards.ActivateAsGuard) : null,
            // The mission's doors, which they open on their way as its own guards do.
            build.Mission.Doors is { } doors ? [.. doors.Doors] : null);

        // Project I.G.I.'s rounds and blasts strike them at the table's DamageFromIgi1.
        _level.JoinTo(build.Mission, build.LevelRoot, _table.DamageFromIgi1);
        _level.BindTo(build.Walker);
    }

    private IOpenIgiLogger Logger => _session?.Logger ?? NullLogger.Instance;

    private string? DataRoot(GameTitle title) => _session is not null && _session.TryGetDataRoot(title, out string? root) ? root : null;
}
