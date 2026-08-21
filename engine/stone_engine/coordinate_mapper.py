def pixel_to_mm(x, y, image_width, image_height, width_mm, height_mm):
    return x * width_mm / image_width, y * height_mm / image_height
