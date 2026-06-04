namespace RecruiterAi.Domain.Entities;

public class Position
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string? SeniorityLevel { get; set; }

    /// <summary>
    /// Required skills / requirements — stored as a JSON array of strings.
    /// </summary>
    public List<string> RequiredSkills { get; set; } = [];

    /// <summary>
    /// Nice-to-have skills — stored as a JSON array of strings.
    /// </summary>
    public List<string> NiceToHaveSkills { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Evaluation> Evaluations { get; set; } = [];
    public ICollection<CvGenerationBatch> GenerationBatches { get; set; } = [];
}
