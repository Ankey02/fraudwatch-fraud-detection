const $ = (id) => document.getElementById(id);
const money = (cents) => "$" + (cents / 100).toLocaleString(undefined, { maximumFractionDigits: 0 });
const SEVERITY = { 1: "Low", 2: "Medium", 3: "High" };

async function getJson(url, options) {
  const res = await fetch(url, options);
  if (!res.ok) throw new Error(url + " returned " + res.status);
  return res.json();
}

function setStatus(text, isError) {
  $("status").textContent = text;
  $("status").className = isError ? "error" : "";
}

function fillTable(tbodyId, rows, cells) {
  const body = $(tbodyId);
  body.replaceChildren();
  for (const row of rows) {
    const tr = document.createElement("tr");
    for (const cell of cells(row)) {
      const td = document.createElement("td");
      if (cell instanceof Node) td.appendChild(cell); else td.textContent = cell;
      tr.appendChild(td);
    }
    body.appendChild(tr);
  }
}

function badge(severity) {
  const span = document.createElement("span");
  span.className = "badge sev" + severity;
  span.textContent = SEVERITY[severity] ?? severity;
  return span;
}

function bar(value, max) {
  const wrap = document.createElement("div");
  wrap.className = "bar";
  const fill = document.createElement("div");
  fill.style.width = (max ? Math.max(2, (value / max) * 100) : 0) + "%";
  wrap.appendChild(fill);
  return wrap;
}

async function load() {
  try {
    const [summary, daily, risk, alerts] = await Promise.all([
      getJson("/reports/summary?days=30"),
      getJson("/reports/daily?days=14"),
      getJson("/reports/top-risk-cards?limit=10"),
      getJson("/fraud/alerts?limit=15"),
    ]);

    $("kpiTx").textContent = summary.totalTransactions.toLocaleString();
    $("kpiVol").textContent = money(summary.approvedVolumeCents);
    $("kpiDecl").textContent = summary.totalTransactions
      ? (summary.declined / summary.totalTransactions * 100).toFixed(1) + "%" : "0%";
    $("kpiAlerts").textContent = summary.totalAlerts.toLocaleString();
    $("kpiCards").textContent = summary.flaggedCards + " of " + summary.totalCards;

    const maxRule = Math.max(0, ...summary.alertsByRule.map((r) => r.count));
    fillTable("rules", summary.alertsByRule, (r) => [r.rule, badge(r.severity), r.count, bar(r.count, maxRule)]);

    const maxVol = Math.max(0, ...daily.map((d) => d.volumeCents));
    fillTable("daily", [...daily].reverse(), (d) => [
      d.day, d.transactions, d.declined, money(d.volumeCents), money(d.movingAvg7dCents), bar(d.volumeCents, maxVol),
    ]);

    fillTable("risk", risk, (r) => [r.rank, r.cardId, r.alerts, r.riskScore, r.rules]);
    fillTable("alerts", alerts, (a) => [badge(a.severity), a.rule, a.cardId, money(a.amountCents), a.detail]);
    setStatus("Updated " + new Date().toLocaleTimeString());
  } catch (err) {
    setStatus("Could not load data: " + err.message + ". Did you seed the data and run the scan?", true);
  }
}

async function seed() {
  setStatus("Seeding demo data...");
  try { await getJson("/demo/seed", { method: "POST" }); await getJson("/fraud/scan?days=30", { method: "POST" }); await load(); }
  catch (err) { setStatus("Seed failed: " + err.message, true); }
}

async function scan() {
  setStatus("Scanning...");
  try { const r = await getJson("/fraud/scan?days=30", { method: "POST" }); await load(); setStatus("Scan done: " + r.totalNewAlerts + " new alerts"); }
  catch (err) { setStatus("Scan failed: " + err.message, true); }
}

$("btnSeed").addEventListener("click", seed);
$("btnScan").addEventListener("click", scan);
$("btnRefresh").addEventListener("click", load);
load();
