using GymTrack.Services;

namespace GymTrack.Tests;

public sealed class ExerciseMediaCatalogTests
{
    [Theory]
    [InlineData("Prancha frontal")]
    [InlineData("Abdominal no solo")]
    [InlineData("Flexão de punhos com halteres")]
    [InlineData("Rosca direta com halteres")]
    [InlineData("Rosca martelo")]
    [InlineData("Remada alta com halteres")]
    [InlineData("Puxada frontal")]
    [InlineData("Remada curvada")]
    [InlineData("Extensão lombar no banco")]
    [InlineData("Desenvolvimento com halteres")]
    [InlineData("Elevação lateral")]
    [InlineData("Supino reto com halteres")]
    [InlineData("Supino inclinado com halteres")]
    [InlineData("Crucifixo com halteres")]
    [InlineData("Flexão de braços")]
    [InlineData("Mobilidade cervical")]
    [InlineData("Encolhimento com halteres")]
    [InlineData("Tríceps na polia")]
    [InlineData("Tríceps francês")]
    [InlineData("Cadeira abdutora")]
    [InlineData("Cadeira adutora")]
    [InlineData("Elevação pélvica")]
    [InlineData("Agachamento sumô")]
    [InlineData("Coice no cabo")]
    [InlineData("Elevação de panturrilhas em pé")]
    [InlineData("Mesa flexora")]
    [InlineData("Levantamento terra romeno")]
    [InlineData("Agachamento livre")]
    [InlineData("Leg press")]
    [InlineData("Caminhada na esteira")]
    [InlineData("Polichinelo")]
    [InlineData("Alongamento geral")]
    public void SeedExercise_HasAnatomicalIllustration(string name)
    {
        var media = ExerciseMediaCatalog.Get(name);
        Assert.NotNull(media.Image);
        Assert.StartsWith("anatomy_", media.Image);
        Assert.EndsWith(".png", media.Image);
        Assert.NotNull(media.Credit);
        Assert.Null(media.SourceUrl);
    }

    [Fact]
    public void LegExtension_KeepsTheLicensedReferenceIllustration()
    {
        var media = ExerciseMediaCatalog.Get("Cadeira extensora");
        Assert.Equal("exercise_leg_extension.jpg", media.Image);
        Assert.Contains("Franpol", media.Credit);
        Assert.StartsWith("https://", media.SourceUrl);
    }

    [Theory]
    [InlineData("Exercício personalizado", "Quadríceps")]
    [InlineData("Cadeira flexora", "Posteriores de coxa")]
    public void UnknownExercise_DoesNotBorrowAnotherMovementsMedia(string name, string group)
    {
        Assert.Null(ExerciseMediaCatalog.ForExercise(name, group));
    }

    [Theory]
    [InlineData("Agachamento livre", "Quadríceps")]
    [InlineData("Leg press", "Quadríceps")]
    [InlineData("Mesa flexora", "Posteriores de coxa")]
    [InlineData("Elevação de panturrilhas em pé", "Panturrilhas")]
    public void LowerBodyGuide_HasExerciseSpecificStepsAndCaution(string name, string group)
    {
        var guide = ExerciseGuideCatalog.ForExercise(name);
        Assert.NotNull(guide);
        Assert.True(guide.Steps.Count >= 3);
        Assert.False(string.IsNullOrWhiteSpace(guide.Caution));
        Assert.False(string.IsNullOrWhiteSpace(guide.Muscles));
        Assert.NotNull(ExerciseMediaCatalog.ForExercise(name, group));
    }
}
