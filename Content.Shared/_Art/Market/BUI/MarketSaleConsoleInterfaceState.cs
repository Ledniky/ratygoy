using Robust.Shared.Serialization;

namespace Content.Shared._Art.Market.BUI;

[Serializable, NetSerializable]
public sealed class MarketSaleConsoleInterfaceState : BoundUserInterfaceState
{
    public Dictionary<string, int> ContainerContents;
    public List<MarketListingData> ActiveListings;
    public int MaxListings;
    public int Balance;

    public MarketSaleConsoleInterfaceState(
        Dictionary<string, int> containerContents,
        List<MarketListingData> activeListings,
        int maxListings,
        int balance)
    {
        ContainerContents = containerContents;
        ActiveListings = activeListings;
        MaxListings = maxListings;
        Balance = balance;
    }
}