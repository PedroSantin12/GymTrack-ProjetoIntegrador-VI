using GymTrack.Models;
using GymTrack.Services;

namespace GymTrack.Tests;

public sealed class ProgressDataServicesTests
{
    [Fact]
    public async Task ManualLogs_StayWithOwnerAndKeepCivilDateAndOrder()
    {
        await using var context = await TestDatabase.CreateAsync();
        var service = new ManualWorkoutLogService(context.Database);
        var today = DateTime.Today;
        var first = await service.AddAsync(1, "  Treino A  ", today.AddHours(17), "  Completo  ");
        var second = await service.AddAsync(1, "Treino B", today.AddHours(9), null);
        var other = await service.AddAsync(2, "Treino C", today.AddDays(-1), null);

        var own = await service.GetForUserAsync(1);
        Assert.Equal([second, first], own.Select(item => item.Id));
        Assert.All(own, item => Assert.Equal(today, item.PerformedOn.Date));
        Assert.Equal("Treino A", own[1].WorkoutName);
        Assert.Equal("Completo", own[1].Notes);
        Assert.All(own, item => Assert.False(item.IsExample));
        Assert.Equal(other, Assert.Single(await service.GetForUserAsync(2)).Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(2, first));
        Assert.Equal(2, (await service.GetForUserAsync(1)).Count);
        await service.DeleteAsync(1, first);
        Assert.Equal(second, Assert.Single(await service.GetForUserAsync(1)).Id);
    }

    [Fact]
    public async Task ManualLogs_RejectInvalidNameDateAndOwner()
    {
        await using var context = await TestDatabase.CreateAsync();
        var service = new ManualWorkoutLogService(context.Database);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.AddAsync(0, "Treino A", DateTime.Today, null));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AddAsync(1, " ", DateTime.Today, null));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.AddAsync(1, "Treino A", DateTime.Today.AddDays(1), null));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.AddAsync(1, "Treino A", default, null));
        Assert.Empty(await service.GetForUserAsync(1));
    }

    [Fact]
    public async Task DemoExamples_AppearOnlyOnceForOriginalConsumerAndStayDeleted()
    {
        await using var context = await TestDatabase.CreateAsync();
        var service = new ManualWorkoutLogService(context.Database);
        var demo = new AppUser
        {
            Id = 1,
            Email = " CONSUMIDOR@GYMTRACK.APP ",
            Role = UserRole.Consumer,
            IsActive = true
        };
        var other = new AppUser
        {
            Id = 2,
            Email = "outro@exemplo.com",
            Role = UserRole.Consumer,
            IsActive = true
        };
        var employee = new AppUser
        {
            Id = 3,
            Email = "consumidor@gymtrack.app",
            Role = UserRole.Employee,
            IsActive = true
        };

        await service.EnsureDemoExamplesAsync(other);
        await service.EnsureDemoExamplesAsync(employee);
        Assert.Empty(await service.GetForUserAsync(2));
        Assert.Empty(await service.GetForUserAsync(3));

        await service.EnsureDemoExamplesAsync(demo);
        await service.EnsureDemoExamplesAsync(demo);
        var examples = await service.GetForUserAsync(1);
        Assert.Equal(3, examples.Count);
        Assert.All(examples, item => Assert.True(item.IsExample));
        Assert.All(examples, item => Assert.True(item.PerformedOn.Date < DateTime.Today));

        foreach (var example in examples)
            await service.DeleteAsync(demo.Id, example.Id);

        await new ManualWorkoutLogService(context.Database).EnsureDemoExamplesAsync(demo);
        Assert.Empty(await service.GetForUserAsync(1));
    }

    [Fact]
    public async Task DemoExamples_DoNotMixWithExistingManualHistory()
    {
        await using var context = await TestDatabase.CreateAsync();
        var service = new ManualWorkoutLogService(context.Database);
        var demo = new AppUser
        {
            Id = 1,
            Email = "consumidor@gymtrack.app",
            Role = UserRole.Consumer,
            IsActive = true
        };

        var realId = await service.AddAsync(1, "Treino real", DateTime.Today.AddDays(-2), null);
        await service.EnsureDemoExamplesAsync(demo);
        Assert.Equal(realId, Assert.Single(await service.GetForUserAsync(1)).Id);

        await service.DeleteAsync(1, realId);
        await new ManualWorkoutLogService(context.Database).EnsureDemoExamplesAsync(demo);
        Assert.Empty(await service.GetForUserAsync(1));
    }

    [Fact]
    public async Task Measurements_UpsertSameDayAndStayWithOwner()
    {
        await using var context = await TestDatabase.CreateAsync();
        var service = new BodyMeasurementService(context.Database);
        var today = DateTime.Today;
        var yesterday = today.AddDays(-1);
        var originalId = await service.SaveAsync(1, yesterday.AddHours(8), 78.2, 176, 82, null, null);
        var updatedId = await service.SaveAsync(1, yesterday.AddHours(19), 77.5, 176, 81, 95, 99);
        var newerId = await service.SaveAsync(1, today, 77, null, null, null, null);
        var otherId = await service.SaveAsync(2, yesterday, 65, null, null, null, null);

        Assert.Equal(originalId, updatedId);
        var own = await service.GetForUserAsync(1);
        Assert.Equal([newerId, originalId], own.Select(item => item.Id));
        Assert.Equal(yesterday, own[1].MeasuredOn.Date);
        Assert.Equal(77.5, own[1].WeightKg);
        Assert.Equal(81, own[1].WaistCm);
        Assert.Equal(95, own[1].HipCm);
        Assert.Equal(otherId, Assert.Single(await service.GetForUserAsync(2)).Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(2, originalId));
        Assert.Equal(2, (await service.GetForUserAsync(1)).Count);
        await service.DeleteAsync(1, originalId);
        Assert.Equal(newerId, Assert.Single(await service.GetForUserAsync(1)).Id);
    }

    [Fact]
    public async Task Measurements_RejectFutureOrInvalidValues()
    {
        await using var context = await TestDatabase.CreateAsync();
        var service = new BodyMeasurementService(context.Database);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SaveAsync(0, DateTime.Today, 80, null, null, null, null));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SaveAsync(1, DateTime.Today.AddDays(1), 80, null, null, null, null));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SaveAsync(1, DateTime.Today, 0, null, null, null, null));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SaveAsync(1, DateTime.Today, double.NaN, null, null, null, null));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SaveAsync(1, DateTime.Today, 80, null, double.PositiveInfinity, null, null));
        Assert.Empty(await service.GetForUserAsync(1));
    }
}
