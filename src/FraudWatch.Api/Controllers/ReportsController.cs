using Dapper;
using FraudWatch.Api.Data;
using FraudWatch.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace FraudWatch.Api.Controllers;

[ApiController]
[Route("reports")]
public class ReportsController(Db db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] int days = 30, CancellationToken ct = default)
    {
        if (days is < 1 or > 365) return BadRequest(new ProblemDetails { Title = "days must be between 1 and 365" });

        const string totalsSql = """
            SELECT count(*) AS "TotalTransactions",
                   count(*) FILTER (WHERE status = 'Declined') AS "Declined",
                   coalesce(sum(amount_cents) FILTER (WHERE status = 'Approved'), 0)::bigint AS "ApprovedVolumeCents",
                   count(DISTINCT card_id) AS "TotalCards",
                   (SELECT count(*) FROM fraud_alerts) AS "TotalAlerts",
                   (SELECT count(DISTINCT card_id) FROM fraud_alerts) AS "FlaggedCards"
            FROM transactions
            WHERE occurred_at >= now() - make_interval(days => @days)
            """;

        const string byRuleSql = """
            SELECT rule AS "Rule", severity AS "Severity", count(*) AS "Count"
            FROM fraud_alerts
            GROUP BY rule, severity
            ORDER BY severity DESC, count(*) DESC, rule
            """;

        await using var conn = await db.OpenAsync(ct);
        var t = await conn.QuerySingleAsync<SummaryTotals>(new CommandDefinition(totalsSql, new { days }, cancellationToken: ct));
        var rules = (await conn.QueryAsync<RuleCount>(new CommandDefinition(byRuleSql, cancellationToken: ct))).ToList();

        return Ok(new
        {
            t.TotalTransactions, t.Declined, t.ApprovedVolumeCents,
            t.TotalCards, t.TotalAlerts, t.FlaggedCards,
            AlertsByRule = rules
        });
    }

    [HttpGet("daily")]
    public async Task<ActionResult<IEnumerable<DailyPoint>>> Daily([FromQuery] int days = 14, CancellationToken ct = default)
    {
        if (days is < 1 or > 365) return BadRequest(new ProblemDetails { Title = "days must be between 1 and 365" });

        const string sql = """
            WITH d AS (
              SELECT (occurred_at AT TIME ZONE 'UTC')::date AS day,
                     count(*) AS txns,
                     count(*) FILTER (WHERE status = 'Declined') AS declined,
                     coalesce(sum(amount_cents) FILTER (WHERE status = 'Approved'), 0) AS volume
              FROM transactions
              WHERE occurred_at >= now() - make_interval(days => @days)
              GROUP BY 1)
            SELECT to_char(day, 'YYYY-MM-DD') AS "Day",
                   txns AS "Transactions",
                   declined AS "Declined",
                   volume::bigint AS "VolumeCents",
                   (SUM(volume) OVER (ORDER BY day))::bigint AS "RunningVolumeCents",
                   (AVG(volume) OVER (ORDER BY day ROWS BETWEEN 6 PRECEDING AND CURRENT ROW))::bigint AS "MovingAvg7dCents"
            FROM d
            ORDER BY day DESC
            """;

        await using var conn = await db.OpenAsync(ct);
        return Ok(await conn.QueryAsync<DailyPoint>(new CommandDefinition(sql, new { days }, cancellationToken: ct)));
    }

    [HttpGet("top-risk-cards")]
    public async Task<ActionResult<IEnumerable<TopRiskCard>>> TopRiskCards([FromQuery] int limit = 10, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);

        const string sql = """
            SELECT RANK() OVER (ORDER BY sum(severity) DESC) AS "Rank",
                   card_id AS "CardId",
                   count(*) AS "Alerts",
                   sum(severity)::int AS "RiskScore",
                   string_agg(DISTINCT rule, ', ' ORDER BY rule) AS "Rules"
            FROM fraud_alerts
            GROUP BY card_id
            ORDER BY "RiskScore" DESC, card_id
            LIMIT @limit
            """;

        await using var conn = await db.OpenAsync(ct);
        return Ok(await conn.QueryAsync<TopRiskCard>(new CommandDefinition(sql, new { limit }, cancellationToken: ct)));
    }
}
