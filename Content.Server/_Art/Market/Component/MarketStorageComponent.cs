using Content.Shared._Art.Market;

namespace Content.Server._Art.Market.Components;

[RegisterComponent]
public sealed partial class MarketStorageComponent : Component
{
    public const string ItemsContainerId = "market_items";

    [DataField]
    public List<MarketListingData> Listings = new();

    [DataField]
    public int NextId = 1;
}