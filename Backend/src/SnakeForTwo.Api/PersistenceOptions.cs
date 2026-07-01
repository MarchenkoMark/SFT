namespace SnakeForTwo.Api;

internal sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    public bool Enabled { get; set; }

    public bool ApplyMigrationsOnStartup { get; set; } = true;
}
