namespace FraudWatch.Api.Models;

public record SummaryTotals
{
    public long TotalTransactions { get; init; }
    public long Declined { get; init; }
    public long ApprovedVolumeCents { get; init; }
    public long TotalCards { get; init; }
    public long TotalAlerts { get; init; }
    public long FlaggedCards { get; init; }
}

public record RuleCount
{
    public string Rule { get; init; } = "";
    public int Severity { get; init; }
    public long Count { get; init; }
}

public record DailyPoint
{
    public string Day { get; init; } = "";
    public long Transactions { get; init; }
    public long Declined { get; init; }
    public long VolumeCents { get; init; }
    public long RunningVolumeCents { get; init; }
    public long MovingAvg7dCents { get; init; }
}

public record TopRiskCard
{
    public long Rank { get; init; }
    public string CardId { get; init; } = "";
    public long Alerts { get; init; }
    public int RiskScore { get; init; }
    public string Rules { get; init; } = "";
}
