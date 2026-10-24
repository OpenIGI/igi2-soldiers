using OpenIGI.Core.Diagnostics;
using OpenIGI.Core.Numerics;
using OpenIGI.Engine.Tasks;
using OpenIGI.Game.Ai;
using OpenIGI.Game.Igi1.Ai;
using OpenIGI.Game.Igi2.Ai.Runtime;
using OpenIGI.Game.Igi2.Collision;
using OpenIGI.Game.Igi2.Player;
using OpenIGI.Game.Igi2.Scripting;
using OpenIGI.Game.Igi2.Soldiers;
using OpenIGI.Game.Igi2.Systems;
using OpenIGI.Game.Player;
using OpenIGI.Game.World;
using OpenIGI.Modding.Testing;
using OpenIGI.Mods.Igi2Soldiers.Collision;
using OpenIGI.Mods.Igi2Soldiers.Mapping;
using OpenIGI.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace OpenIGI.Mods.Igi2Soldiers.Tests;

/// <summary>I.G.I. 2's player in a Project I.G.I. mission, as the remastered crosshair reads him: what is under it, what his rounds do.</summary>
public sealed class Igi2GuestCrosshairTests(ITestOutputHelper output)
{
    [RequiresTitleDataFact("igi2")]
    public void Jones_aimed_at_a_soldier_marks_him_and_is_credited_with_the_kill()
    {
        string igi1 = GameData.RequireRoot();
        (LevelTask level, WorldScene scene, TerrainData terrain, string missionDirectory) = Igi2SoldierLevelTests.LoadMission(igi1, 1);
        Igi2GuestContent kit = Igi2GuestContent.Load(
            GameDataSource.Igi2.RequireRoot(),
            SoldierTable.Shipped.Models.Append(Igi2GuestPlayerStart.JonesModel).Append(Igi2Gameplay.FirstPersonFallbackModel));
        Igi2HostMissionDevice device = new(missionDirectory, kit.Scripts);
        kit.Guest.FileSystem.RegisterDevice(device);
        HumanPlayerTask task = PlayerSpawn.FindGameplayTask(level)!;
        Assert.True(PlayerSpawn.TryFindGameplay(level, out PlayerSpawnPoint spawn));
        Igi2CollisionWorld world = Igi1CollisionWorld.Build(scene.Placements, terrain, static m => m, new object());
        Igi2GuestLevel guest = Igi2GuestLevel.Open(kit);
        Igi2TitlePlayer jones = guest.AddPlayer(world, new Igi2GuestPlayerStart(spawn.Position, spawn.Gamma, AiDifficulty.Normal)
        {
            WeaponScript = Igi2SoldierLevel.PlayerWeaponScript(task, kit),
        });
        Igi2HostedLevel soldiers = Igi2SoldierLevel.Spawn(
            kit, SoldierTable.Shipped, level, NavGraphs.Read(Path.Combine(missionDirectory, "graphs"), level, NullLogger.Instance),
            world, task, AiDifficulty.Normal, new IgiRandom(), NullLogger.Instance, guest: guest, scripts: device);
        for (int tick = 0; tick < 30; tick++)
        {
            soldiers.Tick();
        }

        // A soldier on open ground, and a spot six metres off him from which the first thing a line to his chest meets is him.
        (Igi2HumanSoldier target, Vector3d stand) = ClearShot(soldiers, world, jones, spawn.Position);
        Vector3d chest = new(target.Position.X, target.Position.Y, target.Position.Z + (1.3 * 4096.0));
        PlayerAimFeedback seen = default;
        for (int tick = 0; tick < 240 && !target.IsDead; tick++)
        {
            jones.Human.PopPosition(stand);
            Vector3d eye = jones.Human.View.Position;
            double dx = chest.X - eye.X, dy = chest.Y - eye.Y, dz = chest.Z - eye.Z;
            jones.Human.PopOrientation((float)Math.Atan2(-dx, dy));
            jones.Human.View.IdealPitch = jones.Human.View.Pitch = (float)Math.Atan2(dz, Math.Sqrt((dx * dx) + (dy * dy)));
            bool firing = tick >= 10;
            jones.Tick(new TitlePlayerInput(0f, 0f, firing ? PlayerAction.Fire : default, firing && tick % 4 == 0 ? PlayerAction.Fire : default, 1f, false));
            soldiers.Tick();
            if (tick == 9)
            {
                seen = jones.AimFeedback();
            }
        }

        PlayerAimFeedback after = jones.AimFeedback();
        output.WriteLine($"before firing: {seen}; after: {after}; target dead {target.IsDead}");
        Assert.True(seen.OnEnemy, "The crosshair did not mark the soldier Jones looked at.");
        Assert.NotEqual(CrosshairShape.None, seen.Shape);
        Assert.True(target.IsDead, "Jones's rounds did not kill the soldier.");
        Assert.True(after.Shots > seen.Shots);
        Assert.True(after.Hits > 0);
        Assert.Equal(1, after.Kills);
        Assert.False(after.OnEnemy);
        soldiers.Leave();
    }

    // The living, unscoped soldier nearest a point with a clear line to his chest from six metres off on one of eight sides,
    // and that spot: the first thing the line from Jones's eye there meets is the soldier's own body.
    private static (Igi2HumanSoldier Soldier, Vector3d Stand) ClearShot(Igi2HostedLevel soldiers, Igi2CollisionWorld world, Igi2TitlePlayer jones, Vector3d near)
    {
        foreach (Igi2HumanSoldier soldier in soldiers.HandedOver.Values
            .Where(static s => !s.IsDead && !Game.Weapons.ScopedWeapons.IsScoped(s.Weapon.Inventory?.Current?.WeaponType.WeaponId))
            .OrderBy(s => Math.Pow(s.Position.X - near.X, 2) + Math.Pow(s.Position.Y - near.Y, 2)))
        {
            Vector3d chest = new(soldier.Position.X, soldier.Position.Y, soldier.Position.Z + (1.3 * 4096.0));
            for (int side = 0; side < 8; side++)
            {
                double angle = side * Math.Tau / 8;
                Vector3d stand = new(soldier.Position.X + (Math.Cos(angle) * 6.0 * 4096.0), soldier.Position.Y + (Math.Sin(angle) * 6.0 * 4096.0), soldier.Position.Z + 400.0);
                Vector3d eye = new(stand.X, stand.Y, stand.Z + (1.6 * 4096.0));
                Game.Physics.WorldLineTrace line = new(1) { Includes = owner => !ReferenceEquals(owner, jones.Human) };
                line.Starts[0] = eye;
                line.Ends[0] = new Vector3d(eye.X + ((chest.X - eye.X) * 1.5), eye.Y + ((chest.Y - eye.Y) * 1.5), eye.Z + ((chest.Z - eye.Z) * 1.5));
                world.TraceLines(line, Igi2CollisionWorld.EveryTypeGroup | Igi2CollisionWorld.HumanGroup | Igi2CollisionWorld.TerrainGroup);
                Game.Physics.WorldIntersection met = line.Intersections[0];
                if (met.IsHit && ReferenceEquals(met.HitObject?.Handler ?? met.HitObject?.Owner, soldier))
                {
                    return (soldier, stand);
                }
            }
        }

        throw new InvalidOperationException("No soldier of mission one stands in the open.");
    }
}
