using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Utility;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Content.Client.UserInterface.RichText;

public sealed class SpeciesTagHandler : IMarkupTagHandler
{
    public string Name => "species";

    private static int _speciesCounter;
    public static void ResetSpeciesCounter() => _speciesCounter = 0;

    public void PushDrawContext(MarkupNode node, MarkupDrawingContext context) { }
    public void PopDrawContext(MarkupNode node, MarkupDrawingContext context) { }
    public string TextBefore(MarkupNode node) => "";
    public string TextAfter(MarkupNode node) => "";

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        var btn = new Button
        {
            Text = Loc.GetString("paper-species-insert-button"),
            MinSize = new Vector2(48, PaperTagHelper.FontLineHeight + 4),
            MaxSize = new Vector2(120, PaperTagHelper.FontLineHeight + 4),
            Margin = new Thickness(1, 2, 1, 2),
            StyleClasses = { "ButtonSquare" },
            TextAlign = Label.AlignMode.Center,
            Name = $"species_{_speciesCounter++}"
        };

        btn.OnPressed += _ =>
        {
            if (PaperTagHelper.FindPaperWindow(btn) is { } paperWindow)
            {
                var buttonIndex = PaperTagHelper.CountButtonsBefore(btn,
                    b => b.Text == Loc.GetString("paper-species-insert-button"));
                paperWindow.SendSpeciesRequest(buttonIndex);
            }
        };

        control = btn;
        return true;
    }
}