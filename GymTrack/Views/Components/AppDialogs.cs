using System.Globalization;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.Shapes;
using iOSPage = Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.Page;
using iOSModalStyle = Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.UIModalPresentationStyle;

namespace GymTrack.Views.Components;

internal static class AppDialogs
{
    private static readonly Dictionary<Window, Page> Owners = [];

    internal static bool IsShowingFor(Page page) =>
        page.Window is { } window && Owners.TryGetValue(window, out var owner) && ReferenceEquals(owner, page);

    internal static Task ShowAppMessageAsync(this Page page, string title, string message, string button = "Entendi") =>
        ShowAsync(page, navigation =>
        {
            var dialog = new DialogPage(navigation, title, message);
            dialog.AddActions(dialog.CreateButton(button, () => null, primary: true));
            return dialog;
        });

    internal static async Task<bool> ConfirmAppActionAsync(this Page page, string title, string message,
        string accept, string cancel = "Cancelar")
    {
        var result = await ShowAsync(page, navigation =>
        {
            var dialog = new DialogPage(navigation, title, message);
            dialog.AddActions(dialog.CreateButton(accept, () => accept, primary: true),
                dialog.CreateButton(cancel, () => null));
            return dialog;
        });
        return result == accept;
    }

    internal static Task<string?> ChooseAppActionAsync(this Page page, string title, string cancel,
        params string[] actions) =>
        ChooseAppOptionAsync(page, title, cancel, actions);

    internal static Task<string?> ChooseAppOptionAsync(this Page page, string title, string cancel,
        IReadOnlyList<string> options, string? selected = null) =>
        ChooseAppOptionAsync(page, title, cancel, options, selected, static option => option);

    internal static async Task<T?> ChooseAppOptionAsync<T>(this Page page, string title, string cancel,
        IReadOnlyList<T> options, T? selected, Func<T, string> label) where T : class
    {
        var items = options.ToArray();
        if (items.Length == 0) return null;

        var result = await ShowAsync(page, navigation =>
        {
            var dialog = new DialogPage(navigation, title, pinActions: selected is not null);
            var menu = new VerticalStackLayout { Spacing = 8 };
            for (var index = 0; index < items.Length; index++)
            {
                var key = index.ToString(CultureInfo.InvariantCulture);
                var text = label(items[index]);
                var isSelected = EqualityComparer<T>.Default.Equals(items[index], selected);
                var button = dialog.CreateButton(text, () => key, primary: isSelected);
                if (isSelected) SemanticProperties.SetDescription(button, $"{text}, selecionado");
                menu.Add(button);
            }
            dialog.AddContent(menu);
            dialog.AddActions(dialog.CreateButton(cancel, () => null));
            return dialog;
        });

        return int.TryParse(result, NumberStyles.None, CultureInfo.InvariantCulture, out var choice) &&
            choice < items.Length ? items[choice] : null;
    }

    internal static Task<string?> PromptAppValueAsync(this Page page, string title, string message,
        string accept = "Salvar", string cancel = "Cancelar", string? placeholder = null,
        int maxLength = -1, Keyboard? keyboard = null, string initialValue = "") =>
        ShowAsync(page, navigation =>
        {
            var dialog = new DialogPage(navigation, title, message);
            var input = new Entry
            {
                Text = initialValue,
                Placeholder = placeholder,
                Keyboard = keyboard ?? Keyboard.Text,
                MaxLength = maxLength < 0 ? int.MaxValue : maxLength,
                ReturnType = ReturnType.Done
            };
            SemanticProperties.SetDescription(input, message);
            var field = Ui.Card(input, new Thickness(12, 0));
            field.BackgroundColor = Ui.Elevated;
            field.StrokeShape = new RoundRectangle { CornerRadius = 14 };
            dialog.AddContent(field);
            var save = dialog.CreateButton(accept, () => input.Text ?? string.Empty, primary: true);
            input.Completed += (_, _) => _ = dialog.CloseAsync(input.Text ?? string.Empty);
            dialog.AddActions(save, dialog.CreateButton(cancel, () => null));
            return dialog;
        });

    private static Task<string?> ShowAsync(Page page, Func<INavigation, DialogPage> createDialog) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (page.Window is not { } window || !Owners.TryAdd(window, page)) return null;

            var navigation = page.Navigation;
            DialogPage? dialog = null;
            void OnPopped(object? sender, ModalPoppedEventArgs args)
            {
                if (dialog is not null && ReferenceEquals(args.Modal, dialog))
                    dialog.OnRemovedFromNavigation();
            }
            void OnDestroying(object? sender, EventArgs args) => dialog?.Cancel();

