using TrafficSense;
using Xunit;
public sealed class TaskbarVacancyTests
{
    [Fact] public void ChoosesGapWithMargins()
    {
        PixelRect bar = new(-1920, 1040, 0, 1080); PixelRect[] occupied = [new(-1920,1040,-1600,1080), new(-1200,1040,-600,1080), new(-200,1040,0,1080)];
        var result = TaskbarVacancy.Find(bar, occupied, 230, 32, true);
        Assert.NotNull(result); Assert.All(occupied, rect => Assert.False(result.Value.Intersects(rect))); Assert.Equal(32, result.Value.Height);
    }
    [Fact] public void UnknownFullVerticalOrTooSmallIsUnavailable()
    {
        var horizontal = new PixelRect(0,0,1920,40); PixelRect[] full = [horizontal];
        Assert.Null(TaskbarVacancy.Find(horizontal, full, 230, 32, true));
        Assert.Null(TaskbarVacancy.Find(horizontal, [], 230, 32, true));
        Assert.Null(TaskbarVacancy.Find(horizontal, [new(0,0,40,40)], 230, 32, false));
        Assert.Null(TaskbarVacancy.Find(new(0,0,40,1080), [], 230, 32, true));
        Assert.Null(TaskbarVacancy.Find(horizontal, [new(0,0,40,40)], 230, 50, true));
    }
    [Fact] public void IgnoresOtherMonitorAndMergesOverlap()
    {
        PixelRect bar = new(0,1000,1600,1040); PixelRect[] occupied = [new(0,1000,500,1040), new(450,1000,900,1040), new(1500,1000,1600,1040), new(-1920,1000,0,1040)];
        var result = TaskbarVacancy.Find(bar, occupied, 300, 32, true); Assert.NotNull(result); Assert.True(result.Value.Left >= 908 && result.Value.Right <=1492);
    }
}
