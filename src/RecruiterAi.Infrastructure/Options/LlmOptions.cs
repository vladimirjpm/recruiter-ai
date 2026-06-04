namespace RecruiterAi.Infrastructure.Options;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    public string Provider { get; set; } = "OpenAI";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
}
