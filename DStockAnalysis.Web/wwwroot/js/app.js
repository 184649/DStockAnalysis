"use strict";

// ============================================================
// DStockAnalysis Web フロントエンド
// WPF 版の3画面(スクリーニング/個別分析/比較)を Web に再構成。
// 条件付き書式(MetricRules)は Converters.cs の閾値をそのまま移植。
// ============================================================

const API = {
  meta: () => get("/api/meta"),
  screen: (c) => post("/api/screen", c),
  stock: (code, refresh) => get(`/api/stocks/${encodeURIComponent(code)}${refresh ? "?refresh=true" : ""}`),
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
  sort: { key: null, dir: 1 }, // 一覧の並べ替え(列キー, 1=昇順/-1=降順)
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
  // 基本情報
  { k: "Code", label: "コード", kind: "id" },
  { k: "Name", label: "銘柄名", kind: "name" },
  { k: "Market", label: "市場", kind: "text" },
  { k: "Sector", label: "業種", kind: "text" },
  { k: "Scale", label: "規模", kind: "text" },
  { k: "Theme", label: "テーマ", kind: "text" },
  { k: "FiscalMonth", label: "決算月", kind: "text" },
  // バリュエーション
  { k: "Price", label: "株価", kind: "comma" },
  { k: "MarketCap", label: "時価総額(百万)", kind: "comma" },
  { k: "PER", label: "PER", kind: "num", m: "per", d: 1 },
  { k: "PBR", label: "PBR", kind: "num", m: "pbr", d: 2 },
  { k: "MixFactor", label: "MIX", kind: "num", m: "mix", d: 1 },
  { k: "ROE", label: "ROE%", kind: "num", m: "roe", d: 1 },
  { k: "ROA", label: "ROA%", kind: "num", m: "roe", d: 1 },
  { k: "TotalAssetTurnover", label: "総資産回転率", kind: "num", d: 2 },
  { k: "EPS", label: "EPS", kind: "num", d: 1 },
  { k: "BPS", label: "BPS", kind: "comma" },
  { k: "OperatingMargin", label: "営業利益率%", kind: "num", m: "margin", d: 1 },
  { k: "OrdinaryProfitMargin", label: "経常利益率%", kind: "num", m: "margin", d: 1 },
  { k: "NetProfitMargin", label: "純利益率%", kind: "num", m: "margin", d: 1 },
  // 配当・株主還元
  { k: "DividendYield", label: "配当利回%", kind: "num", m: "dy", d: 2 },
  { k: "PayoutRatio", label: "配当性向%", kind: "num", m: "payout", d: 1 },
  { k: "Dividend", label: "1株配当", kind: "num", d: 1 },
  { k: "DividendTrend", label: "配当傾向", kind: "text" },
  { k: "CumulativeDividend", label: "累進", kind: "flag" },
  { k: "DoeAdopted", label: "DOE", kind: "flag" },
  { k: "ConsecutiveDividendYears", label: "連続増配", kind: "num", d: 0 },
  { k: "DividendCutCount", label: "減配回数", kind: "num", d: 0 },
  { k: "NonDividendCutYears", label: "非減配年", kind: "num", d: 0 },
  { k: "DividendRemainingYears", label: "配当残年", kind: "num", d: 1 },
  { k: "BuybackAmount", label: "自社株買(百万)", kind: "comma" },
  { k: "ShareholderReturnPolicy", label: "還元方針", kind: "text" },
  { k: "DividendGrowth1Y", label: "増配1Y%", kind: "num", m: "growth", d: 1 },
  { k: "DividendGrowth3Y", label: "増配3Y%", kind: "num", m: "growth", d: 1 },
  { k: "DividendGrowth5Y", label: "増配5Y%", kind: "num", m: "growth", d: 1 },
  { k: "DividendGrowth10Y", label: "増配10Y%", kind: "num", m: "growth", d: 1 },
  // 株主優待
  { k: "HasShareholderBenefit", label: "優待", kind: "flag" },
  { k: "BenefitCategory", label: "優待カテゴリ", kind: "text" },
  { k: "BenefitContent", label: "優待内容", kind: "text" },
  { k: "BenefitRightsMonth", label: "権利月", kind: "text" },
  { k: "RequiredSharesForBenefit", label: "必要株数", kind: "comma" },
  { k: "BenefitValue", label: "優待価値(円)", kind: "comma" },
  { k: "BenefitYield", label: "優待利回%", kind: "num", m: "benefityield", d: 2 },
  { k: "TotalYield", label: "総合利回%", kind: "num", m: "totalyield", d: 2 },
  { k: "HasLongTermBenefit", label: "長期優待", kind: "flaglong" },
  // 財務
  { k: "EquityRatio", label: "自己資本%", kind: "num", m: "equity", d: 1 },
  { k: "InterestBearingDebtRatio", label: "有利子負債%", kind: "num", m: "debt", d: 1 },
  // 成長性
  { k: "RevenueGrowth1Y", label: "増収1Y%", kind: "num", m: "growth", d: 1 },
  { k: "RevenueGrowth3Y", label: "増収3Y%", kind: "num", m: "growth", d: 1 },
  { k: "RevenueGrowth5Y", label: "増収5Y%", kind: "num", m: "growth", d: 1 },
  { k: "RevenueGrowth10Y", label: "増収10Y%", kind: "num", m: "growth", d: 1 },
  { k: "OperatingProfitGrowthRate", label: "営利成長%", kind: "num", m: "growth", d: 1 },
  { k: "OrdinaryProfitGrowthRate", label: "経常成長%", kind: "num", m: "growth", d: 1 },
  { k: "NetProfitGrowthRate", label: "純利成長%", kind: "num", m: "growth", d: 1 },
  { k: "EpsGrowthRate", label: "EPS成長%", kind: "num", m: "growth", d: 1 },
  // キャッシュフロー
  { k: "OperatingCF", label: "営業CF(百万)", kind: "cfcomma", m: "cf" },
  { k: "InvestingCF", label: "投資CF(百万)", kind: "comma" },
  { k: "FinancingCF", label: "財務CF(百万)", kind: "comma" },
  { k: "FreeCashFlow", label: "FCF(百万)", kind: "cfcomma", m: "cf" },
  { k: "OperatingCashFlowMargin", label: "営業CF率%", kind: "num", m: "cfmargin", d: 1 },
  // 株価変化
  { k: "StockPriceChange3M", label: "3M変化%", kind: "num", m: "change", d: 1 },
  { k: "AverageStockPriceChange3M", label: "3M平均変化%", kind: "num", m: "change", d: 1 },
  { k: "AveragePrice3M", label: "3M平均株価", kind: "comma" },
  // スコア
  { k: "SafetyScore", label: "安全性", kind: "score" },
  { k: "GrowthScore", label: "成長性", kind: "score" },
  { k: "ProfitabilityScore", label: "収益性", kind: "score" },
  { k: "ReturnScore", label: "還元性", kind: "score" },
  { k: "EfficiencyScore", label: "効率性", kind: "score" },
  { k: "ValuationScore", label: "割安性", kind: "score" },
  { k: "LongTermScore", label: "長期適性", kind: "score" },
  { k: "RevaluationScore", label: "再評価", kind: "score" },
  { k: "BuffettScore", label: "バフェット", kind: "buffett" },
  { k: "Buffett.OverallGrade", label: "評価ランク", kind: "text" },
  { k: "Buffett.DataConfidence", label: "信頼度%", kind: "num", d: 0 },
  { k: "Buffett.BusinessDurabilityScore", label: "事業耐久", kind: "score" },
  { k: "Buffett.ProfitabilityScore", label: "収益力", kind: "score" },
  { k: "Buffett.SafetyScore", label: "財務安全", kind: "score" },
  { k: "Buffett.GrowthStabilityScore", label: "成長安定", kind: "score" },
  { k: "Buffett.CapitalAllocationScore", label: "資本配分", kind: "score" },
  { k: "Buffett.ValuationScore", label: "割安性(B)", kind: "score" },
  { k: "WantToBuyScore", label: "買いたい度", kind: "score" },
  { k: "OverallScore", label: "総合", kind: "score" },
  { k: "OverallGrade", label: "評価", kind: "text" },
  { k: "JudgementText", label: "判定", kind: "text" },
];

