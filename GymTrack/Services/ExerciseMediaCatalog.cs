using System.Globalization;
using System.Text;

namespace GymTrack.Services;

public sealed record ExerciseMedia(string? Image, string? Credit = null, string? SourceUrl = null);

public static class ExerciseMediaCatalog
{
    // Images are exercise-specific. Unknown or custom movements have no assigned media.
    private static readonly IReadOnlyDictionary<string, ExerciseMedia> Media =
        new Dictionary<string, ExerciseMedia>(StringComparer.Ordinal)
        {
            ["prancha frontal"] = new("anatomy_prancha_frontal.png", "Ilustração anatômica gerada para GymTrack"),
            ["abdominal no solo"] = new("anatomy_abdominal_no_solo.png", "Ilustração anatômica gerada para GymTrack"),
            ["flexao de punhos com halteres"] = new("anatomy_flexao_de_punhos_com_halteres.png", "Ilustração anatômica gerada para GymTrack"),
            ["rosca direta com halteres"] = new("anatomy_rosca_direta_com_halteres.png", "Ilustração anatômica gerada para GymTrack"),
            ["rosca martelo"] = new("anatomy_rosca_martelo.png", "Ilustração anatômica gerada para GymTrack"),
            ["remada alta com halteres"] = new("anatomy_remada_alta_com_halteres.png", "Ilustração anatômica gerada para GymTrack"),
            ["puxada frontal"] = new("anatomy_puxada_frontal.png", "Ilustração anatômica gerada para GymTrack"),
            ["remada curvada"] = new("anatomy_remada_curvada.png", "Ilustração anatômica gerada para GymTrack"),
            ["extensao lombar no banco"] = new("anatomy_extensao_lombar_no_banco.png", "Ilustração anatômica gerada para GymTrack"),
            ["desenvolvimento com halteres"] = new("anatomy_desenvolvimento_com_halteres.png", "Ilustração anatômica gerada para GymTrack"),
            ["elevacao lateral"] = new("anatomy_elevacao_lateral.png", "Ilustração anatômica gerada para GymTrack"),
            ["supino reto com halteres"] = new("anatomy_supino_reto_com_halteres.png", "Ilustração anatômica gerada para GymTrack"),
            ["supino inclinado com halteres"] = new("anatomy_supino_inclinado_com_halteres.png", "Ilustração anatômica gerada para GymTrack"),
            ["crucifixo com halteres"] = new("anatomy_crucifixo_com_halteres.png", "Ilustração anatômica gerada para GymTrack"),
            ["flexao de bracos"] = new("anatomy_flexao_de_bracos.png", "Ilustração anatômica gerada para GymTrack"),
            ["mobilidade cervical"] = new("anatomy_mobilidade_cervical.png", "Ilustração anatômica gerada para GymTrack"),
            ["encolhimento com halteres"] = new("anatomy_encolhimento_com_halteres.png", "Ilustração anatômica gerada para GymTrack"),
            ["triceps na polia"] = new("anatomy_triceps_na_polia.png", "Ilustração anatômica gerada para GymTrack"),
            ["triceps frances"] = new("anatomy_triceps_frances.png", "Ilustração anatômica gerada para GymTrack"),
            ["cadeira abdutora"] = new("anatomy_cadeira_abdutora.png", "Ilustração anatômica gerada para GymTrack"),
            ["cadeira adutora"] = new("anatomy_cadeira_adutora.png", "Ilustração anatômica gerada para GymTrack"),
            ["coice no cabo"] = new("anatomy_coice_no_cabo.png", "Ilustração anatômica gerada para GymTrack"),
            ["elevacao de panturrilhas em pe"] = new("anatomy_elevacao_de_panturrilhas_em_pe.png", "Ilustração anatômica gerada para GymTrack"),
            ["elevacao pelvica"] = new("anatomy_elevacao_pelvica.png", "Ilustração anatômica gerada para GymTrack"),
            ["agachamento sumo"] = new("anatomy_agachamento_sumo.png", "Ilustração anatômica gerada para GymTrack"),
            ["mesa flexora"] = new("anatomy_mesa_flexora.png", "Ilustração anatômica gerada para GymTrack"),
            ["levantamento terra romeno"] = new("anatomy_levantamento_terra_romeno.png", "Ilustração anatômica gerada para GymTrack"),
            ["agachamento livre"] = new("anatomy_agachamento_livre.png", "Ilustração anatômica gerada para GymTrack"),
            ["leg press"] = new("anatomy_leg_press.png", "Ilustração anatômica gerada para GymTrack"),
            ["cadeira extensora"] = new("exercise_leg_extension.jpg", "Imagem: Franpol / wger · CC BY-SA 4.0",
                "https://wger.de/api/v2/exerciseimage/386/"),
            ["caminhada na esteira"] = new("anatomy_caminhada_na_esteira.png", "Ilustração anatômica gerada para GymTrack"),
            ["polichinelo"] = new("anatomy_polichinelo.png", "Ilustração anatômica gerada para GymTrack"),
            ["alongamento geral"] = new("anatomy_alongamento_geral.png", "Ilustração anatômica gerada para GymTrack"),
        };

    public static ExerciseMedia Get(string name) =>
        Media.TryGetValue(Normalize(name), out var media) ? media : new ExerciseMedia(null);

    public static string? ForExercise(string name, string muscleGroup) => Get(name).Image;

    internal static string Normalize(string? value)
    {
        var decomposed = (value ?? string.Empty).Trim().Normalize(NormalizationForm.FormD);
        var plain = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                plain.Append(char.ToLowerInvariant(character));
        }
        return plain.ToString().Normalize(NormalizationForm.FormC);
    }
}
