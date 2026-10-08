using Dapper;
using FraudWatch.Api.Data;
using FraudWatch.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace FraudWatch.Api.Controllers;

[ApiController]
[Route("transactions")]
public class TransactionsController(Db db) : ControllerBase
{
    private const string SelectSql = """
        SELECT id AS "Id", card_id AS "CardId", merchant_id AS "MerchantId", amount_cents AS "AmountCents",
               currency AS "Currency", country AS "Country", status AS "Status", occurred_at AS "OccurredAt"
        FROM transactions
        """;

    [HttpPost]
    public async Task<IActionResult> Create(IngestTransactionRequest request, CancellationToken ct)
    {
        var error = Validate(request);
        if (error is not null) return Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);

        var id = Guid.NewGuid();
        var occurredAt = (request.OccurredAt ?? DateTimeOffset.UtcNow).ToUniversalTime(); // PostgreSQL needs UTC
        var status = request.Status.Trim().Equals("Declined", StringComparison.OrdinalIgnoreCase)
            ? "Declined"
            : "Approved";

        await using var connection = await db.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO transactions (id, card_id, merchant_id, amount_cents, currency, country, status, occurred_at)
            VALUES (@id, @cardId, @merchantId, @amountCents, @currency, @country, @status, @occurredAt)
            """,
            new
            {
                id,
                cardId = request.CardId.Trim(),
                merchantId = request.MerchantId.Trim(),
                amountCents = request.AmountCents,
                currency = request.Currency.Trim().ToUpperInvariant(),
                country = request.Country.Trim().ToUpperInvariant(),
                status,
                occurredAt
            },
            cancellationToken: ct));

        var created = await connection.QuerySingleAsync<TransactionDto>(
            new CommandDefinition(SelectSql + " WHERE id = @id", new { id }, cancellationToken: ct));

        return CreatedAtAction(nameof(Get), new { id }, created);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(ct);
        var item = await connection.QuerySingleOrDefaultAsync<TransactionDto>(
            new CommandDefinition(SelectSql + " WHERE id = @id", new { id }, cancellationToken: ct));

        return item is null ? NotFound() : Ok(item);
    }

    // Newest first. Optional filter: ?cardId=card_9001
    [HttpGet]
    public async Task<IActionResult> List(string? cardId, int limit = 50, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        var card = string.IsNullOrWhiteSpace(cardId) ? null : cardId.Trim();

        await using var connection = await db.OpenAsync(ct);
        var items = await connection.QueryAsync<TransactionDto>(new CommandDefinition(
            SelectSql + " WHERE (@card::text IS NULL OR card_id = @card) ORDER BY occurred_at DESC LIMIT @limit",
            new { card, limit },
            cancellationToken: ct));

        return Ok(items);
    }

    private static string? Validate(IngestTransactionRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.CardId) || r.CardId.Trim().Length > 64)
            return "CardId is required (up to 64 characters).";
        if (string.IsNullOrWhiteSpace(r.MerchantId) || r.MerchantId.Trim().Length > 64)
            return "MerchantId is required (up to 64 characters).";
        if (r.AmountCents <= 0)
            return "AmountCents must be greater than zero.";
        if (string.IsNullOrWhiteSpace(r.Currency) || r.Currency.Trim().Length != 3)
            return "Currency must be a 3-letter code, for example USD.";
        if (string.IsNullOrWhiteSpace(r.Country) || r.Country.Trim().Length != 2)
            return "Country must be a 2-letter code, for example US.";
        if (!r.Status.Trim().Equals("Approved", StringComparison.OrdinalIgnoreCase) &&
            !r.Status.Trim().Equals("Declined", StringComparison.OrdinalIgnoreCase))
            return "Status must be Approved or Declined.";
        if (r.OccurredAt > DateTimeOffset.UtcNow.AddMinutes(5))
            return "OccurredAt cannot be in the future.";

        return null;
    }
}
