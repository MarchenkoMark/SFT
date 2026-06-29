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
