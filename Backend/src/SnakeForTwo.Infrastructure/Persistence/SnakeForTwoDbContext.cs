using Microsoft.EntityFrameworkCore;

namespace SnakeForTwo.Infrastructure.Persistence;

public sealed class SnakeForTwoDbContext(DbContextOptions<SnakeForTwoDbContext> options) : DbContext(options)
{
    public DbSet<MatchSummaryEntity> MatchSummaries => Set<MatchSummaryEntity>();

    public DbSet<MatchParticipantEntity> MatchParticipants => Set<MatchParticipantEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var match = modelBuilder.Entity<MatchSummaryEntity>();
        match.ToTable("match_summaries");
        match.HasKey(entity => entity.Id);
        match.Property(entity => entity.Id).HasColumnName("id");
        match.Property(entity => entity.MatchId).HasColumnName("match_id").HasMaxLength(128).IsRequired();
        match.Property(entity => entity.RoomId).HasColumnName("room_id").HasMaxLength(128).IsRequired();
        match.Property(entity => entity.Mode).HasColumnName("mode").HasMaxLength(64).IsRequired();
        match.Property(entity => entity.StartedAt).HasColumnName("started_at").IsRequired();
        match.Property(entity => entity.FinishedAt).HasColumnName("finished_at").IsRequired();
        match.Property(entity => entity.DurationTicks).HasColumnName("duration_ticks").IsRequired();
        match.Property(entity => entity.DurationMs).HasColumnName("duration_ms").IsRequired();
        match.Property(entity => entity.Result).HasColumnName("result").HasMaxLength(64).IsRequired();
        match.Property(entity => entity.Reason).HasColumnName("reason").HasMaxLength(128).IsRequired();
        match.Property(entity => entity.PlayerCount).HasColumnName("player_count").IsRequired();
        match.Property(entity => entity.BoardWidth).HasColumnName("board_width").IsRequired();
        match.Property(entity => entity.BoardHeight).HasColumnName("board_height").IsRequired();
        match.Property(entity => entity.Seed).HasColumnName("seed").IsRequired();
        match.Property(entity => entity.FinalStateHash)
            .HasColumnName("final_state_hash")
            .HasMaxLength(128)
            .IsRequired();
        match.Property(entity => entity.CreatedAt).HasColumnName("created_at").IsRequired();
        match.HasIndex(entity => entity.MatchId).IsUnique();
        match.HasIndex(entity => entity.FinishedAt);
        match.HasMany(entity => entity.Participants)
            .WithOne(entity => entity.Match)
            .HasForeignKey(entity => entity.MatchSummaryId)
            .OnDelete(DeleteBehavior.Cascade);

        var participant = modelBuilder.Entity<MatchParticipantEntity>();
        participant.ToTable("match_participants");
        participant.HasKey(entity => entity.Id);
        participant.Property(entity => entity.Id).HasColumnName("id");
        participant.Property(entity => entity.MatchSummaryId).HasColumnName("match_summary_id");
        participant.Property(entity => entity.TemporaryUserId).HasColumnName("temporary_user_id").IsRequired();
        participant.Property(entity => entity.PlayerId).HasColumnName("player_id").HasMaxLength(128).IsRequired();
        participant.Property(entity => entity.DisplayName).HasColumnName("display_name").HasMaxLength(80);
        participant.Property(entity => entity.Seat).HasColumnName("seat").IsRequired();
        participant.Property(entity => entity.Alive).HasColumnName("alive").IsRequired();
        participant.Property(entity => entity.FinalLength).HasColumnName("final_length").IsRequired();
        participant.Property(entity => entity.OwnFoodEaten).HasColumnName("own_food_eaten").IsRequired();
        participant.Property(entity => entity.TeammateFoodEaten).HasColumnName("teammate_food_eaten").IsRequired();
        participant.Property(entity => entity.FoodEatenByTeammates).HasColumnName("food_eaten_by_teammates").IsRequired();
        participant.Property(entity => entity.Score).HasColumnName("score").IsRequired();
        participant.HasIndex(entity => entity.TemporaryUserId);
        participant.HasIndex(entity => entity.Score);
        participant.HasIndex(entity => new { entity.MatchSummaryId, entity.PlayerId }).IsUnique();
    }
}

public sealed class MatchSummaryEntity
{
    public Guid Id { get; set; }

    public string MatchId { get; set; } = "";

    public string RoomId { get; set; } = "";

    public string Mode { get; set; } = "";

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset FinishedAt { get; set; }

    public long DurationTicks { get; set; }

    public long DurationMs { get; set; }

    public string Result { get; set; } = "";

    public string Reason { get; set; } = "";

    public int PlayerCount { get; set; }

    public int BoardWidth { get; set; }

    public int BoardHeight { get; set; }

    public int Seed { get; set; }

    public string FinalStateHash { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    public List<MatchParticipantEntity> Participants { get; set; } = [];
}

public sealed class MatchParticipantEntity
{
    public Guid Id { get; set; }

    public Guid MatchSummaryId { get; set; }

    public MatchSummaryEntity? Match { get; set; }

    public Guid TemporaryUserId { get; set; }

    public string PlayerId { get; set; } = "";

    public string? DisplayName { get; set; }

    public int Seat { get; set; }

    public bool Alive { get; set; }

    public int FinalLength { get; set; }

    public int OwnFoodEaten { get; set; }

    public int TeammateFoodEaten { get; set; }

    public int FoodEatenByTeammates { get; set; }

    public long Score { get; set; }
}
