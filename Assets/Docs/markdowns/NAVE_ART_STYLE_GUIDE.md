# Guia de Estilo Visual e Pipeline de Prompts: Nave e Props (Meshy AI)

Este documento estabelece a regra oficial de direção de arte para a **Nave Espacial**, suas salas (Lobby, Cockpit, Laboratório), módulos e personagens/props (como o robozinho companheiro). 

Qualquer pessoa da equipe ou IA que for gerar novas peças **DEVE** seguir este documento para garantir que todos os modelos 3D gerados no Meshy tenham **100% de consistência estética, escala e textura**.

---

## 📌 1. Imagens Âncora Obrigatórias

**NUNCA gere uma imagem sem anexar uma imagem âncora aprovada.** O gerador precisa de referência visual direta para não inventar outro estilo de pintura ou iluminação.

* **Âncora de Traço e Textura Geral:**  
  `Assets/_Project/Art/GeneratedMapAssets/safe_room_tile_clean.png`
* **Âncora de Paleta e Paredes de Nave:**  
  `Assets/_Project/Enviroment/Nave/LOBBY_WALL_STRAIGHT_FRONTAL.jpg`
* **Âncora de Props e Portas:**  
  `Assets/_Project/Enviroment/Nave/LOBBY_DOOR_ISOMETRIC_FINAL.jpg`

---

## 🎨 2. A Identidade Visual (Os 4 Mandamentos)

1. **Traço (Lineart):**  
   Contornos pretos grossos e definidos, estilo desenho à mão com tinta (*hand-drawn ink outlines / comic book cel*). Todas as arestas e separações de chapas de metal devem ter linha preta visível.

2. **Pintura e Iluminação (Matte Finish):**  
   Pintura chapada opaca (*matte*). **PROIBIDO** efeito plástico 3D, reflexos hiper-realistas de IA, textura de foto ou brilho neon (*glow*) estourado. As cores devem parecer aquarela/têmpera digital opaca.

3. **Paleta de Cores Oficial da Nave:**  
   * **Metal Primário:** Cinza ardósia (*slate gray*) e grafite escuro industrial (*gunmetal*).
   * **Metal Secundário:** Aço azulado desbotado (*muted steel blue*).
   * **Acentos Mecânicos:** Tubulações e juntas em cobre/bronze envelhecido (*weathered copper/bronze*).
   * **Avisos e Luzes:** Faixas de perigo em amarelo/preto (*hazard stripes*), luzes de sensores em âmbar e teal suave fosco.
   * **Evitar:** Tons roxos excessivos ou monocromáticos dos primeiros rascunhos.

4. **Fundo e Isolamento (Regra Crítica para o Meshy):**  
   * Fundo **100% branco sólido puro** (`pure solid white background`).
   * **ZERO texto, legendas ou marcas d'água.** Se houver texto na imagem, o Meshy criará letras em 3D grudadas no modelo.

---

## 📐 3. Estrutura e Ângulos de Câmera

Para que o Meshy gere modelos 3D utilizáveis sem distorções no Unity, siga a regra de ângulo por tipo de asset:

| Tipo de Asset | Ângulo de Geração | Motivo |
| :--- | :--- | :--- |
| **Paredes / Portais** | **Vista Frontal Reta (Front Orthographic)** | Evita que o Meshy gere uma parede 3D inclinada/torta. No Unity basta rotacionar a parede em 90° ou 180°. |
| **Pisos / Chão** | **Prisma Isométrico 3/4** | Cria o bloco cúbico de chão com espessura perfeita para tilemap em grade. |
| **Props / Móveis** | **Isométrico 3/4 visto de cima** | Dá o volume de profundidade ideal para o Meshy entender todos os lados do objeto. |
| **Personagens / Robôs** | **Frontal ou 3/4 Leve** | Garante pernas e braços simétricos e fáceis de aplicar rigging/animação no Unity. |

---

## 📋 4. Prompts Mestres (Copie e Cole)

### Prompt A: Paredes e Módulos Estruturais (Vista Frontal Reta)
> `"A straight-on frontal view (front orthographic elevation, flat 2D view, NOT isometric, NOT angled) of a modular sci-fi spaceship interior [NOME DO MÓDULO: ex. wall segment / monitor wall / corridor bulkhead], on a pure solid white background. Clean rectangular piece seen straight from the front. Heavy metallic bulkhead paneling, reinforced vertical pillars on each side for modular snapping, industrial pipes along the bottom. ESTILO (OBRIGATÓRIO): Mantenha estritamente o estilo chapado (matte), indie 2D, com contornos pretos grossos em tinta (hand-drawn ink outlines) e acabamento pintado à mão, idêntico à arte de referência. Cores opacas em cinza ardósia e grafite com toques de cobre/azul desbotado, sem render 3D hiper-realista e sem efeito de glow/neon. Absolutely NO text, NO labels, NO typography. Isolado em fundo branco puro."`

### Prompt B: Props e Mobília Isolada (Perspectiva 3/4)
> `"A top-down 3/4 perspective isometric 3D game asset concept art of ONLY a standalone sci-fi spaceship [NOME DO PROP: ex. stasis pod / pilot chair / generator console / doorway], completely isolated on a pure solid white background with NO surrounding walls and NO floor. ESTILO (OBRIGATÓRIO): Mantenha estritamente o estilo chapado (matte), indie 2D isométrico, com contornos pretos grossos em tinta (hand-drawn ink outlines) e acabamento pintado à mão, idêntico à imagem de referência. Cores de metal industrial: cinza ardósia, grafite escuro, placas em aço azulado e conexões de cobre. Sem render hiper-realista, sem acabamento plástico. Absolutely NO text, NO labels. Isolado em fundo branco puro."`

### Prompt C: Robôs e Personagens (Companheiro / Inimigos Mecânicos)
> `"A full-body 3D game asset concept art of [NOME DO PERSONAGEM: ex. cute bipedal companion scout robot], directly polishing and finishing the attached sketch on a pure solid white background. [DESCREVA O CORPO: ex. Spherical round main body chassis, central circular camera eye, top handle, long slender articulated robotic stilt legs with hydraulic piston joints]. ESTILO (OBRIGATÓRIO): Mantenha estritamente o estilo chapado (matte), indie estilizado, com contornos pretos grossos em tinta (hand-drawn ink outlines) e acabamento pintado à mão, idêntico à arte de referência. Paleta de metal industrial: cinza ardósia, grafite escuro, placas em aço azulado e juntas de cobre, com leve luz âmbar/teal na lente. Sem render 3D hiper-realista, sem acabamento plástico. Totalmente isolado em fundo branco puro."`

---

## ⚙️ 5. Configurações Recomendadas no Meshy AI

Ao subir a imagem gerada no **Image to 3D**:

* **Model Style:** Selecione sempre **`Stylized`** ou **`Cartoon`** (nunca escolha *Realistic*).
* **Topology:** **`Triangle`** ou **`Quad`** (Target polycount: 10.000 a 25.000 polys para jogos indie leves).
* **Symmetry:** 
  * Ativar para personagens/robôs com anatomia simétrica.
  * Desativar para paredes com tubos ou consoles assimétricos.
* **PBR Maps:** Exportar com *Base Color*, *Normal* e *Roughness*.
* **Formato de Exportação:** Baixar sempre em **`.FBX`** e salvar direto em `Assets/_Project/Enviroment/Nave/`.
