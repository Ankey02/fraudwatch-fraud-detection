namespace FraudWatch.Api.Data;

public static class Schema
{
    // Safe to run on every start: IF NOT EXISTS means it only creates what is missing.
    public const string Sql = """
        CREATE TABLE IF NOT EXISTS transactions (
            id            uuid PRIMARY KEY,
            card_id       text        NOT NULL,
            merchant_id   text        NOT NULL,
            amount_cents  bigint      NOT NULL CHECK (amount_cents > 0),
            currency      char(3)     NOT NULL,
            country       char(2)     NOT NULL,
            status        text        NOT NULL CHECK (status IN ('Approved', 'Declined')),
            occurred_at   timestamptz NOT NULL,
            created_at    timestamptz NOT NULL DEFAULT now()
        );

        -- The fraud rules look at "this card's transactions in time order" all the time.
        CREATE INDEX IF NOT EXISTS ix_transactions_card_time ON transactions (card_id, occurred_at);
        CREATE INDEX IF NOT EXISTS ix_transactions_time ON transactions (occurred_at);
        CREATE INDEX IF NOT EXISTS ix_transactions_merchant_time ON transactions (merchant_id, occurred_at);

        CREATE TABLE IF NOT EXISTS fraud_alerts (
            id             uuid PRIMARY KEY,
            transaction_id uuid        NOT NULL REFERENCES transactions (id) ON DELETE CASCADE,
            card_id        text        NOT NULL,
            rule           text        NOT NULL,
            severity       int         NOT NULL,
            detail         text        NOT NULL,
            detected_at    timestamptz NOT NULL DEFAULT now(),
            UNIQUE (transaction_id, rule)
        );

        CREATE INDEX IF NOT EXISTS ix_alerts_rule ON fraud_alerts (rule);
        """;
}
