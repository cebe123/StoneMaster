# StoneMaster User Guide

## Input

Supported:

- selected bitmap in the active CorelDRAW document
- external JPG
- external PNG

## Main parameters

- Kumaş Genişliği (mm)
- Taş boyutu: SS6/SS8/SS10/SS12/SS16/SS20/SS30/SS34
- Gap (mm)
- Density
- Background threshold
- Edge sensitivity
- Detail sensitivity
- RGB/Lab color distance
- maximum color count
- FULL / REPORT / BUDGET
- budget limit
- laser tolerance

## Output

### ORIGINAL_FABRIC

Original bitmap is preserved.

### STONE_PREVIEW

Filled circles with the selected/quantized stone RGB color.

### LAZER_KALIP_KESIM

No fill, red hairline circles. Hole diameter:

```text
laser_diameter = stone_diameter + laser_tolerance
```

## Preview vs Apply

`Önizleme` runs the image engine but does not create thousands of CorelDRAW vector objects.

`CorelDRAW'a Uygula` creates production vectors and places them at the bitmap bounding box.

## Undo

All generation is placed in a CorelDRAW command group so one undo should revert the generation.
