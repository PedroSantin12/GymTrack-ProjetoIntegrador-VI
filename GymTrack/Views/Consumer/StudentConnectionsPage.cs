using GymTrack.Models;
using GymTrack.Services;
using GymTrack.Views.Components;
using Microsoft.Maui.Controls.Shapes;

namespace GymTrack.Views.Consumer;

internal sealed class ProfileSettingsPage : ContentPage
{
    private const int NotificationSwitchWidth = 56;
    private readonly Label _status = ConsumerUi.Status();

    public ProfileSettingsPage(AppUser user, AppSession session, IAccountService accounts,
        IAppNavigator navigator, Func<AboutPage> aboutPage)
    {
        Title = "Configurações";
        BackgroundColor = Ui.Background;

        var notifications = new Switch
        {
            IsToggled = user.NotificationsEnabled,
            OnColor = Ui.Accent,
            WidthRequest = NotificationSwitchWidth,
            MinimumWidthRequest = 0,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Center
        };
#if WINDOWS
        notifications.HandlerChanged += (_, _) =>
        {
            if (notifications.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.ToggleSwitch toggle)
            {
                toggle.MinWidth = 0;
                toggle.OnContent = null;
                toggle.OffContent = null;
            }
        };
#endif
        SemanticProperties.SetDescription(notifications, "Notificações no app");
        notifications.Toggled += async (_, args) =>
        {
            if (!notifications.IsEnabled) return;
            notifications.IsEnabled = false;
            try
            {
                var updated = await accounts.SetNotificationsEnabledAsync(user.Id, args.Value);
                session.Refresh(updated);
                _status.Text = string.Empty;
            }
            catch
            {
                notifications.IsToggled = !args.Value;
                _status.Text = "Não foi possível salvar a preferência de notificações.";
            }
            finally
            {
                notifications.IsEnabled = true;
            }
        };

        var darkMode = Ui.Text("Ativo", 12, Ui.Muted);
        darkMode.VerticalTextAlignment = TextAlignment.Center;
        var sections = new VerticalStackLayout
        {
            Spacing = 18,
            Children =
            {
                Section("Minha conta",
                    ActionRow("Editar dados pessoais", "Nome, e-mail e telefone", "personal",
                        () => OpenPersonalDetailsAsync(session, accounts)),
                    ActionRow("Alterar senha", "Segurança da conta", "password",
                        () => OpenChangePasswordAsync(user.Id, accounts))),
                Section("Preferências",
                    Row("Notificações", "Avisos no aplicativo", "notifications", notifications,
                        new GridLength(NotificationSwitchWidth)),
                    Row("Modo escuro", null, "theme", darkMode))
            }
        };
        sections.Add(Group(ActionRow("Sobre o GymTrack", null, "about", () => Navigation.PushAsync(aboutPage()))));

        var signOut = Ui.Button("Sair da conta", async () =>
        {
            if (await this.ConfirmAppActionAsync("Sair da conta?",
                    "Você poderá entrar novamente com seu e-mail e sua senha.", "Sair"))
            {
                await navigator.ShowLoginAsync();
            }
        }, true);
        signOut.BackgroundColor = Colors.Transparent;
        signOut.BorderColor = Ui.Border;
        signOut.BorderWidth = 1;
        signOut.CornerRadius = 22;
        sections.Add(signOut);

        var body = new VerticalStackLayout { Spacing = 10, Children = { sections, _status } };
        Content = Scroll(body);
    }

    private Task OpenPersonalDetailsAsync(AppSession session, IAccountService accounts)
    {
        if (session.CurrentUser is not { } user) return Task.CompletedTask;

        var name = Field("Nome", 100, user.Name);
        var email = Field("E-mail", 254, user.Email, Keyboard.Email);
        var phone = Field("Telefone (opcional)", 30, user.Phone, Keyboard.Telephone);
        var page = Form("Editar dados pessoais",
            [("Nome", name), ("E-mail", email), ("Telefone (opcional)", phone)],
            "Salvar dados", async () =>
            {
                var updated = await accounts.UpdatePersonalDetailsAsync(
                    user.Id, name.Text ?? string.Empty, email.Text ?? string.Empty, phone.Text);
                session.Refresh(updated);
                _status.Text = "Dados pessoais atualizados.";
            },
            user.Role == UserRole.Employee ? "Conta de funcionário" : "Conta de consumidor");
        return Navigation.PushAsync(page);
    }

    private Task OpenChangePasswordAsync(int accountId, IAccountService accounts)
    {
        var current = Field("Senha atual", 128, password: true);
        var password = Field("Nova senha", 128, password: true);
        var confirmation = Field("Confirme a nova senha", 128, password: true);
        var page = Form("Alterar senha",
            [("Senha atual", current), ("Nova senha", password), ("Confirme a nova senha", confirmation)],
            "Salvar senha", async () =>
            {
                if (!string.Equals(password.Text, confirmation.Text, StringComparison.Ordinal))
                    throw new ArgumentException("A confirmação deve ser igual à nova senha.");

                await accounts.ChangePasswordAsync(accountId, current.Text ?? string.Empty, password.Text ?? string.Empty);
                current.Text = password.Text = confirmation.Text = string.Empty;
                _status.Text = "Senha atualizada.";
            },
            "Use de 8 a 128 caracteres na nova senha.");
        return Navigation.PushAsync(page);
    }

