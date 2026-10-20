namespace GymTrack.Models;

public sealed record StarterExercise(string Name, string MuscleGroup, int Sets, int Reps);

public sealed record StarterWorkoutPlan(
    string Key,
    string Name,
    string Description,
    IReadOnlyList<StarterExercise> Exercises);
