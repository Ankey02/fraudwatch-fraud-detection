using System.Net;
using System.Net.Http.Json;

namespace FraudWatch.Tests;

public class FraudDetectionTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private Dictionary<string, int> CountByCard(string rule) =>
        api.Alerts.Where(a => a.Rule == rule)
                  .GroupBy(a => a.CardId)
                  .ToDictionary(g => g.Key, g => g.Count());

    [Fact]
    public void Velocity_flags_the_burst_cards()
    {
        var counts = CountByCard("velocity");
        Assert.Equal(4, counts["card_9001"]);   // 8 txns, 50s apart: alerts from the 5th on
        Assert.Equal(3, counts["card_9004"]);   // 7 txns in 5 minutes
        Assert.Equal(2, counts.Count);
    }

    [Fact]
    public void Unusual_amount_flags_the_big_spikes()
    {
        var counts = CountByCard("unusual_amount");
        Assert.Equal(1, counts["card_9002"]);   // 480000 after 15 small daily payments
        Assert.Equal(1, counts["card_9004"]);   // 90000 on a card that normally spends 100
        Assert.Equal(2, counts.Count);
    }

    [Fact]
    public void Duplicate_flags_repeated_charges()
    {
        var counts = CountByCard("duplicate");
        Assert.Equal(2, counts["card_9003"]);
        Assert.Single(counts);
    }

    [Fact]
    public void Card_testing_flags_the_approval_after_tiny_declines()
    {
        var alert = Assert.Single(api.Alerts, a => a.Rule == "card_testing");
        Assert.Equal("card_9004", alert.CardId);
        Assert.Equal(3, alert.Severity);
        Assert.StartsWith("6 tiny declined", alert.Detail);
    }

    [Fact]
    public void Impossible_travel_flags_US_to_India_in_30_minutes()
    {
        var alert = Assert.Single(api.Alerts, a => a.Rule == "impossible_travel");
        Assert.Equal("card_9005", alert.CardId);
        Assert.Equal("US to IN in 30 minutes", alert.Detail);
    }

    [Fact]
    public void Regular_cards_never_trigger_an_alert()
    {
        var planted = new[] { "card_9001", "card_9002", "card_9003", "card_9004", "card_9005" };
        Assert.All(api.Alerts, a => Assert.Contains(a.CardId, planted));
    }

    [Fact]
    public void First_scan_finds_exactly_13_alerts()
    {
        Assert.Equal(13, api.FirstScan.TotalNewAlerts);
        Assert.Equal(13, api.Alerts.Count);
    }

    [Fact]
    public async Task Scanning_again_creates_no_duplicates()
    {
        var response = await api.Client.PostAsync("/fraud/scan?days=30", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ScanResult>();
        Assert.Equal(0, result!.TotalNewAlerts);
    }

    [Fact]
    public async Task Risk_ranking_orders_cards_and_shares_ties()
    {
        var rows = await api.Client.GetFromJsonAsync<List<RiskRow>>("/reports/top-risk-cards?limit=10");
        Assert.Equal("card_9004", rows![0].CardId);
        Assert.Equal(11, rows[0].RiskScore);
        Assert.Equal(1, rows[0].Rank);
        Assert.Equal(4, rows.Single(r => r.CardId == "card_9002").Rank);
        Assert.Equal(4, rows.Single(r => r.CardId == "card_9003").Rank);
    }

    [Fact]
    public async Task Unknown_rule_filter_is_rejected()
    {
        var response = await api.Client.GetAsync("/fraud/alerts?rule=nope");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Scan_with_zero_days_is_rejected()
    {
        var response = await api.Client.PostAsync("/fraud/scan?days=0", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_page_is_served()
    {
        var html = await api.Client.GetStringAsync("/");
        Assert.Contains("FraudWatch Dashboard", html);
    }
}
