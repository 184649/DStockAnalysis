#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
DStockAnalysis 指標取得スクリプト (標準ライブラリのみ / pip 不要)

対象サイト: IR BANK / 株探 / 株マップ / みんかぶ / みんかぶ優待(株主優待ガイド相当)
  ※ バフェット・コードは robots.txt が AI/各種クローラを拒否し、データもログイン制のため対象外。

責任ある取得のための方針:
  - robots.txt を必ず確認し、Disallow のURLは取得しない。Crawl-delay も尊重。
  - リクエスト間隔を十分に空ける(既定8秒 + ゆらぎ)。短期売買用ではないため低速で良い。
  - ウォッチリスト(codes.txt)の銘柄だけを対象にし、全銘柄一括の高負荷アクセスはしない。
  - キャッシュにより、既定6日以内に取得済みの銘柄は再取得しない(週1運用想定)。
  - 取得できた項目だけを出力(不確実な値は空欄)。出力CSVは「列単位マージ」で安全に取込可能。

使い方:
  python fetch_data.py --test 7203      # 1銘柄だけ取得して結果を表示(キャッシュ非更新)
  python fetch_data.py                  # codes.txt の銘柄を取得して output/stocks_real.csv を生成
  python fetch_data.py --force          # キャッシュを無視して再取得
  python fetch_data.py --delay 12       # リクエスト間隔(秒)を変更
