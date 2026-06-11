namespace Dignite.DocumentAI;

public static class DocumentAIDbProperties
{
    public static string DbTablePrefix { get; set; } = "DocAI";

    public static string? DbSchema { get; set; } = null;

    public const string ConnectionStringName = "DocumentAI";
}
