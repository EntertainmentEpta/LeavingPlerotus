from PIL import Image
import os
import glob

folder = r'C:\Users\vicen\Documents\GitHub\RogueLikeProject\Assets\_Project\Enviroment\map\isometric map'
images = glob.glob(os.path.join(folder, '*.png'))

for img_path in images:
    if 'Transparent' in img_path:
        continue
        
    print(f'Processando {os.path.basename(img_path)}...')
    try:
        img = Image.open(img_path)
        img = img.convert('RGBA')
        datas = img.getdata()
        
        newData = []
        for item in datas:
            # Tolerancia para fundos claros/brancos
            if item[0] > 230 and item[1] > 230 and item[2] > 230:
                newData.append((255, 255, 255, 0))
            else:
                newData.append(item)
                
        img.putdata(newData)
        
        new_path = img_path.replace('.png', '_Transparent.png')
        img.save(new_path, 'PNG')
        print(f'Sucesso: {os.path.basename(new_path)}')
    except Exception as e:
        print(f'Erro ao processar {os.path.basename(img_path)}: {e}')