            try
            {
                dialog = createDialog(navigation);
                window.ModalPopped += OnPopped;
                window.Destroying += OnDestroying;
                await navigation.PushModalAsync(dialog, animated: false);
                return await dialog.Result;
            }
            finally
            {
                window.ModalPopped -= OnPopped;
                window.Destroying -= OnDestroying;
                Owners.Remove(window);
            }
        });

    private sealed class DialogPage : ContentPage
    {
        private const double MarginSize = 18;
        private const double MaximumCardWidth = 420;
        private readonly INavigation _navigation;
        private readonly VerticalStackLayout _content = new() { Spacing = 14 };
        private readonly VerticalStackLayout _actions;
        private readonly TaskCompletionSource<string?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _closing;

        internal DialogPage(INavigation navigation, string title, string? message = null, bool pinActions = false)
        {
            _navigation = navigation;
            Title = title;
            BackgroundColor = Color.FromArgb("#B0000000");
            NavigationPage.SetHasNavigationBar(this, false);
            iOSPage.SetModalPresentationStyle(this.On<iOS>(), iOSModalStyle.OverFullScreen);

            var heading = Ui.Text(title, 22, Ui.Foreground, FontAttributes.Bold);
            SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
            _content.Add(heading);
            if (!string.IsNullOrWhiteSpace(message))
                _content.Add(Ui.Text(message, 14, Ui.Muted));

            var scroll = new ScrollView { Content = _content, VerticalScrollBarVisibility = ScrollBarVisibility.Never };
            View body = scroll;
            _actions = _content;
            if (pinActions)
            {
                _content.Remove(heading);
                _actions = new VerticalStackLayout();
                scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Default;
                var choiceLayout = new Grid
                {
                    RowSpacing = 12,
                    RowDefinitions =
                    {
                        new RowDefinition(GridLength.Auto),
                        new RowDefinition(GridLength.Star),
                        new RowDefinition(GridLength.Auto)
                    }
                };
                choiceLayout.Add(heading);
                choiceLayout.Add(scroll, 0, 1);
                choiceLayout.Add(_actions, 0, 2);
                body = choiceLayout;
            }
            var card = Ui.Card(body, new Thickness(20));
            card.AutomationId = "AppDialogCard";
            card.StrokeShape = new RoundRectangle { CornerRadius = 22 };
            card.Margin = new Thickness(MarginSize);
            card.HorizontalOptions = LayoutOptions.Center;
            card.VerticalOptions = LayoutOptions.Center;
            card.MaximumWidthRequest = MaximumCardWidth;
            var backdrop = new BoxView { Color = Colors.Transparent };
            var outside = new TapGestureRecognizer();
            outside.Tapped += (_, _) => _ = CloseAsync(null);
            backdrop.GestureRecognizers.Add(outside);

            var layout = new Grid
            {
                SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.All),
                Children = { backdrop, card }
            };
            layout.SizeChanged += (_, _) =>
            {
                card.WidthRequest = Math.Min(MaximumCardWidth, Math.Max(0, layout.Width - 2 * MarginSize));
                card.MaximumHeightRequest = Math.Max(0, layout.Height - 2 * MarginSize);
            };
#if WINDOWS
            layout.HandlerChanged += (_, _) =>
            {
                if (layout.Handler?.PlatformView is not Microsoft.UI.Xaml.UIElement element) return;
                var escape = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
                {
                    Key = global::Windows.System.VirtualKey.Escape
                };
                escape.Invoked += (sender, args) =>
                {
                    args.Handled = true;
                    _ = CloseAsync(null);
                };
                element.KeyboardAccelerators.Add(escape);
            };
#endif
            Content = layout;
        }

        internal Task<string?> Result => _result.Task;

        internal void AddContent(View content) => _content.Add(content);

        internal Button CreateButton(string text, Func<string?> result, bool primary = false)
        {
            var button = new Button
            {
                Text = text,
                CornerRadius = 15,
                Padding = new Thickness(10, 8),
                LineBreakMode = LineBreakMode.WordWrap,
                BackgroundColor = primary ? Ui.Accent : Ui.Elevated,
                TextColor = primary ? Ui.Background : Ui.Foreground
            };
            button.Clicked += (_, _) => _ = CloseAsync(result());
            return button;
        }

        internal void AddActions(Button primary, Button? cancel = null)
        {
            if (cancel is null)
            {
                _actions.Add(primary);
                return;
            }

            var actions = new Grid
            {
                ColumnSpacing = 8,
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }
            };
            actions.Add(cancel);
            actions.Add(primary, 1);
            _actions.Add(actions);
        }

        internal async Task CloseAsync(string? result)
        {
            if (_closing || !ReferenceEquals(_navigation.ModalStack.LastOrDefault(), this)) return;
            _closing = true;
            try
            {
                await _navigation.PopModalAsync(animated: false);
                _result.TrySetResult(result);
            }
            catch (Exception exception)
            {
                _closing = false;
                System.Diagnostics.Debug.WriteLine(exception);
                if (!_navigation.ModalStack.Contains(this))
                    Cancel();
            }
        }

        internal void Cancel() => _result.TrySetResult(null);

        internal void OnRemovedFromNavigation()
        {
            if (!_closing)
                Cancel();
        }

        protected override bool OnBackButtonPressed()
        {
            _ = CloseAsync(null);
            return true;
        }
    }
}
