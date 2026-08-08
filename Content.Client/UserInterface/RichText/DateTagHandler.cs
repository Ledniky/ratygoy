using Content.Client.Paper.UI;
using Content.Shared.Paper;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Utility;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Content.Client.UserInterface.RichText;

public sealed class DateTagHandler : IMarkupTagHandler
{
    public string Name => "date";

    private static int _dateCounter;
    public static void ResetDateCounter() => _dateCounter = 0;

    public void PushDrawContext(MarkupNode node, MarkupDrawingContext context) { }
    public void PopDrawContext(MarkupNode node, MarkupDrawingContext context) { }
    public string TextBefore(MarkupNode node) => "";
    public string TextAfter(MarkupNode node) => "";

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        var btn = new Button
        {
            Text = Loc.GetString("paper-date-insert-button"),
            MinSize = new Vector2(48, PaperTagHelper.FontLineHeight + 4),
            MaxSize = new Vector2(120, PaperTagHelper.FontLineHeight + 4),
            Margin = new Thickness(1, 2, 1, 2),
            StyleClasses = { "ButtonSquare" },
            TextAlign = Label.AlignMode.Center,
            Name = $"date_{_dateCounter++}"
        };

        btn.OnPressed += _ =>
        {
            if (PaperTagHelper.FindPaperWindow(btn) is { } paperWindow)
            {
                var buttonIndex = PaperTagHelper.CountButtonsBefore(btn,
                    b => b.Text == Loc.GetString("paper-date-insert-button"));
                var now = DateTime.UtcNow;
                var futureDate = new DateTime(now.Year + 1000, now.Month, now.Day);
                var dateStr = futureDate.ToString("dd.MM.yyyy");
                paperWindow.SaveText(
                    PaperTagUtility.ReplaceNthTag(paperWindow.GetCurrentText(), "[date]", buttonIndex, dateStr));
            }
        };

        control = btn;
        return true;
    }
}