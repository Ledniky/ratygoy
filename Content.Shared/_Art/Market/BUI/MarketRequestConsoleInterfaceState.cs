using Robust.Shared.Serialization;

namespace Content.Shared._Art.Market.BUI;

[Serializable, NetSerializable]
public sealed class MarketRequestConsoleInterfaceState : BoundUserInterfaceState
{
    public List<MarketListingData> Listings;
    public int Balance;

    public MarketRequestConsoleInterfaceState(List<MarketListingData> listings, int balance)
    {
        Listings = listings;
        Balance = balance;
    }
}