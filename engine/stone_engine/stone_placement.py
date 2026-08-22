import math
import random

import numpy as np

from .color_quantization import nearest_palette
from .models import StoneDef, StonePlacement


def create_candidates(image, valid_mask, edge, stones, palette, density, edge_weight=0.30,
                      detail_weight=0.15, color_weight=0.45, local_weight=0.10,
                      sprinkle=False, exclude_dark=False, dark_threshold=70,
                      edge_only=False, edge_threshold=80, fill_interior=True,
                      grid_snap=False, interactive_mask=None):
    if not stones:
        raise ValueError("At least one stone size must be selected")
    if not palette:
        raise ValueError("At least one palette color must be selected")

    h, w = image.shape[:2]
    stone = stones[0]
    density = max(0.01, min(1.0, float(density)))
    base_step = max(2, int(round(stone.diameter_mm / max(0.25, 0.75 * density))))

    def allowed(x, y):
        if not valid_mask[y, x]:
            return False
        if interactive_mask is not None and not interactive_mask[y, x]:
            return False
        if edge_only and edge[y, x] < edge_threshold:
            return False
        r, g, b = map(int, image[y, x])
        if exclude_dark and (0.299 * r + 0.587 * g + 0.114 * b) <= dark_threshold:
            return False
        return True

    if grid_snap:
        step_x = max(2, int(round(stone.diameter_mm / max(0.25, density))))
        row_step = max(2, int(round(step_x * 0.866)))
        points = []
        for row_index, y in enumerate(range(row_step // 2, h, row_step)):
            x_start = step_x // 2 + (step_x // 2 if row_index % 2 else 0)
            for x in range(x_start, w, step_x):
                x = min(w - 1, x)
                if not allowed(x, y):
                    continue
                r, g, b = map(int, image[y, x])
                palette_color, _ = nearest_palette((r, g, b), palette)
                points.append((x, y, palette_color, float(edge[y, x]) / 255.0))
        return points

    interior = None
    if fill_interior and not sprinkle and not edge_only and interactive_mask is None:
        from .preprocessing import interior_mask
        interior = interior_mask(edge, valid_mask)

    points = []
    for y0 in range(base_step // 2, h, base_step):
        for x0 in range(base_step // 2, w, base_step):
            x, y = x0, y0
            if sprinkle:
                rng = random.Random(y0 * w + x0)
                jitter = max(1, base_step // 3)
                x = max(0, min(w - 1, x0 + rng.randint(-jitter, jitter)))
                y = max(0, min(h - 1, y0 + rng.randint(-jitter, jitter)))
            if not allowed(x, y):
                continue
            if interactive_mask is None and fill_interior and interior is not None and not interior[y, x]:
                continue

            r, g, b = map(int, image[y, x])
            palette_color, _ = nearest_palette((r, g, b), palette)
            local = image[max(0, y - 3):min(h, y + 4), max(0, x - 3):min(w, x + 4)]
            gray_local = float(np.mean(local))
            edge_strength = float(edge[y, x]) / 255.0
            local_contrast = min(1.0, float(np.std(local)) / 64.0)
            brightness = gray_local / 255.0
            importance = (
                color_weight * (1.0 - min(1.0, abs(brightness - 0.5) * 1.6))
                + edge_weight * edge_strength
                + detail_weight * local_contrast
                + local_weight * (1.0 - brightness)
            )
            points.append((x, y, palette_color, min(1.0, max(0.0, importance))))
    return points


def adaptive_prune(points, target_fraction):
    if not points:
        return []
    fraction = max(0.01, min(1.0, float(target_fraction)))
    ranked = sorted(points, key=lambda item: item[3], reverse=True)
    return ranked[:max(1, int(round(len(ranked) * fraction)))]


def resolve_collisions(points, radius_px, gap_px):
    accepted = []
    radius = max(0.1, float(radius_px))
    minimum_distance = 2.0 * radius + max(0.0, float(gap_px))
    minimum_distance_sq = minimum_distance * minimum_distance
    cell = max(1.0, minimum_distance)
    grid = {}
    for point in sorted(points, key=lambda item: item[3], reverse=True):
        x, y = point[0], point[1]
        cx, cy = int(x // cell), int(y // cell)
        collision = False
        for gx in range(cx - 1, cx + 2):
            for gy in range(cy - 1, cy + 2):
                for ax, ay, *_ in grid.get((gx, gy), []):
                    if (x - ax) ** 2 + (y - ay) ** 2 < minimum_distance_sq:
                        collision = True
                        break
                if collision:
                    break
            if collision:
                break
        if not collision:
            accepted.append(point)
            grid.setdefault((cx, cy), []).append(point)
    return accepted


def assign_stones(points, stones):
    ordered = sorted(stones, key=lambda item: item.diameter_mm)
    if not ordered:
        raise ValueError("At least one stone size must be selected")
    ranked = sorted(enumerate(points), key=lambda item: item[1][3], reverse=True)
    assigned = [None] * len(points)
    for rank, (index, point) in enumerate(ranked):
        bucket = min(len(ordered) - 1, int(rank * len(ordered) / max(1, len(points))))
        assigned[index] = (*point, ordered[bucket])
    return assigned


def resolve_variable_collisions(points, image_step_mm, gap_mm):
    if not points:
        return []
    accepted = []
    scale = max(float(image_step_mm), 1e-9)
    min_stone_mm = min(point[4].diameter_mm for point in points)
    cell_px = max(1.0, (min_stone_mm + max(0.0, float(gap_mm))) / scale)
    grid = {}
    gap_px = max(0.0, float(gap_mm)) / scale

    for point in sorted(points, key=lambda item: item[3], reverse=True):
        x, y, _, _, stone = point
        radius = max(0.1, stone.diameter_mm / scale / 2.0)
        cx, cy = int(x // cell_px), int(y // cell_px)
        collision = False
        search_radius = max(1, int(math.ceil((radius + gap_px + min_stone_mm / scale) / cell_px)))
        for gx in range(cx - search_radius, cx + search_radius + 1):
            for gy in range(cy - search_radius, cy + search_radius + 1):
                for other in grid.get((gx, gy), []):
                    ax, ay, _, _, other_stone = other
                    other_radius = max(0.1, other_stone.diameter_mm / scale / 2.0)
                    minimum = radius + other_radius + gap_px
                    if (x - ax) ** 2 + (y - ay) ** 2 < minimum ** 2:
                        collision = True
                        break
                if collision:
                    break
            if collision:
                break
        if not collision:
            accepted.append(point)
            grid.setdefault((cx, cy), []).append(point)
    return accepted


def to_placements(points, stone: StoneDef, width_mm, height_mm, image_width, image_height, laser_tolerance):
    placements = []
    sx = float(width_mm) / max(1, int(image_width))
    sy = float(height_mm) / max(1, int(image_height))
    for point in points:
        if len(point) == 5:
            x, y, color, importance, selected_stone = point
        else:
            x, y, color, importance = point
            selected_stone = stone
        placements.append(StonePlacement(
            x_mm=x * sx,
            y_mm=y * sy,
            diameter_mm=selected_stone.diameter_mm,
            laser_diameter_mm=selected_stone.diameter_mm + float(laser_tolerance),
            stone_name=selected_stone.name,
            color_name=color.name,
            hex_color=color.hex,
            importance=importance,
        ))
    return placements
