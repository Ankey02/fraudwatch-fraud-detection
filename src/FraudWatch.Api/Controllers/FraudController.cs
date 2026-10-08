using Dapper;
using FraudWatch.Api.Data;
using FraudWatch.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace FraudWatch.Api.Controllers;

[ApiController]
[Route("fraud")]
public class FraudController(Db db) : ControllerBase
{
    // Runs every rule over the last N days and stores new alerts.
    [HttpPost("scan")]
    public async Task<ActionResult<ScanResponse>> Scan([FromQuery] int days = 30, CancellationToken ct = default)
    {
        if (days is < 1 or > 365)
            return BadRequest(new ProblemDetails { Title = "days must be between 1 and 365" });

        await using var conn = await db.OpenAsync(ct);
        var results = new List<ScanRuleResult>();

        foreach (var rule in FraudRules.All)
        {
            var inserted = await conn.ExecuteAsync(new CommandDefinition(
                rule.Sql,
                new { rule = rule.Name, severity = rule.Severity, days },
                cancellationToken: ct));
            results.Add(new ScanRuleResult(rule.Name, rule.Severity, inserted));
        }

        return Ok(new ScanResponse(days, results.Sum(r => r.NewAlerts), results));
    }

    // Lists alerts, most severe first, optionally filtered.
    [HttpGet("alerts")]
    public async Task<ActionResult<IEnumerable<AlertDto>>> Alerts(
        [FromQuery] string? rule, [FromQuery] string? cardId,
        [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        if (rule is not null && !FraudRules.All.Any(r => r.Name == rule))
            return BadRequest(new ProblemDetails
            {
                Title = "Unknown rule",
                Detail = "Valid rules: " + string.Join(", ", FraudRules.All.Select(r => r.Name))
            });

        limit = Math.Clamp(limit, 1, 200);

        const string sql = """
            SELECT a.id AS "Id", a.transaction_id AS "TransactionId", a.card_id AS "CardId",
                   a.rule AS "Rule", a.severity AS "Severity", a.detail AS "Detail",
                   t.amount_cents AS "AmountCents", t.occurred_at AS "OccurredAt",
                   a.detected_at AS "DetectedAt"
            FROM fraud_alerts a
            JOIN transactions t ON t.id = a.transaction_id
            WHERE (@rule::text IS NULL OR a.rule = @rule)
              AND (@cardId::text IS NULL OR a.card_id = @cardId)
            ORDER BY a.severity DESC, t.occurred_at DESC
            LIMIT @limit
            """;

        await using var conn = await db.OpenAsync(ct);
        var alerts = await conn.QueryAsync<AlertDto>(
            new CommandDefinition(sql, new { rule, cardId, limit }, cancellationToken: ct));
        return Ok(alerts);
    }
}
