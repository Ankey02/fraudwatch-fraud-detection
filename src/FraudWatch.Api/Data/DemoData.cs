namespace FraudWatch.Api.Data;

public static class DemoData
{
    // Normal customers: 300 cards, each with a home country, spread over the last 30 days.
    // Amounts are mostly small ($5 to $155), and a card never changes country, so nothing here should look like fraud.
    public const string RegularTrafficSql = """
        WITH base AS MATERIALIZED (
            SELECT 1 + floor(random() * 300)::int AS c,
                   1 + floor(random() * 40)::int  AS m,
                   random() AS r1, random() AS r2, random() AS r3
            FROM generate_series(1, @count)
        )
        INSERT INTO transactions (id, card_id, merchant_id, amount_cents, currency, country, status, occurred_at)
        SELECT gen_random_uuid(),
               'card_' || lpad(c::text, 4, '0'),
               'merch_' || lpad(m::text, 3, '0'),
               (500 + floor(r1 * r1 * 15000))::bigint,
               'USD',
               (ARRAY['US', 'GB', 'IN', 'DE', 'CA'])[1 + (c % 5)],
               CASE WHEN r2 < 0.03 THEN 'Declined' ELSE 'Approved' END,
               now() - (r3 * interval '30 days')
        FROM base
        """;

    // Five cards that behave badly, one pattern each. Times are relative to "one day ago".
    public const string PlantedFraudSql = """
        WITH t0 AS (SELECT now() - interval '1 day' AS t)
        INSERT INTO transactions (id, card_id, merchant_id, amount_cents, currency, country, status, occurred_at)

        -- card_9001  VELOCITY: 8 purchases in under 6 minutes
        SELECT gen_random_uuid(), 'card_9001', 'merch_' || lpad((1 + g)::text, 3, '0'),
               2000 + g * 300, 'USD', 'US', 'Approved', t0.t + g * interval '50 seconds'
        FROM t0, generate_series(0, 7) g

        UNION ALL
        -- card_9002  UNUSUAL AMOUNT: 15 days of small purchases, then one $4,800 purchase
        SELECT gen_random_uuid(), 'card_9002', 'merch_005',
               2000 + g * 150, 'USD', 'US', 'Approved', t0.t - (g + 1) * interval '1 day'
        FROM t0, generate_series(0, 14) g
        UNION ALL
        SELECT gen_random_uuid(), 'card_9002', 'merch_006',
               480000, 'USD', 'US', 'Approved', t0.t + interval '1 hour'
        FROM t0

        UNION ALL
        -- card_9003  DUPLICATES: the same merchant and amount 3 times in 80 seconds
        SELECT gen_random_uuid(), 'card_9003', 'merch_007',
               12999, 'USD', 'US', 'Approved', t0.t + g * interval '40 seconds'
        FROM t0, generate_series(0, 2) g

        UNION ALL
        -- card_9004  CARD TESTING: six tiny declined attempts, then a big approved one
        SELECT gen_random_uuid(), 'card_9004', 'merch_' || lpad((10 + g)::text, 3, '0'),
               100, 'USD', 'US', 'Declined', t0.t + g * interval '45 seconds'
        FROM t0, generate_series(0, 5) g
        UNION ALL
        SELECT gen_random_uuid(), 'card_9004', 'merch_020',
               90000, 'USD', 'US', 'Approved', t0.t + interval '5 minutes'
        FROM t0

        UNION ALL
        -- card_9005  IMPOSSIBLE TRAVEL: the US, then India 30 minutes later
        SELECT gen_random_uuid(), 'card_9005', 'merch_030',
               4500, 'USD', 'US', 'Approved', t0.t
        FROM t0
        UNION ALL
        SELECT gen_random_uuid(), 'card_9005', 'merch_031',
               6000, 'USD', 'IN', 'Approved', t0.t + interval '30 minutes'
        FROM t0
        """;
}
