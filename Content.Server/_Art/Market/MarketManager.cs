using System.Linq;
using Content.Server._Art.Market.Components;
using Content.Server.GameTicking;
using Content.Shared._Art.Market;
using Content.Shared.Stacks;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server._Art.Market;

public sealed class MarketManager : EntitySystem
{
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly GameTicker _ticker = default!;

    private (EntityUid mapEnt, MarketStorageComponent storage)? GetStorageWithEnt()
    {
        var mapEnt = _map.GetMapOrInvalid(_ticker.DefaultMap);
        if (!mapEnt.IsValid())
            return null;
        if (!TryComp<MarketStorageComponent>(mapEnt, out var storage))
        {
            Log.Error("MarketManager: MarketStorageComponent not found on map entity! " +
                      "Add EnsureComp<MarketStorageComponent> in GameTicker.RoundFlow.cs");
            return null;
        }
        return (mapEnt, storage);
    }

    public List<MarketListingData> GetAllListings()
    {
        var result = GetStorageWithEnt();
        return result?.storage.Listings ?? new List<MarketListingData>();
    }

    public List<MarketListingData> GetListingsBySeller(string sellerName)
    {
        return GetAllListings().Where(l => l.SellerName == sellerName).ToList();
    }

    public void CreateListing(string seller, string lotName, int price, List<EntityUid> entities)
    {
        var result = GetStorageWithEnt();
        if (result == null) return;
        var (mapEnt, storage) = result.Value;

        var id = storage.NextId++;

        var contents = entities
            .GroupBy(e => MetaData(e).EntityName)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(e => TryComp<StackComponent>(e, out var s) ? s.Count : 1)
            );

        var itemContainer = _container.EnsureContainer<Container>(
            mapEnt, MarketStorageComponent.ItemsContainerId);

        foreach (var ent in entities)
        {
            var tag = EnsureComp<MarketItemComponent>(ent);
            tag.ListingId = id;
            if (!_container.Insert(ent, itemContainer))
            {
                Log.Error($"MarketManager: Failed to insert {ToPrettyString(ent)} into market container!");
                _transform.AttachToGridOrMap(ent);
            }
        }

        storage.Listings.Add(new MarketListingData
        {
            Id = id,
            SellerName = seller,
            LotName = lotName,
            Price = price,
            Contents = contents
        });
        Dirty(mapEnt, storage);

        Log.Info($"MarketManager: Created listing #{id} '{lotName}' by {seller} for {price} sp " +
                 $"({entities.Count} items)");
    }

    public bool CancelListing(int id, string seller, EntityCoordinates coords)
    {
        var result = GetStorageWithEnt();
        if (result == null) return false;
        var (mapEnt, storage) = result.Value;

        var listing = storage.Listings.FirstOrDefault(l => l.Id == id && l.SellerName == seller);
        if (listing == null) return false;

        if (!SpawnItemsAt(id, mapEnt, coords))
            return false;

        storage.Listings.Remove(listing);
        Dirty(mapEnt, storage);
        Log.Info($"MarketManager: Cancelled listing #{id} by {seller}");
        return true;
    }

    public bool FulfillListing(int id, EntityCoordinates coords)
    {
        var result = GetStorageWithEnt();
        if (result == null) return false;
        var (mapEnt, storage) = result.Value;

        var listing = storage.Listings.FirstOrDefault(l => l.Id == id);
        if (listing == null) return false;

        if (!SpawnItemsAt(id, mapEnt, coords))
            return false;

        storage.Listings.Remove(listing);
        Dirty(mapEnt, storage);
        Log.Info($"MarketManager: Fulfilled listing #{id} '{listing.LotName}'");
        return true;
    }

    private bool SpawnItemsAt(int listingId, EntityUid mapEnt, EntityCoordinates coords)
    {
        if (!_container.TryGetContainer(mapEnt, MarketStorageComponent.ItemsContainerId, out var itemContainer))
        {
            Log.Error($"MarketManager: market_items container not found on map entity for listing {listingId}");
            return false;
        }

        var items = itemContainer.ContainedEntities
            .Where(e => TryComp<MarketItemComponent>(e, out var tag) && tag.ListingId == listingId)
            .ToList();

        if (items.Count == 0)
        {
            Log.Error($"MarketManager: No items found for listing {listingId}");
            return false;
        }

        foreach (var ent in items)
        {
            _container.TryRemoveFromContainer(ent);
            RemComp<MarketItemComponent>(ent);
            _transform.SetCoordinates(ent, coords);
        }

        return true;
    }
}