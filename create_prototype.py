from PIL import Image

input_path = r'C:\Users\vicen\.gemini\antigravity\brain\f0dd5e15-d87d-4d95-b9e8-796313496349\.user_uploaded\media_1789400328858.jpg'
out_floor = r'C:\Users\vicen\Documents\GitHub\RogueLikeProject\Assets\_Project\Test_Floor.png'
out_prop = r'C:\Users\vicen\Documents\GitHub\RogueLikeProject\Assets\_Project\Test_Prop_Crystal.png'

img = Image.open(input_path).convert('RGBA')
datas = img.getdata()

# 1. FLOOR (Remove white bg)
floor_data = []
for item in datas:
    if item[0] > 230 and item[1] > 230 and item[2] > 230:
        floor_data.append((255, 255, 255, 0))
    else:
        floor_data.append(item)

floor_img = Image.new('RGBA', img.size)
floor_img.putdata(floor_data)
floor_img.save(out_floor, 'PNG')

# 2. CRYSTAL CROP (Bottom Left)
# Approximate box for the big bottom-left crystal
# X: 10 to 180, Y: 580 to 920
prop_img = floor_img.crop((10, 580, 200, 930))

# Make non-pink/purple pixels transparent to isolate the crystal roughly
prop_datas = prop_img.getdata()
new_prop_data = []
for item in prop_datas:
    # Crystal is pinkish: R > 150, B > 150. Or we just keep it as a blocky crop for testing
    # Let's keep it simple: if it's too dark (roots) or too blue (floor), make transparent
    r, g, b, a = item
    if a == 0:
        new_prop_data.append((255,255,255,0))
    elif r > 160 and b > 160: # Pink crystal
        new_prop_data.append(item)
    elif r < 70 and g < 70 and b < 70: # Black roots connected to it
        new_prop_data.append(item)
    else:
        new_prop_data.append((255,255,255,0))

prop_img.putdata(new_prop_data)
prop_img.save(out_prop, 'PNG')
print('Arquivos de teste criados com sucesso!')
