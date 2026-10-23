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
/// <param name="options">What of I.G.I. 2 the missions bring in.</param>
internal sealed class Igi2SoldiersSession(SoldierTable table, Igi2SoldiersOptions options)
{
    private const string LogCategory = "Mods";

    private readonly SoldierTable _table = table;
    private readonly Igi2SoldiersOptions _options = options;
    private IModContext? _session;
    private ITitleAudio? _audio;
    private Igi2GuestContent? _content;
    private Igi2HostedLevel? _level;

    // What CreatePlayer made of the mission before its soldiers: the level with I.G.I. 2's player in it, and the collision both
    // move through.
    private Prepared? _prepared;

    /// <summary>Keeps what the mod reads of a session from then on: its tables, where the games are installed, and its log.</summary>
    /// <param name="session">The session.</param>
    public void Start(IModContext session) => _session = session;

    /// <summary>I.G.I. 2's HUD over I.G.I. 2's player, while it stands.</summary>
    /// <param name="request">What the HUD draws with.</param>
    /// <returns>The HUD, or <see langword="null"/> while Project I.G.I.'s own player stands.</returns>
    public ITitleHudPresenter? Hud(in HudPresentationRequest request) =>
        _content is { } content && _prepared is not null
            ? new Igi2GuestHud(new Presentation.Igi2.Hud.Igi2HudRenderer(request.Device, request.Logger), content)
            : null;

    /// <summary>I.G.I. 2's player's arms and its body in the world, while it stands.</summary>
    /// <param name="request">What they draw with.</param>
    /// <returns>The arms, or <see langword="null"/> while Project I.G.I.'s own player stands.</returns>
    public ITitleViewModelPresenter? Arms(in ViewModelPresentationRequest request) =>
        _content is { } content && _prepared is not null
            ? new Igi2GuestArms(new Presentation.Igi2.ViewModel.Igi2ViewModelRenderer(request.Device, content.Guest.Resources, request.Logger, request.Work), content)
            : null;

    /// <summary>
    /// Stands I.G.I. 2's player where the mission stands its own, as the session's gameplay (<see cref="Igi2PlayerGameplay"/>):
    /// David Jones, carrying the Project I.G.I. player's weapons where I.G.I. 2 has them, in the collision the mission's I.G.I. 2
    /// soldiers will move through.
    /// </summary>
    /// <param name="request">The mission, as the host stands its player.</param>
    /// <returns>The player; <see langword="null"/> to leave the mission's own, with the setting off or I.G.I. 2 not installed.</returns>
    public ITitlePlayer? CreatePlayer(in TitlePlayerRequest request)
    {
        _prepared = null;
        if (!_options.Player.Value || DataRoot(GameTitle.Igi2) is not { } igi2 || DataRoot(GameTitle.Igi1) is null
            || request.World.LevelRoot is not { } root || request.World.Mission is not { } mission
            || PlayerSpawn.FindGameplayTask(root) is not { } task || !PlayerSpawn.TryFindGameplay(root, out PlayerSpawnPoint spawn))
        {
            return null;
        }

        _content ??= Igi2GuestContent.Load(igi2, Models, Logger);
        Game.World.TerrainData? terrain = mission.Terrain is { IsUsable: true } usable ? usable : null;
        Dictionary<Game.World.WorldPlacement, Igi2RigidObject> solids = [];
        Igi2CollisionWorld world = Igi1CollisionWorld.Build(
            request.Built.Scene.Placements, terrain, static material => material, (object?)terrain ?? request.Built.Scene, solids);
        Igi2GuestLevel guest = Igi2GuestLevel.Open(_content);
        string weapons = Igi2SoldierLevel.PlayerWeaponScript(task, _content);
        Game.Igi2.Player.Igi2TitlePlayer player = guest.AddPlayer(world, new Igi2GuestPlayerStart(spawn.Position, spawn.Gamma, request.Difficulty)
        {
            WeaponScript = weapons,
            Audio = Audio,
        });
        _prepared = new Prepared(root, guest, world, solids);
        Logger.Information(LogCategory, $"I.G.I. 2's player stands in the mission's stead, carrying {weapons}.");
        return player;
    }

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
        _prepared = null;
        if (!_options.Soldiers.Value)
        {
            return;
        }

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