// 暫定取得(Yahoo一括)で表示できる指標。これ以外(利益率・ROA・CF・自己資本比率・成長率・スコア等)は
// 会社予想・財務を取得するまで「-」にする(暫定データで誤ったスコアを出さないため)。
const PROV_OK = new Set(["Price", "PER", "PBR", "MixFactor", "EPS", "BPS", "Dividend", "DividendYield"]);

// ドット区切りキー("Buffett.ProfitabilityScore")を解決する。
function gv(o, k) {
  if (k.indexOf(".") < 0) return o ? o[k] : undefined;
  return k.split(".").reduce((a, p) => (a == null ? a : a[p]), o);
}

function cell(col, s) {
  const v = gv(s, col.k);
  const unf = !s.IndicatorsFetched;
  const prov = s.Provisional;
  // 基本情報(コード・銘柄名・市場等)は常に表示。指標・スコアは未取得なら「-」。
  switch (col.kind) {
    case "id": {
      const badge = unf ? ' <span class="flag-off" title="実データ未取得">未</span>'
        : prov ? ' <span class="flag-prov" title="暫定: 株価/PER等のみ。開くと会社予想・財務を取得しスコア算出">暫</span>' : "";
      return `<td class="sticky">${esc(v)}${badge}</td>`;
    }
    case "name": return `<td class="sticky2">${esc(v)}</td>`;
    case "text": {
      // 基本情報(市場/業種/規模)は常に表示。その他のテキスト指標は未取得/空欄なら「-」。
      const basic = (col.k === "Market" || col.k === "Sector" || col.k === "Scale");
      if (!basic && unf) return `<td>-</td>`;
      if (!basic && prov && !PROV_OK.has(col.k)) return `<td>-</td>`; // 暫定は財務由来テキスト(評価ランク等)を出さない
      return `<td>${v ? esc(v) : "-"}</td>`;
    }
  }
  if (unf) { // 指標未取得は色なしの「-」
    if (col.kind === "flag" || col.kind === "flaglong") return `<td><span class="flag-off">-</span></td>`;
    return `<td class="num">-</td>`;
  }
  // 暫定取得: 財務指標・スコアは未取得扱いの「-」(会社予想・財務を取得すると表示)
  if (prov && !PROV_OK.has(col.k)) {
    if (col.kind === "flag" || col.kind === "flaglong") return `<td><span class="flag-off">-</span></td>`;
    return `<td class="num">-</td>`;
  }
  // 取得済みでも個別に値が無い(0)指標は「-」(営業利益率・増収率など出典の無い項目)
  if (["comma", "cfcomma", "num", "score", "buffett"].includes(col.kind) && (v == null || Number(v) === 0))
    return `<td class="num">-</td>`;
  switch (col.kind) {
    case "comma": return `<td class="num">${fcomma(v)}</td>`;
    case "cfcomma": return `<td class="num ${qClass(col.m, v)}">${fcomma(v)}</td>`;
    case "num": return `<td class="num ${col.m ? qClass(col.m, v) : ""}">${fnum(v, col.d)}</td>`;
    case "flag":
      if (col.k === "HasShareholderBenefit" && s.BenefitUnknown)
        return `<td><span class="flag-off" title="優待情報は未取得(CSV取込で反映)">?</span></td>`;
      return `<td>${flag(v)}</td>`;
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

// 列ヘッダークリックでソート。欠損(未取得/0/空)は常に末尾。
function sortResults() {
  const key = state.sort.key; if (!key) return;
  const col = COLS.find(c => c.k === key); if (!col) return;
  const dir = state.sort.dir;
  const isNum = !["id", "name", "text"].includes(col.kind);
  const val = (s) => {
    const v = gv(s, key);
    if (isNum) {
      if (!s.IndicatorsFetched) return null;
      const n = Number(v);
      return (v == null || isNaN(n) || n === 0) ? null : n;
    }
    return (v == null || v === "") ? null : String(v);
  };
  state.results.sort((a, b) => {
    const va = val(a), vb = val(b);
    if (va == null && vb == null) return 0;
    if (va == null) return 1;          // 欠損は末尾
    if (vb == null) return -1;
    return va < vb ? -dir : va > vb ? dir : 0;
  });
}

// 行の仮想化: 全銘柄×全列(数十万セル)を描画せず、画面に見える行だけを描画する。
// 上下に高さスペーサ行を置いてスクロール量を保ち、スクロールに応じて可視範囲のみ再描画する。
function rowsHtml(from, to) {
  const r = state.results; let out = "";
  for (let i = from; i < to; i++) {
    const s = r[i];
    out += `<tr data-code="${esc(s.Code)}" class="${i % 2 ? "" : "zeb"}">` + COLS.map(c => cell(c, s)).join("") + "</tr>";
  }
  return out;
}

function resultLabel() {
  return `${state.results.length} 件  ダブルクリックで個別分析 / 右クリックで比較 / 見出しで並べ替え`;
}

// 可視範囲だけ描画する(DOM は常に ~画面分の行のみ = 高速)。
function renderWindow() {
  const t = document.getElementById("screenTable");
  const wrap = t && t.closest(".table-wrap");
  const tbody = t && t.querySelector("tbody");
  if (!t || !wrap || !tbody) return;
  const total = state.results.length;
  if (total === 0) { tbody.innerHTML = ""; return; }
  // 初回は実際の行高さを測ってから仮想化(ブラウザ/拡大率の差を吸収)
  if (!state.rowH) {
    tbody.innerHTML = rowsHtml(0, Math.min(total, 20));
    const probe = tbody.querySelector("tr");
    state.rowH = probe ? probe.offsetHeight || 27 : 27;
  }
  const rh = state.rowH;
  const start = Math.max(0, Math.floor(wrap.scrollTop / rh) - 8);
  const vis = Math.ceil((wrap.clientHeight || 600) / rh) + 16;
  const end = Math.min(total, start + vis);
  const n = COLS.length;
  tbody.innerHTML =
    `<tr aria-hidden="true"><td colspan="${n}" style="padding:0;border:0;height:${start * rh}px"></td></tr>` +
    rowsHtml(start, end) +
    `<tr aria-hidden="true"><td colspan="${n}" style="padding:0;border:0;height:${(total - end) * rh}px"></td></tr>`;
}

function renderResults() {
  sortResults();
  const t = document.getElementById("screenTable");
  const head = "<thead><tr>" + COLS.map((c) => {
    const cls = c.kind === "id" ? "sticky" : c.kind === "name" ? "sticky2" : "";
    const arrow = state.sort.key === c.k ? `<span class="arrow">${state.sort.dir > 0 ? "▲" : "▼"}</span>` : "";
    return `<th class="${cls}" data-key="${esc(c.k)}" title="クリックで並べ替え">${c.label}${arrow}</th>`;
  }).join("") + "</tr></thead>";
  t.innerHTML = head + "<tbody></tbody>";

  // イベントは行ごとに付けず、テーブルに委譲(listener 数 ~7500 → 数個)。
  t.onclick = (e) => {
    const th = e.target.closest("thead th"); if (!th) return;
    const k = th.dataset.key;
    if (state.sort.key === k) state.sort.dir = -state.sort.dir; else { state.sort.key = k; state.sort.dir = 1; }
    renderResults();
  };
  t.ondblclick = (e) => { const tr = e.target.closest("tbody tr"); if (tr && tr.dataset.code) openAnalysis(tr.dataset.code); };
  t.oncontextmenu = (e) => { const tr = e.target.closest("tbody tr"); if (tr && tr.dataset.code) { e.preventDefault(); addToCompare(tr.dataset.code); } };

  const wrap = t.closest(".table-wrap");
  if (wrap && !wrap._vbound) {
    wrap._vbound = true;
    let ticking = false;
    wrap.addEventListener("scroll", () => { if (!ticking) { ticking = true; requestAnimationFrame(() => { ticking = false; renderWindow(); }); } });
    window.addEventListener("resize", () => requestAnimationFrame(renderWindow));
  }
  if (wrap) wrap.scrollTop = 0;
  renderWindow();
  document.getElementById("resultText").textContent = resultLabel();
  const unf = state.meta.UnfetchedCount, fetched = state.meta.FetchedCount;
  const full = state.meta.FullyFetchedCount ?? fetched;
  const prov = Math.max(0, fetched - full);
  document.getElementById("indicatorNotice").innerHTML =
    `全 ${state.meta.Total} 銘柄 / 実取得(会社予想・財務・スコア) <b>${full}</b> 件` +
    (prov > 0 ? ` ・ <span class="flag-prov">暫定 ${prov} 件</span>(株価/PER等のみ。開くと実取得)` : "") +
    (unf > 0 ? ` ・ <span class="flag-off">未取得 ${unf} 件</span>` : "");
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

async function openAnalysis(code, refresh = false) {
  switchView("analysis");
  document.getElementById("analysisDetail").innerHTML =
    `<div class="empty">${refresh ? "最新の実データを取得中" : "実データを取得中"}...(${esc(code)})<br><span style="font-size:11px">外部サイトから取得するため数秒かかることがあります</span></div>`;
  const data = await API.stock(code, refresh);
  state.selected = data.stock;
  state.links = data.links;
  state.lastFetched = data.lastFetched;
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
function metricCard(label, v, suffix = "") {
  let disp;
  if (typeof v === "number") disp = (v === 0 || isNaN(v)) ? "-" : fnum(v, 2) + suffix; // 0(出典なし)は「-」
  else disp = (v == null || v === "" || v === "-") ? "-" : esc(v) + suffix;
  return `<div class="card"><div class="k">${label}</div><div class="v">${disp}</div></div>`;
}

// ===== 個別分析: 全指標をカテゴリ別テーブルで表示 =====
// 値の表示: テキスト/日付/URLは常に、数値・真偽は未取得(fetched=false)なら「-」、
// 取得済みでも 0/null/空 は「-」(出典の無い項目)。
function dval(s, fetched, k, type) {
  const v = s[k];
  if (type === "text") return (v == null || v === "") ? "-" : esc(v);
  if (type === "url") return v ? `<a href="${esc(v)}" target="_blank" rel="noopener">${esc(v)}</a>` : "-";
  if (type === "date") return v ? new Date(v).toLocaleDateString("ja-JP") : "-";
  if (type === "bool") return fetched ? (v ? "あり" : "なし") : "-";
  if (!fetched) return "-";
  const n = Number(v);
  if (v == null || isNaN(n) || n === 0) return "-";
  switch (type) {
    case "pct1": return fnum(n, 1) + "%";
    case "pct2": return fnum(n, 2) + "%";
    case "num1": return fnum(n, 1);
    case "num2": return fnum(n, 2);
    case "int": return fnum(n, 0);
    case "money": return fcomma(n);
    default: return fnum(n, 2);
  }
}

function catTable(s, fetched, rows) {
  const body = rows.map(([k, label, type]) =>
    `<tr><th>${label}</th><td>${dval(s, fetched, k, type)}</td></tr>`).join("");
  return `<table class="kv">${body}</table>`;
}

// カテゴリ定義(Stock.cs の指標を網羅)
const CAT_VALUATION = [
  ["Price", "株価(円)", "money"], ["MarketCap", "時価総額(百万円)", "money"],
  ["PER", "PER(倍)", "num2"], ["PBR", "PBR(倍)", "num2"], ["MixFactor", "MIX係数", "num1"],
  ["ROE", "ROE", "pct1"], ["ROA", "ROA", "pct1"], ["TotalAssetTurnover", "総資産回転率(回)", "num2"],
  ["EPS", "EPS(円)", "num1"], ["BPS", "BPS(円)", "money"],
  ["OperatingMargin", "営業利益率", "pct1"], ["OrdinaryProfitMargin", "経常利益率", "pct1"], ["NetProfitMargin", "純利益率", "pct1"],
];
const CAT_DIVIDEND = [
  ["DividendYield", "配当利回り", "pct2"], ["PayoutRatio", "配当性向", "pct1"], ["Dividend", "1株配当(円)", "num1"],
  ["DividendTrend", "配当傾向", "text"], ["CumulativeDividend", "累進配当", "bool"], ["DoeAdopted", "DOE採用", "bool"],
  ["ConsecutiveDividendYears", "連続増配年数", "int"], ["DividendCutCount", "減配回数", "int"],
  ["NonDividendCutYears", "非減配年数", "int"], ["DividendRemainingYears", "配当金残年数", "num1"],
  ["BuybackAmount", "自社株買い(百万円)", "money"], ["ShareholderReturnPolicy", "還元方針", "text"],
  ["DividendGrowth1Y", "増配率1Y", "pct1"], ["DividendGrowth3Y", "増配率3Y", "pct1"],
  ["DividendGrowth5Y", "増配率5Y", "pct1"], ["DividendGrowth10Y", "増配率10Y", "pct1"],
];
const CAT_BENEFIT = [
  ["HasShareholderBenefit", "株主優待", "bool"], ["ShareholderBenefit", "優待(短縮)", "text"],
  ["BenefitContent", "優待内容", "text"], ["BenefitCategory", "優待カテゴリ", "text"],
  ["BenefitRightsMonth", "権利確定月", "text"], ["RequiredSharesForBenefit", "必要株数", "int"],
  ["BenefitValue", "優待価値(円)", "money"], ["BenefitYield", "優待利回り", "pct2"], ["TotalYield", "総合利回り", "pct2"],
  ["HasLongTermBenefit", "長期保有優遇", "bool"], ["LongTermBenefitCondition", "長期保有条件", "text"],
  ["LongTermBenefitContent", "長期保有優遇内容", "text"], ["BenefitRiskMemo", "廃止リスクメモ", "text"],
];
const CAT_FINANCE = [
  ["EquityRatio", "自己資本比率", "pct1"], ["InterestBearingDebtRatio", "有利子負債比率", "pct1"],
];
const CAT_GROWTH = [
  ["RevenueGrowth1Y", "増収率1Y", "pct1"], ["RevenueGrowth3Y", "増収率3Y", "pct1"],
  ["RevenueGrowth5Y", "増収率5Y", "pct1"], ["RevenueGrowth10Y", "増収率10Y", "pct1"],
  ["RevenueGrowthRate", "売上高成長率", "pct1"], ["AverageRevenueGrowth3Y", "平均増収率3Y", "pct1"],
  ["OperatingProfitGrowthRate", "営業利益成長率", "pct1"], ["OrdinaryProfitGrowthRate", "経常利益成長率", "pct1"],
  ["NetProfitGrowthRate", "純利益成長率", "pct1"], ["EpsGrowthRate", "EPS成長率", "pct1"],
];
const CAT_CF = [
  ["OperatingCF", "営業CF(百万円)", "money"], ["InvestingCF", "投資CF(百万円)", "money"],
  ["FinancingCF", "財務CF(百万円)", "money"], ["FreeCashFlow", "フリーCF(百万円)", "money"],
  ["OperatingCashFlowMargin", "営業CFマージン", "pct1"],
];
const CAT_PRICECHG = [
  ["StockPriceChange3M", "3ヶ月株価変化率", "pct1"], ["AverageStockPriceChange3M", "3ヶ月平均株価変化率", "pct1"],
  ["AveragePrice3M", "3ヶ月平均株価(円)", "money"], ["PriceChange3M", "株価変化率(別系列)", "pct1"],
  ["PriceChangeAverage3M", "平均株価変化率(別系列)", "pct1"],
];
const CAT_SCORES = [
  ["SafetyScore", "安全性", "int"], ["GrowthScore", "成長性", "int"], ["ProfitabilityScore", "収益性", "int"],
  ["ReturnScore", "還元性", "int"], ["EfficiencyScore", "効率性", "int"], ["ValuationScore", "割安性", "int"],
  ["LongTermScore", "長期適性", "int"], ["RevaluationScore", "再評価期待", "int"], ["BuffettScore", "バフェット", "int"],
  ["WantToBuyScore", "買いたい度", "int"], ["OverallScore", "総合評価", "int"], ["OverallGrade", "総合グレード", "text"],
  ["JudgementText", "総合判定", "text"], ["UserInterest", "興味度", "int"],
];

function renderDetail() {
  const s = state.selected;
  const el = document.getElementById("analysisDetail");
  const links = (state.links || []).map(l => `<a href="${esc(l.Url)}" target="_blank" rel="noopener">${esc(l.Name)}</a>`).join("");
  const F = s.IndicatorsFetched;
  const basicRows = [
    ["Market", "市場", "text"], ["Sector", "業種", "text"], ["Scale", "規模", "text"],
    ["Theme", "テーマ", "text"], ["FiscalMonth", "決算月", "text"], ["DataUpdated", "データ更新日", "date"],
    ["Description", "企業概要", "text"], ["IRUrl", "IRリンク", "url"],
  ];
  const benefitHtml = s.BenefitUnknown
    ? `<div class="desc">株主優待は自動取得の対象外です。<b>未取得</b>(CSV取込で反映されます)。</div>` +
      catTable(s, F, [["DividendYield", "配当利回り", "pct2"], ["TotalYield", "総合利回り", "pct2"]])
    : catTable(s, F, CAT_BENEFIT);
  const catBlock = `
    <div class="detail-tables">
      <div class="box"><h3>基本情報</h3>${catTable(s, true, basicRows)}</div>
      <div class="box"><h3>バリュエーション</h3>${catTable(s, F, CAT_VALUATION)}</div>
      <div class="box"><h3>配当・株主還元</h3>${catTable(s, F, CAT_DIVIDEND)}</div>
      <div class="box"><h3>株主優待</h3>${benefitHtml}</div>
      <div class="box"><h3>財務</h3>${catTable(s, F, CAT_FINANCE)}</div>
      <div class="box"><h3>成長性</h3>${catTable(s, F, CAT_GROWTH)}</div>
      <div class="box"><h3>キャッシュフロー</h3>${catTable(s, F, CAT_CF)}</div>
      <div class="box"><h3>株価変化</h3>${catTable(s, F, CAT_PRICECHG)}</div>
      <div class="box"><h3>スコア</h3>${catTable(s, F, CAT_SCORES)}</div>
    </div>`;
  const bf = s.Buffett || {};
  const bfRows = [
    ["事業耐久力", bf.BusinessDurabilityScore, "10年後も稼げるか(利益率・長期成長・利益とCFの安定・堀)"],
    ["収益力", bf.ProfitabilityScore, "ROE・ROA・利益率・営業CFマージン"],
    ["財務安全性", bf.SafetyScore, "自己資本比率・有利子負債・FCF・配当余力(金融業は専用基準)"],
    ["成長安定性", bf.GrowthStabilityScore, "売上・営業利益・EPS成長と下方耐性"],
    ["株主還元・資本配分", bf.CapitalAllocationScore, "連続増配・増配率・性向・自社株買い・総利回り"],
    ["割安性", bf.ValuationScore, "PER・PBR・MIX・FCF利回り(質を加味)"],
  ];
  const buffettBox = (s.IndicatorsFetched && !s.Provisional) ? `
    <div class="box">
      <h3>バフェット採点
        <span class="bd-total ${qClass("buffett", bf.BuffettScore || 0)}">${Math.round(bf.BuffettScore || 0)} / 100</span>
        <span class="bd-grade">${esc(bf.OverallGrade || "-")}</span>
        <span class="bd-conf">データ信頼度 ${Math.round(bf.DataConfidence || 0)}%</span>
      </h3>
      <div class="desc">採点プロファイル: <b>${esc(bf.Profile || "StandardCompany")}</b>${bf.Profile === "TradingCompany" ? " — 卸売業・総合商社は営業利益率の絶対値だけで評価せず、ROE・CF・資本配分・財務安全性を重視して補正しています。" : ""}</div>
      <div class="desc">${esc(bf.JudgementText || "")}</div>
      <div class="desc" style="font-size:11px">配点: 事業耐久力25% / 収益力20% / 財務安全性15% / 成長安定性15% / 資本配分10% / 割安性15%。
      事業の質を割安性より重視。欠損指標は除外して重み再配分し、未取得が多い銘柄はデータ信頼度に応じて上限を設けます。</div>
      <div class="buffett-bd">
        ${bfRows.map(([label, v, note]) => {
          const val = Math.round(v || 0);
          const q = val >= 70 ? "good" : val >= 45 ? "ok" : val >= 25 ? "caution" : "bad";
          return `<div class="bd-row">
            <div class="bd-h"><span>${esc(label)}</span><span class="bd-pts">${val} / 100</span></div>
            <div class="bd-bar"><div class="bd-fill ${q}" style="width:${val}%"></div></div>
            <div class="bd-note">${esc(note)}</div>
          </div>`;
        }).join("")}
      </div>
      <div class="reasons">
        <div><span class="rk good">高評価要因</span> ${esc(bf.HighScoreReasons || "-")}</div>
        <div><span class="rk bad">減点要因</span> ${esc(bf.PenaltyReasons || "-")}</div>
        <div><span class="rk">ランク判定</span> ${esc(bf.RankDecisionReasons || "-")}</div>
        ${bf.UsedWeights ? `<div><span class="rk">使用重み</span> 事業耐久力${Math.round(bf.UsedWeights.BusinessDurabilityWeight*100)}% / 収益力${Math.round(bf.UsedWeights.ProfitabilityWeight*100)}% / 財務安全${Math.round(bf.UsedWeights.SafetyWeight*100)}% / 成長${Math.round(bf.UsedWeights.GrowthStabilityWeight*100)}% / 資本配分${Math.round(bf.UsedWeights.CapitalAllocationWeight*100)}% / 割安${Math.round(bf.UsedWeights.ValuationWeight*100)}%${bf.CalibrationInfo ? "（" + esc(bf.CalibrationInfo) + "）" : ""}</div>` : ""}
      </div>
    </div>` : "";
  el.innerHTML = `
    <div class="head">
      <span class="name">${esc(s.Name)}</span><span class="code">${esc(s.Code)}</span>
    </div>
    <div class="tags">
      <span>${esc(s.Market)}</span><span>${esc(s.Sector)}</span><span>${esc(s.Scale)}</span>
      ${s.Theme ? `<span>${esc(s.Theme)}</span>` : ""}${s.FiscalMonth ? `<span>決算 ${esc(s.FiscalMonth)}</span>` : ""}
      ${s.IndicatorsFetched
        ? `<span class="flag-on">実データ${state.lastFetched ? "(" + new Date(state.lastFetched).toLocaleDateString("ja-JP") + ")" : ""}</span>`
        : `<span class="flag-off">実データ未取得</span>`}
      <button class="btn" id="btnRefresh" style="margin-left:6px">実データ更新</button>
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
      ${scoreCard("総合評価(" + ((s.Buffett && s.Buffett.OverallGrade) || "-") + ")", s.BuffettScore, "buffett")}
      ${scoreCard("長期適性", s.LongTermScore)}
      ${scoreCard("再評価期待", s.RevaluationScore)}
      ${scoreCard("買いたい度", s.WantToBuyScore)}
      ${scoreCard("参考: 旧総合(" + esc(s.OverallGrade) + ")", s.OverallScore)}
    </div>

    ${buffettBox}

    ${catBlock}

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
  const rb = document.getElementById("btnRefresh");
  if (rb) rb.onclick = () => openAnalysis(s.Code, true);
}

async function saveUserData() {
  const s = state.selected;
  const check = {};
  document.querySelectorAll('[data-bk]').forEach(sel => check[sel.dataset.bk] = Number(sel.value));
  const memo = { Classification: document.getElementById("memo_Classification").value };
  for (const [k] of MEMO_FIELDS) memo[k] = document.getElementById("memo_" + k).value;
  const interest = Number(document.getElementById("memo_Interest").value);
  const res = await API.saveUser(s.Code, { Memo: memo, BuffettCheck: check, UserInterest: interest });
  const updated = res.stock || res; // { stock }
  state.selected = updated;
  // 一覧キャッシュも更新
  const idx = state.allCache.findIndex(x => x.Code === updated.Code);
  if (idx >= 0) Object.assign(state.allCache[idx], { BuffettScore: updated.BuffettScore, OverallScore: updated.OverallScore, Buffett: updated.Buffett });
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

// [キー, ラベル, 書式, 色メトリック?]
const CMP_ROWS = [
  ["Price", "株価(円)", "comma"], ["MarketCap", "時価総額(百万)", "comma"],
  ["PER", "PER", "num2", "per"], ["PBR", "PBR", "num2", "pbr"], ["MixFactor", "MIX係数", "num1", "mix"],
  ["ROE", "ROE%", "pct1", "roe"], ["ROA", "ROA%", "pct1", "roe"], ["TotalAssetTurnover", "総資産回転率", "num2"],
  ["EPS", "EPS(円)", "num1"], ["BPS", "BPS(円)", "comma"],
  ["DividendYield", "配当利回り%", "pct2", "dy"], ["PayoutRatio", "配当性向%", "pct1", "payout"],
  ["Dividend", "1株配当(円)", "num1"], ["TotalYield", "総合利回り%", "pct2", "totalyield"],
  ["EquityRatio", "自己資本比率%", "pct1", "equity"], ["InterestBearingDebtRatio", "有利子負債比率%", "pct1", "debt"],
  ["OperatingMargin", "営業利益率%", "pct1", "margin"], ["OrdinaryProfitMargin", "経常利益率%", "pct1", "margin"],
  ["NetProfitMargin", "純利益率%", "pct1", "margin"],
  ["RevenueGrowth1Y", "増収率1Y%", "pct1", "growth"], ["RevenueGrowth3Y", "増収率3Y%", "pct1", "growth"],
  ["RevenueGrowth5Y", "増収率5Y%", "pct1", "growth"], ["RevenueGrowth10Y", "増収率10Y%", "pct1", "growth"],
  ["OperatingProfitGrowthRate", "営業利益成長率%", "pct1", "growth"], ["OrdinaryProfitGrowthRate", "経常利益成長率%", "pct1", "growth"],
  ["NetProfitGrowthRate", "純利益成長率%", "pct1", "growth"], ["EpsGrowthRate", "EPS成長率%", "pct1", "growth"],
  ["OperatingCF", "営業CF(百万)", "comma", "cf"], ["InvestingCF", "投資CF(百万)", "comma"],
  ["FinancingCF", "財務CF(百万)", "comma"], ["FreeCashFlow", "フリーCF(百万)", "comma", "cf"],
  ["OperatingCashFlowMargin", "営業CFマージン%", "pct1", "cfmargin"],
  ["StockPriceChange3M", "3M株価変化%", "pct1", "change"], ["AverageStockPriceChange3M", "3M平均変化%", "pct1", "change"],
  ["SafetyScore", "安全性", "int", "score"], ["GrowthScore", "成長性", "int", "score"],
  ["ProfitabilityScore", "収益性", "int", "score"], ["ReturnScore", "還元性", "int", "score"],
  ["EfficiencyScore", "効率性", "int", "score"], ["ValuationScore", "割安性", "int", "score"],
  ["LongTermScore", "長期適性", "int", "score"], ["RevaluationScore", "再評価", "int", "score"],
  ["WantToBuyScore", "買いたい度", "int", "score"], ["OverallScore", "総合", "int", "score"],
  // バフェット採点(総合・6サブスコア・信頼度・ランク)
  ["BuffettScore", "バフェット総合", "int", "buffett"],
  ["Buffett.OverallGrade", "評価ランク", "text"],
  ["Buffett.DataConfidence", "データ信頼度%", "int"],
  ["Buffett.BusinessDurabilityScore", "事業耐久力", "int", "score"],
  ["Buffett.ProfitabilityScore", "収益力", "int", "score"],
  ["Buffett.SafetyScore", "財務安全性", "int", "score"],
  ["Buffett.GrowthStabilityScore", "成長安定性", "int", "score"],
  ["Buffett.CapitalAllocationScore", "資本配分", "int", "score"],
  ["Buffett.ValuationScore", "割安性(B)", "int", "score"],
];

function cmpVal(s, k, fmt) {
  if (!s.IndicatorsFetched) return "-";
  const v = gv(s, k);
  if (fmt === "text") return v ? esc(String(v)) : "-";
  const n = Number(v);
  if (v == null || isNaN(n) || n === 0) return "-";
  switch (fmt) {
    case "comma": return fcomma(n);
    case "int": return fnum(n, 0);
    case "pct1": return fnum(n, 1) + "%";
    case "pct2": return fnum(n, 2) + "%";
    case "num1": return fnum(n, 1);
    default: return fnum(n, 2);
  }
}

async function renderCompare() {
  const chips = document.getElementById("compareChips");
  chips.innerHTML = state.compare.map(c => `<span class="chip" data-code="${esc(c)}">${esc(c)}<span class="x">✕</span></span>`).join("")
    || `<span style="color:var(--sub)">比較対象なし</span>`;
  chips.querySelectorAll(".chip").forEach(ch => ch.onclick = () => removeCompare(ch.dataset.code));

  const body = document.getElementById("compareBody");
  if (state.compare.length === 0) { body.innerHTML = `<div class="empty">左の一覧から銘柄を選んで比較に追加してください(最大6件)。</div>`; return; }
  const stocks = await API.compare(state.compare);
  let html = "<table class='cmp'><thead><tr><th>指標</th>" + stocks.map(s => `<th>${esc(s.Name)}<br><span style="color:var(--sub)">${esc(s.Code)}</span></th>`).join("") + "</tr></thead><tbody>";
  for (const [k, label, fmt, m] of CMP_ROWS) {
    html += `<tr><th>${label}</th>` + stocks.map(s => {
      const v = gv(s, k);
      const cls = (m && s.IndicatorsFetched && v != null && Number(v) !== 0) ? qClass(m, v) : "";
      return `<td class="${cls}">${cmpVal(s, k, fmt)}</td>`;
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
