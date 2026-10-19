using GymTrack.Views.Components;

namespace GymTrack.Views.Consumer;

internal static class ConsumerUi
{
    public static Label Status() => Ui.Text(string.Empty, 13, Ui.Accent);

    public static string ExerciseCount(int count) => count == 1 ? "1 exercício" : $"{count} exercícios";

    public static View Section(string title, string description)
    {
        var content = new VerticalStackLayout { Spacing = 5 };
        content.Add(Ui.Text(title, 21, null, FontAttributes.Bold));
        content.Add(Ui.Text(description, 13, Ui.Muted));
        return content;
    }

    public static Border Empty(string title, string description)
    {
        var content = new VerticalStackLayout { Spacing = 9 };
        content.Add(Ui.Text(title, 18, null, FontAttributes.Bold));
        content.Add(Ui.Text(description, 14, Ui.Muted));
        return Ui.Card(content);
    }

    public static Border Metric(string value, string title)
    {
        var content = new VerticalStackLayout { Spacing = 5 };
        content.Add(Ui.Text(value, 26, Ui.Accent, FontAttributes.Bold));
        content.Add(Ui.Text(title, 12, Ui.Muted));
        return Ui.Card(content, new Thickness(14));
    }

    public static View Detail(string title, string value)
    {
        var content = new VerticalStackLayout { Spacing = 5 };
        content.Add(Ui.Text(title, 12, Ui.Muted));
        content.Add(Ui.Text(value, 16, null, FontAttributes.Bold));
        return content;
    }
}
