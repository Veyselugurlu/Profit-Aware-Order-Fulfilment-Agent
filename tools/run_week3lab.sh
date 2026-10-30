#!/usr/bin/env bash
# Week 3 class experiment on the unchanged lecture scripts (Stay on Platform):
# 3 mutation rates x 3 seeds, population 24, 8 s trials, 30 generations, run headless in the player at 10x time.
# Usage (repository root, after a player build): bash tools/run_week3lab.sh [out_dir]
set -euo pipefail
OUT="${1:-results/week3lab}"
EXE="Builds/ProfitFulfilmentAgent/ProfitFulfilmentAgent.exe"
mkdir -p "$OUT/runs"
for rate in 0 0.1 0.3; do
  for seed in 2026 2027 2028; do
    echo "mutation $rate seed $seed"
    "$EXE" -batchmode -nographics -week3lab -w3mutation "$rate" -w3seed "$seed" -w3generations 30 -w3timescale 10 \
      -w3out "$OUT/runs/m${rate}_s${seed}.csv" -logFile "$OUT/runs/m${rate}_s${seed}.log" > /dev/null
  done
done
echo "done -> $OUT/runs"
