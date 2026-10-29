"""Analyse an experiment: summary statistics, paired comparisons, paper table and figures.

Every number and figure in the paper is produced by this script from results/<experiment>/runs.csv.

Usage (from the repository root):
    python tools/analyze.py results/vize_prelim [--reference LowestCost] [--focus Utility]

Writes into the experiment folder:
    summary.csv        mean, std, 95 % CI per policy x scenario x regime (10 seeds each)
    summary_pooled.csv the same pooled over regimes (30 episodes per cell)
    comparisons.csv    paired comparison of the focus policy against every other policy, per scenario x regime
    table_main.csv/.tex  paper table: per load level, pooled over regimes
    figures/*.png|pdf  profit difference, service level, lateness and split rate
"""

import argparse
import json
import sys
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from scipy import stats

# Categorical slots 1-4 of the reference palette in fixed order, never cycled. Policies sit side by side in a fixed
# order, so only adjacent pairs must separate (validated, light mode); a marker shape per policy is the secondary
# encoding, and every figure has a legend and a table next to it.
POLICY_COLOURS = {"Nearest": "#2a78d6", "LowestCost": "#eb6834", "Utility": "#1baf7a", "Evolved": "#eda100"}
POLICY_MARKERS = {"Nearest": "o", "LowestCost": "s", "Utility": "D", "Evolved": "^"}
INK, INK_MUTED, GRID = "#0b0b0b", "#52514e", "#e4e3df"
SCENARIOS = ["low", "medium", "high"]
SCENARIO_TR = {"low": "Low", "medium": "Medium", "high": "High"}
REGIMES = ["stable", "seasonal", "volatile"]
REGIME_TR = {"stable": "Sabit", "seasonal": "Mevsimsel", "volatile": "Dalgalı"}

METRICS = ["profit", "total_cost", "shipping_cost", "split_cost", "lateness_cost", "stockout_penalty",
           "opportunity_cost", "depletion_events", "stockout_rate", "split_rate", "late_rate",
           "avg_late_hours", "avg_fulfilment_km", "service_level"]
# For the paired comparison: +1 if higher is better, -1 if lower is better.
DIRECTION = {"profit": 1, "service_level": 1, "total_cost": -1, "shipping_cost": -1, "split_cost": -1,
             "lateness_cost": -1, "stockout_penalty": -1, "opportunity_cost": -1, "depletion_events": -1,
             "stockout_rate": -1, "split_rate": -1, "late_rate": -1, "avg_late_hours": -1, "avg_fulfilment_km": -1}


def ci95(values: np.ndarray) -> tuple[float, float]:
    n = len(values)
    if n < 2:
        return float("nan"), float("nan")
    half = stats.t.ppf(0.975, n - 1) * values.std(ddof=1) / np.sqrt(n)
    return float(values.mean() - half), float(values.mean() + half)


def describe(df: pd.DataFrame, keys: list[str]) -> pd.DataFrame:
    rows = []
    for key, group in df.groupby(keys, sort=False):
        key = key if isinstance(key, tuple) else (key,)
        for metric in METRICS:
            v = group[metric].to_numpy(float)
            lo, hi = ci95(v)
            rows.append({**dict(zip(keys, key)), "metric": metric, "n": len(v), "mean": v.mean(),
                         "std": v.std(ddof=1) if len(v) > 1 else 0.0, "ci95_low": lo, "ci95_high": hi})
    return pd.DataFrame(rows)


def paired(df: pd.DataFrame, focus: str, keys: list[str]) -> pd.DataFrame:
    """Focus policy minus each other policy, matched on seed (and on regime/scenario when they are not grouping keys)."""
    rows = []
    others = [p for p in df["policy"].unique() if p != focus]
    for key, group in df.groupby(keys, sort=False):
        key = key if isinstance(key, tuple) else (key,)
        # Pair on every column that is not a grouping key, e.g. (seed, regime) when pooling over regimes.
        index = ["seed"] + [c for c in ("scenario", "regime") if c not in keys]
        wide = group.pivot_table(index=index, columns="policy", values=METRICS, aggfunc="first")
        for other in others:
            for metric in METRICS:
                a, b = wide[(metric, focus)], wide[(metric, other)]
                d = (a - b).dropna().to_numpy(float)
                if len(d) == 0:
                    continue
                lo, hi = ci95(d)
                sd = d.std(ddof=1) if len(d) > 1 else 0.0
                t_p = stats.ttest_rel(a.loc[a.index], b.loc[a.index]).pvalue if sd > 0 else float("nan")
                try:
                    w_p = stats.wilcoxon(d, zero_method="zsplit").pvalue if np.any(d != 0) else float("nan")
                except ValueError:
                    w_p = float("nan")
                better = np.sum(np.sign(d) * DIRECTION[metric] > 0)
                # Spell the direction out so nobody has to remember that mean_diff is focus minus versus.
                gain = d.mean() * DIRECTION[metric]
                better_policy = focus if gain > 0 else other if gain < 0 else "tie"
                significant = bool(not np.isnan(lo) and (lo > 0 or hi < 0))
                rows.append({**dict(zip(keys, key)), "focus": focus, "versus": other, "metric": metric,
                             "n": len(d), "mean_diff": d.mean(), "std_diff": sd, "ci95_low": lo, "ci95_high": hi,
                             "rel_diff_pct": 100 * d.mean() / abs(b.mean()) if b.mean() != 0 else float("nan"),
                             "t_test_p": t_p, "wilcoxon_p": w_p, "cohen_dz": d.mean() / sd if sd > 0 else float("nan"),
                             "focus_better_seeds": int(better), "better_policy": better_policy,
                             "significant_95": significant})
    return pd.DataFrame(rows)


