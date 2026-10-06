from PIL import Image

input_path = r'C:\Users\vicen\.gemini\antigravity\brain\f0dd5e15-d87d-4d95-b9e8-796313496349\.user_uploaded\media_1789400328858.jpg'
img = Image.open(input_path)
print(f'Width: {img.width}, Height: {img.height}')
