namespace FraudWatch.Api.Models;

public record IngestTransactionRequest(
    string CardId,
    string MerchantId,
    long AmountCents,
    string Currency,
    string Country,
    string Status,
    DateTimeOffset? OccurredAt);

public record TransactionDto
{
    public Guid Id { get; init; }
    public string CardId { get; init; } = "";
    public string MerchantId { get; init; } = "";
    public long AmountCents { get; init; }
    public string Currency { get; init; } = "";
    public string Country { get; init; } = "";
    public string Status { get; init; } = "";
    public DateTime OccurredAt { get; init; }
}
