using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Audio;

namespace Content.Shared._Art.Hookah;

[RegisterComponent, NetworkedComponent]
public sealed partial class HookahComponent : Component
{
    [DataField] public string PipeSlotId = "pipe_slot";
    [DataField] public string SolutionName = "tank";
    [DataField] public string SolutionNeeded = "Water";
    [DataField] public float ExplosionIntensity = 2.5f;
    [DataField] public Gas GasType = Gas.WaterVapor;
    [DataField] public float ReductionFactor = 10f;
    [DataField] public FixedPoint2 PuffAmount = FixedPoint2.New(5);
    [DataField, ViewVariables] public DamageSpecifier Damage = default!;
	[DataField] public SoundSpecifier? PuffSound;
}