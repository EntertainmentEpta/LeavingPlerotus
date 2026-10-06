# Resumo de Contexto - Viagem (VicenzoWS)

Este arquivo foi gerado para manter o contexto do projeto ao trocar de máquina (Notebook).

## 1. Modelos 3D (Blender) e Git LFS
* **Erro de Importação (FBX/GLB):** Se ao abrir a Unity no notebook você ou a equipe se depararem com centenas de erros vermelhos do tipo `ImportFBX Errors: Couldn't read file`, isso ocorre porque os modelos 3D exportados do Blender estão sendo lidos apenas como ponteiros de texto.
* **A Solução:** Nunca baixe o projeto como `.zip`. Após clonar o projeto no notebook, é **obrigatório** rodar no terminal:
  ```bash
  git lfs install
  git lfs pull
  ```
  Isso garantirá que as texturas e malhas pesadas do Blender sejam baixadas corretamente.

## 2. Sistemas Atuais: Sinergia e Câmera
* **Sistema de Sinergias (Crafting):** Desenvolvemos recentemente a arquitetura do `Synergy Crafting Manager`. O sistema suporta um layout dinâmico de 1 a 4 itens. O jogador usa "Essência" para acoplar partes do corpo de inimigos abatidos no astronauta. 
* **Tiers de Upgrade:** Os loots variam de Tier 1 (utilitário comum) a Tier 4 (Lendário). O Tier Lendário tem uma regra rígida: ele não pode dar apenas "ganhos de dano passivo"; ele obrigatoriamente deve alterar ativamente a build e a forma de jogar do usuário.
* **Câmera Isométrica:** O jogo conta com um *toggle* de Câmera Isométrica (tecla `F4`). Devido a isso, devemos manter no radar soluções de *Game Design* para lidar com oclusão de visão (ex: inimigos ou projéteis escondidos atrás de pilares de cristal).

## 3. Desafio Interno EPTA (Entertainment)
Criamos um desafio teórico de Game Design para transição de membros:
* **Parte 1 (Inimigo Inédito):** O candidato recebe a lista completa da nossa fauna (Aranha, Golem, Peixe, Sharp Blur, etc.) e deve deduzir qual arquétipo comportamental de combate está faltando, criando um inimigo para forçar novas estratégias (como o uso obrigatório do recém-criado efeito de *Dash*).
* **Parte 2 (Loot Integrado):** Desenvolver 4 partes de corpo (Tiers 1 a 4) desse novo inimigo e obrigatoriamente linkar uma **Sinergia** com um item já existente no GDD.
* **Parte 3 (Identidade de UX/UI):** Desenvolver um *Moodboard* e um *Wireframe* da tela de infusões, dando uma "cara" própria ao jogo (mesclando o Sci-Fi do Astronauta com o mundo orgânico/cristalizado).
* **Bônus:** Sugerir nome comercial para o jogo baseado na sobrevivência com o cão cibernético Eptinho.

---
### 🤖 Instrução para o Antigravity do Notebook:
*Olá Antigravity! Se o Vicenzo pediu para você ler este arquivo, agora você tem o contexto dos últimos dias de trabalho. Estamos na branch `VicenzoWS`. Seu foco principal a partir de agora é ajudar com integrações do sistema de Sinergia, importações corretas de FBX/Blender, e auxílio com o Game Design Document (GDD).*
