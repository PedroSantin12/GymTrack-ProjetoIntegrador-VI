using GymTrack.Services;

namespace GymTrack.Tests;

public sealed class ExerciseMediaCatalogTests
{
    [Theory]
    [InlineData("Supino reto com halteres", "Peito", "demo_bench_press.gif")]
    [InlineData("Supino inclinado com halteres", "Peito", "demo_incline_press.gif")]
    [InlineData("Crucifixo com halteres", "Peito", "demo_fly.gif")]
    [InlineData("Flexão de braços", "Peito", "demo_pushup.gif")]
    [InlineData("Agachamento livre", "Quadríceps", "demo_squat.gif")]
    [InlineData("Leg press", "Quadríceps", "demo_leg_press.gif")]
    [InlineData("Cadeira extensora", "Quadríceps", "demo_leg_extension.gif")]
    [InlineData("Mesa flexora", "Posteriores de coxa", "demo_leg_curl.gif")]
    [InlineData("Elevação de panturrilhas em pé", "Panturrilhas", "demo_calf_raise.gif")]
    [InlineData("Elevação pélvica", "Glúteos", "demo_hip_thrust.gif")]
    [InlineData("Agachamento sumô", "Glúteos", "demo_sumo_squat.gif")]
    [InlineData("Coice no cabo", "Glúteos", "demo_kickback.gif")]
    [InlineData("Cadeira abdutora", "Abdutores", "demo_abductor.gif")]
    public void ForExercise_UsesDistinctMovementForStarterWorkouts(
        string name, string group, string expected)
    {
        Assert.Equal(expected, ExerciseMediaCatalog.ForExercise(name, group));
    }

    [Fact]
    public void ForExercise_UsesMuscleGroupForNewExercise()
    {
        Assert.Equal("demo_lateral_raise.gif",
            ExerciseMediaCatalog.ForExercise("Movimento personalizado", "Ombros"));
        Assert.Equal("demo_stretch.gif",
            ExerciseMediaCatalog.ForExercise("Movimento personalizado", "Outro"));
    }

    [Theory]
    [InlineData("Costas", "demo_row.gif")]
    [InlineData("Pernas", "demo_squat.gif")]
    [InlineData("Braços", "demo_curl.gif")]
    [InlineData("Core", "demo_crunch.gif")]
    public void ForExercise_MapsLegacyGroups(string group, string expected)
    {
        Assert.Equal(expected, ExerciseMediaCatalog.ForExercise("Exercício antigo", group));
    }

    [Theory]
    [InlineData("Supino reto", "Peito", "demo_bench_press.gif")]
    [InlineData("Supino inclinado", "Peito", "demo_incline_press.gif")]
    [InlineData("Remada baixa", "Costas", "demo_row.gif")]
    [InlineData("Rosca direta", "Bíceps", "demo_curl.gif")]
    [InlineData("Desenvolvimento militar", "Ombros", "demo_press.gif")]
    [InlineData("Elevação de panturrilha", "Panturrilhas", "demo_calf_raise.gif")]
    public void ForExercise_MapsExistingStarterPlans(string name, string group, string expected)
    {
        Assert.Equal(expected, ExerciseMediaCatalog.ForExercise(name, group));
    }

    [Fact]
    public void ForExercise_MapsExistingPosteriorDeCoxaGroup()
    {
        Assert.Equal("demo_hinge.gif",
            ExerciseMediaCatalog.ForExercise("Movimento personalizado", "Posterior de coxa"));
    }
}
