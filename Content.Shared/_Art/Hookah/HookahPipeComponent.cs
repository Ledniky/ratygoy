using Robust.Shared.GameStates;

namespace Content.Shared._Art.Hookah;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HookahPipeComponent : Component
{
    [DataField, AutoNetworkedField] public EntityUid? AttachedTo;

    [DataField] public float Range = 2f;

    [DataField] public float Delay = 2f;
}