def main_table(df: pd.DataFrame, out: Path) -> None:
    """Per load level, pooled over regimes: mean ± std over all episodes."""
    cols = [("profit", "Kâr (bin TL)", 1e-3, "{:.1f}"), ("service_level", "Servis (%)", 100, "{:.1f}"),
            ("late_rate", "Gecikme (%)", 100, "{:.2f}"), ("split_rate", "Split (%)", 100, "{:.2f}"),
            ("stockout_rate", "Stock-out (%)", 100, "{:.2f}"), ("avg_fulfilment_km", "Mesafe (km)", 1, "{:.2f}")]
    rows = []
    for scenario in [s for s in SCENARIOS if s in df["scenario"].unique()]:
        for policy in POLICY_COLOURS:
            g = df[(df.scenario == scenario) & (df.policy == policy)]
            if g.empty:
                continue
            row = {"Yük": SCENARIO_TR[scenario], "Politika": policy, "n": len(g)}
            for metric, label, scale, fmt in cols:
                v = g[metric].to_numpy(float) * scale
                row[label] = f"{fmt.format(v.mean())} ± {fmt.format(v.std(ddof=1))}"
            rows.append(row)
    table = pd.DataFrame(rows)
    table.to_csv(out / "table_main.csv", index=False, encoding="utf-8")

    header = " & ".join(["Yük", "Politika"] + [c[1] for c in cols]) + r" \\"
    lines = [r"\begin{tabular}{ll" + "r" * len(cols) + "}", r"\hline", header.replace("%", r"\%"), r"\hline"]
    for _, r in table.iterrows():
        cells = [r["Yük"], r["Politika"]] + [r[c[1]].replace("±", r"$\pm$") for c in cols]
        lines.append(" & ".join(cells) + r" \\")
    lines += [r"\hline", r"\end{tabular}"]
    (out / "table_main.tex").write_text("\n".join(lines) + "\n", encoding="utf-8")


def style_axes(ax) -> None:
    ax.grid(axis="y", color=GRID, linewidth=0.8)
    ax.set_axisbelow(True)
    for side in ("top", "right"):
        ax.spines[side].set_visible(False)
    for side in ("left", "bottom"):
        ax.spines[side].set_color(INK_MUTED)
    ax.tick_params(colors=INK_MUTED, labelsize=9)


def fig_profit_difference(runs: pd.DataFrame, reference: str, figdir: Path) -> None:
    """Paired profit difference (policy - reference, matched on seed) per load and regime, with 95 % CI."""
    fig, axes = plt.subplots(1, len(REGIMES), figsize=(10, 3.4), sharey=True)
    policies = [p for p in POLICY_COLOURS if p != reference and p in runs.policy.unique()]
    for ax, regime in zip(axes, REGIMES):
        style_axes(ax)
        ax.axhline(0, color=INK_MUTED, linewidth=1)
        for i, policy in enumerate(policies):
            xs, ys, err = [], [], []
            for j, scenario in enumerate(SCENARIOS):
                g = runs[(runs.regime == regime) & (runs.scenario == scenario)]
                wide = g.pivot_table(index="seed", columns="policy", values="profit")
                if policy not in wide or reference not in wide:
                    continue
                d = (wide[policy] - wide[reference]).dropna().to_numpy(float) / 1000
                lo, _ = ci95(d)
                xs.append(j + (i - (len(policies) - 1) / 2) * 0.2)
                ys.append(d.mean())
                err.append(d.mean() - lo)
            ax.errorbar(xs, ys, yerr=err, fmt=POLICY_MARKERS[policy], ms=6, color=POLICY_COLOURS[policy], elinewidth=2, capsize=0,
                        label=f"{policy} − {reference}")
        ax.set_xticks(range(len(SCENARIOS)), [SCENARIO_TR[s] for s in SCENARIOS])
        ax.set_title(REGIME_TR[regime], color=INK, fontsize=10)
    axes[0].set_ylabel("Kâr farkı (bin TL, aynı seed)", color=INK, fontsize=9)
    handles, labels = axes[0].get_legend_handles_labels()
    fig.legend(handles, labels, loc="upper right", frameon=False, fontsize=9)
    fig.tight_layout(rect=(0, 0, 0.86, 1))
    save(fig, figdir / "profit_difference")