        // Without I.G.I. 2's player and with the soldiers switched off, the mission is the game's own.
        Prepared? prepared = _prepared is { } made && ReferenceEquals(made.Root, build.LevelRoot) ? made : null;
        if (prepared is null && !_options.Soldiers.Value)
        {
            return;
        }

        _content ??= Igi2GuestContent.Load(igi2, Models, Logger);
        string missionDirectory = Path.Combine(igi1, build.Loaded.Descriptor.MissionDirectory);
        Igi2HostMissionDevice scripts = new(missionDirectory, _content.Scripts);
        _content.Guest.FileSystem.RegisterDevice(scripts);
        Game.World.TerrainData? terrain = build.Loaded.Terrain is { IsUsable: true } usable ? usable : null;

        // The models' solids follow Project I.G.I.'s collision of them as they move, and what rides them goes along.
        Dictionary<Game.World.WorldPlacement, Igi2RigidObject> solids = prepared?.Solids ?? [];
        Igi2CollisionWorld world = prepared?.World
            ?? Igi1CollisionWorld.Build(scene.Placements, terrain, static material => material, (object?)terrain ?? scene, solids);
        Game.World.ObjectCollisionIndex? moving = build.Solids;
        Igi2PlacedCarriers carriers = new(
            solids,
            moving is null ? null : placement => (moving.TryGetPose(placement, out Vector3d at, out Matrix3f turn), at, turn));
        // The weapons lying about are I.G.I. 2's for I.G.I. 2's player to take, each where the mission's lay, which is taken out.
        if (prepared is not null && _options.Pickups.Value && build.Mission.GunPickups is { } lying)
        {
            int laid = 0;
            foreach (Game.World.GunPickup pickup in lying.Items.Where(static p => !p.IsCollected).ToArray())
            {
                Vector3f angles = pickup.Task is { } placed && placed.TryGetValue("Orientation", out object? raw) && raw is Vector3f given ? given : default;
                if (prepared.Guest.AddGunPickup(pickup.Weapon.ScriptId, pickup.Position, angles))
                {
                    lying.Withdraw(pickup);
                    laid++;
                }
            }

            Logger.Information(LogCategory, $"{laid} of the mission's weapons lie as I.G.I. 2's.");
        }

