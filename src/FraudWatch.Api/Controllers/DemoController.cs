using Dapper;
using FraudWatch.Api.Data;
using Microsoft.AspNetCore.Mvc;

namespace FraudWatch.Api.Controllers;

// Development only. Replaces ALL transactions and alerts with fresh fake data.
[ApiController]
[Route("demo")]
public class DemoController(Db db, IWebHostEnvironment env) : ControllerBase
{
    [HttpPost("seed")]
    public async Task<IActionResult> Seed(int count = 20000, CancellationToken ct = default)
    {
        if (!env.IsDevelopment()) return NotFound();
        if (count is < 1000 or > 200000)
            return Problem(detail: "count must be between 1000 and 200000.", statusCode: StatusCodes.Status400BadRequest);

        await using var connection = await db.OpenAsync(ct);
        await using (var tx = await connection.BeginTransactionAsync(ct))
        {
            await connection.ExecuteAsync("SELECT setseed(0.42)", transaction: tx); // same fake data every time
            await connection.ExecuteAsync("TRUNCATE fraud_alerts, transactions", transaction: tx);
            await connection.ExecuteAsync(DemoData.RegularTrafficSql, new { count }, tx);
            await connection.ExecuteAsync(DemoData.PlantedFraudSql, transaction: tx);
            await tx.CommitAsync(ct);
        }

        var total = await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM transactions");

        return Ok(new
        {
            totalTransactions = total,
            plantedFraud = new
            {
                card_9001 = "velocity: 8 purchases in under 6 minutes",
                card_9002 = "unusual amount: one $4,800 purchase after small ones",
                card_9003 = "duplicate attempts: same merchant and amount 3 times in 80 seconds",
                card_9004 = "card testing: 6 tiny declined attempts, then a large approved one",
                card_9005 = "impossible travel: US then India 30 minutes later"
            }
        });
    }
}
