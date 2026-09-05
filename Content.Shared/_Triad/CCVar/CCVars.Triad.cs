
using Robust.Shared.Configuration;

namespace Content.Shared._Triad.CCVar;

/// <summary>
/// Configuration variables for Triad features
/// </summary>
[CVarDefs]
public sealed class TriadCCVars
{
    // Triad: radiator overhaul
    /// <summary>
    /// Whether radiators pushed into the top thermal bucket (white-hot) slowly
    /// take structural damage until they rupture. Off by default: the glow ramp
    /// and the contact burn already telegraph an overloaded fin, so losing the
    /// hardware on top of that is punishment rather than feedback. Turn it on
    /// to force players to spread load across an array.
    /// </summary>
    public static readonly CVarDef<bool> RadiatorOverheatDamage =
        CVarDef.Create("triad.radiator_overheat_damage", false, CVar.SERVERONLY);
}
