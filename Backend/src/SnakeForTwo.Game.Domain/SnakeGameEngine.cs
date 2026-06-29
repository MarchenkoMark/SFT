using System.Security.Cryptography;
using System.Text;

namespace SnakeForTwo.Game.Domain;

public static class SnakeGameEngine
{
    public static GameState CreateInitialState(
        GameRules rules,
        IEnumerable<SnakeSpawn> snakeSpawns,
        IEnumerable<FoodItem>? food = null)
    {
        ArgumentNullException.ThrowIfNull(snakeSpawns);

        var snakes = snakeSpawns
            .Select(spawn => new Snake(spawn.PlayerId, spawn.Color, spawn.Direction, spawn.Body))
            .ToArray();

        ValidateInitialSnakes(rules, snakes);

        var suppliedFood = food?.ToArray();
        if (suppliedFood is not null)
        {
            ValidateFood(rules, snakes, suppliedFood);
            return new GameState(
                GameTick.Zero,
                rules,
                GameStatus.Running,
                snakes,
                suppliedFood,
                foodSpawnCursor: 0);
        }

        var spawnedFood = new List<FoodItem>();
        var blockedCells = OccupiedCells(snakes).ToHashSet();
        var cursor = 0;
        foreach (var snake in snakes)
        {
            var nextFood = SpawnFood(rules, snake, blockedCells, cursor);
            spawnedFood.Add(nextFood);
            blockedCells.Add(nextFood.Cell);
            cursor++;
        }

        return new GameState(
            GameTick.Zero,
            rules,
            GameStatus.Running,
            snakes,
            spawnedFood,
            foodSpawnCursor: cursor);
    }

    public static GameState Advance(GameState state, TickInputs inputs) =>
        Advance(state, inputs, state.Rules);

    public static GameState Advance(GameState state, TickInputs inputs, GameRules rules)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(inputs);

        if (state.Status != GameStatus.Running)
        {
            return state.Copy();
        }

        var plannedSnakes = new List<Snake>(state.Snakes.Count);
        var eatenFood = new List<FoodItem>();
        GameOverReason? finishReason = null;

        foreach (var snake in state.Snakes)
        {
            if (snake.Body.Count == 0)
            {
                plannedSnakes.Add(snake.With());
                finishReason ??= GameOverReason.LengthDepleted;
                continue;
            }

            var requestedDirection = inputs.DirectionFor(snake.PlayerId, snake.Direction);
            var nextDirection = requestedDirection.IsOpposite(snake.Direction)
                ? snake.Direction
                : requestedDirection;
            var rawHead = Move(snake.Head, nextDirection);
            var nextHead = rawHead;

            if (!IsInsideBoard(rawHead, rules))
            {
                if (rules.WallWrapping)
                {
                    nextHead = Wrap(rawHead, rules);
                }
                else
                {
                    finishReason ??= GameOverReason.WallCollision;
                }
            }

            var foodAtHead = IsInsideBoard(nextHead, rules)
                ? state.Food.Where(food => food.Cell == nextHead).ToArray()
                : Array.Empty<FoodItem>();
            var growthDelta = foodAtHead.Sum(food => food.OwnerPlayerId == snake.PlayerId ? 1 : -1);

            var nextBody = new List<Cell>(snake.Body.Count + 1) { nextHead };
            nextBody.AddRange(snake.Body);

            var cellsToRemove = 1 - growthDelta;
            for (var removed = 0; removed < cellsToRemove && nextBody.Count > 0; removed++)
            {
                nextBody.RemoveAt(nextBody.Count - 1);
            }

            if (nextBody.Count == 0)
            {
                finishReason ??= GameOverReason.LengthDepleted;
            }

            plannedSnakes.Add(snake.With(nextDirection, nextBody));
            eatenFood.AddRange(foodAtHead);
        }

        finishReason ??= DetectCollision(plannedSnakes);

        var nextFood = state.Food.Except(eatenFood).ToList();
        var nextCursor = state.FoodSpawnCursor;

