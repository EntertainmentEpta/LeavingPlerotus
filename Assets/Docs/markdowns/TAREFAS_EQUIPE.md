# 📋 Divisão Oficial de Tarefas da Equipe — Living Plerotus

> **Documento de Distribuição de Atividades & Sprint Final**  
> **Status:** Enviado e Sincronizado com a Equipe  
> **Meta:** Conclusão da Versão Oficial de Entrega  

---

## 👥 Atribuições Oficiais por Membro da Equipe

---

### 🌐 1. @Ana Lu — Site de Divulgação (Landing Page)
* **Objetivo:** Estruturar a Landing Page oficial de apresentação e financiamento do jogo em scroll contínuo.
* **Tarefas:**
  - [ ] Montar a estrutura da Landing Page de apresentação em scroll contínuo (imagens e textos serão fornecidos).
  - [ ] Organizar os materiais visuais:
    - Vídeo do Menu Principal em loop (usar o antigo como base e botões de navegação por seção idênticos aos do jogo, além da logo oficial).
    - Imagens dos 3 ângulos do Player e do Mercador.
    - Imagens dos inimigos e do Eptin.
  - [ ] Estruturar as áreas de seções: Lore, Gameplay, Bestiário, FAQ, Discord e Patreon (os links finais serão configurados).

---

### ⚡ 2. @Isaac — Dash, Mecânicas e VFX
* **Objetivo:** Finalizar o sistema de mobilidade e a biblioteca de efeitos visuais.
* **Tarefas:**
  - [ ] **Dash & VFX:** Terminar e polir a mecânica de Dash e seu VFX de rastro para encaixar perfeitamente no ritmo de gameplay.
  - [ ] **VFX de Ataques:** Organizar e validar a coesão de todos os hits básicos e o VFX especial de acerto crítico.
  - [ ] **VFX de Ambiente:** Partículas atmosféricas de poeira/névoa cristalina no cenário.
  - [ ] **VFX da Pedra Mágica:** Pesquisar e integrar o VFX do raio descendente (*Skybeam*) vindo da Asset Store.
  - [ ] **VFX de Desintegração (*Disintegrate*):** Implementar o efeito visual de dissolução quando os monstros morrem em vez de sumirem repentinamente (usar assets da Asset Store).

---

### 👑 3. @Gui Enter — Terceira Fase do Boss e Lobby
* **Objetivo:** Implementar a 3ª Fase do Boss Cromático e montar o Lobby da Base.
* **Tarefas:**
  - [ ] **Chicote de Raiz / Braço de Cipó:** Ataque melee frontal em área longa (Slam com o braço esticado).
  - [ ] **Cuspe de Ácido Parabólico:** Projétil que viaja pelo ar e cria a poça de ácido no chão no impacto (sem invocar do nada).
  - [ ] **Salva de Espinhos:** Estacas de cristal arremessadas na direção do jogador.
  - [ ] **Transição & Balanceamento:** Entrada na Fase 3 (usando a Fase 2 como base), balanceamento de dano e gatilhos de vitória.
  - [ ] **Animações:** Garantir a integração e sincronia de todas as animações da fase.

---

### 🔊 4. @Pedro Enter — Sound Effects dos Inimigos (Ajustes e Novos)
* **Objetivo:** Ajustar e finalizar a sonoplastia dos monstros.
* **Ajustes:**
  - [ ] **Crystal Watcher:** Charge aprovado; aumentar e encorpar o som durante o disparo do laser em si, combinando com o carregamento ou aumentando seu volume relativo.
  - [ ] **Goblin:** Som da bomba precisa perdurar mais como uma explosão real (acompanhando o tempo da fumaça na tela) em vez de uma pancada seca.
  - [ ] **Pedra Mágica:** Som do raio com áudio de antecipação no chão avisando o jogador (chiado/estalo estilo 'bacon fritando') antes do impacto.
  - [ ] **Golem:** Verificar e garantir o som do Stun/impacto pesado.
* **Novos Sons:**
  - [ ] **Star / Cristalus:** Som do disparo e som de choque elétrico (*zap*).
  - [ ] **Teleporte:** Som de distorção para a Pedra Mágica e para o SharpBlur.
  - [ ] **Ataques de SharpBlur:** Som de *swoosh/swish* dos ataques de cristal.

---

### 🎛️ 5. @Matheus Enter — Sound Effects (Player, Armas, Mecânicas e Eptin)
* **Objetivo:** Implementar os efeitos sonoros do protagonista, combate e feedback do mundo.
* **Tarefas:**
  - [ ] **Machado:** 1 som de impacto/corte para cada golpe do combo (Hits 1, 2, 3 e 4), sincronizados com os eventos de animação.
  - [ ] **Swoosh do Machado:** Som pesado cortando o ar durante o balanço da arma (mesmo no vazio).
  - [ ] **Impacto nos Escudos:** Som metálico/cristalino ao tentar bater no Mercador ou no Eptinho (variedade de 3 sons coerentes entre si que tocam aleatoriamente).
  - [ ] **Player sem Escudo:** Som tenso de batimento cardíaco (*coração pulsando*, em volume sutil/baixo) quando o AP zera.
  - [ ] **Passos do Player:** Passos no solo rochoso/cristalino (sutil e bem baixo).
  - [ ] **Eptinho:** Latido ao conversar com ele apertando F, e bip/notificação suave ao abrir pop-ups na tela.

---

### ⚙️ 6. @Lucass Enter — Menu de Pausa (ESC) e Configurações
* **Objetivo:** Finalizar completamente o Menu de Pausa e Configurações para a versão final.
* **Tarefas:**
  - [ ] Finalizar o visual e navegação do Menu de ESC (Pausa e Opções).
  - [ ] Sliders de volume (Master, Música, SFX) conectados ao `AudioMixer`.
  - [ ] Configurações de vídeo (Resolução, Tela Cheia / Janela, VSync).
  - [ ] Mapeamento e visualização clara de controles.
