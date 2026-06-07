namespace RecruiterAi.Infrastructure.Services;

// Pricing as of 2025 (USD per 1K tokens). Best-effort estimate for cost visibility in logs.
// Update when OpenAI changes pricing — single source of truth for all OpenAI services.
internal static class OpenAiPricing
{
    private static readonly Dictionary<string, (decimal InputPer1K, decimal OutputPer1K)> Prices = new()
    {
        ["gpt-4o-mini"] = (0.00015m,  0.0006m),
        ["gpt-4o"]      = (0.0025m,   0.010m),
        ["gpt-4-turbo"] = (0.010m,    0.030m),
    };

    public static decimal EstimateCost(string model, int inputTokens, int outputTokens)
    {
        if (!Prices.TryGetValue(model, out var p)) return 0m;
        return (inputTokens / 1000m) * p.InputPer1K + (outputTokens / 1000m) * p.OutputPer1K;
    }
}
