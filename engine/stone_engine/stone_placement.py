import math
import numpy as np
import random
from .color_quantization import nearest_palette
from .models import StonePlacement, StoneDef, PaletteColor

def create_candidates(image, valid_mask, edge, stones, palette, density, edge_weight=0.30,
                      detail_weight=0.15, color_weight=0.45, local_weight=0.10,
                      sprinkle=False, exclude_dark=False, dark_threshold=70,
                      edge_only=False, edge_threshold=80, fill_interior=True):
    h, w = image.shape[:2]
    # Physical pitch approximation is handled after physical scale is known.
    # Pixel pitch is chosen adaptively from the desired density.
    # Use first stone for base step calculation
    if isinstance(stones, list) and len(stones) > 0:
        stone = stones[0]
    else:
        stone = stones
    base_step = max(2, int(round(stone.diameter_mm / max(0.25, 0.75 * density))))
    
    # İç alan maskesi oluştur (fill modu için)
    interior = None
    if fill_interior and not sprinkle and not edge_only:
        from .preprocessing import interior_mask
        interior = interior_mask(edge, valid_mask)
    
    points = []

    for y in range(base_step // 2, h, base_step):
        for x in range(base_step // 2, w, base_step):
            if sprinkle:
                random.seed(y * w + x)
                x = max(0, min(w - 1, x + random.randint(-base_step // 3, base_step // 3)))
                y = max(0, min(h - 1, y + random.randint(-base_step // 3, base_step // 3)))
            
            # Normal modda hem valid_mask hem de interior_mask kontrolü
            if not valid_mask[y, x]:
                continue
            
            # fill_interior modunda: sadece iç alandaki noktalara taş koy
            if fill_interior and interior is not None and not sprinkle and not edge_only:
                if not interior[y, x]:
                    continue
            elif edge_only and edge[y, x] < edge_threshold:
                continue

            r, g, b = map(int, image[y, x])
            if exclude_dark and (0.299 * r + 0.587 * g + 0.114 * b) <= dark_threshold:
                continue
            palette_color, _ = nearest_palette((r, g, b), palette)
            gray_local = float(np.mean(image[max(0,y-2):min(h,y+3), max(0,x-2):min(w,x+3)]))
            edge_strength = float(edge[y, x]) / 255.0
            local_contrast = float(np.std(image[max(0,y-3):min(h,y+4), max(0,x-3):min(w,x+4)])) / 64.0
            brightness = gray_local / 255.0

            importance = (
                color_weight * (1.0 - min(1.0, abs(brightness - 0.5) * 1.6)) +
                edge_weight * edge_strength +
                detail_weight * min(1.0, local_contrast) +
                local_weight * (1.0 - brightness)
            )
            points.append((x, y, palette_color, min(1.0, importance)))

    return points

def adaptive_prune(points, target_fraction):
    if not points:
        return []
    target_fraction = max(0.01, min(1.0, target_fraction))
    points = sorted(points, key=lambda x: x[3], reverse=True)
    keep = max(1, int(round(len(points) * target_fraction)))
    return points[:keep]

def resolve_collisions(points, radius_px, gap_px):
    accepted = []
    r = max(0.1, radius_px)
    min_dist2 = (2 * r + gap_px) ** 2
    cell = max(1.0, 2 * r + gap_px)
    grid = {}

    for x, y, color, importance in sorted(points, key=lambda p: p[3], reverse=True):
        cx, cy = int(x // cell), int(y // cell)
        collision = False
        for gx in range(cx - 1, cx + 2):
            for gy in range(cy - 1, cy + 2):
                for ax, ay, *_ in grid.get((gx, gy), []):
                    if (x - ax) ** 2 + (y - ay) ** 2 < min_dist2:
                        collision = True
                        break
                if collision:
                    break
            if collision:
                break
        if not collision:
            accepted.append((x, y, color, importance))
            grid.setdefault((cx, cy), []).append((x, y, color, importance))
    return accepted

def assign_stones(points, stones):
    if not stones:
        raise ValueError("At least one stone size must be selected")
    ordered = sorted(stones, key=lambda item: item.diameter_mm)
    ranked = sorted(enumerate(points), key=lambda item: item[1][3], reverse=True)
    assigned = [None] * len(points)
    for rank, (index, point) in enumerate(ranked):
        bucket = min(len(ordered) - 1, int(rank * len(ordered) / max(1, len(points))))
        assigned[index] = (*point, ordered[bucket])
    return assigned

def resolve_variable_collisions(points, image_step_mm, gap_mm):
    accepted = []
    for point in sorted(points, key=lambda item: item[3], reverse=True):
        x, y, color, importance, stone = point
        radius = max(0.1, stone.diameter_mm / image_step_mm / 2.0)
        collision = False
        for ax, ay, acolor, aimportance, astone in accepted:
            other_radius = max(0.1, astone.diameter_mm / image_step_mm / 2.0)
            minimum_distance = radius + other_radius + gap_mm / image_step_mm
            if (x - ax) ** 2 + (y - ay) ** 2 < minimum_distance ** 2:
                collision = True
                break
        if not collision:
            accepted.append(point)
    return accepted

def to_placements(points, stone: StoneDef, width_mm, height_mm, image_width, image_height, laser_tolerance):
    placements = []
    sx = width_mm / image_width
    sy = height_mm / image_height
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
            laser_diameter_mm=selected_stone.diameter_mm + laser_tolerance,
            stone_name=selected_stone.name,
            color_name=color.name,
            hex_color=color.hex,
            importance=importance,
        ))
    return placements