    internal static Entry Field(string label, int maxLength, string? value = null,
        Keyboard? keyboard = null, bool password = false)
    {
        var entry = new Entry
        {
            Text = value,
            MaxLength = maxLength,
            Keyboard = keyboard ?? Keyboard.Default,
            IsPassword = password,
            IsTextPredictionEnabled = !password && keyboard is null,
            IsSpellCheckEnabled = !password && keyboard is null
        };
        SemanticProperties.SetDescription(entry, label);
        return entry;
    }

    internal static ContentPage Form(string title, (string Label, Entry Field)[] fields,
        string saveLabel, Func<Task> save, string? description = null)
    {
        var page = new ContentPage { Title = title, BackgroundColor = Ui.Background };
        var status = ConsumerUi.Status();
        var inputs = new VerticalStackLayout { Spacing = 12 };
        if (description is not null)
            inputs.Add(Ui.Text(description, 13, Ui.Muted));
        foreach (var (label, field) in fields)
        {
            var surface = Ui.Card(field, new Thickness(8, 0));
            surface.BackgroundColor = Ui.Elevated;
            var caption = Ui.Text(label, 13);
            AutomationProperties.SetIsInAccessibleTree(caption, false);
            inputs.Add(new VerticalStackLayout { Spacing = 4, Children = { caption, surface } });
        }
        var saveButton = Ui.Button(saveLabel, async () =>
        {
            status.Text = string.Empty;
            try
            {
                await save();
                if (page.Navigation.NavigationStack.LastOrDefault() == page)
                    await page.Navigation.PopAsync();
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or UnauthorizedAccessException)
            {
                status.Text = exception.Message;
            }
        });
        saveButton.CornerRadius = 22;
        page.Content = Scroll(new VerticalStackLayout
        {
            Spacing = 12,
            Children = { Ui.Card(inputs), status, saveButton }
        });
        return page;
    }

    private static ScrollView Scroll(View content) => new()
    {
        Content = content,
        MaximumWidthRequest = 460,
        Padding = new Thickness(18, 16, 18, 24)
    };

    private static View Section(string title, params View[] rows)
    {
        var heading = Ui.Text(title, 16, attributes: FontAttributes.Bold);
        SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level2);
        return new VerticalStackLayout { Spacing = 8, Children = { heading, Group(rows) } };
    }

    private static Border Group(params View[] rows)
    {
        var content = new VerticalStackLayout();
        foreach (var row in rows)
        {
            if (content.Children.Count > 0)
            {
                content.Add(new BoxView
                {
                    HeightRequest = 1,
                    Color = Ui.Border,
                    Margin = new Thickness(14, 0),
                    InputTransparent = true
                });
            }
            content.Add(row);
        }
        var card = Ui.Card(content, new Thickness(0));
        card.StrokeShape = new RoundRectangle { CornerRadius = 18 };
        return card;
    }

    private static View ActionRow(string title, string? description, string icon, Func<Task> action)
    {
        var visual = Row(title, description, icon, Icon("chevron", Ui.Muted, 15));
        visual.InputTransparent = true;
        AutomationProperties.SetExcludedWithChildren(visual, true);
        var button = Ui.Button(string.Empty, action);
        button.SetDynamicResource(VisualElement.StyleProperty, "QuietButton");
        SemanticProperties.SetDescription(button, description is null ? title : $"{title}, {description}");
        return new Grid { Children = { button, visual } };
    }

    private static Grid Row(string title, string? description, string icon, View trailing,
        GridLength? trailingWidth = null)
    {
        var text = new VerticalStackLayout
        {
            Spacing = 3,
            VerticalOptions = LayoutOptions.Center,
            Children = { Ui.Text(title, 14, attributes: FontAttributes.Bold) }
        };
        if (description is not null)
            text.Add(Ui.Text(description, 12, Ui.Muted));
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(24), new ColumnDefinition(GridLength.Star), new ColumnDefinition(trailingWidth ?? GridLength.Auto)
            },
            ColumnSpacing = 10,
            MinimumHeightRequest = description is null ? 52 : 66,
            Padding = new Thickness(14, 9)
        };
        row.Add(Icon(icon, Ui.Accent, 21), 0);
        row.Add(text, 1);
        row.Add(trailing, 2);
        return row;
    }

    private static View Icon(string name, Color color, int size)
    {
        var data = name switch
        {
            "personal" => "M 14,6 A 4,4 0 1 1 6,6 A 4,4 0 1 1 14,6 M 2,21 L 2,19 C 2,15 5,13 10,13 M 13,19 L 19,13 L 22,16 L 16,22 L 12,23 Z",
            "password" => "M 5,10 L 19,10 L 19,22 L 5,22 Z M 8,10 L 8,6 A 4,4 0 1 1 16,6 L 16,10 M 12,15 L 12,18",
            "notifications" => "M 4,17 L 6,14 L 6,8 A 6,6 0 1 1 18,8 L 18,14 L 20,17 Z M 9,20 C 10,23 14,23 15,20",
            "theme" => "M 21,14 C 19,21 10,24 5,18 C 0,12 4,4 11,3 C 7,10 14,17 21,14 Z",
            "about" => "M 22,12 A 10,10 0 1 1 2,12 A 10,10 0 1 1 22,12 M 12,11 L 12,17 M 12,7 L 12,8",
            _ => "M 9,5 L 15,12 L 9,19"
        };
        return new Microsoft.Maui.Controls.Shapes.Path
        {
            Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(data)!,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 1.7,
            StrokeLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            WidthRequest = size,
            HeightRequest = size,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Aspect = Stretch.Uniform,
            InputTransparent = true
        };
    }
}
