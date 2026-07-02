namespace Content.Shared._Art.Market.Components;

[RegisterComponent]
public sealed partial class MarketSaleConsoleComponent : Component
{
    [DataField]
    public int MaxListingsPerPlayer = 10;

    [DataField]
    public float ContainerSearchRadius = 2.0f;
}