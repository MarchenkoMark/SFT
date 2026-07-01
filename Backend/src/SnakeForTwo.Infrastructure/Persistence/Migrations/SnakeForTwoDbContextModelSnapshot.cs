#nullable disable

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace SnakeForTwo.Infrastructure.Persistence.Migrations;

[DbContext(typeof(SnakeForTwoDbContext))]
partial class SnakeForTwoDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");

        modelBuilder.Entity("SnakeForTwo.Infrastructure.Persistence.MatchParticipantEntity", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uuid")
                .HasColumnName("id");

            b.Property<bool>("Alive")
                .HasColumnType("boolean")
                .HasColumnName("alive");

            b.Property<string>("DisplayName")
                .HasMaxLength(80)
                .HasColumnType("character varying(80)")
                .HasColumnName("display_name");

            b.Property<int>("FinalLength")
                .HasColumnType("integer")
                .HasColumnName("final_length");

            b.Property<int>("FoodEatenByTeammates")
                .HasColumnType("integer")
                .HasColumnName("food_eaten_by_teammates");

            b.Property<Guid>("MatchSummaryId")
                .HasColumnType("uuid")
                .HasColumnName("match_summary_id");

            b.Property<int>("OwnFoodEaten")
                .HasColumnType("integer")
                .HasColumnName("own_food_eaten");

            b.Property<string>("PlayerId")
                .IsRequired()
                .HasMaxLength(128)
                .HasColumnType("character varying(128)")
                .HasColumnName("player_id");

            b.Property<long>("Score")
                .HasColumnType("bigint")
                .HasColumnName("score");

            b.Property<int>("Seat")
                .HasColumnType("integer")
                .HasColumnName("seat");

            b.Property<int>("TeammateFoodEaten")
                .HasColumnType("integer")
                .HasColumnName("teammate_food_eaten");

            b.Property<Guid>("TemporaryUserId")
                .HasColumnType("uuid")
                .HasColumnName("temporary_user_id");

            b.HasKey("Id");

            b.HasIndex("Score");

            b.HasIndex("TemporaryUserId");

            b.HasIndex("MatchSummaryId", "PlayerId")
                .IsUnique();

            b.ToTable("match_participants", (string)null);
        });

        modelBuilder.Entity("SnakeForTwo.Infrastructure.Persistence.MatchSummaryEntity", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uuid")
                .HasColumnName("id");

            b.Property<int>("BoardHeight")
                .HasColumnType("integer")
                .HasColumnName("board_height");

            b.Property<int>("BoardWidth")
                .HasColumnType("integer")
                .HasColumnName("board_width");

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("created_at");

            b.Property<long>("DurationMs")
                .HasColumnType("bigint")
                .HasColumnName("duration_ms");

            b.Property<long>("DurationTicks")
                .HasColumnType("bigint")
                .HasColumnName("duration_ticks");

            b.Property<string>("FinalStateHash")
                .IsRequired()
                .HasMaxLength(128)
                .HasColumnType("character varying(128)")
                .HasColumnName("final_state_hash");

            b.Property<DateTimeOffset>("FinishedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("finished_at");

            b.Property<string>("MatchId")
                .IsRequired()
                .HasMaxLength(128)
                .HasColumnType("character varying(128)")
                .HasColumnName("match_id");

            b.Property<string>("Mode")
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnType("character varying(64)")
                .HasColumnName("mode");

            b.Property<int>("PlayerCount")
                .HasColumnType("integer")
                .HasColumnName("player_count");

            b.Property<string>("Reason")
                .IsRequired()
                .HasMaxLength(128)
                .HasColumnType("character varying(128)")
                .HasColumnName("reason");

            b.Property<string>("Result")
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnType("character varying(64)")
                .HasColumnName("result");

            b.Property<string>("RoomId")
                .IsRequired()
                .HasMaxLength(128)
                .HasColumnType("character varying(128)")
                .HasColumnName("room_id");

            b.Property<int>("Seed")
                .HasColumnType("integer")
                .HasColumnName("seed");

            b.Property<DateTimeOffset>("StartedAt")
                .HasColumnType("timestamp with time zone")
                .HasColumnName("started_at");

            b.HasKey("Id");

            b.HasIndex("FinishedAt");

            b.HasIndex("MatchId")
                .IsUnique();

            b.ToTable("match_summaries", (string)null);
        });

        modelBuilder.Entity("SnakeForTwo.Infrastructure.Persistence.MatchParticipantEntity", b =>
        {
            b.HasOne("SnakeForTwo.Infrastructure.Persistence.MatchSummaryEntity", "Match")
                .WithMany("Participants")
                .HasForeignKey("MatchSummaryId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Match");
        });

        modelBuilder.Entity("SnakeForTwo.Infrastructure.Persistence.MatchSummaryEntity", b =>
        {
            b.Navigation("Participants");
        });
    }
}
