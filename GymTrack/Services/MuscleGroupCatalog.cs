namespace GymTrack.Services;

public static class MuscleGroupCatalog
{
    public static IReadOnlyList<string> UpperBody { get; } =
    [
        "Abdominais", "Antebraços", "Bíceps", "Costas superiores", "Dorsais",
        "Lombar", "Ombros", "Peito", "Pescoço", "Trapézio", "Tríceps"
    ];

    public static IReadOnlyList<string> LowerBody { get; } =
    [
        "Abdutores", "Adutores", "Glúteos", "Panturrilhas",
        "Posteriores de coxa", "Quadríceps"
    ];

    public static IReadOnlyList<string> Other { get; } =
    ["Cardio", "Corpo inteiro", "Outro"];

    public static IReadOnlyList<string> Legacy { get; } =
    ["Costas", "Pernas", "Braços", "Core"];

    public static IReadOnlyList<string> All { get; } =
        [.. UpperBody, .. LowerBody, .. Other];

    public static IReadOnlyList<string> AllForEditing { get; } =
        [.. All, .. Legacy];
}
