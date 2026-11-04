"""Analyse the evolution experiment (Week 3 GA class experiment applied to the fulfilment agent).

Usage (from the repository root):
    python tools/analyze_evolution.py results/evolve_profiles

Writes into the experiment folder:
    evolution_summary.csv/.tex  per mutation rate: final best training fitness and validation fitness (mean ± std over GA seeds)
    figures/evolution.png|pdf   mean fitness, best fitness and distinct genomes per generation
"""

import argparse
import json
import sys
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import pandas as pd

# Mutation rate is ordinal, so one blue ramp light -> dark (reference palette steps 250, 450, 650).
RAMP = ["#86b6ef", "#2a78d6", "#104281"]
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
    gens = pd.read_csv(out / "generations.csv")
    runs = pd.read_csv(out / "runs.csv")
    meta = json.loads((out / "meta.json").read_text(encoding="utf-8"))
    if meta.get("git_dirty"):
        print("UYARI: deney commit'lenmemiş kodla koşturulmuş (git_dirty=true).", file=sys.stderr)

    rates = sorted(gens.mutation_rate.unique())
    colours = {r: RAMP[min(i, len(RAMP) - 1)] for i, r in enumerate(rates)}

    plt.rcParams.update({"font.family": "DejaVu Sans"})
    fig, axes = plt.subplots(1, 3, figsize=(11, 3.4))
    panels = [("mean_fitness", "Ortalama fitness (TL/sipariş)"),
              ("best_fitness", "En iyi fitness (TL/sipariş)"),
              ("distinct_genomes", "Farklı genom sayısı (24 bireyde)")]
    for ax, (column, label) in zip(axes, panels):
        style_axes(ax)
        for rate in rates:
            g = gens[gens.mutation_rate == rate].groupby("generation")[column]
            mean, lo, hi = g.mean(), g.min(), g.max()
            ax.fill_between(mean.index, lo, hi, color=colours[rate], alpha=0.18, linewidth=0)
            ax.plot(mean.index, mean.values, color=colours[rate], linewidth=2, label=f"mutasyon %{rate * 100:.0f}")
        ax.set_xlabel("Nesil", color=INK, fontsize=9)
        ax.set_title(label, color=INK, fontsize=10)
    handles, labels = axes[0].get_legend_handles_labels()
    fig.legend(handles, labels, loc="upper center", ncol=len(rates), frameon=False, fontsize=9, bbox_to_anchor=(0.5, 1.02))
    fig.tight_layout(rect=(0, 0, 1, 0.92))
    figdir = out / "figures"
    figdir.mkdir(parents=True, exist_ok=True)
    fig.savefig(figdir / "evolution.png", dpi=200, facecolor="white")
    fig.savefig(figdir / "evolution.pdf", facecolor="white")
    plt.close(fig)

    rows = []
    for rate in rates:
        r = runs[runs.mutation_rate == rate]
        last = gens[(gens.mutation_rate == rate) & (gens.generation == gens.generation.max())]
        rows.append({
            "Mutasyon": f"%{rate * 100:.0f}",
            "Son nesil ortalama fitness": f"{last.mean_fitness.mean():.2f} ± {last.mean_fitness.std(ddof=1):.2f}",
            "Son nesil en iyi (eğitim)": f"{r.train_fitness.mean():.2f} ± {r.train_fitness.std(ddof=1):.2f}",
            "Doğrulama": f"{r.validation_fitness.mean():.2f} ± {r.validation_fitness.std(ddof=1):.2f}",
            "Son nesil farklı genom": f"{last.distinct_genomes.mean():.1f}",
            "En iyi DNA'lar": " ".join(r.best_dna.astype(str)),
        })
    table = pd.DataFrame(rows)
    table.to_csv(out / "evolution_summary.csv", index=False, encoding="utf-8")
    cols = [c for c in table.columns if c != "En iyi DNA'lar"]
    lines = [r"\begin{tabular}{l" + "r" * (len(cols) - 1) + "}", r"\hline", " & ".join(cols).replace("%", r"\%") + r" \\", r"\hline"]
    for _, row in table.iterrows():
        lines.append(" & ".join(str(row[c]).replace("%", r"\%").replace("±", r"$\pm$") for c in cols) + r" \\")
    lines += [r"\hline", r"\end{tabular}"]
    (out / "evolution_summary.tex").write_text("\n".join(lines) + "\n", encoding="utf-8")

    print(table.to_string(index=False))
    print(f"\nseçilen DNA: {meta['selected_dna']} (mutasyon {meta['selected_mutation_rate']}, seed {meta['selected_ga_seed']}), "
          f"doğrulama {meta['selected_validation_fitness']} TL/sipariş")


if __name__ == "__main__":
    main()
