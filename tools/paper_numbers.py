"""Collect every number and table the paper quotes into one JSON file, straight from the exported results.

The paper source refers to these values by key ({{n:...}} and {{table:...}}), so a re-run of the experiments and of
this script updates the paper without copying numbers by hand. Numbers are formatted the Turkish way (1.234,5).

Usage (from the repository root, after tools/analyze.py, analyze_evolution.py and analyze_week3lab.py):
    python tools/paper_numbers.py --out paper/numbers.json
"""

import argparse
import json
import sys
from pathlib import Path

import numpy as np
import pandas as pd
from scipy import stats

sys.path.insert(0, str(Path(__file__).resolve().parent))
from analyze import DIRECTION, SCENARIOS, SCENARIO_TR, ci95  # noqa: E402


def tr(x: float, digits: int = 0, sign: bool = False) -> str:
    """Turkish number format: dot for thousands, comma for decimals, a real minus sign."""
    if round(x, digits) == 0:
        x, sign = 0.0, False  # no "-0,00" or "+0,00"
    text = f"{x:{'+' if sign else ''},.{digits}f}"
    return text.replace(",", "\0").replace(".", ",").replace("\0", ".").replace("-", "−")


def p_text(p: float) -> str:
    if np.isnan(p):
        return "-"
    return "< 0,001" if p < 0.001 else tr(p, 3)


