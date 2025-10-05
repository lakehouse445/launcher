from PIL import Image

# Open the PNG file
png_image = Image.open('icon.png')

# Convert to RGBA if it's not already
if png_image.mode != 'RGBA':
    png_image = png_image.convert('RGBA')

# Create different sizes for the ICO file
sizes = [(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
icon_images = []

for size in sizes:
    resized = png_image.resize(size, Image.Resampling.LANCZOS)
    icon_images.append(resized)

# Save as ICO file with multiple sizes
icon_images[0].save('icon.ico', format='ICO', sizes=[(img.width, img.height) for img in icon_images])

print("Successfully converted icon.png to icon.ico with multiple sizes")
