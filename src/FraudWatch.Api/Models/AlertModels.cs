namespace FraudWatch.Api.Models;

public record AlertDto
{
    public Guid Id { get; init; }
    public Guid TransactionId { get; init; }
    public string CardId { get; init; } = "";
    public string Rule { get; init; } = "";
    public int Severity { get; init; }
    public string Detail { get; init; } = "";
    public long AmountCents { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public DateTimeOffset DetectedAt { get; init; }
}

public record ScanRuleResult(string Rule, int Severity, int NewAlerts);

public record ScanResponse(int Days, int TotalNewAlerts, List<ScanRuleResult> Rules);
