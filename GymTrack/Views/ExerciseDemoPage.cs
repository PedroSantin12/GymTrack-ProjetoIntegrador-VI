using GymTrack.Data.Dao;
using GymTrack.Services;
using GymTrack.Views.Components;
using Microsoft.Maui.Controls.Shapes;

namespace GymTrack.Views;

public sealed class ExerciseDemoPage : ContentPage
{
    private readonly IExerciseDao _exercises;
    private readonly Label _name = Ui.Text(string.Empty, 26, attributes: FontAttributes.Bold);
    private readonly Label _group = Ui.Tag(string.Empty);
    private readonly Label _notes = Ui.Text(string.Empty, 14, Ui.Muted);
    private readonly Image _animation = new()
    {
        Aspect = Aspect.AspectFit,
        HeightRequest = 290,
        IsAnimationPlaying = true
    };
    private readonly Button _playButton;

    public ExerciseDemoPage(IExerciseDao exercises)
    {
        _exercises = exercises;
        Title = "Execução";
        BackgroundColor = Ui.Background;

        _playButton = Ui.Button("Pausar", () =>
        {
            _animation.IsAnimationPlaying = !_animation.IsAnimationPlaying;
            _playButton.Text = _animation.IsAnimationPlaying ? "Pausar" : "Reproduzir";
            return Task.CompletedTask;
        }, true);

        var media = new VerticalStackLayout { Spacing = 12 };
        media.Add(new Border
        {
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 20 },
            Padding = new Thickness(8),
            Content = _animation
        });
        media.Add(_playButton);

        var instructions = new VerticalStackLayout { Spacing = 10 };
        instructions.Add(Ui.Text("Como executar", 18, attributes: FontAttributes.Bold));
        instructions.Add(_notes);

        var content = new VerticalStackLayout
        {
            Padding = new Thickness(22, 24, 22, 32),
            Spacing = 18
        };
        content.Add(Ui.Tag("DEMONSTRAÇÃO"));
        content.Add(_name);
        content.Add(_group);
        content.Add(media);
        content.Add(Ui.Card(instructions));
        content.Add(Ui.Text("Animação ilustrativa do movimento.", 12, Ui.Muted));

        Content = new ScrollView { Content = content };
    }

    public async Task InitializeAsync(int exerciseId)
    {
        var exercise = await _exercises.GetByIdAsync(exerciseId);
        if (exercise is null)
            throw new InvalidOperationException("Exercício não encontrado.");

        _name.Text = exercise.Name;
        _group.Text = $"PRIMÁRIO: {exercise.MuscleGroup.ToUpperInvariant()}";
        _notes.Text = string.IsNullOrWhiteSpace(exercise.Notes)
            ? "Observe a execução e mantenha o movimento controlado."
            : exercise.Notes;
        _animation.Source = ExerciseMediaCatalog.ForExercise(exercise.Name, exercise.MuscleGroup);
        SemanticProperties.SetDescription(_animation, $"Demonstração animada de {exercise.Name}");
        _animation.IsAnimationPlaying = true;
        _playButton.Text = "Pausar";
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _animation.IsAnimationPlaying = true;
        _playButton.Text = "Pausar";
    }

    protected override void OnDisappearing()
    {
        _animation.IsAnimationPlaying = false;
        base.OnDisappearing();
    }
}
