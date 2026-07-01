using Microsoft.EntityFrameworkCore;

namespace SnakeForTwo.Infrastructure.Persistence;

public sealed class SnakeForTwoDbContext(DbContextOptions<SnakeForTwoDbContext> options) : DbContext(options)
{
    public DbSet<UserAccountEntity> UserAccounts => Set<UserAccountEntity>();

    public DbSet<UserLoginEntity> UserLogins => Set<UserLoginEntity>();

    public DbSet<MatchSummaryEntity> MatchSummaries => Set<MatchSummaryEntity>();

    public DbSet<MatchParticipantEntity> MatchParticipants => Set<MatchParticipantEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<UserAccountEntity>();
        user.ToTable("user_accounts");
        user.HasKey(entity => entity.Id);
        user.Property(entity => entity.Id).HasColumnName("id");
        user.Property(entity => entity.Username).HasColumnName("username").HasMaxLength(24).IsRequired();
        user.Property(entity => entity.NormalizedUsername)
            .HasColumnName("normalized_username")
            .HasMaxLength(24)
            .IsRequired();
        user.Property(entity => entity.Email).HasColumnName("email").HasMaxLength(320);
        user.Property(entity => entity.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(320);
        user.Property(entity => entity.PictureUrl).HasColumnName("picture_url").HasMaxLength(2048);
        user.Property(entity => entity.HasCustomUsername)
            .HasColumnName("has_custom_username")
            .IsRequired();
        user.Property(entity => entity.CreatedAt).HasColumnName("created_at").IsRequired();
        user.Property(entity => entity.UpdatedAt).HasColumnName("updated_at").IsRequired();
        user.Property(entity => entity.LastSignedInAt).HasColumnName("last_signed_in_at").IsRequired();
        user.HasIndex(entity => entity.NormalizedUsername).IsUnique();
        user.HasIndex(entity => entity.NormalizedEmail);
        user.HasMany(entity => entity.Logins)
            .WithOne(entity => entity.User)
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        var login = modelBuilder.Entity<UserLoginEntity>();
        login.ToTable("user_logins");
        login.HasKey(entity => entity.Id);
        login.Property(entity => entity.Id).HasColumnName("id");
        login.Property(entity => entity.UserId).HasColumnName("user_id").IsRequired();
        login.Property(entity => entity.Provider).HasColumnName("provider").HasMaxLength(64).IsRequired();
        login.Property(entity => entity.ProviderUserId)
            .HasColumnName("provider_user_id")
            .HasMaxLength(256)
            .IsRequired();
        login.Property(entity => entity.Email).HasColumnName("email").HasMaxLength(320);
        login.Property(entity => entity.DisplayName).HasColumnName("display_name").HasMaxLength(256);
        login.Property(entity => entity.PictureUrl).HasColumnName("picture_url").HasMaxLength(2048);
        login.Property(entity => entity.CreatedAt).HasColumnName("created_at").IsRequired();
        login.Property(entity => entity.LastSignedInAt).HasColumnName("last_signed_in_at").IsRequired();
        login.HasIndex(entity => new { entity.Provider, entity.ProviderUserId }).IsUnique();
        login.HasIndex(entity => entity.UserId);

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
        participant.Property(entity => entity.UserId).HasColumnName("user_id");
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
        participant.HasIndex(entity => entity.UserId);
        participant.HasIndex(entity => entity.Score);
        participant.HasIndex(entity => new { entity.MatchSummaryId, entity.PlayerId }).IsUnique();
        participant.HasOne(entity => entity.User)
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class UserAccountEntity
{
    public Guid Id { get; set; }

    public string Username { get; set; } = "";

    public string NormalizedUsername { get; set; } = "";

    public string? Email { get; set; }

    public string? NormalizedEmail { get; set; }

    public string? PictureUrl { get; set; }

    public bool HasCustomUsername { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset LastSignedInAt { get; set; }

    public List<UserLoginEntity> Logins { get; set; } = [];
}

public sealed class UserLoginEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public UserAccountEntity? User { get; set; }

    public string Provider { get; set; } = "";

    public string ProviderUserId { get; set; } = "";

    public string? Email { get; set; }

    public string? DisplayName { get; set; }

    public string? PictureUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSignedInAt { get; set; }
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

    public Guid? UserId { get; set; }

    public UserAccountEntity? User { get; set; }

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
