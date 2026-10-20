using GymTrack.Data;
using GymTrack.Models;
using SQLite;

namespace GymTrack.Services;

public sealed class StarterCatalogService(GymTrackDatabase database) : IStarterCatalogService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _seeded;

    public async Task EnsureSeededAsync()
    {
        if (_seeded) return;

        await _gate.WaitAsync();
        try
        {
            if (_seeded) return;

            var connection = await database.GetConnectionAsync();
            await connection.ExecuteAsync(
                "CREATE TABLE IF NOT EXISTS StarterCatalogSeed (Key TEXT PRIMARY KEY)");
            await connection.RunInTransactionAsync(Seed);
            _seeded = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static void Seed(SQLiteConnection connection)
    {
        if (connection.ExecuteScalar<int>(
                "SELECT COUNT(1) FROM StarterCatalogSeed WHERE Key = ?", "v1") != 0)
        {
            return;
        }

        var exercises = connection.Table<Exercise>().ToList();
        var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var exercise in exercises)
        {
            ids.TryAdd(Key(exercise.Name, exercise.MuscleGroup), exercise.Id);
        }

        foreach (var (name, group, notes) in ExerciseSeeds)
        {
            var key = Key(name, group);
            if (ids.ContainsKey(key)) continue;

            var exercise = new Exercise { Name = name, MuscleGroup = group, Notes = notes };
            connection.Insert(exercise);
            ids.Add(key, exercise.Id);
        }

        var existingTemplates = connection.Table<Workout>()
            .Where(workout => workout.IsTemplate)
            .Select(workout => workout.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;

        foreach (var template in TemplateSeeds)
        {
            if (existingTemplates.Contains(template.Name)) continue;

            var workout = new Workout
            {
                Name = template.Name,
                Description = template.Description,
                IsTemplate = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            connection.Insert(workout);

            for (var index = 0; index < template.Plans.Length; index++)
            {
                var plan = template.Plans[index];
                connection.Insert(new WorkoutExercise
                {
                    WorkoutId = workout.Id,
                    ExerciseId = ids[Key(plan.Name, plan.Group)],
                    OrderIndex = index,
                    PlannedSets = plan.Sets,
                    PlannedReps = plan.Reps
                });
            }
        }

        connection.Execute("INSERT INTO StarterCatalogSeed (Key) VALUES (?)", "v1");
    }

    private static string Key(string name, string group) => $"{group.Trim()}\u001F{name.Trim()}";

    private static readonly (string Name, string Group, string Notes)[] ExerciseSeeds =
    [
        ("Prancha frontal", "Abdominais", "Alinhe ombros, quadris e tornozelos. Apoie os antebraços e mantenha o abdômen firme."),
        ("Abdominal no solo", "Abdominais", "Deitado de costas, flexione o tronco suavemente e retorne sem puxar o pescoço."),
        ("Flexão de punhos com halteres", "Antebraços", "Apoie os antebraços e mova apenas os punhos, controlando a volta."),
        ("Rosca direta com halteres", "Bíceps", "Mantenha os cotovelos perto do corpo e flexione os braços sem balançar o tronco."),
        ("Rosca martelo", "Bíceps", "Segure os halteres com as palmas voltadas uma para a outra e flexione os cotovelos."),
        ("Remada alta com halteres", "Costas superiores", "Puxe os halteres em direção ao peito com movimento controlado, sem elevar demais os ombros."),
        ("Puxada frontal", "Dorsais", "Puxe a barra à frente do peito, sem jogar o tronco para trás, e retorne com controle."),
        ("Remada curvada", "Dorsais", "Incline o tronco com a coluna neutra e puxe os halteres em direção à cintura."),
        ("Extensão lombar no banco", "Lombar", "Mantenha a coluna neutra e eleve o tronco apenas até ficar alinhado às pernas."),
        ("Desenvolvimento com halteres", "Ombros", "Pressione os halteres acima da cabeça sem arquear demais a lombar."),
        ("Elevação lateral", "Ombros", "Eleve os braços lateralmente até a altura dos ombros, com cotovelos levemente flexionados."),
        ("Supino reto com halteres", "Peito", "Deitado no banco, desça os halteres com controle e empurre de volta acima do peito."),
        ("Supino inclinado com halteres", "Peito", "Com o banco inclinado, desça os halteres ao lado do peito e empurre para cima."),
        ("Crucifixo com halteres", "Peito", "Abra os braços em arco com cotovelos levemente flexionados e feche acima do peito."),
        ("Flexão de braços", "Peito", "Mantenha o corpo alinhado, flexione os cotovelos e empurre o chão para voltar."),
        ("Mobilidade cervical", "Pescoço", "Faça movimentos lentos e confortáveis com o pescoço, sem forçar a amplitude."),
        ("Encolhimento com halteres", "Trapézio", "Eleve os ombros em direção às orelhas e abaixe devagar, sem girar."),
        ("Tríceps na polia", "Tríceps", "Mantenha os cotovelos junto ao corpo e estenda os braços para baixo."),
        ("Tríceps francês", "Tríceps", "Com os cotovelos apontando para cima, flexione e estenda os braços de modo controlado."),
        ("Cadeira abdutora", "Abdutores", "Afaste as pernas contra a resistência e retorne lentamente, mantendo o tronco estável."),
        ("Cadeira adutora", "Adutores", "Aproxime as pernas contra a resistência e retorne lentamente sem soltar o peso."),
        ("Elevação pélvica", "Glúteos", "Apoie as costas no banco, eleve o quadril até alinhar o tronco às coxas e retorne."),
        ("Agachamento sumô", "Glúteos", "Com os pés afastados, flexione quadris e joelhos e volte mantendo o tronco firme."),
        ("Coice no cabo", "Glúteos", "Estenda a perna para trás sem arquear a lombar e volte devagar."),
        ("Elevação de panturrilhas em pé", "Panturrilhas", "Suba na ponta dos pés e desça de forma controlada."),
        ("Mesa flexora", "Posteriores de coxa", "Flexione os joelhos contra o apoio e retorne sem soltar o peso."),
        ("Levantamento terra romeno", "Posteriores de coxa", "Leve os quadris para trás com a coluna neutra e volte estendendo o quadril."),
        ("Agachamento livre", "Quadríceps", "Flexione quadris e joelhos com os pés firmes no chão e depois volte a ficar de pé."),
        ("Leg press", "Quadríceps", "Empurre a plataforma sem travar os joelhos e retorne de forma controlada."),
        ("Cadeira extensora", "Quadríceps", "Estenda os joelhos sem impulso e retorne devagar."),
        ("Caminhada na esteira", "Cardio", "Caminhe em ritmo confortável, com postura ereta e passada natural."),
        ("Polichinelo", "Corpo inteiro", "Salte abrindo braços e pernas e retorne ao centro em um ritmo confortável."),
        ("Alongamento geral", "Outro", "Mova as articulações de forma suave e mantenha posições confortáveis, sem dor.")
    ];

    private sealed record Plan(string Name, string Group, int Sets, int Reps);

    private sealed record Template(string Name, string Description, Plan[] Plans);

    private static readonly Template[] TemplateSeeds =
    [
        new("Peito essencial", "Quatro exercícios para organizar o treino de peito.",
        [
            new("Supino reto com halteres", "Peito", 3, 10),
            new("Supino inclinado com halteres", "Peito", 3, 10),
            new("Crucifixo com halteres", "Peito", 3, 12),
            new("Flexão de braços", "Peito", 3, 8)
        ]),
        new("Pernas completas", "Quadríceps, posteriores e panturrilhas em uma rotina.",
        [
            new("Agachamento livre", "Quadríceps", 3, 10),
            new("Leg press", "Quadríceps", 3, 12),
            new("Cadeira extensora", "Quadríceps", 3, 12),
            new("Mesa flexora", "Posteriores de coxa", 3, 12),
            new("Elevação de panturrilhas em pé", "Panturrilhas", 3, 15)
        ]),
        new("Foco em glúteos", "Uma sequência de quatro exercícios para glúteos e abdutores.",
        [
            new("Elevação pélvica", "Glúteos", 3, 12),
            new("Agachamento sumô", "Glúteos", 3, 10),
            new("Coice no cabo", "Glúteos", 3, 12),
            new("Cadeira abdutora", "Abdutores", 3, 15)
        ])
    ];
}
