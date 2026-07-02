using Content.Client._Art.Market.UI;
using Content.Shared._Art.Market.BUI;
using Robust.Client.UserInterface;

namespace Content.Client._Art.Market.BUI;

public sealed class MarketSaleConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables] private MarketSaleMenu? _menu;

    public MarketSaleConsoleBoundUserInterface(EntityUid owner, Enum uiKey)
        : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<MarketSaleMenu>();
        _menu.OnCreateListing += (name, price) =>
            SendMessage(new MarketCreateListingMessage(name, price));
        _menu.OnCancelListing += id =>
            SendMessage(new MarketCancelListingMessage(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not MarketSaleConsoleInterfaceState s) return;
        _menu?.Populate(s.ContainerContents, s.ActiveListings, s.MaxListings);
    }
}