出力CSVをアプリの「CSV取込」で読み込むと、記入済み列のみ実データで上書きされます。
"""
import argparse
import csv
import json
import os
import random
import re
import sys
import time
import urllib.request
import urllib.robotparser
from datetime import datetime, timezone

HERE = os.path.dirname(os.path.abspath(__file__))
CODES_FILE = os.path.join(HERE, "codes.txt")
OUT_DIR = os.path.join(HERE, "output")
OUT_CSV = os.path.join(OUT_DIR, "stocks_real.csv")
CACHE_FILE = os.path.join(HERE, ".cache", "fetch_state.json")

UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) DStockAnalysis-personal/1.0 (weekly; local research)"

# 出力する列(アプリのCSV取込が解釈する列名)
# 株主優待は、みんかぶ優待ページが非実施銘柄でもテンプレ項目を含み誤検出が避けられないため
# 自動取得対象から除外。優待は個別分析画面のリンク(株主優待ガイド/みんかぶ優待)で確認してください。
OUTPUT_COLUMNS = [
    "Code", "Price", "PER", "PBR", "ROE", "DividendYield", "PayoutRatio",
    "MarketCap", "EquityRatio", "EPS",
]

_robots_cache = {}


def log(msg):
    print(msg, flush=True)


def robots_allowed(url):
    """robots.txt を確認し、取得可否と crawl-delay を返す。
    robots.txt 自体を通常UAで取得して解析する(既定UAだと 403 で全拒否扱いになるサイトがあるため)。"""
    from urllib.parse import urlparse
    p = urlparse(url)
    base = f"{p.scheme}://{p.netloc}"
    rp = _robots_cache.get(base)
    if rp is None:
        rp = urllib.robotparser.RobotFileParser()
        try:
            req = urllib.request.Request(base + "/robots.txt", headers={"User-Agent": UA})
            with urllib.request.urlopen(req, timeout=20) as r:
                text = r.read().decode("utf-8", "replace")
            rp.parse(text.splitlines())
        except Exception:
            rp.parse(["User-agent: *", "Allow: /"])  # 取得失敗時は許可扱い(404相当)
        _robots_cache[base] = rp
    try:
        allowed = rp.can_fetch(UA, url)
    except Exception:
        allowed = True
    try:
        delay = rp.crawl_delay(UA) or rp.crawl_delay("*")
    except Exception:
        delay = None
    return allowed, delay


def fetch(url, timeout=25):
    """1ページ取得。robots不許可なら None。文字コードはmeta優先で判定。"""
    allowed, _ = robots_allowed(url)
    if not allowed:
        log(f"    [robots] 取得不可のためスキップ: {url}")
        return None
    req = urllib.request.Request(url, headers={"User-Agent": UA, "Accept-Language": "ja,en;q=0.8"})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            raw = r.read()
    except Exception as e:
        log(f"    [error] {url} : {e}")
        return None
    enc = "utf-8"
    m = re.search(rb'charset=["\']?([\w\-]+)', raw[:3000])
    if m:
        enc = m.group(1).decode("ascii", "ignore") or "utf-8"
    try:
        html = raw.decode(enc, "replace")
    except LookupError:
        html = raw.decode("utf-8", "replace")
    return html


def strip(html):
    t = re.sub(r"<[^>]+>", " ", html)
    return re.sub(r"\s+", " ", t)


def num(s):
    try:
        return float(s.replace(",", ""))
    except Exception:
        return None


def find(text, pattern, conv=num):
    m = re.search(pattern, text)
    if not m:
        return None
    return conv(m.group(1)) if conv else m.group(1)


def to_million_yen(value, unit):
    if value is None:
        return None
    if unit == "兆":
        return round(value * 1_000_000)
    if unit == "億":
        return round(value * 100)
    return round(value)  # 既に百万円想定


# ---------- 各サイトの抽出(主担当) ----------

def from_minkabu(code, delay_fn):
    """みんかぶ: 株価/PER/PBR/配当利回り。"""
    html = fetch(f"https://minkabu.jp/stock/{code}")
    delay_fn()
    if not html:
        return {}
    t = strip(html)
    d = {}
    d["Price"] = find(t, r"現在値[^0-9\-]*([0-9,]+(?:\.[0-9]+)?)")
    d["PER"] = find(t, r"PER[^0-9\-]*([0-9,]+\.?[0-9]*)\s*倍")
    d["PBR"] = find(t, r"PBR[^0-9\-]*([0-9,]+\.?[0-9]*)\s*倍")
    d["DividendYield"] = find(t, r"配当利回り[^0-9\-]*([0-9]+\.?[0-9]*)\s*[%％]")
    return {k: v for k, v in d.items() if v is not None}


def from_irbank(code, delay_fn):
    """IR BANK: ROE/PER/PBR/配当利回り/EPS/時価総額/自己資本比率。"""
    html = fetch(f"https://irbank.net/{code}")
    delay_fn()
    if not html:
        return {}
    t = strip(html)
    d = {}
    d["ROE"] = find(t, r"ROE[^0-9\-]*([0-9]+\.?[0-9]*)\s*[%％]")
    d["PER"] = find(t, r"PER[^0-9\-]*([0-9,]+\.?[0-9]*)\s*倍")
    d["PBR"] = find(t, r"PBR[^0-9\-]*([0-9,]+\.?[0-9]*)\s*倍")
    d["DividendYield"] = find(t, r"配当利回り[^0-9\-]*([0-9]+\.?[0-9]*)\s*[%％]")
    d["EPS"] = find(t, r"\bEPS[^0-9\-]*([0-9,]+\.?[0-9]*)")
    d["EquityRatio"] = find(t, r"自己資本比率[^0-9\-]*([0-9]+\.?[0-9]*)\s*[%％]")
    d["PayoutRatio"] = find(t, r"配当性向[^0-9\-]*([0-9]+\.?[0-9]*)\s*[%％]")
    mm = re.search(r"時価総額[^0-9\-]*([0-9,]+\.?[0-9]*)\s*(兆|億)", t)
    if mm:
        d["MarketCap"] = to_million_yen(num(mm.group(1)), mm.group(2))
    return {k: v for k, v in d.items() if v is not None}


def from_kabutan(code, delay_fn):
    """株探(任意): 株価/PER/PBR/利回り。robotsでCrawl-delay 3。"""
    html = fetch(f"https://kabutan.jp/stock/?code={code}")
    delay_fn()
    if not html:
        return {}
    t = strip(html)
    d = {}
    d["Price"] = find(t, r"現在値[^0-9\-]*([0-9,]+(?:\.[0-9]+)?)")
    m = re.search(r"PER\s+PBR[^0-9\-]*([0-9]+\.[0-9]+)\s+([0-9]+\.[0-9]+)\s+([0-9]+\.[0-9]+)", t)
    if m:
        d["PER"], d["PBR"], d["DividendYield"] = num(m.group(1)), num(m.group(2)), num(m.group(3))
    return {k: v for k, v in d.items() if v is not None}


def merge(code, delay_fn, use_kabutan):
    """各ソースを統合。優先度: IR BANK -> みんかぶ -> 株探(任意)。"""
    row = {"Code": code}
    for src in (from_irbank, from_minkabu):
        for k, v in src(code, delay_fn).items():
            row.setdefault(k, v)
    if use_kabutan:
        for k, v in from_kabutan(code, delay_fn).items():
            row.setdefault(k, v)
    return row


def load_cache():
    try:
        with open(CACHE_FILE, encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return {}


def save_cache(c):
    os.makedirs(os.path.dirname(CACHE_FILE), exist_ok=True)
    with open(CACHE_FILE, "w", encoding="utf-8") as f:
        json.dump(c, f, ensure_ascii=False, indent=0)


def read_codes():
    if not os.path.exists(CODES_FILE):
        sample = ["7203", "8058", "9433", "6758", "8035", "2914", "9831", "3048"]
        with open(CODES_FILE, "w", encoding="utf-8") as f:
            f.write("# 1行1銘柄コード。# で始まる行はコメント。\n")
            f.write("\n".join(sample) + "\n")
        log(f"codes.txt を作成しました(サンプル {len(sample)} 銘柄)。対象銘柄を編集してください: {CODES_FILE}")
    codes = []
    with open(CODES_FILE, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line and not line.startswith("#"):
                codes.append(line.split(",")[0].strip())
    return codes


def main():
    ap = argparse.ArgumentParser(description="DStockAnalysis 指標取得(robots順守・低速・週次)")
    ap.add_argument("--test", metavar="CODE", help="1銘柄だけ取得して表示(キャッシュ非更新)")
    ap.add_argument("--delay", type=float, default=8.0, help="リクエスト間隔の基準秒(既定8)")
    ap.add_argument("--max-age-days", type=int, default=6, help="この日数以内に取得済みなら再取得しない(既定6)")
    ap.add_argument("--force", action="store_true", help="キャッシュを無視して全件取得")
    ap.add_argument("--kabutan", action="store_true", help="株探も取得対象に含める(Crawl-delay 3秒順守)")
    args = ap.parse_args()

    def delay_fn():
        time.sleep(args.delay + random.uniform(0, args.delay * 0.5))

    if args.test:
        log(f"=== TEST: {args.test} ===")
        row = merge(args.test, delay_fn, args.kabutan)
        for k in OUTPUT_COLUMNS:
            log(f"  {k} = {row.get(k, '')}")
        return

    codes = read_codes()
    if not codes:
        log("codes.txt に銘柄がありません。")
        return
    if len(codes) > 200:
        log(f"[注意] 対象が {len(codes)} 銘柄です。サイト負荷に配慮し、分割実行を推奨します。")

    cache = {} if args.force else load_cache()
    now = datetime.now(timezone.utc)
    rows, done, skipped = [], 0, 0

    os.makedirs(OUT_DIR, exist_ok=True)
    # 既存出力を読み込み、再取得しない銘柄の行を保持
    existing = {}
    if os.path.exists(OUT_CSV):
        try:
            with open(OUT_CSV, encoding="utf-8-sig", newline="") as f:
                for r in csv.DictReader(f):
                    existing[r.get("Code", "")] = r
        except Exception:
            pass

    for i, code in enumerate(codes, 1):
        last = cache.get(code)
        if last and not args.force:
            try:
                age = (now - datetime.fromisoformat(last)).days
                if age < args.max_age_days:
                    skipped += 1
                    if code in existing:
                        rows.append(existing[code])
                    continue
            except Exception:
                pass
        log(f"[{i}/{len(codes)}] {code} 取得中...")
        row = merge(code, delay_fn, args.kabutan)
        if len(row) > 1:
            rows.append(row)
            cache[code] = now.isoformat()
            done += 1
        else:
            log(f"    取得できた項目がありませんでした({code})")

    # CSV 出力
    with open(OUT_CSV, "w", encoding="utf-8-sig", newline="") as f:
        w = csv.DictWriter(f, fieldnames=OUTPUT_COLUMNS, extrasaction="ignore")
        w.writeheader()
        for r in rows:
            w.writerow(r)
    save_cache(cache)
    log(f"完了: 取得 {done} / スキップ(期間内) {skipped} / 出力 {len(rows)} 行")
    log(f"出力: {OUT_CSV}")
    log("アプリの「CSV取込」でこのファイルを読み込むと、取得済み列だけが実データで上書きされます。")


if __name__ == "__main__":
    main()
