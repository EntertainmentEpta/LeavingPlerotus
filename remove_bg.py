from PIL import Image

input_path = r'C:\Users\vicen\.gemini\antigravity\brain\f0dd5e15-d87d-4d95-b9e8-796313496349\.user_uploaded\media_1789400328858.jpg'
output_path = r'C:\Users\vicen\Documents\GitHub\RogueLikeProject\Assets\_Project\Map_Z_Transparent.png'

try:
    img = Image.open(input_path)
    img = img.convert('RGBA')
    datas = img.getdata()
    
    newData = []
    for item in datas:
        # Check if the pixel is white or very close to white (JPEG artifacts)
        if item[0] > 230 and item[1] > 230 and item[2] > 230:
            newData.append((255, 255, 255, 0)) # transparent
        else:
            newData.append(item)
            
    img.putdata(newData)
    img.save(output_path, 'PNG')
    print('Imagem processada com sucesso!')
except Exception as e:
    print('Erro:', e)
