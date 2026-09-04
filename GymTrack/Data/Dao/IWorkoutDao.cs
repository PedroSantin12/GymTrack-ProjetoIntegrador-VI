using GymTrack.Models;

namespace GymTrack.Data.Dao;

public interface IWorkoutDao
{
    Task<IReadOnlyList<Workout>> GetAllAsync();

    Task<Workout?> GetByIdAsync(int id);

    Task<IReadOnlyList<WorkoutExercise>> GetExercisesAsync(int workoutId);

    Task<int> SaveAsync(Workout workout, IReadOnlyList<WorkoutExercise> exercises);

    Task<bool> HasSessionsAsync(int id);

    Task<int> DeleteAsync(int id);
}
