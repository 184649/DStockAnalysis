"use strict";

// ============================================================
// DStockAnalysis Web フロントエンド
// WPF 版の3画面(スクリーニング/個別分析/比較)を Web に再構成。
// 条件付き書式(MetricRules)は Converters.cs の閾値をそのまま移植。
// ============================================================

const API = {
  meta: () => get("/api/meta"),
  screen: (c) => post("/api/screen", c),
  stock: (code) => get(`/api/stocks/${encodeURIComponent(code)}`),
  compare: (codes) => get(`/api/compare?codes=${encodeURIComponent(codes.join(","))}`),
  saveUser: (code, body) => post(`/api/stocks/${encodeURIComponent(code)}/userdata`, body),
  importCsv: (text) => fetch("/api/import", { method: "POST", headers: { "Content-Type": "text/csv" }, body: text }).then(j),
  updateMaster: () => fetch("/api/admin/update-master", { method: "POST" }).then(j),
  fetchStatus: () => get("/api/admin/fetch/status"),
  fetchRun: (codes) => fetch(`/api/admin/fetch/run${codes && codes.length ? "?codes=" + codes.join(",") : ""}`, { method: "POST" }).then(j),
};
async function get(u) { return j(await fetch(u)); }
async function post(u, b) { return j(await fetch(u, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(b) })); }
async function j(r) { if (!r.ok) throw new Error(r.status + " " + r.statusText); return r.json(); }

const state = {
  meta: null,
  results: [],       // 現在のスクリーニング結果(summary)
  allCache: [],      // 個別分析・比較の一覧用(全件 summary)
  selected: null,    // 個別分析で選択中の詳細 stock
  compare: [],       // 比較対象コード
};

// ===== 条件付き書式(Converters.cs の MetricRules.Evaluate を移植) =====
function quality(metric, v) {
  switch (metric) {
    case "per": return v <= 0 ? "" : v <= 15 ? "good" : v <= 25 ? "" : v <= 35 ? "caution" : "bad";
    case "pbr": return v <= 0 ? "" : v <= 1.0 ? "good" : v <= 2.0 ? "" : v <= 3.5 ? "caution" : "bad";
    case "mix": return v <= 0 ? "" : v <= 10 ? "good" : v <= 22.5 ? "" : v <= 40 ? "caution" : "bad";
    case "roe": return v >= 15 ? "good" : v >= 10 ? "ok" : v >= 5 ? "" : v >= 0 ? "caution" : "bad";
    case "dy": return v >= 7 ? "caution" : v >= 4 ? "good" : v >= 3 ? "ok" : "";
    case "benefityield": return v >= 2 ? "good" : v >= 1 ? "ok" : "";
    case "totalyield": return v >= 5 ? "good" : v >= 4 ? "ok" : "";
    case "payout": return v <= 0 ? "" : v < 30 ? "" : v <= 60 ? "good" : v <= 80 ? "caution" : "bad";
    case "equity": return v >= 60 ? "good" : v >= 40 ? "ok" : v >= 30 ? "" : v >= 20 ? "caution" : "bad";
    case "debt": return v <= 20 ? "good" : v <= 40 ? "" : v <= 70 ? "caution" : "bad";
    case "growth": return v >= 10 ? "good" : v >= 3 ? "ok" : v >= 0 ? "" : "bad";
    case "margin": return v >= 15 ? "good" : v >= 8 ? "ok" : v >= 3 ? "" : v >= 0 ? "caution" : "bad";
    case "cf": return v > 0 ? "ok" : v === 0 ? "" : "bad";
    case "cfmargin": return v >= 15 ? "good" : v >= 8 ? "ok" : v >= 0 ? "" : "bad";
    case "score": return v >= 75 ? "good" : v >= 60 ? "ok" : v >= 45 ? "" : v >= 30 ? "caution" : "bad";
    case "buffett": return v >= 80 ? "good" : v >= 65 ? "ok" : v >= 50 ? "" : v >= 35 ? "caution" : "bad";
    case "change": return v >= 5 ? "ok" : v >= -5 ? "" : "caution";
    default: return "";
  }
}
function qClass(metric, v) { const q = quality(metric, v); return q ? "q-" + q : ""; }
const SOLID = { good: "#43A047", ok: "#29B6F6", caution: "#FFA726", bad: "#EF5350", "": "#78909C" };
function scoreColor(v, metric) { return SOLID[quality(metric || "score", v)] || SOLID[""]; }

