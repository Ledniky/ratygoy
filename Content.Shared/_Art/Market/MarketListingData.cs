using Robust.Shared.Serialization;

namespace Content.Shared._Art.Market;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class MarketListingData
{
    [DataField] public int Id;
    [DataField] public string SellerName = string.Empty;
    [DataField] public string LotName = string.Empty;
    [DataField] public int Price;
    [DataField] public Dictionary<string, int> Contents = new();
}