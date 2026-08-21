def optimize_budget(placements, price_per_stone, target_budget, prices=None):
    if target_budget is None:
        return placements

    prices = prices or {}
    ranked = sorted(
        placements,
        key=lambda item: item.importance / max(0.0001, prices.get(item.stone_name, price_per_stone)),
        reverse=True,
    )
    selected = []
    spent = 0.0
    for placement in ranked:
        price = prices.get(placement.stone_name, price_per_stone)
        if spent + price <= target_budget:
            selected.append(placement)
            spent += price
    return selected
