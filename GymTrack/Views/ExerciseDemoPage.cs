using GymTrack.Data.Dao;
using GymTrack.Services;
using GymTrack.Views.Components;
using Microsoft.Maui.Controls.Shapes;

namespace GymTrack.Views;

public sealed class ExerciseDemoPage : ContentPage
{
    private static readonly Color PageBackground = Color.FromArgb("#101113");
    private static readonly Color Surface = Color.FromArgb("#202126");
    private static readonly Color Accent = Color.FromArgb("#FF8A1F");
    private static readonly Color Muted = Color.FromArgb("#A4A5AC");
    private static readonly Color BorderColor = Color.FromArgb("#34363B");

    private readonly IExerciseDao _exercises;
    private readonly Label _name = Ui.Text(string.Empty, 26, attributes: FontAttributes.Bold);
    private readonly Label _group = Ui.Text(string.Empty, 13, Accent);
    private readonly VerticalStackLayout _media = new() { Spacing = 8 };
    private readonly VerticalStackLayout _details = new() { Spacing = 16 };

    public ExerciseDemoPage(IExerciseDao exercises)
    {
        _exercises = exercises;
        Title = "Ver execução";
        BackgroundColor = PageBackground;

        var close = new ToolbarItem { Text = "Fechar" };
        close.Clicked += async (_, _) => await CloseAsync();
        ToolbarItems.Add(close);

        var content = new VerticalStackLayout
        {
            Padding = new Thickness(20, 24, 20, 36),
            Spacing = 18
        };
        content.Add(Ui.Text("GUIA DO EXERCÍCIO", 11, Accent, FontAttributes.Bold));
        content.Add(_name);
        content.Add(_group);
        content.Add(_media);
        content.Add(_details);
        Content = new ScrollView { Content = content };
    }

    public async Task InitializeAsync(int exerciseId)
    {
        var exercise = await _exercises.GetByIdAsync(exerciseId)
            ?? throw new InvalidOperationException("Exercício não encontrado.");
        var media = ExerciseMediaCatalog.Get(exercise.Name);
        var guide = ExerciseGuideCatalog.ForExercise(exercise.Name);

        _name.Text = exercise.Name;
        _group.Text = exercise.MuscleGroup.ToUpperInvariant();
        _media.Clear();
        _details.Clear();
        if (!string.IsNullOrWhiteSpace(media.Image))
        {
            var illustration = new Image
            {
                Source = media.Image,
                Aspect = Aspect.AspectFit,
                BackgroundColor = Colors.White,
                HeightRequest = 234,
                HorizontalOptions = LayoutOptions.Fill
            };
            SemanticProperties.SetDescription(illustration, $"Ilustração demonstrativa de {exercise.Name}");
            _media.Add(MediaFrame(illustration));
            _media.Add(Ui.Text("Posições ilustrativas · Consulte as etapas abaixo", 12, Muted));
        }
        else
        {
            var placeholder = new VerticalStackLayout
            {
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                Spacing = 5,
                Children =
                {
                    Ui.Text("Sem mídia verificada", 16, Ui.Foreground, FontAttributes.Bold),
                    Ui.Text("Siga as instruções de execução abaixo.", 12, Muted)
                }
            };
            _media.Add(MediaFrame(new Grid { HeightRequest = 150, Children = { placeholder } }));
        }

        var steps = new VerticalStackLayout { Spacing = 10 };
        if (!string.IsNullOrWhiteSpace(exercise.Notes))
            steps.Add(Ui.Text(exercise.Notes, 14, Ui.Foreground));
        if (guide is { } known)
        {
            for (var index = 0; index < known.Steps.Count; index++)
                steps.Add(Ui.Text($"{index + 1}. {known.Steps[index]}", 14, Ui.Foreground));
        }
        else if (string.IsNullOrWhiteSpace(exercise.Notes))
        {
            steps.Add(Ui.Text("Instruções específicas ainda não cadastradas para este exercício.", 14, Muted));
        }
        _details.Add(Section("Como executar", steps));

        var muscles = guide?.Muscles ?? exercise.MuscleGroup;
        _details.Add(Section("Músculos trabalhados", Ui.Text(muscles, 14, Ui.Foreground)));
        _details.Add(Section("Cuidados", Ui.Text(guide?.Caution ??
            "Peça orientação de um profissional para confirmar a técnica e a carga adequadas.", 14, Ui.Foreground)));
        if (!string.IsNullOrWhiteSpace(media.Credit))
            _details.Add(Ui.Text(media.Credit, 11, Muted));
        if (media.SourceUrl is { } sourceUrl)
        {
            var source = Ui.Button("Fonte e licença  ↗",
                () => Microsoft.Maui.ApplicationModel.Launcher.Default.OpenAsync(sourceUrl), true);
            source.TextColor = Accent;
            source.BackgroundColor = Colors.Transparent;
            source.BorderWidth = 0;
            source.Padding = new Thickness(0, 2);
            source.MinimumHeightRequest = 36;
            source.HorizontalOptions = LayoutOptions.Start;
            _details.Add(source);
        }
    }

    private static Border MediaFrame(View content) => new()
    {
        Content = content,
        BackgroundColor = PageBackground,
        Stroke = new SolidColorBrush(BorderColor),
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = 12 },
        Padding = 4
    };

    private static Border Section(string title, View content)
    {
        var stack = new VerticalStackLayout { Spacing = 10 };
        stack.Add(Ui.Text(title, 17, Ui.Foreground, FontAttributes.Bold));
        stack.Add(content);
        return new Border
        {
            Content = stack,
            BackgroundColor = Surface,
            Stroke = new SolidColorBrush(BorderColor),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = 16
        };
    }

    private async Task CloseAsync()
    {
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
        else
            await Navigation.PopModalAsync();
    }

}
