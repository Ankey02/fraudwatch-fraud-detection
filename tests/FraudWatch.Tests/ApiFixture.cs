using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Testcontainers.PostgreSql;

namespace FraudWatch.Tests;

public record AlertRow(string CardId, string Rule, int Severity, string Detail);
public record ScanRow(string Rule, int NewAlerts);
public record ScanResult(int TotalNewAlerts, List<ScanRow> Rules);
public record RiskRow(long Rank, string CardId, int RiskScore);

// Starts one real PostgreSQL container and one in-memory copy of the API,
// seeds the fake data and runs the first scan. All tests share the result.
public class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16").Build();
    private WebApplicationFactory<Program>? _factory;

    public HttpClient Client { get; private set; } = null!;
    public ScanResult FirstScan { get; private set; } = null!;
    public List<AlertRow> Alerts { get; private set; } = [];

    public async Task InitializeAsync()
    {
        await _db.StartAsync();

        // Program.cs reads ConnectionStrings:Default, so point it at the test container.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _db.GetConnectionString());

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseEnvironment("Development"));
        Client = _factory.CreateClient();

        (await Client.PostAsync("/demo/seed?count=5000", null)).EnsureSuccessStatusCode();

        var scan = await Client.PostAsync("/fraud/scan?days=30", null);
        scan.EnsureSuccessStatusCode();
        FirstScan = (await scan.Content.ReadFromJsonAsync<ScanResult>())!;

        Alerts = (await Client.GetFromJsonAsync<List<AlertRow>>("/fraud/alerts?limit=200"))!;
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        _factory?.Dispose();
        await _db.DisposeAsync();
    }
}
