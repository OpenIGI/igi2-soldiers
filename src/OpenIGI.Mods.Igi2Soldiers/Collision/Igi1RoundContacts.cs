using OpenIGI.Game.Igi2.Collision;
using OpenIGI.Game.Weapons;
using OpenIGI.Game.World;

namespace OpenIGI.Mods.Igi2Soldiers.Collision;

/// <summary>
/// I.G.I. 2's rounds landing on Project I.G.I.'s placed objects, handed to the mission as its own player's would be
/// (<see cref="MissionState.RoundContact"/>): a camera, an alarm light, a pane or a barrel struck breaks as the mission breaks it.
/// </summary>
public static class Igi1RoundContacts
{
    /// <summary>Has each placed solid that answers nothing of its own hand the rounds it takes to the mission.</summary>
    /// <param name="solids">The level's placed solids, by the placement each stands for.</param>
    /// <param name="mission">The mission, whose answer to a round is taken as it is when a round lands.</param>
    /// <returns>How many solids now answer.</returns>
    public static int Attach(IReadOnlyDictionary<WorldPlacement, Igi2RigidObject> solids, MissionState mission)
    {
        ArgumentNullException.ThrowIfNull(solids);
        ArgumentNullException.ThrowIfNull(mission);
        int attached = 0;
        foreach ((WorldPlacement placement, Igi2RigidObject solid) in solids)
        {
            if (solid.Handler is null)
            {
                solid.Handler = new Target(placement, mission);
                attached++;
            }
        }

        return attached;
    }

    private sealed class Target(WorldPlacement placement, MissionState mission) : IWeaponHitTarget
    {
        public void WeaponHit(in WeaponHit hit) => mission.RoundContact?.Invoke(hit.Position, placement.Owner, placement, hit.Damage);
    }
}
