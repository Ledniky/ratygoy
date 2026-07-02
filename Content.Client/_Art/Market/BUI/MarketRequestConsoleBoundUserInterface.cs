using Content.Client._Art.Market.UI;
using Content.Shared._Art.Market.BUI;
using Robust.Client.UserInterface;

namespace Content.Client._Art.Market.BUI;

public sealed class MarketRequestConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables] private MarketRequestMenu? _menu;

    public MarketRequestConsoleBoundUserInterface(EntityUid owner, Enum uiKey)
        : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<MarketRequestMenu>();
        _menu.OnBuyListing += id =>
            SendMessage(new MarketBuyListingMessage(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not MarketRequestConsoleInterfaceState s) return;
        _menu?.Populate(s.Listings);
    }
}