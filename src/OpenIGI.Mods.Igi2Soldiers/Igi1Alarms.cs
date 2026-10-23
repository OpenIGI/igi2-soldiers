namespace OpenIGI.Mods.Igi2Soldiers;

/// <summary>The Project I.G.I. mission's alarms, as its soldiers' I.G.I. 2 squads raise and hear them.</summary>
/// <param name="IsOn">Whether an alarm control's alarm sounds this tick, by the control's id.</param>
/// <param name="Press">Presses an alarm trigger, by its id, as one of the mission's own guards presses it on reaching it.</param>
public sealed record Igi1Alarms(Func<int, bool> IsOn, Action<int> Press);
