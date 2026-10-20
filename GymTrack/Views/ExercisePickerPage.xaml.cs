using GymTrack.Services;
using GymTrack.ViewModels;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GymTrack.Views;

public partial class ExercisePickerPage : ContentPage
{
    private readonly ExercisePickerViewModel _viewModel;
    private readonly IServiceProvider _services;
    private readonly ILogger<ExercisePickerPage> _logger;
    private bool _hasAppeared;
    private bool _isClosing;
    private bool _isOpeningManager;
    private bool _isOpeningPreview;
    private bool _isInitialized;

    public ExercisePickerPage(
        ExercisePickerViewModel viewModel,
        AppSession session,
        IServiceProvider services,
        ILogger<ExercisePickerPage> logger)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        _services = services;
        _logger = logger;
        ManageExercisesButton.IsVisible = session.IsEmployee;
        BuildGroupSections();

        viewModel.ExerciseSelected += OnExerciseSelected;
        viewModel.PreviewRequested += OnPreviewRequested;
        viewModel.ManageExercisesRequested += OnManageExercisesRequested;
        viewModel.CloseRequested += OnCloseRequested;
    }

    public event EventHandler<ExercisePickerSelectionEventArgs>? ExerciseSelected;

    public event EventHandler? ManageExercisesRequested;

    public async Task InitializeAsync(IReadOnlyCollection<int> excludedIds)
    {
        await _viewModel.InitializeAsync(excludedIds);
        _isInitialized = true;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_isInitialized)
        {
            return;
        }

        if (!_hasAppeared)
        {
            _hasAppeared = true;
            return;
        }

        try
        {
            await _viewModel.LoadCommand.ExecuteAsync(null);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao atualizar o seletor de exercícios.");
            _viewModel.StatusMessage = "Não foi possível atualizar os exercícios.";
        }
    }

    private async void OnExerciseSelected(
        object? sender,
        ExercisePickerSelectionEventArgs e)
    {
        ExerciseSelected?.Invoke(this, e);
        await CloseAsync();
    }

    private async void OnPreviewRequested(object? sender, ExercisePickerSelectionEventArgs e)
    {
        if (_isOpeningPreview) return;
        _isOpeningPreview = true;
        try
        {
            await _services.GetRequiredService<IAppNavigator>().OpenExerciseDemoAsync(e.Exercise.Id);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao abrir a execução do exercício.");
            _viewModel.StatusMessage = "Não foi possível abrir a execução deste exercício.";
        }
        finally
        {
            _isOpeningPreview = false;
        }
    }

    private void BuildGroupSections()
    {
        AddGroupSection("Parte superior", MuscleGroupCatalog.UpperBody);
        AddGroupSection("Parte inferior", MuscleGroupCatalog.LowerBody);
        AddGroupSection("Outros", MuscleGroupCatalog.Other);
        AddGroupSection("Grupos de exercícios antigos", MuscleGroupCatalog.Legacy);
    }

    private void AddGroupSection(string title, IReadOnlyList<string> groups)
    {
        var section = new VerticalStackLayout { Spacing = 12 };
        section.Add(new Label
        {
            Text = title,
            FontSize = 17,
            TextColor = Color.FromArgb("#B9B9B9")
        });

        var grid = new Grid
        {
            ColumnSpacing = 10,
            RowSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };

        for (var index = 0; index < groups.Count; index++)
        {
            var group = groups[index];
            if (index % 2 == 0) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var icon = new Border
            {
                WidthRequest = 44,
                HeightRequest = 44,
                Padding = 0,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 22 },
                BackgroundColor = Color.FromArgb("#493322"),
                Content = new Label
                {
                    Text = GroupInitials(group),
                    TextColor = Color.FromArgb("#FF913A"),
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                }
            };

            var content = new Grid
            {
                ColumnSpacing = 10,
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                }
            };
            content.Add(icon);
            content.Add(new Label
            {
                Text = group,
                FontSize = 14,
                TextColor = Colors.White,
                VerticalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.WordWrap
            }, 1);

            var card = new Border
            {
                MinimumHeightRequest = 74,
                Padding = new Thickness(10),
                Stroke = new SolidColorBrush(Color.FromArgb("#4C4C4C")),
                StrokeShape = new RoundRectangle { CornerRadius = 18 },
                BackgroundColor = Color.FromArgb("#2B2B2B"),
                Content = content
            };
            card.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = _viewModel.SelectGroupCommand,
                CommandParameter = group
            });
            SemanticProperties.SetDescription(card, $"Ver exercícios de {group}");
            grid.Add(card, index % 2, index / 2);
        }

        section.Add(grid);
        GroupSections.Add(section);
    }

    private static string GroupInitials(string group)
    {
        var parts = group.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1
            ? string.Concat(char.ToUpperInvariant(parts[0][0]), char.ToUpperInvariant(parts[^1][0]))
            : group[..Math.Min(2, group.Length)].ToUpperInvariant();
    }

    private async void OnManageExercisesRequested(object? sender, EventArgs e)
    {
        if (_isOpeningManager)
        {
            return;
        }

        _isOpeningManager = true;
        ManageExercisesRequested?.Invoke(this, EventArgs.Empty);

        try
        {
            var exercisesPage = _services.GetRequiredService<ExercisesPage>();

            if (Parent is NavigationPage || Navigation.NavigationStack.Contains(this))
            {
                await Navigation.PushAsync(exercisesPage);
                return;
            }

            var managerNavigation = new NavigationPage(exercisesPage);
            var closeItem = new ToolbarItem { Text = "Fechar" };
            closeItem.Clicked += async (_, _) => await Navigation.PopModalAsync();
            exercisesPage.ToolbarItems.Add(closeItem);
            await Navigation.PushModalAsync(managerNavigation);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao abrir o gerenciamento de exercícios.");
            _viewModel.StatusMessage = "Não foi possível abrir o gerenciamento de exercícios.";
        }
        finally
        {
            _isOpeningManager = false;
        }
    }

    private async void OnCloseRequested(object? sender, EventArgs e)
    {
        await CloseAsync();
    }

    private async Task CloseAsync()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;

        try
        {
            if (Navigation.ModalStack.Contains(this) ||
                (Parent is NavigationPage && Navigation.ModalStack.Count > 0))
            {
                await Navigation.PopModalAsync();
            }
            else if (Navigation.NavigationStack.Contains(this) &&
                     Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync();
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao fechar o seletor de exercícios.");
            _viewModel.StatusMessage = "Não foi possível fechar o seletor.";
        }
        finally
        {
            _isClosing = false;
        }
    }
}