        // The mission's mounted guns are I.G.I. 2's for I.G.I. 2's player and soldiers to work, each on the mission's mount.
        if (prepared is not null && build.Mission.Interactions is { } mounts)
        {
            int mounted = 0;
            foreach (Game.World.WorldInteraction mount in mounts.Items)
            {
                if (mount.StationaryGun is not { } gun)
                {
                    continue;
                }

                Vector3f angles = mount.Task.TryGetValue("Orientation", out object? raw) && raw is Vector3f given ? given : default;
                string weapon = Igi2SoldierLevel.HasWeapon(_content, gun.WeaponId) ? gun.WeaponId : "WEAPON_ID_M2HB";
                if (prepared.Guest.AddStationaryGun(
                    gun.TaskId, gun.GunPosition, angles, gun.MaximumAlphaDegrees, gun.MinimumAlphaDegrees, gun.MaximumGammaDegrees, weapon) is not null)
                {
                    gun.IsHandedOver = true;
                    mounted++;
                }
            }

            Logger.Information(LogCategory, $"{mounted} of the mission's mounted guns are I.G.I. 2's.");
        }

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
            Audio,
            // The light the level bakes into its buildings, which the soldiers see the player by indoors.
            Igi1ObjectLight.InMission(scene.Placements, missionDirectory),
            // Each guard's own orders, its patrol among them, written into the script its I.G.I. 2 mind reads.
            scripts,
            missionDirectory,
            // The mission's own guards, whose alarms its soldiers raise and hear.
            (build.Mission as MissionWorld)?.Soldiers is { } guards ? new Igi1Alarms(guards.IsAlarmOn, guards.ActivateAsGuard) : null,
            // The mission's doors, which they open on their way as its own guards do.
            build.Mission.Doors is { } doors ? [.. doors.Doors] : null,
            // The level I.G.I. 2's player stands in, when it stands in the mission's stead.
            prepared?.Guest,
            // The doors and switches it uses, telling the mission's alarms and scripts as the mission's own player does.
            build.Mission.Interactions is { } interactions ? [.. interactions.Items] : null,
            id => (build.Mission as MissionWorld)?.Soldiers?.ActivateTask(id),
            // The ladders it climbs.
            scene.Ladders);

        // The mission's player is I.G.I. 2's from here: its scripts, objectives and guards find it where I.G.I. 2's body is, and
        // the mission's rounds and blasts strike that body.
        if (_level.Player is { } own)
        {
            Game.Igi2.Player.Igi2PlayerInHost body = new(own.Human);
            player.Body = body;
            player.Events = body;
        }

        // A gun a guard of the mission's drops falls as I.G.I. 2's, for I.G.I. 2's player to take, and the mission's is taken out.
        if (_level.Player is not null && _options.Pickups.Value && build.Mission.GunPickups is { } dropping)
        {
            Igi2HostedLevel hosted = _level;
            void Drop(Game.World.GunPickup pickup)
            {
                if (hosted.DropGun(pickup.Weapon.ScriptId, pickup.Position, pickup.Orientation))
                {
                    dropping.Withdraw(pickup);
                }
            }

            dropping.Spawned += Drop;
            hosted.OnLeave(() => dropping.Spawned -= Drop);
        }

        // The ammunition and the items the mission lays about, which I.G.I. 2's pickups do not hold: I.G.I. 2's player takes
        // them as the mission's own would on reaching them, the ammunition into its I.G.I. 2 pouches.
        if (_level.Player is { } taker && _options.Pickups.Value && build.Mission.GenericPickups is { } generic)
        {
            Game.World.MissionState mission = build.Mission;
            _level.EachTick(() => TakeGenericPickups(taker, generic, mission));
        }

        // Project I.G.I.'s rounds and blasts strike them at the table's DamageFromIgi1.
        _level.JoinTo(build.Mission, build.LevelRoot, _table.DamageFromIgi1);
        _level.BindTo(build.Walker);
    }

    // What I.G.I. 2's player reaches this tick of what the mission lays about, from the middle of its body as the mission's own
    // player's origin reaches; the ammunition is added by its id, which both games share.
    private void TakeGenericPickups(Game.Igi2.Player.Igi2TitlePlayer taker, Game.World.GenericPickupRegistry generic, Game.World.MissionState mission)
    {
        Game.Igi2.Player.Igi2HumanPlayer jones = taker.Human;
        if (jones.IsDead)
        {
            return;
        }

        Vector3d origin = new(jones.Position.X, jones.Position.Y, (jones.Position.Z + jones.OriginalViewPosition.Z) * 0.5);
        foreach (Game.World.GenericPickup pickup in generic.CollectAt(origin))
        {
            if (mission.AmmoTypes is { } ammoTypes && Game.World.AmmoPickupCatalog.TryResolve(pickup, ammoTypes, out Game.Weapons.AmmoType ammo))
            {
                int rounds = Game.World.AmmoPickupCatalog.Amount(pickup, ammo.AmmoId);
                string? into = taker.Inventory is { } pouches ? Igi1AmmoForIgi2.Add(pouches, ammo, rounds, mission.WeaponTypes, ammoTypes) : null;
                Logger.Information(LogCategory, $"I.G.I. 2's player picks up {rounds} of {ammo.ScriptId}, into {into ?? "nothing it fires"}.");
            }
        }
    }

    // Every model the content may dress a soldier or the player in.
    private IEnumerable<string> Models =>
        _table.Models.Append(Igi2GuestPlayerStart.JonesModel).Append(Game.Igi2.Player.Igi2Gameplay.FirstPersonFallbackModel);

    // I.G.I. 2's sounds, heard through the game's own device beside its sounds.
    private ITitleAudio? Audio => _audio ??= _content is { } content && _session is not null && _session.Services.TryGet(out ITitleAudioMixer? mixer)
        ? mixer.Open(content.Guest.FileSystem, content.SoundDefinitions, content.SoundBanks)
        : null;

    private IOpenIgiLogger Logger => _session?.Logger ?? NullLogger.Instance;

    private string? DataRoot(GameTitle title) => _session is not null && _session.TryGetDataRoot(title, out string? root) ? root : null;

    // A mission CreatePlayer made ready: its level, the guest level its player stands in, and the collision they move through.
    private sealed record Prepared(
        Engine.Tasks.GameTask Root, Igi2GuestLevel Guest, Igi2CollisionWorld World, Dictionary<Game.World.WorldPlacement, Igi2RigidObject> Solids);
}
