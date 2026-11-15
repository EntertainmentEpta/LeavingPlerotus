# 🐝 Shard Swarm (Estrela de Fragmentos) — Concept & Technical Specifications

## Identidade & Conceito

**Nome:** Shard Swarm (Estrela de Fragmentos)  
**Tipo:** Inimigo Ranged / Defensivo Estratégico  
**Comportamento-Chave:** Flutua com movimentação orgânica 2D ao redor do jogador. Dispara suas 5 pontas em ataque ioiô telegrafado, deixando o Núcleo (Core) 100% exposto a **1.5x Dano Crítico** enquanto as pontas estão no ar. Se defende em forma fechada com **50% de redução de dano**, retaliação elétrica de contato e pulso holográfico de escudo.

---

## Anatomia & Design Visual

### 1. Núcleo (CORE) Central
- **Estética**: Cubo/Cristal central translúcido de luz pura que gira em torno do eixo Y ($200^\circ/\text{s}$).
- **Vulnerabilidade**: Quando exposto sem pontas, o contato físico é seguro e o dano recebido ganha bônus de **1.5x Dano Crítico**.

### 2. Espinhos / Pontas de Cristal (5 Unidades)
- **Arranjo**: 5 pontas em simetria radial orbital de 5 pontas.
- **Telegrafo Visual**: Antes de atirar, abre em **$2.2\times$ de amplitude** e segura por **0.85s** encarando o player.
- **Voo & Retorno**: Voam a $60\text{ m/s}$ até $25\text{ m}$, congelam no ar por $1.2\text{ s}$ e retornam uma a uma em ordem aleatória.
- **Giro de Reencaixe**: Ao chegar na órbita, executam um giro de 360° (*Wheel Spin*) a $360^\circ/\text{s}$ no pivô do Core.

### 3. Escudo de Holograma (`Escudo`)
- Esfera de holograma com Shader Graph que aciona um **Fade In (0.12s) e Fade Out (0.23s)** suave ao receber dano em modo protegido.

### 4. UI da Barra de Vida (`HealthBar_Canvas`)
- Possui o script `FaceCamera` para garantir que a barra de vida permaneça perfeitamente reta e alinhada perante a câmera do jogador sem girar com a malha 3D.

---

## Mecânicas de Combate & Riscos

| Estado / Elemento | Dano | Efeito no Player | Mecânica |
| :--- | :---: | :---: | :--- |
| **Escudo Fechado (Contato < 1.3m)** | 8 HP | Knockback (6f) + 50% Slow (3s) | Retaliação do escudo de espinhos |
| **Dano no Escudo Fechado** | 50% Armadura | - | O mob sofre apenas metade do dano |
| **Projétil Direto (Disparo)** | 10 HP | Dano Físico Limpo | Voo de $60\text{ m/s}$ a até $25\text{ m}$ |
| **Trilha Elétrica Ionizada (`ElectricTrailVFX`)** | 5 HP/s | Status Eletrocutado (50% Slow por 3s) | Rastro 3D de $0.35\text{ m}$ que dura $3.0\text{ s}$. Renova o timer ao pisar. |
| **Núcleo Exposto (Punish)** | **1.5x Crítico** | **Ataque Livre Sem Shock** | Raio seguro de $3.0\text{ m}$ sem rastros em volta do Core |
| **Morte do Core** | - | - | Todas as 5 pontas desanexadas são destruídas na hora |
