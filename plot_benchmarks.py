import matplotlib.pyplot as plt
import numpy as np

# Results from BenchmarkDotNet, time in microseconds.
data_sizes = [1000, 1000, 10000, 10000, 100000, 100000]
window_sizes = [10, 50, 10, 50, 10, 50]

naive = [4.521, 23.992, 44.608, 238.108, 549.716, 2499.750]
optimized = [1.758, 1.753, 16.944, 17.891, 278.669, 273.361]

labels = [
    "N=1 000\nw=10",
    "N=1 000\nw=50",
    "N=10 000\nw=10",
    "N=10 000\nw=50",
    "N=100 000\nw=10",
    "N=100 000\nw=50",
]

plt.rcParams["font.family"] = "DejaVu Sans"

# GRAPH 1: execution time
x = np.arange(len(labels))
width = 0.36

fig, ax = plt.subplots(figsize=(12, 6))

ax.bar(
    x - width / 2,
    naive,
    width,
    label="Naive algorithm"
)

ax.bar(
    x + width / 2,
    optimized,
    width,
    label="Optimized algorithm"
)

ax.set_title("Moving Average: Execution Time")
ax.set_xlabel("Data size and window")
ax.set_ylabel("Execution time (microseconds)")
ax.set_xticks(x)
ax.set_xticklabels(labels)
ax.set_yscale("log")
ax.legend()
ax.grid(axis="y", linestyle="--", alpha=0.4)

fig.tight_layout()
fig.savefig("benchmark-runtime.png", dpi=300)
plt.close(fig)

# GRAPH 2: speedup
speedup = [
    n / o for n, o in zip(naive, optimized)
]

sizes = ["1 000", "10 000", "100 000"]

speedup_w10 = [
    speedup[0],
    speedup[2],
    speedup[4],
]

speedup_w50 = [
    speedup[1],
    speedup[3],
    speedup[5],
]

x = np.arange(len(sizes))

fig, ax = plt.subplots(figsize=(9, 6))

ax.bar(
    x - width / 2,
    speedup_w10,
    width,
    label="Window = 10"
)

ax.bar(
    x + width / 2,
    speedup_w50,
    width,
    label="Window = 50"
)

ax.set_title("Optimized Algorithm Speedup")
ax.set_xlabel("Number of elements")
ax.set_ylabel("Speedup (times)")
ax.set_xticks(x)
ax.set_xticklabels(sizes)
ax.legend()
ax.grid(axis="y", linestyle="--", alpha=0.4)

fig.tight_layout()
fig.savefig("benchmark-speedup.png", dpi=300)
plt.close(fig)

# Print calculated results
print("Graphs created successfully!")
print("1. benchmark-runtime.png")
print("2. benchmark-speedup.png")
print()
print("Calculated speedup:")

for n, w, s in zip(data_sizes, window_sizes, speedup):
    print(f"N={n:>6}, window={w:>2}: {s:.2f}x")
