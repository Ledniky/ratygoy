using Content.Shared.Research.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Research.UI;

[UsedImplicitly]
public sealed class ResearchServerBoundUserInterface(EntityUid owner, Enum uiKey)
    : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private ResearchServerMenu? _menu;

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<ResearchServerMenu>();
        _menu.OnClientEntryPressed += netEnt =>
        {
            SendPredictedMessage(new ToggleResearchClientMessage(netEnt));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ResearchServerBuiState msg)
            return;
        _menu?.Update(msg);
    }
}