        if (finishReason is null && eatenFood.Count > 0)
        {
            var blockedCells = OccupiedCells(plannedSnakes)
                .Concat(nextFood.Select(food => food.Cell))
                .ToHashSet();

            foreach (var owner in EatenOwnersInSnakeOrder(eatenFood, plannedSnakes))
            {
                var ownerSnake = plannedSnakes.Single(snake => snake.PlayerId == owner);
                var replacement = SpawnFood(rules, ownerSnake, blockedCells, nextCursor);
                nextFood.Add(replacement);
                blockedCells.Add(replacement.Cell);
                nextCursor++;
            }
        }

        return new GameState(
            state.Tick.Next(),
            rules,
            finishReason is null ? GameStatus.Running : GameStatus.Finished,
            plannedSnakes,
            nextFood,
            finishReason ?? GameOverReason.None,
            nextCursor);
    }

    public static string ComputeStateHash(GameState state)
    {
        var builder = new StringBuilder();
        builder.Append("tick:").Append(state.Tick.Value).Append('|');
        builder.Append("board:")
            .Append(state.Rules.BoardWidth)
            .Append('x')
            .Append(state.Rules.BoardHeight)
            .Append(":wrap:")
            .Append(state.Rules.WallWrapping)
            .Append('|');
        builder.Append("status:")
            .Append(state.Status)
            .Append(":reason:")
            .Append(state.GameOverReason)
            .Append(":foodCursor:")
            .Append(state.FoodSpawnCursor)
            .Append('|');

        foreach (var snake in state.Snakes)
        {
            builder.Append("snake:")
                .Append(snake.PlayerId.Value)
                .Append(':')
                .Append(snake.Color.Value)
                .Append(':')
                .Append(snake.Direction)
                .Append(':');
            foreach (var cell in snake.Body)
            {
                builder.Append(cell.X).Append(',').Append(cell.Y).Append(';');
            }

            builder.Append('|');
        }

        foreach (var food in state.Food)
        {
            builder.Append("food:")
                .Append(food.OwnerPlayerId.Value)
                .Append(':')
                .Append(food.Color.Value)
                .Append(':')
                .Append(food.Cell.X)
                .Append(',')
                .Append(food.Cell.Y)
                .Append('|');
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static void ValidateInitialSnakes(GameRules rules, IReadOnlyList<Snake> snakes)
    {
        if (snakes.Count < 2)
        {
            throw new ArgumentException("A game requires at least two snakes.", nameof(snakes));
        }

        if (snakes.Select(snake => snake.PlayerId).Distinct().Count() != snakes.Count)
        {
            throw new ArgumentException("Each snake must be controlled by a unique player.", nameof(snakes));
        }

        if (snakes.Select(snake => snake.Color).Distinct().Count() != snakes.Count)
        {
            throw new ArgumentException("Each snake must have a unique color.", nameof(snakes));
        }

        var occupied = new HashSet<Cell>();
        foreach (var snake in snakes)
        {
            if (snake.Body.Count == 0)
            {
                throw new ArgumentException("A snake must contain at least one body cell.", nameof(snakes));
            }

            foreach (var cell in snake.Body)
            {
                if (!IsInsideBoard(cell, rules))
                {
                    throw new ArgumentException("Initial snake cells must be inside the board.", nameof(snakes));
                }

                if (!occupied.Add(cell))
                {
                    throw new ArgumentException("Initial snake bodies cannot overlap.", nameof(snakes));
                }
            }
        }
    }

    private static void ValidateFood(GameRules rules, IReadOnlyList<Snake> snakes, IReadOnlyList<FoodItem> foodItems)
    {
        var snakesByPlayer = snakes.ToDictionary(snake => snake.PlayerId);
        var blocked = OccupiedCells(snakes).ToHashSet();
        var occupiedFood = new HashSet<Cell>();

        foreach (var food in foodItems)
        {
            if (!snakesByPlayer.TryGetValue(food.OwnerPlayerId, out var owner))
            {
                throw new ArgumentException("Food must belong to an existing snake.", nameof(foodItems));
            }

            if (owner.Color != food.Color)
            {
                throw new ArgumentException("Food color must match the owning snake color.", nameof(foodItems));
            }

            if (!IsInsideBoard(food.Cell, rules))
            {
                throw new ArgumentException("Food must be inside the board.", nameof(foodItems));
            }

            if (blocked.Contains(food.Cell))
            {
                throw new ArgumentException("Food cannot start inside a snake body.", nameof(foodItems));
            }

            if (!occupiedFood.Add(food.Cell))
            {
                throw new ArgumentException("Food items cannot overlap.", nameof(foodItems));
            }
        }
    }

    private static GameOverReason? DetectCollision(IReadOnlyList<Snake> snakes)
    {
        foreach (var snake in snakes)
        {
            if (snake.Body.Count > 0 && snake.Body.Skip(1).Contains(snake.Head))
            {
                return GameOverReason.SelfCollision;
            }
        }

        var occupancy = new Dictionary<Cell, int>();
        foreach (var cell in OccupiedCells(snakes))
        {
            occupancy[cell] = occupancy.TryGetValue(cell, out var count) ? count + 1 : 1;
        }

        foreach (var snake in snakes.Where(snake => snake.Body.Count > 0))
        {
            if (occupancy[snake.Head] > 1)
            {
                return GameOverReason.SnakeCollision;
            }
        }

        return null;
    }

    private static IEnumerable<PlayerId> EatenOwnersInSnakeOrder(
        IReadOnlyCollection<FoodItem> eatenFood,
        IReadOnlyList<Snake> snakes)
    {
        var eatenOwners = eatenFood.Select(food => food.OwnerPlayerId).ToHashSet();
        foreach (var snake in snakes)
        {
            if (eatenOwners.Contains(snake.PlayerId))
            {
                yield return snake.PlayerId;
            }
        }
    }

    private static IEnumerable<Cell> OccupiedCells(IEnumerable<Snake> snakes) =>
        snakes.SelectMany(snake => snake.Body);

    private static Cell Move(Cell cell, Direction direction) =>
        direction switch
        {
            Direction.Up => cell with { Y = cell.Y + 1 },
            Direction.Right => cell with { X = cell.X + 1 },
            Direction.Down => cell with { Y = cell.Y - 1 },
            Direction.Left => cell with { X = cell.X - 1 },
            _ => cell
        };

    private static Cell Wrap(Cell cell, GameRules rules) =>
        new(Mod(cell.X, rules.BoardWidth), Mod(cell.Y, rules.BoardHeight));

    private static int Mod(int value, int divisor) =>
        ((value % divisor) + divisor) % divisor;

    private static bool IsInsideBoard(Cell cell, GameRules rules) =>
        cell.X >= 0 && cell.X < rules.BoardWidth && cell.Y >= 0 && cell.Y < rules.BoardHeight;

    private static FoodItem SpawnFood(GameRules rules, Snake owner, IReadOnlySet<Cell> blockedCells, int cursor)
    {
        var area = rules.BoardWidth * rules.BoardHeight;
        for (var attempt = 0; attempt < area; attempt++)
        {
            var index = CandidateIndex(rules.FoodSeed, cursor, owner.PlayerId, attempt, area);
            var candidate = new Cell(index % rules.BoardWidth, index / rules.BoardWidth);
            if (!blockedCells.Contains(candidate))
            {
                return new FoodItem(owner.PlayerId, owner.Color, candidate);
            }
        }

        throw new InvalidOperationException("Cannot spawn food because the board is full.");
    }

    private static int CandidateIndex(int seed, int cursor, PlayerId owner, int attempt, int area)
    {
        unchecked
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;

            var hash = offset;
            Add(seed);
            Add(cursor);
            foreach (var character in owner.Value)
            {
                Add(character);
            }

            Add(attempt);
            return (int)(hash % (uint)area);

            void Add(int value)
            {
                hash ^= (uint)value;
                hash *= prime;
            }
        }
    }
}
