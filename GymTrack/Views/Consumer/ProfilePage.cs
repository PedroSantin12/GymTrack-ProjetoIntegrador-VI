using System.Globalization;
using GymTrack.Models;
using GymTrack.Services;
using GymTrack.Views.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace GymTrack.Views.Consumer;

public sealed class ProfilePage : WorkspacePage
{
    private readonly IAppNavigator _navigator;
    private readonly AppSession _session;
    private readonly IAccountService _accounts;
    private readonly IBodyMeasurementService _measurements;
    private readonly IServiceProvider _services;
    private readonly VerticalStackLayout _content = new() { Spacing = 20 };
    private readonly ScrollView _scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Never };
    private readonly Label _status = ConsumerUi.Status();
    private IReadOnlyList<BodyMeasurement> _history = [];
    private string _measurementMessage = string.Empty;
    private int _loadedUserId;
    private VerticalStackLayout? _formAnchor;
    private Label? _measurementStatus;
    private DatePicker? _measurementDate;
    private Entry? _weightEntry;
    private Entry? _heightEntry;
    private Entry? _waistEntry;
    private Entry? _hipEntry;
    private Entry? _chestEntry;
    private bool _busy;

    public ProfilePage(IAppNavigator navigator, AppSession session,
        IAccountService accounts, IBodyMeasurementService measurements, IServiceProvider services)
        : base("Meu perfil", "Sua conta e preferências.", AppSection.Profile, navigator, session)
    {
        _navigator = navigator;
        _session = session;
        _accounts = accounts;
        _measurements = measurements;
        _services = services;
        _scroll.Content = new VerticalStackLayout { Spacing = 12, Children = { _status, _content } };
        SetBody(_scroll, false);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_session.CurrentUser is not { } user) return;
        if (_loadedUserId == user.Id)
        {
            if (_measurementDate is not null)
                _measurementDate.MaximumDate = DateTime.Today;
            return;
        }

        _loadedUserId = user.Id;
        _history = [];
        _measurementMessage = string.Empty;
        _status.Text = string.Empty;
        Render();
        if (_session.IsEmployee) return;
        try { await LoadMeasurementsAsync(user.Id); }
        catch { SetMeasurementStatus("Não foi possível carregar suas medidas. Tente salvar novamente."); }
    }

    private void Render()
    {
        _content.Clear();
        if (_session.CurrentUser is not { } user) return;
        var role = _session.IsEmployee ? "Funcionário" : "Consumidor";

        var identity = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 12
        };
        var initial = string.IsNullOrWhiteSpace(user.Name) ? "?" : user.Name.Trim()[0].ToString().ToUpperInvariant();
        identity.Add(new Border
        {
            WidthRequest = 52, HeightRequest = 52, Padding = 0,
            BackgroundColor = Ui.Elevated,
            Stroke = new SolidColorBrush(Ui.Border), StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 26 },
            Content = new Label
            {
                Text = initial, FontSize = 21, FontAttributes = FontAttributes.Bold,
                TextColor = Ui.Accent,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        });
        var name = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        name.Add(Ui.Text(user.Name, 18, Ui.Foreground, FontAttributes.Bold));
        name.Add(Ui.Text(user.Email, 12, Ui.Muted));
        identity.Add(name, 1);
        _content.Add(identity);

        _content.Add(Ui.Text("Informações da conta", 17, Ui.Foreground, FontAttributes.Bold));
        var account = new VerticalStackLayout { Spacing = 0 };
        account.Add(DetailRow("Nome", user.Name));
        account.Add(DetailRow("E-mail", user.Email));
        account.Add(DetailRow("Tipo de conta", role));
        _content.Add(account);

        if (!_session.IsEmployee)
            _content.Add(BodyMeasurements());

        _content.Add(Ui.Text("Configurações", 17, Ui.Foreground, FontAttributes.Bold));
        var settings = new VerticalStackLayout { Spacing = 0 };
        settings.Add(ActionRow("Editar perfil", EditProfileAsync));
        settings.Add(ActionRow("Sobre o GymTrack", async () =>
            await Navigation.PushAsync(_services.GetRequiredService<AboutPage>())));
        settings.Add(ActionRow("Sair da conta", SignOutAsync, true));
        _content.Add(settings);
    }

    private async Task LoadMeasurementsAsync(int userId)
    {
        var history = await _measurements.GetForUserAsync(userId);
        if (_session.IsEmployee || _session.CurrentUser?.Id != userId) return;
        _history = history.OrderByDescending(item => item.MeasuredOn).ThenByDescending(item => item.Id).ToArray();
        Render();
    }

    private View BodyMeasurements()
    {
        var section = new VerticalStackLayout { Spacing = 14 };
        section.Add(ConsumerUi.Section("Dados corporais", "Acompanhe suas medidas ao longo do tempo."));

        if (_history.Count == 0)
        {
            section.Add(ConsumerUi.Empty("Seu primeiro registro aparece aqui.",
                "Informe seu peso para começar a acompanhar sua evolução."));
        }
        else
        {
            var latest = _history[0];
            var height = _history.FirstOrDefault(item => item.HeightCm.HasValue)?.HeightCm;
            var summary = new Grid
            {
                ColumnSpacing = 9,
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }
            };
            summary.Add(ConsumerUi.Metric(Measure(latest.WeightKg, "kg"), "Último peso"), 0);
            summary.Add(ConsumerUi.Metric(height is { } cm ? Measure(cm, "cm") : "—", "Altura"), 1);
            section.Add(summary);

            if (_history.Count > 1)
            {
                var previous = _history[1];
                var difference = latest.WeightKg - previous.WeightKg;
                var sign = difference > 0 ? "+" : difference < 0 ? "−" : string.Empty;
                section.Add(Ui.Text($"Variação: {sign}{Measure(Math.Abs(difference), "kg")} desde {DateText(previous.MeasuredOn)}",
                    12, Ui.Muted));
            }

            var points = _history.Reverse().TakeLast(12).ToArray();
            section.Add(ConsumerUi.Section("Evolução do peso", "Últimos registros · kg"));
            var chart = new GraphicsView
            {
                HeightRequest = 170,
                Drawable = new WeightChartDrawable(points)
            };
            SemanticProperties.SetDescription(chart,
                "Evolução do peso: " + string.Join(", ", points.Select(item =>
                    $"{DateText(item.MeasuredOn)}: {Measure(item.WeightKg, "kg")}")));
            section.Add(Ui.Card(chart, new Thickness(8, 12)));
        }

        section.Add(MeasurementForm());
        section.Add(ConsumerUi.Section("Histórico de medidas", "Toque em editar para corrigir um registro do mesmo dia."));
        if (_history.Count == 0)
            section.Add(Ui.Text("Nenhuma medida registrada ainda.", 13, Ui.Muted));
        else
            foreach (var item in _history)
                section.Add(HistoryCard(item));

        return section;
    }

    private View MeasurementForm()
    {
        var form = new VerticalStackLayout { Spacing = 10 };
        _formAnchor = form;
        form.Add(ConsumerUi.Section("Registrar medidas", "Um registro por dia. Salvar a mesma data atualiza suas medidas."));

        _measurementDate = new DatePicker
        {
            Date = DateTime.Today,
            MaximumDate = DateTime.Today,
            MinimumDate = new DateTime(1900, 1, 1),
            Format = "dd/MM/yyyy",
            TextColor = Ui.Foreground,
            BackgroundColor = Colors.Transparent,
            FontSize = 14
        };
        _measurementDate.DateSelected += (_, _) =>
        {
            var day = SelectedDate();
            FillForm(_history.FirstOrDefault(item => item.MeasuredOn.Date == day));
        };

        _weightEntry = NumberEntry("Ex.: 72,5");
        _heightEntry = NumberEntry("Ex.: 175");
        _waistEntry = NumberEntry("Opcional");
        _hipEntry = NumberEntry("Opcional");
        _chestEntry = NumberEntry("Opcional");

        var fields = new VerticalStackLayout { Spacing = 12 };
        fields.Add(LabeledField("Data", _measurementDate));
        var measures = new Grid
        {
            RowSpacing = 12,
            ColumnSpacing = 10,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }
        };
        measures.Add(LabeledField("Peso (kg) *", _weightEntry), 0, 0);
        measures.Add(LabeledField("Altura (cm)", _heightEntry), 1, 0);
        measures.Add(LabeledField("Cintura (cm)", _waistEntry), 0, 1);
        measures.Add(LabeledField("Quadril (cm)", _hipEntry), 1, 1);
        var chest = LabeledField("Peito (cm)", _chestEntry);
        measures.Add(chest, 0, 2);
        Grid.SetColumnSpan(chest, 2);
        fields.Add(measures);
        fields.Add(Ui.Text("* Peso obrigatório. As outras medidas são opcionais.", 11, Ui.Muted));
        form.Add(Ui.Card(fields));
        form.Add(Ui.Button("Salvar medidas", SaveMeasurementAsync));
        _measurementStatus = ConsumerUi.Status();
        _measurementStatus.Text = _measurementMessage;
        _measurementStatus.IsVisible = !string.IsNullOrEmpty(_measurementMessage);
        form.Add(_measurementStatus);
        FillForm(_history.FirstOrDefault(item => item.MeasuredOn.Date == DateTime.Today));
        return form;
    }

    private static Entry NumberEntry(string placeholder) => new()
    {
        Placeholder = placeholder,
        PlaceholderColor = Ui.Muted,
        TextColor = Ui.Foreground,
        BackgroundColor = Colors.Transparent,
        Keyboard = Keyboard.Numeric,
        FontSize = 14,
        HeightRequest = 44
    };

    private static View LabeledField(string label, View input)
    {
        var field = new VerticalStackLayout { Spacing = 5 };
        field.Add(Ui.Text(label, 12, Ui.Muted));
        field.Add(new Border
        {
            Padding = new Thickness(9, 1),
            BackgroundColor = Ui.Elevated,
            Stroke = new SolidColorBrush(Ui.Border),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Content = input
        });
        return field;
    }

    private void FillForm(BodyMeasurement? item)
    {
        if (_weightEntry is null || _heightEntry is null || _waistEntry is null ||
            _hipEntry is null || _chestEntry is null) return;

        _weightEntry.Text = item is null ? string.Empty : NumberText(item.WeightKg);
        _heightEntry.Text = item?.HeightCm is { } height
            ? NumberText(height)
            : item is null && _history.FirstOrDefault(entry => entry.HeightCm.HasValue)?.HeightCm is { } recentHeight
                ? NumberText(recentHeight) : string.Empty;
        _waistEntry.Text = item?.WaistCm is { } waist ? NumberText(waist) : string.Empty;
        _hipEntry.Text = item?.HipCm is { } hip ? NumberText(hip) : string.Empty;
        _chestEntry.Text = item?.ChestCm is { } chest ? NumberText(chest) : string.Empty;
    }

    private View HistoryCard(BodyMeasurement item)
    {
        var content = new VerticalStackLayout { Spacing = 9 };
        var heading = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        heading.Add(Ui.Text(DateText(item.MeasuredOn), 15, Ui.Foreground, FontAttributes.Bold));
        heading.Add(Ui.Text(Measure(item.WeightKg, "kg"), 15, Ui.Accent, FontAttributes.Bold), 1);
        content.Add(heading);

        var details = new List<string>();
        if (item.HeightCm is { } height) details.Add("Altura " + Measure(height, "cm"));
        if (item.WaistCm is { } waist) details.Add("Cintura " + Measure(waist, "cm"));
        if (item.HipCm is { } hip) details.Add("Quadril " + Measure(hip, "cm"));
        if (item.ChestCm is { } chest) details.Add("Peito " + Measure(chest, "cm"));
        if (details.Count > 0) content.Add(Ui.Text(string.Join(" · ", details), 12, Ui.Muted));

        var actions = new Grid
        {
            ColumnSpacing = 9,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }
        };
        actions.Add(Ui.Button("Editar", () => EditMeasurementAsync(item), true), 0);
        var delete = Ui.Button("Excluir", () => DeleteMeasurementAsync(item), true);
        delete.TextColor = Color.FromArgb("#FF7474");
        actions.Add(delete, 1);
        content.Add(actions);
        return Ui.Card(content);
    }

    private async Task EditMeasurementAsync(BodyMeasurement item)
    {
        if (_session.IsEmployee || _measurementDate is null || _formAnchor is null) return;
        _measurementDate.Date = item.MeasuredOn.Date;
        FillForm(item);
        SetMeasurementStatus($"Editando as medidas de {DateText(item.MeasuredOn)}. Toque em salvar para atualizar.");
        await _scroll.ScrollToAsync(_formAnchor, ScrollToPosition.Start, true);
    }

    private async Task SaveMeasurementAsync()
    {
        if (_busy || _session.IsEmployee || _session.CurrentUser is not { } user) return;
        try
        {
            var weight = ReadMeasure(_weightEntry, "peso", true)!.Value;
            var height = ReadMeasure(_heightEntry, "altura");
            var waist = ReadMeasure(_waistEntry, "cintura");
            var hip = ReadMeasure(_hipEntry, "quadril");
            var chest = ReadMeasure(_chestEntry, "peito");
            _busy = true;
            await _measurements.SaveAsync(user.Id, SelectedDate(), weight, height, waist, hip, chest);
            await LoadMeasurementsAsync(user.Id);
            SetMeasurementStatus("Medidas salvas.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            SetMeasurementStatus(exception.Message);
        }
        catch { SetMeasurementStatus("Não foi possível salvar suas medidas. Tente novamente."); }
        finally { _busy = false; }
    }

    private async Task DeleteMeasurementAsync(BodyMeasurement item)
    {
        if (_busy || _session.IsEmployee || _session.CurrentUser is not { } user) return;
        if (!await DisplayAlertAsync("Excluir medidas?",
                $"O registro de {DateText(item.MeasuredOn)} será excluído do seu histórico.",
                "Excluir", "Cancelar")) return;

        _busy = true;
        try
        {
            await _measurements.DeleteAsync(user.Id, item.Id);
            await LoadMeasurementsAsync(user.Id);
            SetMeasurementStatus("Registro excluído.");
        }
        catch (InvalidOperationException exception) { SetMeasurementStatus(exception.Message); }
        catch { SetMeasurementStatus("Não foi possível excluir o registro. Tente novamente."); }
        finally { _busy = false; }
    }

    private void SetMeasurementStatus(string message)
    {
        _measurementMessage = message;
        _status.Text = message;
        if (_measurementStatus is null) return;
        _measurementStatus.Text = message;
        _measurementStatus.IsVisible = !string.IsNullOrEmpty(message);
    }

    private DateTime SelectedDate() => _measurementDate?.Date is DateTime date ? date.Date : DateTime.Today;

    private MeasurementDraft? CaptureDraft() => _measurementDate is null ? null : new MeasurementDraft(
        SelectedDate(), _weightEntry?.Text, _heightEntry?.Text, _waistEntry?.Text,
        _hipEntry?.Text, _chestEntry?.Text);

    private void RestoreDraft(MeasurementDraft? draft)
    {
        if (draft is null || _measurementDate is null) return;
        _measurementDate.Date = draft.Date;
        if (_weightEntry is not null) _weightEntry.Text = draft.Weight;
        if (_heightEntry is not null) _heightEntry.Text = draft.Height;
        if (_waistEntry is not null) _waistEntry.Text = draft.Waist;
        if (_hipEntry is not null) _hipEntry.Text = draft.Hip;
        if (_chestEntry is not null) _chestEntry.Text = draft.Chest;
    }

    private sealed record MeasurementDraft(DateTime Date, string? Weight, string? Height,
        string? Waist, string? Hip, string? Chest);

    private static double? ReadMeasure(Entry? entry, string label, bool required = false)
    {
        var text = entry?.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            if (required) throw new ArgumentException($"Informe o {label}.");
            return null;
        }

        if ((!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) &&
             !double.TryParse(text, NumberStyles.Float, NumberCulture, out value) &&
             !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) ||
            !double.IsFinite(value) || value <= 0)
            throw new ArgumentException($"Informe um valor de {label} maior que zero.");
        return value;
    }

    private static readonly CultureInfo NumberCulture = CultureInfo.GetCultureInfo("pt-BR");

    private static string NumberText(double value) => value.ToString("0.##", NumberCulture);

    private static string Measure(double value, string unit) => $"{NumberText(value)} {unit}";

    private static string DateText(DateTime date) => date.ToString("dd/MM/yyyy", NumberCulture);

    private static View DetailRow(string label, string value)
    {
        var row = new Grid
        {
            Padding = new Thickness(0, 10),
            ColumnDefinitions = { new ColumnDefinition(new GridLength(104)), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 8
        };
        row.Add(Ui.Text(label, 12, Ui.Muted));
        var detail = Ui.Text(value, 13, Ui.Foreground);
        detail.LineBreakMode = LineBreakMode.TailTruncation;
        row.Add(detail, 1);
        return new VerticalStackLayout { Children = { row, Divider() } };
    }

    private static View ActionRow(string label, Func<Task> action, bool danger = false)
    {
        var row = new Grid { HeightRequest = 48,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        row.Add(Ui.Text(label, 14, danger ? Color.FromArgb("#FF7474") : Ui.Foreground));
        row.Add(Ui.Text("›", 20, Ui.Muted), 1);
        var button = new Button
        {
            Text = string.Empty, BackgroundColor = Colors.Transparent,
            BorderWidth = 0, Padding = 0, CornerRadius = 0,
            AutomationId = label.Replace(" ", string.Empty)
        };
        SemanticProperties.SetDescription(button, label);
        button.Clicked += async (_, _) =>
        {
            if (!button.IsEnabled) return;
            button.IsEnabled = false;
            try { await action(); }
            finally { button.IsEnabled = true; }
        };
        Grid.SetColumnSpan(button, 2);
        row.Add(button);
        return new VerticalStackLayout { Children = { row, Divider() } };
    }

    private static View Divider() => new BoxView { HeightRequest = 1, BackgroundColor = Ui.Border };

    private async Task EditProfileAsync()
    {
        if (_busy || _session.CurrentUser is not { } user) return;
        var choice = await DisplayActionSheetAsync("Editar perfil", "Cancelar", null, "Alterar nome", "Alterar e-mail");
        if (choice is not ("Alterar nome" or "Alterar e-mail")) return;
        var changeName = choice == "Alterar nome";
        var value = await DisplayPromptAsync(choice, changeName ? "Nome" : "E-mail",
            "Salvar", "Cancelar", initialValue: changeName ? user.Name : user.Email,
            maxLength: changeName ? 100 : 254,
            keyboard: changeName ? Keyboard.Text : Keyboard.Email);
        if (value is null) return;
        value = value.Trim();
        if (changeName && value.Length < 2)
        {
            _status.Text = "Informe um nome com pelo menos 2 caracteres.";
            return;
        }
        _busy = true;
        try
        {
            var updated = new AppUser
            {
                Id = user.Id, Name = changeName ? value : user.Name,
                Email = changeName ? user.Email : value, Role = user.Role,
                IsActive = user.IsActive, CreatedAt = user.CreatedAt
            };
            await _accounts.UpdateUserAsync(updated);
            _session.SignIn(updated);
            _status.Text = "Perfil atualizado.";
            var draft = CaptureDraft();
            Render();
            RestoreDraft(draft);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _status.Text = exception.Message;
        }
        finally { _busy = false; }
    }

    private async Task SignOutAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            if (!await DisplayAlertAsync("Sair da conta?", "Você poderá entrar novamente com seu e-mail e sua senha.",
                    "Sair", "Continuar aqui")) return;
            _session.SignOut();
            await _navigator.ShowLoginAsync();
        }
        catch { _status.Text = "Não foi possível voltar ao login. Tente novamente."; }
        finally { _busy = false; }
    }

    private sealed class WeightChartDrawable(IReadOnlyList<BodyMeasurement> records) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF bounds)
        {
            if (records.Count == 0) return;

            var left = 46f;
            var right = Math.Max(left + 20, bounds.Width - 12);
            var top = 12f;
            var bottom = Math.Max(top + 20, bounds.Height - 28);
            var lowest = records.Min(item => item.WeightKg);
            var highest = records.Max(item => item.WeightKg);
            var padding = Math.Max(0.5, (highest - lowest) * 0.15);
            var minimum = Math.Max(0, lowest - padding);
            var maximum = highest + padding;

            canvas.FontSize = 10;
            canvas.FontColor = Ui.Muted;
            canvas.StrokeColor = Ui.Border;
            canvas.StrokeSize = 1;
            for (var tick = 0; tick < 3; tick++)
            {
                var y = top + (bottom - top) * tick / 2;
                var value = maximum - (maximum - minimum) * tick / 2;
                canvas.DrawLine(left, y, right, y);
                canvas.DrawString(NumberText(value), 0, y - 9, left - 6, 18,
                    HorizontalAlignment.Right, VerticalAlignment.Center);
            }

            var positions = records.Select((item, index) =>
            {
                var x = records.Count == 1 ? (left + right) / 2 :
                    left + (right - left) * index / (records.Count - 1);
                var y = bottom - (float)((item.WeightKg - minimum) / (maximum - minimum)) * (bottom - top);
                return (X: x, Y: y);
            }).ToArray();

            canvas.StrokeColor = Ui.Accent;
            canvas.StrokeSize = 2.5f;
            for (var index = 1; index < positions.Length; index++)
                canvas.DrawLine(positions[index - 1].X, positions[index - 1].Y,
                    positions[index].X, positions[index].Y);

            canvas.FillColor = Ui.Accent;
            foreach (var (x, y) in positions)
                canvas.FillCircle(x, y, 3.5f);

            canvas.FontColor = Ui.Muted;
            canvas.FontSize = 10;
            if (records.Count == 1)
            {
                canvas.DrawString(records[0].MeasuredOn.ToString("dd/MM", NumberCulture), left, bottom + 5,
                    right - left, 18, HorizontalAlignment.Center, VerticalAlignment.Center);
            }
            else
            {
                canvas.DrawString(records[0].MeasuredOn.ToString("dd/MM", NumberCulture), left, bottom + 5,
                    48, 18, HorizontalAlignment.Left, VerticalAlignment.Center);
                canvas.DrawString(records[^1].MeasuredOn.ToString("dd/MM", NumberCulture), right - 48, bottom + 5,
                    48, 18, HorizontalAlignment.Right, VerticalAlignment.Center);
                if (records.Count >= 5)
                    canvas.DrawString(records[records.Count / 2].MeasuredOn.ToString("dd/MM", NumberCulture),
                        (left + right) / 2 - 24, bottom + 5, 48, 18,
                        HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }
    }
}
