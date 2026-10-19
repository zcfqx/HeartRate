"""生成 HeartRate Monitor 应用图标。

输出:
  src/HeartRateMonitor.App/Assets/app.png  256x256 预览图
  src/HeartRateMonitor.App/Assets/app.ico  多尺寸图标 (16/20/24/32/40/48/64/128/256)

设计: 靛蓝渐变圆角方块 + 白色爱心 + 镂空 ECG 心电波线。
每个尺寸独立按 4 倍超采样渲染, 保证小尺寸图标边缘干净。

用法: python scripts/generate_app_icon.py
"""

from __future__ import annotations

import math
import os

from PIL import Image, ImageDraw

# ---------------------------------------------------------------- 基础参数
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
ASSETS = os.path.join(ROOT, "src", "HeartRateMonitor.App", "Assets")

SS = 4  # 超采样倍数

TOP_COLOR = (129, 140, 248)     # #818CF8  PrimaryLight
BOTTOM_COLOR = (67, 56, 202)    # #4338CA  深靛蓝
HEART_COLOR = (255, 255, 255)
PULSE_MIN_SIZE = 40             # 小于该尺寸不镂空心电波线(太小会糊)

ICO_SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]


def _lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def _gradient(size: int) -> Image.Image:
    """垂直渐变背景(带一点横向明暗, 避免死板)。"""
    strip = Image.new("RGB", (1, size))
    for y in range(size):
        t = y / max(size - 1, 1)
        # 轻微 ease-out, 让上半部分保持明亮
        tt = t ** 0.8
        strip.putpixel(
            (0, y),
            tuple(int(round(_lerp(TOP_COLOR[i], BOTTOM_COLOR[i], tt))) for i in range(3)),
        )
    return strip.resize((size, size), Image.Resampling.BILINEAR)


def _heart_points(size: int, with_pulse: bool) -> list[tuple[float, float]]:
    """参数方程爱心, 自动缩放到目标包围盒。"""
    raw = []
    steps = 1440
    for i in range(steps):
        t = 2 * math.pi * i / steps
        x = 16 * math.sin(t) ** 3
        y = (
            13 * math.cos(t)
            - 5 * math.cos(2 * t)
            - 2 * math.cos(3 * t)
            - math.cos(4 * t)
        )
        raw.append((x, y))

    xs = [p[0] for p in raw]
    ys = [p[1] for p in raw]
    w = max(xs) - min(xs)
    h = max(ys) - min(ys)

    # 不带心电波时爱心放大, 小尺寸更饱满
    box_w = size * (0.630 if with_pulse else 0.680)
    box_h = size * (0.650 if with_pulse else 0.700)
    scale = min(box_w / w, box_h / h)
    cx = size / 2 - (max(xs) + min(xs)) / 2 * scale
    cy = size * 0.485 + (max(ys) + min(ys)) / 2 * scale
    return [(cx + x * scale, cy - y * scale) for x, y in raw]


def _pulse_polyline(size: int) -> list[tuple[float, float]]:
    """ECG 心电波形折线 (相对坐标: x 为宽度比例, y 为相对基线的高度偏移)。"""
    shape = [
        (0.00, 0.000),
        (0.14, 0.000),
        (0.19, -0.028),
        (0.24, 0.000),
        (0.40, 0.000),
        (0.45, 0.030),
        (0.50, -0.085),
        (0.55, 0.040),
        (0.60, 0.000),
        (0.72, 0.000),
        (0.78, 0.020),
        (0.84, 0.000),
        (1.00, 0.000),
    ]
    x0 = size * 0.10
    x1 = size * 0.90
    base = size * 0.455
    return [(x0 + (x1 - x0) * px, base + py * size) for px, py in shape]


def render(size: int) -> Image.Image:
    """渲染单个尺寸的图标。"""
    work = size * SS
    radius = int(round(work * 0.235))

    # 圆角方块遮罩
    tile_mask = Image.new("L", (work, work), 0)
    ImageDraw.Draw(tile_mask).rounded_rectangle(
        [0, 0, work - 1, work - 1], radius=radius, fill=255
    )

    img = _gradient(work).convert("RGBA")

    # 左上角高光
    highlight = Image.new("L", (work, work), 0)
    hd = ImageDraw.Draw(highlight)
    hd.ellipse(
        [-work * 0.35, -work * 0.75, work * 0.95, work * 0.55],
        fill=52,
    )
    img.alpha_composite(Image.merge("RGBA", (*img.split()[:3], highlight)))

    # 爱心遮罩
    with_pulse = size >= PULSE_MIN_SIZE
    heart_mask = Image.new("L", (work, work), 0)
    ImageDraw.Draw(heart_mask).polygon(_heart_points(work, with_pulse), fill=255)

    # 镂空 ECG 线(只在较大尺寸)
    if with_pulse:
        punch = Image.new("L", (work, work), 0)
        ImageDraw.Draw(punch).line(
            _pulse_polyline(work),
            fill=255,
            width=max(int(round(work * 0.038)), 2),
            joint="curve",
        )
        heart_mask = Image.composite(Image.new("L", (work, work), 0), heart_mask, punch)

    heart = Image.new("RGBA", (work, work), (*HEART_COLOR, 0))
    heart.putalpha(heart_mask)
    img.alpha_composite(heart)

    # 裁到圆角方块
    img.putalpha(Image.composite(img.getchannel("A"), Image.new("L", (work, work), 0), tile_mask))

    return img.resize((size, size), Image.Resampling.LANCZOS)


def main() -> None:
    os.makedirs(ASSETS, exist_ok=True)
    images = {s: render(s) for s in ICO_SIZES}

    png_path = os.path.join(ASSETS, "app.png")
    images[256].save(png_path, "PNG")
    print(f"已生成 {png_path}")

    ico_path = os.path.join(ASSETS, "app.ico")
    images[256].save(
        ico_path,
        format="ICO",
        sizes=[(s, s) for s in ICO_SIZES],
        append_images=[images[s] for s in ICO_SIZES if s != 256],
    )
    print(f"已生成 {ico_path}")


if __name__ == "__main__":
    main()
