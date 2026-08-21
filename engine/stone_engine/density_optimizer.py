def optimize_density(points, density):
    # Importance-aware pruning. High-priority detail survives first.
    density = max(0.01, min(1.0, density))
    points = sorted(points, key=lambda p: p[3], reverse=True)
    keep = max(1, int(round(len(points) * density)))
    return points[:keep]
