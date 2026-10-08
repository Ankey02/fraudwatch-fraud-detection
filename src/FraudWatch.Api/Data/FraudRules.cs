namespace FraudWatch.Api.Data;

public record FraudRule(string Name, int Severity, string Sql);

public static class FraudRules
{
    // Every rule: window functions look at neighbouring rows of the same card,
    // then matching rows are inserted into fraud_alerts.
    // ON CONFLICT DO NOTHING means scanning twice never creates duplicate alerts.
    public static readonly FraudRule[] All =
    [
        new("velocity", 2, """
            WITH scored AS (
              SELECT id, card_id, occurred_at,
                     COUNT(*) OVER (PARTITION BY card_id ORDER BY occurred_at
                       RANGE BETWEEN INTERVAL '10 minutes' PRECEDING AND CURRENT ROW) AS n
              FROM transactions)
            INSERT INTO fraud_alerts (id, transaction_id, card_id, rule, severity, detail)
            SELECT gen_random_uuid(), id, card_id, @rule, @severity, n || ' transactions in 10 minutes'
            FROM scored
            WHERE n >= 5 AND occurred_at >= now() - make_interval(days => @days)
            ON CONFLICT (transaction_id, rule) DO NOTHING
            """),

        new("unusual_amount", 2, """
            WITH scored AS (
              SELECT id, card_id, occurred_at, amount_cents,
                     COUNT(*) OVER w AS prior_n,
                     AVG(amount_cents) OVER w AS prior_avg
              FROM transactions
              WINDOW w AS (PARTITION BY card_id ORDER BY occurred_at
                           ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING))
            INSERT INTO fraud_alerts (id, transaction_id, card_id, rule, severity, detail)
            SELECT gen_random_uuid(), id, card_id, @rule, @severity,
                   'Amount ' || amount_cents || ' is ' || round(amount_cents / prior_avg, 1)
                   || 'x the card average of ' || round(prior_avg)
            FROM scored
            WHERE prior_n >= 5 AND amount_cents >= 50000 AND amount_cents >= 5 * prior_avg
              AND occurred_at >= now() - make_interval(days => @days)
            ON CONFLICT (transaction_id, rule) DO NOTHING
            """),

        new("duplicate", 1, """
            WITH scored AS (
              SELECT id, card_id, occurred_at,
                     occurred_at - LAG(occurred_at) OVER (
                       PARTITION BY card_id, merchant_id, amount_cents ORDER BY occurred_at) AS gap
              FROM transactions)
            INSERT INTO fraud_alerts (id, transaction_id, card_id, rule, severity, detail)
            SELECT gen_random_uuid(), id, card_id, @rule, @severity,
                   'Same card, merchant and amount ' || round(extract(epoch FROM gap))
                   || ' seconds after the previous one'
            FROM scored
            WHERE gap <= INTERVAL '2 minutes' AND occurred_at >= now() - make_interval(days => @days)
            ON CONFLICT (transaction_id, rule) DO NOTHING
            """),

        new("card_testing", 3, """
            WITH scored AS (
              SELECT id, card_id, occurred_at, status,
                     COUNT(*) FILTER (WHERE status = 'Declined' AND amount_cents <= 200) OVER (
                       PARTITION BY card_id ORDER BY occurred_at
                       RANGE BETWEEN INTERVAL '10 minutes' PRECEDING AND CURRENT ROW
                       EXCLUDE CURRENT ROW) AS tiny_declines
              FROM transactions)
            INSERT INTO fraud_alerts (id, transaction_id, card_id, rule, severity, detail)
            SELECT gen_random_uuid(), id, card_id, @rule, @severity,
                   tiny_declines || ' tiny declined attempts in the 10 minutes before this approval'
            FROM scored
            WHERE status = 'Approved' AND tiny_declines >= 3
              AND occurred_at >= now() - make_interval(days => @days)
            ON CONFLICT (transaction_id, rule) DO NOTHING
            """),

        new("impossible_travel", 3, """
            WITH scored AS (
              SELECT id, card_id, occurred_at, country,
                     LAG(country) OVER w AS prev_country,
                     occurred_at - LAG(occurred_at) OVER w AS gap
              FROM transactions
              WINDOW w AS (PARTITION BY card_id ORDER BY occurred_at))
            INSERT INTO fraud_alerts (id, transaction_id, card_id, rule, severity, detail)
            SELECT gen_random_uuid(), id, card_id, @rule, @severity,
                   prev_country || ' to ' || country || ' in '
                   || round(extract(epoch FROM gap) / 60) || ' minutes'
            FROM scored
            WHERE prev_country IS NOT NULL AND country <> prev_country AND gap <= INTERVAL '1 hour'
              AND occurred_at >= now() - make_interval(days => @days)
            ON CONFLICT (transaction_id, rule) DO NOTHING
            """),
    ];
}