// ===== フォーマッタ =====
const nf0 = new Intl.NumberFormat("ja-JP", { maximumFractionDigits: 0 });
function fnum(v, d = 2) { if (v == null || isNaN(v)) return "-"; return Number(v).toFixed(d); }
function fcomma(v) { if (v == null || isNaN(v)) return "-"; return nf0.format(Math.round(v)); }
function flag(b) { return b ? '<span class="flag-on">○</span>' : '<span class="flag-off">-</span>'; }
function flagLong(b) { return b ? '<span class="flag-on">◎</span>' : '<span class="flag-off">-</span>'; }
function esc(s) { return (s == null ? "" : String(s)).replace(/[&<>"]/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c])); }

// ===== スクリーニング表のカラム定義(カテゴリ順) =====
const COLS = [
  { k: "Code", label: "コード", kind: "id" },
  { k: "Name", label: "銘柄名", kind: "name" },
  { k: "Market", label: "市場", kind: "text" },
  { k: "Sector", label: "業種", kind: "text" },
  { k: "Scale", label: "規模", kind: "text" },
  { k: "Theme", label: "テーマ", kind: "text" },
  { k: "Price", label: "株価", kind: "comma" },
  { k: "MarketCap", label: "時価総額(百万)", kind: "comma" },
  { k: "PER", label: "PER", kind: "num", m: "per", d: 1 },
  { k: "PBR", label: "PBR", kind: "num", m: "pbr", d: 2 },
  { k: "MixFactor", label: "MIX", kind: "num", m: "mix", d: 1 },
  { k: "ROE", label: "ROE%", kind: "num", m: "roe", d: 1 },
  { k: "EPS", label: "EPS", kind: "num", d: 1 },
  { k: "DividendYield", label: "配当利回%", kind: "num", m: "dy", d: 2 },
  { k: "PayoutRatio", label: "配当性向%", kind: "num", m: "payout", d: 1 },
  { k: "ConsecutiveDividendYears", label: "連続増配", kind: "num", d: 0 },
  { k: "CumulativeDividend", label: "累進", kind: "flag" },
  { k: "DoeAdopted", label: "DOE", kind: "flag" },
  { k: "HasShareholderBenefit", label: "優待", kind: "flag" },
  { k: "BenefitCategory", label: "優待内容", kind: "text" },
  { k: "BenefitYield", label: "優待利回%", kind: "num", m: "benefityield", d: 2 },
  { k: "TotalYield", label: "総合利回%", kind: "num", m: "totalyield", d: 2 },
  { k: "HasLongTermBenefit", label: "長期優待", kind: "flaglong" },
  { k: "EquityRatio", label: "自己資本%", kind: "num", m: "equity", d: 1 },
  { k: "InterestBearingDebtRatio", label: "有利子負債%", kind: "num", m: "debt", d: 1 },
  { k: "OperatingMargin", label: "営業利益率%", kind: "num", m: "margin", d: 1 },
  { k: "NetProfitMargin", label: "純利益率%", kind: "num", m: "margin", d: 1 },
  { k: "OperatingCashFlowMargin", label: "営業CF率%", kind: "num", m: "cfmargin", d: 1 },
  { k: "RevenueGrowth3Y", label: "増収率3Y%", kind: "num", m: "growth", d: 1 },
  { k: "OperatingProfitGrowthRate", label: "営利成長%", kind: "num", m: "growth", d: 1 },
  { k: "FreeCashFlow", label: "FCF", kind: "cfcomma", m: "cf" },
  { k: "StockPriceChange3M", label: "3M変化%", kind: "num", m: "change", d: 1 },
  { k: "SafetyScore", label: "安全性", kind: "score" },
  { k: "GrowthScore", label: "成長性", kind: "score" },
  { k: "ProfitabilityScore", label: "収益性", kind: "score" },
  { k: "ReturnScore", label: "還元性", kind: "score" },
  { k: "EfficiencyScore", label: "効率性", kind: "score" },
  { k: "ValuationScore", label: "割安性", kind: "score" },
  { k: "LongTermScore", label: "長期適性", kind: "score" },
  { k: "RevaluationScore", label: "再評価", kind: "score" },
  { k: "BuffettScore", label: "バフェット", kind: "buffett" },
  { k: "WantToBuyScore", label: "買いたい度", kind: "score" },
  { k: "OverallScore", label: "総合", kind: "score" },
  { k: "OverallGrade", label: "評価", kind: "text" },
  { k: "JudgementText", label: "判定", kind: "text" },
];

function cell(col, s) {
  const v = s[col.k];
  switch (col.kind) {
    case "id": return `<td class="sticky">${esc(v)}${s.IsSampleIndicators ? ' <span class="sample-dot" title="サンプル指標">*</span>' : ""}</td>`;
    case "name": return `<td class="sticky2">${esc(v)}</td>`;
    case "text": return `<td>${esc(v)}</td>`;
    case "comma": return `<td class="num">${fcomma(v)}</td>`;
    case "cfcomma": return `<td class="num ${qClass(col.m, v)}">${fcomma(v)}</td>`;
    case "num": return `<td class="num ${col.m ? qClass(col.m, v) : ""}">${fnum(v, col.d)}</td>`;
    case "flag": return `<td>${flag(v)}</td>`;
    case "flaglong": return `<td>${flagLong(v)}</td>`;
    case "score": return `<td class="num ${qClass("score", v)}">${fnum(v, 0)}</td>`;
    case "buffett": return `<td class="num ${qClass("buffett", v)}">${fnum(v, 0)}</td>`;
    default: return `<td>${esc(v)}</td>`;
  }
}

// ===== フィルタパネル定義 =====
const RANGES = [
  ["MarketCap", "時価総額(百万)"], ["Price", "株価"], ["PER", "PER"], ["PBR", "PBR"],
  ["ROE", "ROE%"], ["DividendYield", "配当利回り%"], ["PayoutRatio", "配当性向%"],
  ["ConsecutiveDividendYears", "連続増配年"], ["EquityRatio", "自己資本比率%"],
  ["InterestBearingDebtRatio", "有利子負債比率%"], ["RevenueGrowth3Y", "増収率3Y%"],
  ["OperatingMargin", "営業利益率%"], ["NetProfitMargin", "純利益率%"],
  ["BenefitYield", "優待利回り%"], ["TotalYield", "総合利回り%"], ["BuffettScore", "バフェット最小"],
];
const FLAGS = [
  ["BenefitOnly", "株主優待あり"], ["LongTermBenefitOnly", "長期保有優遇"],
  ["NoDividendCut", "減配なし"], ["CumulativeOnly", "累進配当"], ["DoeOnly", "DOE採用"],
  ["HasBenefitRiskMemo", "優待リスクメモ"],
];

function buildFilterPanel() {
  const m = state.meta;
  const opt = (arr) => `<option value="">指定なし</option>` + arr.map(x => `<option>${esc(x)}</option>`).join("");
  let html = `
    <h3>市場</h3>
    <div class="toggle-group" id="marketToggles">
      ${["東証", "東証PR", "東証GR", "東証ST"].map(t => `<button data-token="${t}">${t}</button>`).join("")}
    </div>
    <h3>規模</h3>
    <div class="toggle-group" id="scaleToggles">
      ${["小型", "中型", "大型"].map(t => `<button data-token="${t}">${t}</button>`).join("")}
    </div>
    <h3>分類</h3>
    <div class="field"><label>業種</label><select id="sel_Sector">${opt(m.Sectors)}</select></div>
    <div class="field"><label>優待カテゴリ</label><select id="sel_BenefitCategory">${opt(m.BenefitCategories)}</select></div>
    <div class="field"><label>優待権利確定月</label><select id="sel_BenefitRightsMonth">${opt(m.BenefitMonths)}</select></div>
    <h3>フラグ</h3>
    <div class="toggle-group" id="flagToggles">
      ${FLAGS.map(([k, l]) => `<button data-flag="${k}">${l}</button>`).join("")}
    </div>
    <h3>数値レンジ(最小〜最大)</h3>
  `;
  for (const [k, l] of RANGES) {
    html += `<div class="field"><label>${l}</label><div class="range">
      <input type="number" id="min_${k}" placeholder="最小" step="any" />
      <span>〜</span>
      <input type="number" id="max_${k}" placeholder="最大" step="any" />
    </div></div>`;
  }
  html += `<div class="field" style="margin-top:12px;display:flex;gap:6px;">
    <button class="btn accent" id="btnApply" style="flex:1">絞り込み</button>
    <button class="btn" id="btnClear" style="flex:1">クリア</button></div>`;
  document.getElementById("filterPanel").innerHTML = html;

  // イベント
  document.querySelectorAll("#marketToggles button").forEach(b => b.onclick = () => toggleSingle(b, "#marketToggles"));
  document.querySelectorAll("#scaleToggles button").forEach(b => b.onclick = () => toggleSingle(b, "#scaleToggles"));
  document.querySelectorAll("#flagToggles button").forEach(b => b.onclick = () => { b.classList.toggle("on"); runScreen(); });
  document.querySelectorAll("#filterPanel select").forEach(s => s.onchange = runScreen);
  document.querySelectorAll("#filterPanel input").forEach(i => i.onchange = runScreen);
  document.getElementById("btnApply").onclick = runScreen;
  document.getElementById("btnClear").onclick = clearFilter;
}

function toggleSingle(btn, group) {
  const on = btn.classList.contains("on");
  document.querySelectorAll(group + " button").forEach(b => b.classList.remove("on"));
  if (!on) btn.classList.add("on");
  runScreen();
}

function readCriteria() {
  const c = {};
  const sel = (id) => { const v = document.getElementById(id).value; return v || null; };
  c.Sector = sel("sel_Sector");
  c.BenefitCategory = sel("sel_BenefitCategory");
  c.BenefitRightsMonth = sel("sel_BenefitRightsMonth");
  const mt = document.querySelector("#marketToggles button.on");
  c.MarketToken = mt ? mt.dataset.token : null;
  const st = document.querySelector("#scaleToggles button.on");
  c.ScaleToken = st ? st.dataset.token : null;
  for (const [k] of FLAGS) c[k] = !!document.querySelector(`#flagToggles button[data-flag="${k}"].on`);
  for (const [k] of RANGES) {
    const mn = document.getElementById("min_" + k).value;
    const mx = document.getElementById("max_" + k).value;
    c[k] = { Min: mn === "" ? null : Number(mn), Max: mx === "" ? null : Number(mx) };
  }
  return c;
}

function applyCriteria(c) {
  const setSel = (id, v) => { const e = document.getElementById(id); if (e) e.value = v || ""; };
  setSel("sel_Sector", c.Sector);
  setSel("sel_BenefitCategory", c.BenefitCategory);
  setSel("sel_BenefitRightsMonth", c.BenefitRightsMonth);
  document.querySelectorAll("#marketToggles button").forEach(b => b.classList.toggle("on", b.dataset.token === c.MarketToken));
  document.querySelectorAll("#scaleToggles button").forEach(b => b.classList.toggle("on", b.dataset.token === c.ScaleToken));
  document.querySelectorAll("#flagToggles button").forEach(b => b.classList.toggle("on", !!c[b.dataset.flag]));
  for (const [k] of RANGES) {
    const r = c[k] || {};
    document.getElementById("min_" + k).value = r.Min == null ? "" : r.Min;
    document.getElementById("max_" + k).value = r.Max == null ? "" : r.Max;
  }
}

function clearFilter() {
  document.querySelectorAll("#filterPanel select").forEach(s => s.value = "");
  document.querySelectorAll("#filterPanel input").forEach(i => i.value = "");
  document.querySelectorAll("#filterPanel button.on").forEach(b => b.classList.remove("on"));
  runScreen();
}

async function runScreen() {
  const res = await API.screen(readCriteria());
  state.results = res.stocks;
  renderResults();
}

function renderResults() {
  const t = document.getElementById("screenTable");
  let head = "<thead><tr>" + COLS.map((c, i) => {
    const cls = c.kind === "id" ? "sticky" : c.kind === "name" ? "sticky2" : "";
    return `<th class="${cls}">${c.label}</th>`;
  }).join("") + "</tr></thead>";
  let body = "<tbody>" + state.results.map(s =>
    `<tr data-code="${esc(s.Code)}">` + COLS.map(c => cell(c, s)).join("") + "</tr>"
  ).join("") + "</tbody>";
  t.innerHTML = head + body;
  t.querySelectorAll("tbody tr").forEach(tr => {
    tr.ondblclick = () => openAnalysis(tr.dataset.code);
    tr.oncontextmenu = (e) => { e.preventDefault(); addToCompare(tr.dataset.code); };
  });
  document.getElementById("resultText").textContent = `${state.results.length} 件 (行をダブルクリックで個別分析 / 右クリックで比較に追加)`;
  const sample = state.meta.SampleCount;
  document.getElementById("indicatorNotice").innerHTML =
    sample > 0 ? `全 ${state.meta.Total} 銘柄 / うち <span class="sample-dot">${sample}</span> 件は指標がサンプル値(*)` : `全 ${state.meta.Total} 銘柄`;
}

// ===== プリセット =====
function renderPresets() {
  const div = document.getElementById("presets");
  div.innerHTML = state.meta.Presets.map((p, i) => `<button data-i="${i}">${esc(p.Name)}</button>`).join("");
  div.querySelectorAll("button").forEach(b => b.onclick = () => {
    const p = state.meta.Presets[Number(b.dataset.i)];
    applyCriteria(p.Criteria);
    runScreen();
  });
}

// ============================================================
// 個別分析
// ============================================================
function renderAnalysisList(filter = "") {
  const f = filter.trim().toLowerCase();
  const items = state.allCache.filter(s => !f || s.Code.toLowerCase().includes(f) || s.Name.toLowerCase().includes(f)).slice(0, 500);
  document.getElementById("analysisItems").innerHTML = items.map(s =>
    `<div class="item ${state.selected && state.selected.Code === s.Code ? "sel" : ""}" data-code="${esc(s.Code)}">
      <div>${esc(s.Name)}</div><div class="code">${esc(s.Code)} ・ ${esc(s.Market)}</div></div>`).join("");
  document.querySelectorAll("#analysisItems .item").forEach(d => d.onclick = () => openAnalysis(d.dataset.code));
}

async function openAnalysis(code) {
  switchView("analysis");
  const data = await API.stock(code);
  state.selected = data.stock;
  state.links = data.links;
  renderAnalysisList(document.getElementById("analysisSearch").value);
  renderDetail();
}

const BUFFETT_ITEMS = [
  ["CanExplainEarnings", "何で稼いでいるか説明できる"],
  ["UnderstandBusiness", "事業内容を理解できる"],
  ["DemandIn10Years", "10年後も需要がある"],
  ["HasCompetitiveAdvantage", "競争優位性がある"],
  ["HasEntryBarrier", "参入障壁がある"],
  ["HighMargin", "高い利益率を維持"],
  ["StableHighRoe", "ROEが安定して高い"],
  ["StablePositiveOperatingCf", "営業CFが安定黒字"],
  ["StablePositiveFreeCf", "フリーCFが安定黒字"],
  ["SoundFinance", "財務が健全"],
  ["SustainableReturn", "配当/自社株買いに無理がない"],
  ["TrustManagement", "経営者の説明に納得"],
  ["NotOverpriced", "割高すぎない"],
  ["WantToBuyOnCrash", "暴落時に買い増したい"],
  ["CanWrite10YearReason", "10年保有の理由を書ける"],
];
const YESNO = { 0: "不明", 1: "はい", 2: "いいえ" };
const CLASSIFICATIONS = ["未分類", "最重要候補", "長期優良株候補", "第二のキオクシア候補", "再評価候補", "決算確認待ち", "保留", "除外"];
const MEMO_FIELDS = [
  ["DiscoveryReason", "見つけた理由"], ["InterestingNumbers", "気になった数値"],
  ["GoodPoints", "良い点"], ["BadPoints", "悪い点"],
  ["LongTermEvaluation", "長期優良株としての評価"], ["RevaluationEvaluation", "再評価候補としての評価"],
  ["KioxiaReason", "第二のキオクシア候補として見る理由"], ["NextToCheck", "次に確認する情報"],
];

function scoreCard(label, v, metric) {
  return `<div class="card score"><div class="k">${label}</div><div class="v" style="background:${scoreColor(v, metric)}">${fnum(v, 0)}</div></div>`;
}
function metricCard(label, v, suffix = "") { return `<div class="card"><div class="k">${label}</div><div class="v">${typeof v === "number" ? fnum(v, 2) : esc(v)}${suffix}</div></div>`; }

function renderDetail() {
  const s = state.selected;
  const el = document.getElementById("analysisDetail");
  const links = (state.links || []).map(l => `<a href="${esc(l.Url)}" target="_blank" rel="noopener">${esc(l.Name)}</a>`).join("");
  el.innerHTML = `
    <div class="head">
      <span class="name">${esc(s.Name)}</span><span class="code">${esc(s.Code)}</span>
    </div>
    <div class="tags">
      <span>${esc(s.Market)}</span><span>${esc(s.Sector)}</span><span>${esc(s.Scale)}</span>
      ${s.Theme ? `<span>${esc(s.Theme)}</span>` : ""}${s.FiscalMonth ? `<span>決算 ${esc(s.FiscalMonth)}</span>` : ""}
      ${s.IsSampleIndicators ? `<span class="sample-dot">指標はサンプル値</span>` : ""}
    </div>
    ${s.Description ? `<div class="desc">${esc(s.Description)}</div>` : ""}
    <div class="links">${links}</div>

    <div class="cards">
      ${metricCard("株価", s.Price, "")}
      ${metricCard("時価総額(百万)", fcomma(s.MarketCap))}
      ${metricCard("PER", s.PER, "倍")}
      ${metricCard("PBR", s.PBR, "倍")}
      ${metricCard("ROE", s.ROE, "%")}
      ${metricCard("MIX係数", s.MixFactor)}
      ${metricCard("自己資本比率", s.EquityRatio, "%")}
      ${metricCard("配当利回り", s.DividendYield, "%")}
      ${metricCard("配当性向", s.PayoutRatio, "%")}
    </div>
    <div class="cards">
      ${scoreCard("総合評価(" + esc(s.OverallGrade) + ")", s.OverallScore)}
      ${scoreCard("長期適性", s.LongTermScore)}
      ${scoreCard("再評価期待", s.RevaluationScore)}
      ${scoreCard("バフェット", s.BuffettScore, "buffett")}
      ${scoreCard("買いたい度", s.WantToBuyScore)}
    </div>

    <div class="panel-row">
      <div class="box grow">
        <h3>スコアレーダー</h3>
        ${radarSvg(s)}
        <div style="text-align:center;color:var(--sub);font-size:12px;margin-top:4px">判定: ${esc(s.JudgementText)}</div>
      </div>
      <div class="box grow">
        <h3>バフェットチェック(15項目)</h3>
        <div id="buffettList">${BUFFETT_ITEMS.map(([k, l]) => `
          <div class="buffett-item"><span>${l}</span>
          <select data-bk="${k}">${[0, 1, 2].map(o => `<option value="${o}" ${s.BuffettCheck[k] === o ? "selected" : ""}>${YESNO[o]}</option>`).join("")}</select></div>`).join("")}
        </div>
      </div>
    </div>

    <div class="box">
      <h3>チャート</h3>
      <div class="charts">
        ${barChart("業績推移", s.History, [["Revenue", "売上", "#29B6F6"], ["OperatingProfit", "営業利益", "#43A047"], ["NetIncome", "純利益", "#FFA726"]])}
        ${barChart("配当推移", s.History, [["EPS", "EPS", "#29B6F6"], ["Dividend", "配当", "#43A047"]])}
        ${barChart("財務推移", s.History, [["NetAssets", "純資産", "#43A047"], ["Liabilities", "負債", "#EF5350"]])}
        ${barChart("キャッシュフロー", s.History, [["OperatingCF", "営業CF", "#29B6F6"], ["InvestingCF", "投資CF", "#FFA726"], ["FinancingCF", "財務CF", "#AB47BC"], ["FreeCF", "FCF", "#43A047"]])}
        ${barChart("自社株買い", s.History, [["BuybackAmount", "取得額", "#26A69A"]])}
      </div>
    </div>

    <div class="box">
      <h3>株主優待・株主還元</h3>
      <div class="cards">
        ${metricCard("優待", s.HasShareholderBenefit ? "あり" : "なし")}
        ${metricCard("優待内容", s.BenefitContent || "-")}
        ${metricCard("優待利回り", s.BenefitYield, "%")}
        ${metricCard("総合利回り", s.TotalYield, "%")}
        ${metricCard("長期保有優遇", s.HasLongTermBenefit ? "あり" : "なし")}
        ${metricCard("権利確定月", s.BenefitRightsMonth || "-")}
      </div>
      ${s.BenefitRiskMemo ? `<div class="desc">廃止リスク: ${esc(s.BenefitRiskMemo)}</div>` : ""}
    </div>

    <div class="box">
      <h3>メモ・分類</h3>
      <div class="memo-grid" id="memoGrid">
        <div><label>分類</label><select id="memo_Classification">${CLASSIFICATIONS.map(c => `<option ${s.Memo.Classification === c ? "selected" : ""}>${c}</option>`).join("")}</select></div>
        <div><label>興味度 (0-100)</label><input type="number" id="memo_Interest" min="0" max="100" value="${s.UserInterest}" /></div>
        ${MEMO_FIELDS.map(([k, l]) => `<div class="full"><label>${l}</label><textarea rows="2" id="memo_${k}">${esc(s.Memo[k])}</textarea></div>`).join("")}
      </div>
      <div style="margin-top:10px"><button class="btn accent" id="btnSaveMemo">チェック・メモを保存してスコア再計算</button></div>
    </div>
  `;
  el.querySelectorAll('[data-bk]').forEach(sel => sel.onchange = saveUserData);
  document.getElementById("btnSaveMemo").onclick = saveUserData;
}

async function saveUserData() {
  const s = state.selected;
  const check = {};
  document.querySelectorAll('[data-bk]').forEach(sel => check[sel.dataset.bk] = Number(sel.value));
  const memo = { Classification: document.getElementById("memo_Classification").value };
  for (const [k] of MEMO_FIELDS) memo[k] = document.getElementById("memo_" + k).value;
  const interest = Number(document.getElementById("memo_Interest").value);
  const updated = await API.saveUser(s.Code, { Memo: memo, BuffettCheck: check, UserInterest: interest });
  state.selected = updated;
  // 一覧キャッシュも更新
  const idx = state.allCache.findIndex(x => x.Code === updated.Code);
  if (idx >= 0) Object.assign(state.allCache[idx], { BuffettScore: updated.BuffettScore, OverallScore: updated.OverallScore });
  renderDetail();
  toast("保存し、スコアを再計算しました");
}

// ===== SVG チャート =====
function barChart(title, history, series) {
  if (!history || history.length === 0) return `<div class="chart"><h4>${title}</h4><div class="empty">データなし</div></div>`;
  const W = 300, H = 130, pad = 18, n = history.length;
  let max = 0;
  for (const h of history) for (const [k] of series) max = Math.max(max, Math.abs(h[k] || 0));
  if (max === 0) max = 1;
  const groupW = (W - pad * 2) / n;
  const barW = Math.max(1, (groupW - 2) / series.length);
  const zeroY = H - pad - (H - pad * 2) * 0.5;
  let bars = "";
  history.forEach((h, i) => {
    series.forEach(([k, , color], si) => {
      const v = h[k] || 0;
      const ratio = v / max; // -1..1
      const half = (H - pad * 2) / 2;
      const barH = Math.abs(ratio) * half;
      const x = pad + i * groupW + si * barW + 1;
      const y = v >= 0 ? zeroY - barH : zeroY;
      bars += `<rect x="${x.toFixed(1)}" y="${y.toFixed(1)}" width="${barW.toFixed(1)}" height="${barH.toFixed(1)}" fill="${color}"></rect>`;
    });
  });
  const legend = series.map(([, name, color]) => `<span style="color:${color}">■</span>${name}`).join(" ");
  return `<div class="chart"><h4>${title}</h4>
    <svg viewBox="0 0 ${W} ${H}" width="100%" height="${H}">
      <line x1="${pad}" y1="${zeroY}" x2="${W - pad}" y2="${zeroY}" stroke="#444" stroke-width="0.5"></line>
      ${bars}
    </svg>
    <div style="font-size:11px;color:var(--sub)">${legend}　(${history[0].FiscalYear}〜${history[n - 1].FiscalYear})</div>
  </div>`;
}

function radarSvg(s) {
  const axes = [["安全性", s.SafetyScore], ["成長性", s.GrowthScore], ["収益性", s.ProfitabilityScore],
  ["還元性", s.ReturnScore], ["効率性", s.EfficiencyScore], ["割安性", s.ValuationScore]];
  const cx = 150, cy = 120, R = 90, N = axes.length;
  const pt = (i, r) => { const a = -Math.PI / 2 + i * 2 * Math.PI / N; return [cx + Math.cos(a) * r, cy + Math.sin(a) * r]; };
  let grid = "";
  for (let g = 1; g <= 4; g++) {
    const pts = axes.map((_, i) => pt(i, R * g / 4).map(n => n.toFixed(1)).join(",")).join(" ");
    grid += `<polygon points="${pts}" fill="none" stroke="#3a434d" stroke-width="0.5"></polygon>`;
  }
  let labels = "", spokes = "";
  axes.forEach(([name, v], i) => {
    const [lx, ly] = pt(i, R + 14);
    labels += `<text x="${lx.toFixed(1)}" y="${ly.toFixed(1)}" fill="#9AA7B4" font-size="11" text-anchor="middle">${name}</text>`;
    const [sx, sy] = pt(i, R);
    spokes += `<line x1="${cx}" y1="${cy}" x2="${sx.toFixed(1)}" y2="${sy.toFixed(1)}" stroke="#3a434d" stroke-width="0.5"></line>`;
  });
  const poly = axes.map(([, v], i) => pt(i, R * Math.max(0, Math.min(100, v)) / 100).map(n => n.toFixed(1)).join(",")).join(" ");
  return `<svg viewBox="0 0 300 250" width="100%" height="250">
    ${grid}${spokes}
    <polygon points="${poly}" fill="rgba(45,156,219,.35)" stroke="#2D9CDB" stroke-width="1.5"></polygon>
    ${labels}</svg>`;
}

// ============================================================
// 比較
// ============================================================
function renderCompareList(filter = "") {
  const f = filter.trim().toLowerCase();
  const items = state.allCache.filter(s => !f || s.Code.toLowerCase().includes(f) || s.Name.toLowerCase().includes(f)).slice(0, 500);
  document.getElementById("compareItems").innerHTML = items.map(s =>
    `<div class="item" data-code="${esc(s.Code)}"><div>${esc(s.Name)}</div><div class="code">${esc(s.Code)}</div></div>`).join("");
  document.querySelectorAll("#compareItems .item").forEach(d => d.onclick = () => addToCompare(d.dataset.code));
}

function addToCompare(code) {
  if (state.compare.includes(code)) return;
  if (state.compare.length >= 6) { toast("比較は最大6件です"); return; }
  state.compare.push(code);
  renderCompare();
  toast("比較に追加しました");
}
function removeCompare(code) { state.compare = state.compare.filter(c => c !== code); renderCompare(); }

const CMP_ROWS = [
  ["Price", "株価", "comma"], ["MarketCap", "時価総額(百万)", "comma"], ["PER", "PER", "per"],
  ["PBR", "PBR", "pbr"], ["ROE", "ROE%", "roe"], ["DividendYield", "配当利回り%", "dy"],
  ["PayoutRatio", "配当性向%", "payout"], ["TotalYield", "総合利回り%", "totalyield"],
  ["EquityRatio", "自己資本比率%", "equity"], ["OperatingMargin", "営業利益率%", "margin"],
  ["RevenueGrowth3Y", "増収率3Y%", "growth"], ["FreeCashFlow", "FCF", "cf"],
  ["SafetyScore", "安全性", "score"], ["GrowthScore", "成長性", "score"],
  ["ProfitabilityScore", "収益性", "score"], ["ReturnScore", "還元性", "score"],
  ["BuffettScore", "バフェット", "buffett"], ["OverallScore", "総合", "score"],
];

async function renderCompare() {
  const chips = document.getElementById("compareChips");
  chips.innerHTML = state.compare.map(c => `<span class="chip" data-code="${esc(c)}">${esc(c)}<span class="x">✕</span></span>`).join("")
    || `<span style="color:var(--sub)">比較対象なし</span>`;
  chips.querySelectorAll(".chip").forEach(ch => ch.onclick = () => removeCompare(ch.dataset.code));

  const body = document.getElementById("compareBody");
  if (state.compare.length === 0) { body.innerHTML = `<div class="empty">左の一覧から銘柄を選んで比較に追加してください(最大6件)。</div>`; return; }
  const stocks = await API.compare(state.compare);
  let html = "<table class='cmp'><thead><tr><th>指標</th>" + stocks.map(s => `<th>${esc(s.Name)}<br><span style="color:var(--sub)">${esc(s.Code)}</span></th>`).join("") + "</tr></thead><tbody>";
  for (const [k, label, m] of CMP_ROWS) {
    html += `<tr><th>${label}</th>` + stocks.map(s => {
      const v = s[k];
      const cls = m === "comma" ? "" : qClass(m, v);
      const text = (m === "comma" || k === "FreeCashFlow") ? fcomma(v) : (m === "score" || m === "buffett") ? fnum(v, 0) : fnum(v, 2);
      return `<td class="${cls}">${text}</td>`;
    }).join("") + "</tr>";
  }
  html += "</tbody></table>";
  body.innerHTML = html;
}

// ============================================================
// 共通: ビュー切替・ツールバー・初期化
// ============================================================
function switchView(name) {
  document.querySelectorAll(".view").forEach(v => v.classList.toggle("active", v.id === name));
  document.querySelectorAll("nav button").forEach(b => b.classList.toggle("active", b.dataset.view === name));
  if (name === "analysis") renderAnalysisList(document.getElementById("analysisSearch").value);
  if (name === "comparison") { renderCompareList(document.getElementById("compareSearch").value); renderCompare(); }
}

function renderMeta() {
  const m = state.meta;
  const date = m.MasterDate ? new Date(m.MasterDate).toLocaleDateString("ja-JP") : "―";
  document.getElementById("metaInfo").innerHTML =
    `データ更新日: ${date}${m.MasterStale ? ' <span class="stale">(更新を推奨)</span>' : ""}<br>全 ${m.Total} 銘柄`;
}

let toastTimer = null;
function toast(msg) {
  let t = document.querySelector(".toast");
  if (!t) { t = document.createElement("div"); t.className = "toast"; document.body.appendChild(t); }
  t.textContent = msg;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => t.remove(), 3500);
}

function bindToolbar() {
  document.querySelectorAll("nav button").forEach(b => b.onclick = () => switchView(b.dataset.view));
  document.getElementById("analysisSearch").oninput = (e) => renderAnalysisList(e.target.value);
  document.getElementById("compareSearch").oninput = (e) => renderCompareList(e.target.value);

  document.getElementById("btnImport").onclick = () => document.getElementById("csvFile").click();
  document.getElementById("csvFile").onchange = async (e) => {
    const file = e.target.files[0]; if (!file) return;
    const text = await file.text();
    try { const r = await API.importCsv(text); toast(`CSV取込: 更新 ${r.updated} / 追加 ${r.added}`); await reloadAll(); }
    catch (err) { toast("取込失敗: " + err.message); }
    e.target.value = "";
  };
  document.getElementById("btnTemplate").onclick = () => window.location = "/api/template.csv";
  document.getElementById("btnUpdateMaster").onclick = async () => {
    toast("JPX 全銘柄を更新中...");
    try { const r = await API.updateMaster(); toast(`更新完了: ${r.total} 銘柄`); await reloadAll(); }
    catch (err) { toast("更新失敗: " + err.message); }
  };
  document.getElementById("btnFetch").onclick = fetchIndicators;
}

async function fetchIndicators() {
  const codes = state.results.slice(0, 50).map(s => s.Code); // 表示中の上位銘柄を対象(責任ある低頻度取得)
  if (codes.length === 0) { toast("対象銘柄がありません"); return; }
  try {
    await API.fetchRun(codes);
    toast(`実データ取得を開始しました(${codes.length}銘柄, robots順守・低速)`);
    pollFetch();
  } catch (err) { toast("取得開始に失敗: " + err.message); }
}
let fetchPollTimer = null;
async function pollFetch() {
  clearInterval(fetchPollTimer);
  fetchPollTimer = setInterval(async () => {
    try {
      const st = await API.fetchStatus();
      if (st.Running) toast(`取得中 ${st.Processed}/${st.Total} (更新 ${st.Updated}) ${st.CurrentCode || ""}`);
      else { clearInterval(fetchPollTimer); toast(`取得完了: 更新 ${st.Updated} / スキップ ${st.Skipped}`); await reloadAll(); }
    } catch { clearInterval(fetchPollTimer); }
  }, 4000);
}

async function reloadAll() {
  state.meta = await API.meta();
  renderMeta();
  // 全件キャッシュ(空条件でスクリーニング)
  const all = await API.screen({});
  state.allCache = all.stocks;
  await runScreen();
  if (state.selected) { const d = await API.stock(state.selected.Code); state.selected = d.stock; state.links = d.links; renderDetail(); }
}

async function init() {
  state.meta = await API.meta();
  renderMeta();
  buildFilterPanel();
  renderPresets();
  bindToolbar();
  const all = await API.screen({});
  state.allCache = all.stocks;
  state.results = all.stocks;
  renderResults();
}

init().catch(err => { document.getElementById("metaInfo").textContent = "初期化エラー: " + err.message; console.error(err); });
