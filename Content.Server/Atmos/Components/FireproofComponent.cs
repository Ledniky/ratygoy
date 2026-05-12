namespace Content.Server.Atmos;

[RegisterComponent]
public sealed partial class FireproofComponent : Component
{
    /// <summary>
    /// Determines if a container protects its contents from fire.
    /// </summary>
    [DataField]
    public bool ProtectContents = true;
}