using SnakeForTwo.Game.Domain;

namespace SnakeForTwo.Game.Domain.Tests;

public sealed class DomainPlaceholderTests
{
    [Fact]
    public void GameTick_advances_by_one()
    {
        Assert.Equal(new GameTick(1), GameTick.Zero.Next());
    }
}
