"""Analyse the Week 3 class experiment run on the unchanged lecture scripts (Stay on Platform).

Usage (from the repository root, after tools/run_week3lab.sh):
    python tools/analyze_week3lab.py results/week3lab

Reads runs/m<rate>_s<seed>.csv and writes:
    generations.csv            all runs combined
    week3lab_summary.csv/.tex  per mutation rate: final mean/best fitness, survivors, best DNAs (mean ± std over seeds)
    figures/week3lab.png|pdf   mean fitness, best fitness and survivors per generation
"""

import argparse
import sys
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import pandas as pd

RAMP = ["#86b6ef", "#2a78d6", "#104281"]  # one blue ramp, light -> dark, for the ordinal mutation rate
INK, INK_MUTED, GRID = "#0b0b0b", "#52514e", "#e4e3df"


def style_axes(ax) -> None:
    ax.grid(axis="y", color=GRID, linewidth=0.8)
    ax.set_axisbelow(True)
    for side in ("top", "right"):
        ax.spines[side].set_visible(False)
    for side in ("left", "bottom"):
        ax.spines[side].set_color(INK_MUTED)
    ax.tick_params(colors=INK_MUTED, labelsize=9)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("experiment_dir", type=Path)
    args = parser.parse_args()
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")

    out: Path = args.experiment_dir
    files = sorted((out / "runs").glob("m*_s*.csv"))
    if not files:
        raise SystemExit(f"no runs in {out / 'runs'}")
    gens = pd.concat([pd.read_csv(f) for f in files], ignore_index=True)
    gens.to_csv(out / "generations.csv", index=False)

    rates = sorted(gens.mutation_rate.unique())
    colours = {r: RAMP[min(i, len(RAMP) - 1)] for i, r in enumerate(rates)}

    plt.rcParams.update({"font.family": "DejaVu Sans"})
    fig, axes = plt.subplots(1, 3, figsize=(11, 3.4))
    panels = [("mean_fitness", "Ortalama fitness"), ("best_fitness", "En iyi fitness"), ("alive", "Hayatta kalan (24 bireyde)")]
    for ax, (column, title) in zip(axes, panels):
        style_axes(ax)
        for rate in rates:
            g = gens[gens.mutation_rate == rate].groupby("generation")[column]
            mean, lo, hi = g.mean(), g.min(), g.max()
            ax.fill_between(mean.index, lo, hi, color=colours[rate], alpha=0.18, linewidth=0)
            ax.plot(mean.index, mean.values, color=colours[rate], linewidth=2, label=f"mutasyon %{rate * 100:.0f}")
        ax.set_xlabel("Nesil", color=INK, fontsize=9)
        ax.set_title(title, color=INK, fontsize=10)
    handles, labels = axes[0].get_legend_handles_labels()
    fig.legend(handles, labels, loc="upper center", ncol=len(rates), frameon=False, fontsize=9, bbox_to_anchor=(0.5, 1.02))
    fig.tight_layout(rect=(0, 0, 1, 0.92))
    figdir = out / "figures"
    figdir.mkdir(parents=True, exist_ok=True)
    fig.savefig(figdir / "week3lab.png", dpi=200, facecolor="white")
    fig.savefig(figdir / "week3lab.pdf", facecolor="white")
    plt.close(fig)

    last_gen = gens.generation.max()
    rows = []
    for rate in rates:
        last = gens[(gens.mutation_rate == rate) & (gens.generation == last_gen)]
        first = gens[(gens.mutation_rate == rate) & (gens.generation == 1)]
        rows.append({
            "Mutasyon": f"%{rate * 100:.0f}",
            "1. nesil ortalama": f"{first.mean_fitness.mean():.2f} ± {first.mean_fitness.std(ddof=1):.2f}",
            f"{last_gen}. nesil ortalama": f"{last.mean_fitness.mean():.2f} ± {last.mean_fitness.std(ddof=1):.2f}",
            f"{last_gen}. nesil en iyi": f"{last.best_fitness.mean():.2f} ± {last.best_fitness.std(ddof=1):.2f}",
            "Hayatta kalan": f"{last.alive.mean():.1f}",
            "En iyi DNA (seed başına)": " ".join(last.sort_values("seed").best_dna.astype(str)),
        })
    table = pd.DataFrame(rows)
    table.to_csv(out / "week3lab_summary.csv", index=False, encoding="utf-8")
    cols = [c for c in table.columns if not c.startswith("En iyi DNA")]
    lines = [r"\begin{tabular}{l" + "r" * (len(cols) - 1) + "}", r"\hline", " & ".join(cols).replace("%", r"\%") + r" \\", r"\hline"]
    for _, row in table.iterrows():
        lines.append(" & ".join(str(row[c]).replace("%", r"\%").replace("±", r"$\pm$") for c in cols) + r" \\")
    lines += [r"\hline", r"\end{tabular}"]
    (out / "week3lab_summary.tex").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"{len(files)} koşu, {last_gen} nesil")
    print(table.to_string(index=False))


if __name__ == "__main__":
    main()
