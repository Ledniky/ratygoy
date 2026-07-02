using System.Linq;
using Content.Server._Art.Market.Components;
using Content.Server._NF.Bank;
using Content.Shared._Art.Market;
using Content.Shared._Art.Market.BUI;
using Content.Shared._Art.Market.Components;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server._Art.Market;

public sealed class MarketSystem : EntitySystem
{
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly MarketManager _market = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private readonly HashSet<Entity<MarketContainerComponent>> _containerSet = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<MarketSaleConsoleComponent>(MarketConsoleUiKey.Sale, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnSaleUIOpened);
            subs.Event<MarketCreateListingMessage>(OnCreateListing);
            subs.Event<MarketCancelListingMessage>(OnCancelListing);
        });

        Subs.BuiEvents<MarketRequestConsoleComponent>(MarketConsoleUiKey.Request, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnRequestUIOpened);
            subs.Event<MarketBuyListingMessage>(OnBuyListing);
        });
    }

    private void OnSaleUIOpened(EntityUid uid, MarketSaleConsoleComponent component, BoundUIOpenedEvent args)
    {
        UpdateSaleState(uid, component, args.Actor);
    }

    private void UpdateSaleState(EntityUid uid, MarketSaleConsoleComponent component, EntityUid player)
    {
        var containerContents = new Dictionary<string, int>();
        var nearbyContainer = FindNearbyContainer(uid, component.ContainerSearchRadius);

        if (nearbyContainer != null && TryComp<StorageComponent>(nearbyContainer, out var storage))
        {
            containerContents = storage.Container.ContainedEntities
                .GroupBy(e => MetaData(e).EntityName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(e => TryComp<StackComponent>(e, out var s) ? s.Count : 1)
                );
        }

        var sellerName = Name(player);
        var activeListings = _market.GetListingsBySeller(sellerName);
        _bank.TryGetBalance(player, out var balance);

        _uiSystem.SetUiState(uid, MarketConsoleUiKey.Sale,
            new MarketSaleConsoleInterfaceState(
                containerContents,
                activeListings,
                component.MaxListingsPerPlayer,
                balance));
    }

    private void OnCreateListing(EntityUid uid, MarketSaleConsoleComponent component, MarketCreateListingMessage args)
    {
        var player = args.Actor;
        var sellerName = Name(player);

        if (string.IsNullOrWhiteSpace(args.LotName) || args.LotName.Length > 25)
            return;

        if (_market.GetListingsBySeller(sellerName).Count >= component.MaxListingsPerPlayer)
            return;

        var nearbyContainer = FindNearbyContainer(uid, component.ContainerSearchRadius);
        if (nearbyContainer == null)
            return;

        if (!TryComp<StorageComponent>(nearbyContainer, out var storage))
            return;

        var entities = storage.Container.ContainedEntities.ToList();
        if (entities.Count == 0)
            return;

        foreach (var ent in entities)
            _container.TryRemoveFromContainer(ent);

        _market.CreateListing(sellerName, args.LotName, args.Price, entities);

        UpdateSaleState(uid, component, player);
    }

    private void OnCancelListing(EntityUid uid, MarketSaleConsoleComponent component, MarketCancelListingMessage args)
    {
        var player = args.Actor;
        var sellerName = Name(player);
        var coords = Transform(uid).Coordinates;

        _market.CancelListing(args.ListingId, sellerName, coords);

        UpdateSaleState(uid, component, player);
    }

    private void OnRequestUIOpened(EntityUid uid, MarketRequestConsoleComponent component, BoundUIOpenedEvent args)
    {
        UpdateRequestState(uid, args.Actor);
    }

    private void UpdateRequestState(EntityUid uid, EntityUid player)
    {
        var allListings = _market.GetAllListings();
        _bank.TryGetBalance(player, out var balance);

        _uiSystem.SetUiState(uid, MarketConsoleUiKey.Request,
            new MarketRequestConsoleInterfaceState(allListings, balance));
    }

    private void OnBuyListing(EntityUid uid, MarketRequestConsoleComponent component, MarketBuyListingMessage args)
    {
        var player = args.Actor;
        var allListings = _market.GetAllListings();
        var listing = allListings.FirstOrDefault(l => l.Id == args.ListingId);
        if (listing == null)
            return;

        if (!_bank.TryBankWithdraw(player, listing.Price))
            return;

        _bank.TryBankDeposit(listing.SellerName, listing.Price);

        var coords = Transform(uid).Coordinates;
        if (!_market.FulfillListing(args.ListingId, coords))
        {
            _bank.TryBankDeposit(Name(player), listing.Price);
            return;
        }

        UpdateRequestState(uid, player);
    }

    private EntityUid? FindNearbyContainer(EntityUid uid, float radius)
    {
        _containerSet.Clear();
        _lookup.GetEntitiesInRange(Transform(uid).Coordinates, radius, _containerSet);
        foreach (var ent in _containerSet)
            return ent.Owner;
        return null;
    }
}