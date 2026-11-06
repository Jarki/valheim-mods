"""Compose the public package icon from Rotheart's original inventory render."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1]
canvas = Image.new("RGBA", (256, 256))
pixels = canvas.load()
for y in range(256):
    for x in range(256):
        glow = max(0, 1 - ((x - 128) ** 2 + (y - 112) ** 2) / 22000)
        pixels[x, y] = (int(12 + 14 * glow), int(22 + 28 * glow), int(18 + 9 * glow), 255)
draw = ImageDraw.Draw(canvas)
draw.rounded_rectangle((5, 5, 250, 250), radius=15, outline=(103, 122, 68), width=2)
heart = Image.open(root / "Assets/Models/rotheart.png").convert("RGBA")
heart.thumbnail((202, 202), Image.Resampling.LANCZOS)
canvas.alpha_composite(heart, ((256 - heart.width) // 2, 9))
draw = ImageDraw.Draw(canvas)
font = ImageFont.load_default(size=25)
label = "ROTHEART"
box = draw.textbbox((0, 0), label, font=font)
draw.text(((256 - (box[2] - box[0])) // 2, 216), label, font=font, fill=(222, 231, 168))
output = root / "package/icon.png"
output.parent.mkdir(parents=True, exist_ok=True)
canvas.convert("RGB").save(output)
print(output)
