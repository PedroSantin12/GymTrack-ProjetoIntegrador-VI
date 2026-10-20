using System.Globalization;
using System.Text;

namespace GymTrack.Services;

public static class ExerciseMediaCatalog
{
    public static string ForExercise(string name, string muscleGroup)
    {
        var key = Normalize(name);
        return key switch
        {
            "prancha frontal" => "demo_plank.gif",
            "abdominal no solo" => "demo_crunch.gif",
            "flexao de punhos com halteres" => "demo_wrist_curl.gif",
            "rosca direta" or "rosca direta com halteres" or "rosca martelo" => "demo_curl.gif",
            "remada alta com halteres" => "demo_upright_row.gif",
            "puxada frontal" => "demo_pulldown.gif",
            "remada curvada" or "remada baixa" => "demo_row.gif",
            "extensao lombar no banco" => "demo_back_extension.gif",
            "desenvolvimento com halteres" or "desenvolvimento militar" => "demo_press.gif",
            "elevacao lateral" or "elevacao lateral (halter)" => "demo_lateral_raise.gif",
            "supino reto" or "supino reto com halteres" => "demo_bench_press.gif",
            "supino inclinado" or "supino inclinado com halteres" => "demo_incline_press.gif",
            "crucifixo com halteres" => "demo_fly.gif",
            "flexao de bracos" => "demo_pushup.gif",
            "mobilidade cervical" => "demo_neck.gif",
            "encolhimento com halteres" => "demo_shrug.gif",
            "triceps na polia" => "demo_triceps.gif",
            "triceps frances" => "demo_overhead_triceps.gif",
            "cadeira abdutora" => "demo_abductor.gif",
            "cadeira adutora" => "demo_adductor.gif",
            "elevacao pelvica" => "demo_hip_thrust.gif",
            "agachamento sumo" => "demo_sumo_squat.gif",
            "coice no cabo" => "demo_kickback.gif",
            "elevacao de panturrilha" or "elevacao de panturrilhas em pe" => "demo_calf_raise.gif",
            "mesa flexora" => "demo_leg_curl.gif",
            "levantamento terra romeno" => "demo_hinge.gif",
            "agachamento livre" => "demo_squat.gif",
            "leg press" => "demo_leg_press.gif",
            "cadeira extensora" => "demo_leg_extension.gif",
            "caminhada na esteira" => "demo_walk.gif",
            "polichinelo" => "demo_jumping_jack.gif",
            "alongamento geral" => "demo_stretch.gif",
            _ => ForGroup(muscleGroup)
        };
    }

    private static string ForGroup(string muscleGroup) => Normalize(muscleGroup) switch
    {
        "abdominais" => "demo_crunch.gif",
        "antebracos" => "demo_wrist_curl.gif",
        "biceps" => "demo_curl.gif",
        "costas superiores" => "demo_upright_row.gif",
        "dorsais" => "demo_row.gif",
        "lombar" => "demo_back_extension.gif",
        "ombros" => "demo_lateral_raise.gif",
        "peito" => "demo_bench_press.gif",
        "pescoco" => "demo_neck.gif",
        "trapezio" => "demo_shrug.gif",
        "triceps" => "demo_triceps.gif",
        "abdutores" => "demo_abductor.gif",
        "adutores" => "demo_adductor.gif",
        "gluteos" => "demo_hip_thrust.gif",
        "panturrilhas" => "demo_calf_raise.gif",
        "posterior de coxa" or "posteriores de coxa" => "demo_hinge.gif",
        "quadriceps" => "demo_squat.gif",
        "cardio" => "demo_walk.gif",
        "corpo inteiro" => "demo_jumping_jack.gif",
        "costas" => "demo_row.gif",
        "pernas" => "demo_squat.gif",
        "bracos" => "demo_curl.gif",
        "core" => "demo_crunch.gif",
        _ => "demo_stretch.gif"
    };

    private static string Normalize(string? value)
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