def paired(runs: pd.DataFrame, a: str, b: str, scenario: str, metric: str) -> dict:
    """a - b on the same (regime, seed) episodes of one load level, pooled over regimes (n = 30)."""
    g = runs[runs.scenario == scenario]
    wide = g.pivot_table(index=["regime", "seed"], columns="policy", values=metric)
    d = (wide[a] - wide[b]).dropna().to_numpy(float)
    lo, hi = ci95(d)
    sd = d.std(ddof=1)
    p = stats.ttest_rel(wide[a], wide[b]).pvalue if sd > 0 else float("nan")
    better = int((np.sign(d) == DIRECTION[metric]).sum())
    return {"n": len(d), "mean": float(d.mean()), "lo": lo, "hi": hi, "p": float(p),
            "dz": float(d.mean() / sd) if sd > 0 else float("nan"), "better": better}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--results", type=Path, default=Path("results"))
    parser.add_argument("--out", type=Path, default=Path("paper/numbers.json"))
    args = parser.parse_args()
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")

    prelim = args.results / "vize_prelim"
    runs = pd.read_csv(prelim / "runs.csv")
    meta = json.loads((prelim / "meta.json").read_text(encoding="utf-8"))
    evo_meta = json.loads((args.results / "evolve_profiles" / "meta.json").read_text(encoding="utf-8"))
    for name, m in (("vize_prelim", meta), ("evolve_profiles", evo_meta)):
        if m.get("git_dirty"):
            print(f"UYARI: {name} commit'lenmemiş kodla koşturulmuş.", file=sys.stderr)

    n: dict[str, str] = {
        "episodes": tr(meta["episodes"]),
        "commit": meta["git_commit"][:7],
        "evo_commit": evo_meta["git_commit"][:7],
        "wall_seconds": tr(meta["wall_seconds"], 0),
        "evo_dna": evo_meta["selected_dna"],
        "evo_rate": f"%{evo_meta['selected_mutation_rate'] * 100:.0f}",
        "evo_seed": str(evo_meta["selected_ga_seed"]),
        "evo_validation": tr(evo_meta["selected_validation_fitness"], 2),
        "evo_wall_hours": tr(evo_meta["wall_seconds"] / 3600, 1),
    }

    # Paired comparisons quoted in the text, as "focus - versus" (sign shows the direction).
    pairs = [("Utility", "LowestCost", "U_LC"), ("Utility", "Nearest", "U_N"), ("Evolved", "Utility", "EV_U"),
             ("Nearest", "LowestCost", "N_LC")]
    paired_rows = []
    for a, b, key in pairs:
        for s in SCENARIOS:
            pr = paired(runs, a, b, s, "profit")
            k = f"{key}_{s}"
            n[f"{k}_profit"] = tr(pr["mean"], 0, sign=True)
            n[f"{k}_profit_ci"] = f"[{tr(pr['lo'], 0, sign=True)}; {tr(pr['hi'], 0, sign=True)}]"
            n[f"{k}_profit_p"] = p_text(pr["p"])
            n[f"{k}_profit_better"] = f"{pr['better']}/{pr['n']}"
            n[f"{k}_profit_dz"] = tr(pr["dz"], 2)
            row = {"m": {}}
            for metric, scale, digits in (("late_rate", 100, 2), ("service_level", 100, 2), ("split_rate", 100, 2),
                                          ("stockout_rate", 100, 2), ("avg_fulfilment_km", 1, 2)):
                q = paired(runs, a, b, s, metric)
                n[f"{k}_{metric}"] = tr(q["mean"] * scale, digits, sign=True)
                n[f"{k}_{metric}_ci"] = f"[{tr(q['lo'] * scale, digits, sign=True)}; {tr(q['hi'] * scale, digits, sign=True)}]"
                n[f"{k}_{metric}_p"] = p_text(q["p"])
                n[f"{k}_{metric}_better"] = f"{q['better']}/{q['n']}"
                row["m"][metric] = q
            if key != "N_LC":
                paired_rows.append([
                    f"{a} − {b}", SCENARIO_TR[s],
                    f"{tr(pr['mean'], 0, sign=True)} {n[f'{k}_profit_ci']}",
                    p_text(pr["p"]), f"{pr['better']}/{pr['n']}",
                    tr(row["m"]["late_rate"]["mean"] * 100, 2, sign=True) + f" ({p_text(row['m']['late_rate']['p'])})",
                    tr(row["m"]["service_level"]["mean"] * 100, 2, sign=True) + f" ({p_text(row['m']['service_level']['p'])})",
                    tr(row["m"]["split_rate"]["mean"] * 100, 2, sign=True) + f" ({p_text(row['m']['split_rate']['p'])})",
                ])

    # Where a profit difference comes from: mean difference of each money component per episode.
    means = runs.assign(margin=runs.revenue - runs.cogs).groupby(["policy", "scenario"]).mean(numeric_only=True)
    for a, b, key in pairs:
        for s in SCENARIOS:
            diff = means.loc[(a, s)] - means.loc[(b, s)]
            for column in ("margin", "shipping_cost", "split_cost", "lateness_cost", "stockout_penalty"):
                n[f"{key}_{s}_{column}_tl"] = tr(diff[column], 0, sign=True)
            n[f"{key}_{s}_lostsale_tl"] = tr(diff["margin"] - diff["stockout_penalty"], 0, sign=True)

    # Per-regime profit difference Utility - LowestCost (RQ2 preview).
    for s in SCENARIOS:
        for regime in ("stable", "seasonal", "volatile"):
            sub = runs[(runs.scenario == s) & (runs.regime == regime)]
            wide = sub.pivot_table(index="seed", columns="policy", values="profit")
            d = (wide["Utility"] - wide["LowestCost"]).to_numpy(float)
            lo, hi = ci95(d)
            n[f"U_LC_{s}_{regime}_profit"] = tr(d.mean(), 0, sign=True)
            n[f"U_LC_{s}_{regime}_profit_ci"] = f"[{tr(lo, 0, sign=True)}; {tr(hi, 0, sign=True)}]"
            for policy in ("LowestCost", "Utility", "Evolved"):
                v = sub[sub.policy == policy]
                n[f"{policy}_{s}_{regime}_late"] = tr(v.late_rate.mean() * 100, 2)
                n[f"{policy}_{s}_{regime}_service"] = tr(v.service_level.mean() * 100, 1)

    # Pooled means per policy and load level.
    for (policy, s), v in runs.groupby(["policy", "scenario"]):
        k = f"{policy}_{s}"
        n[f"{k}_profit_k"] = tr(v.profit.mean() / 1000, 1)
        n[f"{k}_late"] = tr(v.late_rate.mean() * 100, 2)
        n[f"{k}_service"] = tr(v.service_level.mean() * 100, 1)
        n[f"{k}_split"] = tr(v.split_rate.mean() * 100, 2)
        n[f"{k}_stockout"] = tr(v.stockout_rate.mean() * 100, 2)
        n[f"{k}_km"] = tr(v.avg_fulfilment_km.mean(), 2)
        n[f"{k}_orders"] = tr(v.orders.mean(), 0)
    n["stockout_min"] = tr(runs.groupby(["policy", "scenario"]).stockout_rate.mean().min() * 100, 1)
    n["stockout_max"] = tr(runs.groupby(["policy", "scenario"]).stockout_rate.mean().max() * 100, 1)

    def table_from_csv(path: Path) -> dict:
        t = pd.read_csv(path, dtype=str)
        rows = [[cell.replace(".", ",") if any(ch.isdigit() for ch in cell) and "[" not in cell else cell
                 for cell in row] for row in t.fillna("").values.tolist()]
        return {"header": list(t.columns), "rows": rows}

    main_table = table_from_csv(prelim / "table_main.csv")
    main_table["header"] = [h for h in main_table["header"] if h != "n"]
    idx = list(pd.read_csv(prelim / "table_main.csv", nrows=0).columns).index("n")
    main_table["rows"] = [r[:idx] + r[idx + 1:] for r in main_table["rows"]]

    evo = table_from_csv(args.results / "evolve_profiles" / "evolution_summary.csv")
    evo["header"] = [h for h in evo["header"] if h != "En iyi DNA'lar"]
    evo["rows"] = [r[:-1] for r in evo["rows"]]
    week3 = table_from_csv(args.results / "week3lab" / "week3lab_summary.csv")

    gens = pd.read_csv(args.results / "evolve_profiles" / "generations.csv")
    zero = gens[gens.mutation_rate == 0]
    collapsed = zero[zero.distinct_genomes == 1].groupby("ga_seed" if "ga_seed" in zero else "seed").generation.min()
    n["evo_zero_collapse_min"] = str(int(collapsed.min()))
    n["evo_zero_collapse_max"] = str(int(collapsed.max()))

    out = {
        "numbers": n,
        "tables": {
            "main": main_table,
            "paired": {"header": ["Karşılaştırma", "Yük", "Kâr farkı TL [%95 GA]", "p", "Daha iyi seed",
                                  "Gecikme (puan)", "Servis (puan)", "Split (puan)"], "rows": paired_rows},
            "evolution": evo,
            "week3lab": week3,
        },
    }
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(out, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"{len(n)} sayı, {len(out['tables'])} tablo -> {args.out}")


if __name__ == "__main__":
    main()