def fig_policy_metric(df: pd.DataFrame, metric: str, label: str, scale: float, figdir: Path, name: str) -> None:
    """Mean and 95 % CI per policy, load level (x) and regime (panel)."""
    fig, axes = plt.subplots(1, len(REGIMES), figsize=(10, 3.4), sharey=True)
    policies = [p for p in POLICY_COLOURS if p in df.policy.unique()]
    for ax, regime in zip(axes, REGIMES):
        style_axes(ax)
        for i, policy in enumerate(policies):
            xs, ys, err = [], [], []
            for j, scenario in enumerate(SCENARIOS):
                v = df[(df.regime == regime) & (df.scenario == scenario) & (df.policy == policy)][metric].to_numpy(float) * scale
                if len(v) == 0:
                    continue
                lo, hi = ci95(v)
                xs.append(j + (i - (len(policies) - 1) / 2) * 0.17)
                ys.append(v.mean())
                err.append(v.mean() - lo)
            ax.errorbar(xs, ys, yerr=err, fmt=POLICY_MARKERS[policy], ms=6, color=POLICY_COLOURS[policy], elinewidth=2, capsize=0, label=policy)
        ax.set_xticks(range(len(SCENARIOS)), [SCENARIO_TR[s] for s in SCENARIOS])
        ax.set_title(REGIME_TR[regime], color=INK, fontsize=10)
    axes[0].set_ylabel(label, color=INK, fontsize=9)
    handles, labels = axes[0].get_legend_handles_labels()
    fig.legend(handles, labels, loc="upper right", frameon=False, fontsize=9)
    fig.tight_layout(rect=(0, 0, 0.9, 1))
    save(fig, figdir / name)


def save(fig, stem: Path) -> None:
    stem.parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(stem.with_suffix(".png"), dpi=200, facecolor="white")
    fig.savefig(stem.with_suffix(".pdf"), facecolor="white")
    plt.close(fig)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("experiment_dir", type=Path)
    parser.add_argument("--focus", default="Utility")
    parser.add_argument("--reference", default="LowestCost")
    args = parser.parse_args()
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")

    out: Path = args.experiment_dir
    runs = pd.read_csv(out / "runs.csv")
    meta = json.loads((out / "meta.json").read_text(encoding="utf-8")) if (out / "meta.json").exists() else {}
    if meta.get("git_dirty"):
        print("UYARI: deney commit'lenmemiş kodla koşturulmuş (git_dirty=true); raporlanacak sonuçlar için commit sonrası tekrar koşturun.", file=sys.stderr)

    describe(runs, ["policy", "scenario", "regime"]).to_csv(out / "summary.csv", index=False)
    describe(runs, ["policy", "scenario"]).to_csv(out / "summary_pooled.csv", index=False)
    cmp = paired(runs, args.focus, ["scenario", "regime"])
    cmp.to_csv(out / "comparisons.csv", index=False)
    paired(runs, args.focus, ["scenario"]).to_csv(out / "comparisons_pooled.csv", index=False)
    main_table(runs, out)

    plt.rcParams.update({"font.family": "DejaVu Sans", "axes.edgecolor": INK_MUTED})
    figdir = out / "figures"
    fig_profit_difference(runs, args.reference, figdir)
    fig_policy_metric(runs, "service_level", "Servis seviyesi (%)", 100, figdir, "service_level")
    fig_policy_metric(runs, "late_rate", "Gecikme oranı (%)", 100, figdir, "late_rate")
    fig_policy_metric(runs, "split_rate", "Split oranı (%)", 100, figdir, "split_rate")

    pooled = pd.read_csv(out / "comparisons_pooled.csv")
    key = pooled[pooled.metric.isin(["profit", "service_level", "late_rate", "split_rate", "stockout_rate"])]
    print(f"{len(runs)} episode, politikalar: {', '.join(runs.policy.unique())}")
    print("mean_diff = focus - versus; better_policy names the better one for that metric")
    print(key[["scenario", "versus", "metric", "mean_diff", "ci95_low", "ci95_high", "t_test_p", "focus_better_seeds", "n", "better_policy", "significant_95"]]
          .round(4).to_string(index=False))


if __name__ == "__main__":
    main()
