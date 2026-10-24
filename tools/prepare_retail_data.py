"""Derive the demand profile of the simulator from UCI Online Retail II.

Only order *patterns* are taken from the data (hour-of-day profile, basket size, line quantity and the
rank-frequency curve of product popularity); the fulfilment network itself stays synthetic, as the
project brief allows. The raw dataset is not redistributed.

Usage (from the repository root):
    python tools/prepare_retail_data.py data/raw/online_retail_ii.zip \
        Assets/StreamingAssets/configs/demand_profile.json docs/data/retail_profile_stats.json

Dataset: Chen, D. (2012). Online Retail II [Dataset]. UCI Machine Learning Repository.
https://doi.org/10.24432/C5CG6D  (CC BY 4.0)
"""

import json
import sys
import zipfile
from pathlib import Path

import numpy as np
import pandas as pd

MAX_BASKET = 5          # the simulator draws 1..5 distinct SKUs per order
MAX_QUANTITY = 3        # and 1..3 units per line
TOP_SKUS = 30           # size of the simulator's SKU catalogue
NIGHT_FLOOR = 0.05      # hours with no data get 5 % of the mean hourly rate
SHEETS = ["Year 2009-2010", "Year 2010-2011"]


def load(zip_path: Path) -> pd.DataFrame:
    """Read both yearly sheets; a CSV cache next to the zip makes reruns fast."""
    cache = zip_path.with_suffix(".cache.csv.gz")
    if cache.exists():
        return pd.read_csv(cache, dtype={"Invoice": str, "StockCode": str}, parse_dates=["InvoiceDate"])

    with zipfile.ZipFile(zip_path) as archive:
        name = next(n for n in archive.namelist() if n.lower().endswith(".xlsx"))
        with archive.open(name) as handle:
            frames = pd.read_excel(handle, sheet_name=SHEETS, dtype={"Invoice": str, "StockCode": str})
    df = pd.concat(frames.values(), ignore_index=True)
    df.to_csv(cache, index=False, compression="gzip")
    return df


def clean(df: pd.DataFrame) -> tuple[pd.DataFrame, dict]:
    steps = {"raw_rows": int(len(df))}
    df = df.drop_duplicates()
    steps["after_duplicates"] = int(len(df))
    df = df[~df["Invoice"].str.startswith("C")]                     # cancellations
    steps["after_cancellations"] = int(len(df))
    df = df[(df["Quantity"] > 0) & (df["Price"] > 0)]               # returns, adjustments, free items
    steps["after_non_positive"] = int(len(df))
    df = df[df["StockCode"].str.contains(r"\d", regex=True)]        # POST, DOT, M, BANK CHARGES ...
    steps["after_non_product_codes"] = int(len(df))
    # One line per (invoice, product): the simulator draws distinct SKUs per basket.
    df = df.groupby(["Invoice", "StockCode"], as_index=False).agg(
        Quantity=("Quantity", "sum"), InvoiceDate=("InvoiceDate", "min"))
    steps["order_lines"] = int(len(df))
    steps["invoices"] = int(df["Invoice"].nunique())
    return df, steps


def zipf_exponent(counts: np.ndarray) -> float:
    ranks = np.arange(1, len(counts) + 1)
    slope, _ = np.polyfit(np.log(ranks), np.log(counts), 1)
    return float(-slope)


def main(zip_path: Path, profile_path: Path, stats_path: Path) -> None:
    df, steps = clean(load(zip_path))

    invoices = df.groupby("Invoice").agg(lines=("StockCode", "size"), time=("InvoiceDate", "min"))

    # Hour of day: share of invoices per hour, scaled to mean 1, empty hours get a small floor.
    hour_counts = invoices["time"].dt.hour.value_counts().reindex(range(24), fill_value=0).to_numpy(float)
    hour_weights = hour_counts / hour_counts.mean()
    empty_hours = [h for h in range(24) if hour_counts[h] == 0]
    hour_weights = np.maximum(hour_weights, NIGHT_FLOOR)
    hour_weights = hour_weights / hour_weights.mean()

    # Basket size: distribution of small orders (1..5 distinct products), the B2C-like part of the data.
    small = invoices[invoices["lines"] <= MAX_BASKET]
    basket = small["lines"].value_counts().reindex(range(1, MAX_BASKET + 1), fill_value=0).to_numpy(float)
    basket_weights = basket / basket.sum()

    # Quantity per line within those small orders, limited to 1..3 units.
    small_lines = df[df["Invoice"].isin(small.index) & (df["Quantity"] <= MAX_QUANTITY)]
    quantity = small_lines["Quantity"].value_counts().reindex(range(1, MAX_QUANTITY + 1), fill_value=0).to_numpy(float)
    quantity_weights = quantity / quantity.sum()

    # Product popularity: invoices containing each product; the top 30 map to SKU-01..SKU-30.
    popularity = df.groupby("StockCode")["Invoice"].nunique().sort_values(ascending=False)
    top = popularity.head(TOP_SKUS).to_numpy(float)
    sku_weights = top / top.sum()

    profile = {
        "source": ("UCI Online Retail II (CC BY 4.0), tools/prepare_retail_data.py: hour profile of invoices "
                   f"(empty hours floored at {NIGHT_FLOOR:.0%} of mean), basket size of orders with 1-{MAX_BASKET} products, "
                   f"line quantity 1-{MAX_QUANTITY}, top-{TOP_SKUS} product rank-frequency"),
        "hourWeights": [round(float(w), 4) for w in hour_weights],
        "skuWeights": [round(float(w), 5) for w in sku_weights],
        "zipfExponent": round(zipf_exponent(top), 4),
        "basketSizeWeights": [round(float(w), 4) for w in basket_weights],
        "quantityWeights": [round(float(w), 4) for w in quantity_weights],
    }

    all_lines = invoices["lines"]
    stats = {
        "dataset": "UCI Online Retail II, https://doi.org/10.24432/C5CG6D",
        "period": [str(df["InvoiceDate"].min()), str(df["InvoiceDate"].max())],
        "cleaning": steps,
        "products": int(len(popularity)),
        "lines_per_invoice": {"mean": float(all_lines.mean()), "median": float(all_lines.median()),
                              "share_with_at_most_5": float((all_lines <= MAX_BASKET).mean())},
        "small_orders_used": int(len(small)),
        "small_order_lines_used": int(len(small_lines)),
        "empty_hours_in_data": empty_hours,
        "hour_invoice_counts": [int(c) for c in hour_counts],
        "top30_invoice_counts": [int(c) for c in top],
        "top30_stock_codes": [str(c) for c in popularity.head(TOP_SKUS).index],
        "zipf_exponent_top30": zipf_exponent(top),
        "zipf_exponent_top1000": zipf_exponent(popularity.head(1000).to_numpy(float)),
        "mean_basket_size_in_profile": float(np.dot(np.arange(1, MAX_BASKET + 1), basket_weights)),
        "mean_quantity_in_profile": float(np.dot(np.arange(1, MAX_QUANTITY + 1), quantity_weights)),
    }

    profile_path.write_text(json.dumps(profile, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    stats_path.parent.mkdir(parents=True, exist_ok=True)
    stats_path.write_text(json.dumps(stats, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({"profile": profile, "stats": stats}, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    if len(sys.argv) != 4:
        sys.exit(__doc__)
    main(Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3]))
