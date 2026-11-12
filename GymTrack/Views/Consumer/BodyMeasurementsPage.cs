using System.Globalization;
using GymTrack.Models;
using GymTrack.Services;
using GymTrack.Views.Components;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace GymTrack.Views.Consumer;

public sealed class BodyMeasurementsPage : ContentPage
{
    private static readonly CultureInfo NumberCulture = CultureInfo.GetCultureInfo("pt-BR");
    private readonly AppSession _session;
    private readonly IBodyMeasurementService _measurements;
    private readonly int? _userId;
    private readonly VerticalStackLayout _summary = new() { Spacing = 12 };
    private readonly VerticalStackLayout _historyList = new() { Spacing = 10 };
    private readonly Label _formStatus = ConsumerUi.Status();
    private readonly Label _historyStatus = ConsumerUi.Status();
    private readonly ScrollView _scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Never };
    private readonly VerticalStackLayout _form;
    private readonly Button _register;
    private readonly DatePicker _date = new()
    {
        Date = DateTime.Today,
        MaximumDate = DateTime.Today,
        MinimumDate = new DateTime(1900, 1, 1),
        Format = "dd/MM/yyyy",
        TextColor = Ui.Foreground,
        BackgroundColor = Colors.Transparent,
        FontSize = 14,
        AutomationId = "MeasurementDate"
    };
    private readonly Entry _weight = NumberEntry("Ex.: 72,5", "MeasurementWeight");
    private readonly Entry _height = NumberEntry("Ex.: 175", "MeasurementHeight");
    private readonly Entry _waist = NumberEntry("Opcional", "MeasurementWaist");
    private readonly Entry _hip = NumberEntry("Opcional", "MeasurementHip");
    private readonly Entry _chest = NumberEntry("Opcional", "MeasurementChest");
    private IReadOnlyList<BodyMeasurement> _history = [];
    private bool _loaded;
    private bool _busy;

    public BodyMeasurementsPage(AppSession session, IBodyMeasurementService measurements)
    {
        _session = session;
        _measurements = measurements;
        _userId = session.CurrentUser?.Id;
        Title = "Medidas";
        BackgroundColor = Ui.Background;

        _formStatus.IsVisible = false;
        _historyStatus.IsVisible = false;
        _date.DateSelected += (_, _) =>
        {
            FillFormForDate();
            SetStatus(_formStatus, string.Empty);
        };
        _form = CreateForm();
        _form.IsVisible = false;
        _register = PillButton("Registrar medidas", ShowRegistrationAsync, "RegisterMeasurements");
        _scroll.Content = new VerticalStackLayout
        {
            Padding = new Thickness(18, 14, 18, 24),
            Spacing = 18,
            MaximumWidthRequest = 540,
            Children =
            {
                _register,
                _form,
                _summary,
                new VerticalStackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        ConsumerUi.Section("Histórico de medidas", "Toque em editar para corrigir um registro."),
                        _historyStatus,
                        _historyList
                    }
                }
            }
        };
        Content = _scroll;
    }

    private int? ActiveUserId => _session.IsPersonalMode && _session.CurrentUser?.Id == _userId
        ? _userId
        : null;

    private DateTime SelectedDate => _date.Date is DateTime date ? date.Date : DateTime.Today;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _date.MaximumDate = DateTime.Today;
        if (_loaded || _busy || ActiveUserId is not { } userId) return;

        _busy = true;
        _form.IsEnabled = false;
        try
        {
            await LoadMeasurementsAsync(userId);
            FillFormForDate();
            _loaded = true;
            SetStatus(_historyStatus, string.Empty);
        }
        catch
        {
            SetStatus(_historyStatus, "Não foi possível carregar suas medidas.");
        }
        finally
        {
            _form.IsEnabled = true;
            _busy = false;
        }
    }

    private async Task LoadMeasurementsAsync(int userId)
    {
        var history = await _measurements.GetForUserAsync(userId);
        if (ActiveUserId != userId) return;
        _history = history.OrderByDescending(item => item.MeasuredOn).ThenByDescending(item => item.Id).ToArray();
        RenderHistory();
    }

    private void RenderHistory()
    {
        _summary.Clear();
        _historyList.Clear();
        if (_history.Count == 0)
        {
            _summary.Add(ConsumerUi.Empty("Ainda sem dados", "Registre seu peso para acompanhar a evolução."));
            _historyList.Add(Ui.Text("Nenhuma medida registrada ainda.", 13, Ui.Muted));
            return;
        }

        var latest = _history[0];
        var height = _history.FirstOrDefault(item => item.HeightCm.HasValue)?.HeightCm;
        var metrics = new Grid
        {
            ColumnSpacing = 9,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }
        };
        metrics.Add(ConsumerUi.Metric(Measure(latest.WeightKg, "kg"), "Último peso"), 0);
        metrics.Add(ConsumerUi.Metric(height is { } cm ? Measure(cm, "cm") : "—", "Altura"), 1);
        _summary.Add(metrics);

        if (_history.Count > 1)
        {
            var previous = _history[1];
            var difference = latest.WeightKg - previous.WeightKg;
            var sign = difference > 0 ? "+" : difference < 0 ? "−" : string.Empty;
            _summary.Add(Ui.Text($"Variação: {sign}{Measure(Math.Abs(difference), "kg")} desde {DateText(previous.MeasuredOn)}",
                12, Ui.Muted));
        }

        var points = _history.Reverse().TakeLast(12).ToArray();
        _summary.Add(ConsumerUi.Section("Evolução do peso", "Últimos registros · kg"));
        var chart = new GraphicsView
        {
            HeightRequest = 170,
            Drawable = new WeightChartDrawable(points)
        };
        SemanticProperties.SetDescription(chart,
            "Evolução do peso: " + string.Join(", ", points.Select(item =>
                $"{DateText(item.MeasuredOn)}: {Measure(item.WeightKg, "kg")}")));
        _summary.Add(Ui.Card(chart, new Thickness(8, 12)));

        foreach (var item in _history)
            _historyList.Add(HistoryCard(item));
    }

    private VerticalStackLayout CreateForm()
    {
        var fields = new VerticalStackLayout { Spacing = 10 };
        fields.Add(LabeledField("Data", _date));
        var measures = new Grid
        {
            RowSpacing = 10,
            ColumnSpacing = 10,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }
        };
        measures.Add(LabeledField("Peso (kg) *", _weight), 0, 0);
        measures.Add(LabeledField("Altura (cm)", _height), 1, 0);
        measures.Add(LabeledField("Cintura (cm)", _waist), 0, 1);
        measures.Add(LabeledField("Quadril (cm)", _hip), 1, 1);
        var chest = LabeledField("Peito (cm)", _chest);
        measures.Add(chest, 0, 2);
        Grid.SetColumnSpan(chest, 2);
        fields.Add(measures);
        fields.Add(Ui.Text("* Peso obrigatório. As outras medidas são opcionais.", 11, Ui.Muted));

        return new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                ConsumerUi.Section("Registrar medidas", "Salvar na mesma data atualiza o registro daquele dia."),
                Ui.Card(fields),
                _formStatus,
                PillButton("Salvar medidas", SaveMeasurementAsync, "SaveMeasurements"),
                PillButton("Fechar formulário", CloseFormAsync, "CloseMeasurementForm", true)
            }
        };
    }

    private static Entry NumberEntry(string placeholder, string automationId) => new()
    {
        Placeholder = placeholder,
        PlaceholderColor = Ui.Muted,
        TextColor = Ui.Foreground,
        BackgroundColor = Colors.Transparent,
        Keyboard = Keyboard.Numeric,
        FontSize = 14,
        HeightRequest = 44,
        AutomationId = automationId
    };

    private static View LabeledField(string label, View input)
    {
        SemanticProperties.SetDescription(input, label);
        return new VerticalStackLayout
        {
            Spacing = 5,
            Children =
            {
                Ui.Text(label, 12, Ui.Muted),
                new Border
                {
                    Padding = new Thickness(11, 1),
                    BackgroundColor = Ui.Elevated,
                    Stroke = new SolidColorBrush(Ui.Border),
                    StrokeThickness = 1,
                    StrokeShape = new RoundRectangle { CornerRadius = 16 },
                    Content = input
                }
            }
        };
    }

    private void FillFormForDate()
    {
        var item = _history.FirstOrDefault(entry => entry.MeasuredOn.Date == SelectedDate);
        _weight.Text = item is null ? string.Empty : NumberText(item.WeightKg);
        var height = item is null ? _history.FirstOrDefault(entry => entry.HeightCm.HasValue)?.HeightCm : item.HeightCm;
        _height.Text = height is { } cm ? NumberText(cm) : string.Empty;
        _waist.Text = item?.WaistCm is { } waist ? NumberText(waist) : string.Empty;
        _hip.Text = item?.HipCm is { } hip ? NumberText(hip) : string.Empty;
        _chest.Text = item?.ChestCm is { } chest ? NumberText(chest) : string.Empty;
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
        var edit = PillButton("Editar", () => EditMeasurementAsync(item), $"EditMeasurement{item.Id}", true);
        SemanticProperties.SetDescription(edit, $"Editar medidas de {DateText(item.MeasuredOn)}");
        var delete = PillButton("Excluir", () => DeleteMeasurementAsync(item), $"DeleteMeasurement{item.Id}", true);
        delete.TextColor = Color.FromArgb("#FF7474");
        SemanticProperties.SetDescription(delete, $"Excluir medidas de {DateText(item.MeasuredOn)}");
        actions.Add(edit, 0);
        actions.Add(delete, 1);
        content.Add(actions);
        return Ui.Card(content);
    }

    private Task ShowRegistrationAsync()
    {
        _form.IsVisible = true;
        _register.IsVisible = false;
        return _scroll.ScrollToAsync(0, 0, true);
    }

    private Task CloseFormAsync()
    {
        if (_busy) return Task.CompletedTask;
        _weight.Unfocus();
        _height.Unfocus();
        _waist.Unfocus();
        _hip.Unfocus();
        _chest.Unfocus();
        _form.IsVisible = false;
        _register.IsVisible = true;
        return _scroll.ScrollToAsync(0, 0, true);
    }

    private async Task EditMeasurementAsync(BodyMeasurement item)
    {
        if (_busy || ActiveUserId is null) return;
        _date.Date = item.MeasuredOn.Date;
        FillFormForDate();
        SetStatus(_formStatus, $"Editando as medidas de {DateText(item.MeasuredOn)}.");
        await ShowRegistrationAsync();
    }

    private async Task SaveMeasurementAsync()
    {
        if (_busy || ActiveUserId is not { } userId) return;
        _busy = true;
        _form.IsEnabled = false;
        try
        {
            var weight = ReadMeasure(_weight, "peso", true)!.Value;
            var height = ReadMeasure(_height, "altura");
            var waist = ReadMeasure(_waist, "cintura");
            var hip = ReadMeasure(_hip, "quadril");
            var chest = ReadMeasure(_chest, "peito");
            await _measurements.SaveAsync(userId, SelectedDate, weight, height, waist, hip, chest);
            await LoadMeasurementsAsync(userId);
            SetStatus(_formStatus, "Medidas salvas.");
            SetStatus(_historyStatus, string.Empty);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            SetStatus(_formStatus, exception.Message);
        }
        catch
        {
            SetStatus(_formStatus, "Não foi possível salvar suas medidas. Tente novamente.");
        }
        finally
        {
            _form.IsEnabled = true;
            _busy = false;
        }
    }

    private async Task DeleteMeasurementAsync(BodyMeasurement item)
    {
        if (_busy || ActiveUserId is not { } userId) return;
        _busy = true;
        try
        {
            if (!await this.ConfirmAppActionAsync("Excluir medidas?",
                    $"O registro de {DateText(item.MeasuredOn)} será excluído do seu histórico.",
                    "Excluir") || ActiveUserId != userId) return;
            await _measurements.DeleteAsync(userId, item.Id);
            await LoadMeasurementsAsync(userId);
            if (SelectedDate == item.MeasuredOn.Date)
            {
                FillFormForDate();
                SetStatus(_formStatus, string.Empty);
            }
            SetStatus(_historyStatus, "Registro excluído.");
        }
        catch (InvalidOperationException exception)
        {
            SetStatus(_historyStatus, exception.Message);
        }
        catch
        {
            SetStatus(_historyStatus, "Não foi possível excluir o registro. Tente novamente.");
        }
        finally { _busy = false; }
    }

    private static Button PillButton(string text, Func<Task> action, string automationId, bool secondary = false)
    {
        var button = Ui.Button(text, action, secondary);
        button.CornerRadius = 24;
        button.AutomationId = automationId;
        return button;
    }

    private static void SetStatus(Label label, string message)
    {
        label.Text = message;
        label.IsVisible = message.Length > 0;
    }

    private static double? ReadMeasure(Entry entry, string label, bool required = false)
    {
        var text = entry.Text?.Trim();
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

    private static string NumberText(double value) => value.ToString("0.##", NumberCulture);

    private static string Measure(double value, string unit) => $"{NumberText(value)} {unit}";

    private static string DateText(DateTime date) => date.ToString("dd/MM/yyyy", NumberCulture);

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
