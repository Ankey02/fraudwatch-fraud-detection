# FraudWatch

A fraud detection and reporting service built with C# / .NET 9 and PostgreSQL. It ingests card transactions, flags suspicious patterns with SQL window functions, and shows the results on a dashboard served by the API itself.

## What it does

- **Ingests transactions** through a REST API (`POST /transactions`, `GET /transactions`).
- **Detects fraud with 5 rules**, each written as a single SQL query using window functions (`COUNT/AVG/LAG ... OVER`), CTEs and `FILTER`.
- **Reports** on volume, decline rate, alerts by rule, a 7-day moving average and a risk ranking of cards (`RANK()`).
- **Dashboard** at `/` with no frontend build step (plain HTML and JavaScript).
- **Idempotent scans**: running a scan twice never creates duplicate alerts (`UNIQUE (transaction_id, rule)` plus `ON CONFLICT DO NOTHING`).

## Fraud rules

| Rule | Severity | Flags a transaction when... |
|---|---|---|
| velocity | Medium | the card has 5 or more transactions within 10 minutes |
| unusual_amount | Medium | the amount is at least 5x the card's average and at least 50,000 cents, with 5+ earlier transactions |
| duplicate | Low | same card, merchant and amount repeated within 2 minutes |
| card_testing | High | an approval follows 3+ tiny (200 cents or less) declined attempts in the previous 10 minutes |
| impossible_travel | High | the country changes within 1 hour on the same card |

## Quick start

Requires Docker Desktop.

    docker compose up -d --build

Then open http://localhost:5100/ and click **Seed demo data + scan**.
The demo seed creates 20,036 synthetic transactions: 20,000 random ones plus 36 planted fraud cases. The scan then finds 13 alerts on 5 cards.

API docs (Scalar): http://localhost:5100/scalar/v1

## API

| Method | Path | Purpose |
|---|---|---|
| POST | /transactions | Ingest a transaction |
| GET | /transactions?cardId=&limit= | List transactions |
| POST | /fraud/scan?days=30 | Run all rules, store new alerts |
| GET | /fraud/alerts?rule=&cardId=&limit= | List alerts, most severe first |
| GET | /reports/summary?days=30 | Totals and alerts by rule |
| GET | /reports/daily?days=14 | Daily volume, running total, 7-day moving average |
| GET | /reports/top-risk-cards?limit=10 | Cards ranked by risk score |
| POST | /demo/seed?count=20000 | Load synthetic data (Development only) |

## Tests

    dotnet test

12 integration tests start a real PostgreSQL 16 in Docker (Testcontainers), run the API in memory, seed the data and check that each rule flags exactly the planted cards and nothing else, that re-scanning creates 0 new alerts, and that invalid input returns 400.

## Tech

C# / .NET 9, ASP.NET Core, Dapper, Npgsql, PostgreSQL 16, Docker Compose, xUnit, Testcontainers, Scalar (OpenAPI).

## Design notes

- Rules live in SQL, next to the data, so the database does the heavy lifting instead of loading rows into the application.
- A composite index on `(card_id, occurred_at)` supports the per-card window functions.
- Demo data uses a fixed random seed, so results are repeatable.
- The demo endpoint and API docs are enabled only in the Development environment.
