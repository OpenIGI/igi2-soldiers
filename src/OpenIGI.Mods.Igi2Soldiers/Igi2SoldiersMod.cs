using OpenIGI.Core.Diagnostics;
using OpenIGI.Core.Numerics;
using OpenIGI.Core.Titles;
using OpenIGI.Engine.Titles;
using OpenIGI.Game.Ai;
using OpenIGI.Game.Hosting;
using OpenIGI.Game.Level;
using OpenIGI.Game.Player;
using OpenIGI.Presentation;
using OpenIGI.Modding;
using OpenIGI.Modding.Presentation;
using OpenIGI.Mods.Igi2Soldiers;
using OpenIGI.Mods.Igi2Soldiers.Collision;
using OpenIGI.Mods.Igi2Soldiers.Mapping;
using OpenIGI.Mods.Igi2Soldiers.Scripts;

[assembly: OpenIgiMod(typeof(Igi2SoldiersMod))]

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
public sealed class Igi2SoldiersMod : IOpenIgiMod, IOpenIgiPresentationMod
{
    /// <summary>The system that claims the soldiers.</summary>
    public const string ClaimSystem = "Igi2Soldiers.Claim";

    /// <summary>The system that spawns them.</summary>
    public const string SpawnSystem = "Igi2Soldiers.Spawn";

    private readonly Session _session = new();

    /// <inheritdoc/>
    public void Configure(IModBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.Title != GameTitle.Igi1)
        {
            return;
        }

        _session.Table = SoldierTable.Load(Path.GetDirectoryName(typeof(Igi2SoldiersMod).Assembly.Location) ?? AppContext.BaseDirectory);
        builder.AddModule(_session);
        builder.EditLevelSystems<MissionLevelBuild>(systems => systems
            .InsertBefore(MissionSystemNames.Soldiers, new LevelSystem<MissionLevelBuild>(ClaimSystem, _session.Claim))
            .InsertAfter(MissionSystemNames.Player, new LevelSystem<MissionLevelBuild>(SpawnSystem, _session.Spawn)));
    }

    /// <inheritdoc/>
    /// <remarks>The soldiers are drawn by one presenter for the level, which skins every body in one batch.</remarks>
    public void ConfigurePresentation(GameTitle title, PresenterRegistry presenters)
    {
        ArgumentNullException.ThrowIfNull(presenters);
        if (title != GameTitle.Igi1)
        {
            return;
        }

        presenters.ForLevel(request => _session is { Kit: { } kit, Level: { } level }
            ? new Presentation.Igi2SoldierPresenter(request.Device, kit, level, request.Logger)
            : null);
    }

    // What the mod keeps across a session's missions: the table, the soldiers' kit, read once, and the mission's soldiers.
    private sealed class Session : IContentModule
    {
        private IServiceCatalog? _services;
        private IOpenIgiLogger _logger = NullLogger.Instance;
        private Igi2SoldierKit? _kit;
        private Igi2SoldierLevel? _level;

        public SoldierTable Table { get; set; } = SoldierTable.Shipped;

        public Igi2SoldierKit? Kit => _kit;

        public Igi2SoldierLevel? Level => _level;

        public string Name => "Igi2Soldiers.Session";

        public void Register(ContentRegistrationContext context)
        {
        }

        public void LoadTables(ContentLoadContext context)
        {
            _services = context.Services;
            _logger = context.Logger;
        }

        public void Claim(MissionLevelBuild build)
        {
            _level?.Leave();
            _level = null;
            if (DataRoot(GameTitle.Igi2) is null)
            {
                _logger.Warning("Mods", "I.G.I. 2 is not installed: the mission's soldiers stay Project I.G.I.'s.");
                return;
            }

            Igi2SoldierLevel.Claim(build.LevelRoot, Table);
        }

        public void Spawn(MissionLevelBuild build)
        {
            if (DataRoot(GameTitle.Igi2) is not { } igi2 || DataRoot(GameTitle.Igi1) is not { } igi1)
            {
                return;
            }

            if (build.Scene is not { } scene || PlayerSpawn.FindGameplayTask(build.LevelRoot) is not { } player)
            {
                _logger.Warning("Mods", "The mission has no scene or no player: its I.G.I. 2 soldiers are not spawned.");
                return;
            }

            _kit ??= Igi2SoldierKit.Load(igi2, Table, _logger);
            _kit.Guest.FileSystem.RegisterDevice(
                new Igi1MissionDevice(Path.Combine(igi1, build.Loaded.Descriptor.MissionDirectory), _kit.Scripts));
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
                _logger,
                carriers);
            _level.JoinTo(build.Mission, build.LevelRoot);
            _level.BindTo(build.Walker);
        }

        private string? DataRoot(GameTitle title) =>
            _services is not null && _services.TryGet(out ITitleDataLocator? locator) && locator.TryGetDataRoot(title, out string? root)
                ? root
                : null;
    }
}
