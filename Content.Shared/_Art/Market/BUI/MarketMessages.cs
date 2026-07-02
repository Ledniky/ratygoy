using Robust.Shared.Serialization;

namespace Content.Shared._Art.Market.BUI;

[Serializable, NetSerializable]
public sealed class MarketCreateListingMessage : BoundUserInterfaceMessage
{
    public string LotName;
    public int Price;

    public MarketCreateListingMessage(string lotName, int price)
    {
        LotName = lotName;
        Price = price;
    }
}

[Serializable, NetSerializable]
public sealed class MarketCancelListingMessage : BoundUserInterfaceMessage
{
    public int ListingId;

    public MarketCancelListingMessage(int listingId)
    {
        ListingId = listingId;
    }
}

[Serializable, NetSerializable]
public sealed class MarketBuyListingMessage : BoundUserInterfaceMessage
{
    public int ListingId;

    public MarketBuyListingMessage(int listingId)
    {
        ListingId = listingId;
    }
}