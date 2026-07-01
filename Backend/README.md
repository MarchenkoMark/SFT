# SnakeForTwo Backend

Draft architecture and placeholder .NET 10 solution for a WebSocket-driven two-player Snake backend.

Start here:

- [Architecture](docs/architecture.md)
- [Frontend Architecture](docs/frontend-architecture.md)

Current project layout:

- `src/SnakeForTwo.Api`
- `src/SnakeForTwo.Contracts`
- `src/SnakeForTwo.Game.Domain`
- `src/SnakeForTwo.Game.Application`
- `src/SnakeForTwo.Infrastructure`
- `tests/SnakeForTwo.Game.Domain.Tests`
- `tests/SnakeForTwo.Game.Application.Tests`

Confirmed POC decisions:

- No Kafka for now.
- Single backend instance first; keep room ownership compatible with future multi-server deployment.
- Anonymous room tokens, no authentication.
- Co-op rules: one player losing means the team loses.
- Walls wrap around the board.
- Server sends full authoritative state every tick.
- Initial timing is 2 tile ticks per second and 5 client-side animation frames per tile.

## Phase 2 persistence

Persistence is PostgreSQL-backed and intentionally outside the hot gameplay loop.
Live rooms and rollback state stay in memory; the backend writes one durable match
summary after a server-authoritative game finish.

Local Docker setup from the repository root:

```powershell
docker compose -f compose.prod.yml -f compose.local.yml up --build
```

This starts PostgreSQL, exposes it on `localhost:5432`, and runs EF Core
migrations on backend startup. The default local connection values are:

- database: `snakefortwo`
- user: `snakefortwo`
- password: `snakefortwo_dev_password`
- connection string: `Host=localhost;Port=5432;Database=snakefortwo;Username=snakefortwo;Password=snakefortwo_dev_password`

If you prefer a native PostgreSQL install, create the same database/user and run
the API with:

```powershell
$env:Persistence__Enabled='true'
$env:ConnectionStrings__SnakeForTwo='Host=localhost;Port=5432;Database=snakefortwo;Username=snakefortwo;Password=snakefortwo_dev_password'
dotnet run --project src/SnakeForTwo.Api/SnakeForTwo.Api.csproj
```

Read endpoints:

- `GET /leaderboard?window=daily|monthly|all-time&limit=50`
- `GET /matches?limit=50`
- `GET /matches/{matchId}`

Writes are backend-owned only. During Phase 2, each player gets a temporary UUID
and optional display name; Phase 3 accounts can replace that identity link.
