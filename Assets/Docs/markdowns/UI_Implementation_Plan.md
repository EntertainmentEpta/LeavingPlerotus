# Dois Sistemas de UI: Menu Principal + Menu de Controles

## Contexto

O projeto roguelike já possui vários sistemas de UI criados inteiramente por código (sem prefabs do Editor), como `EconomyHUD`, `AttributeDisplayOnScreenSplit`, `InventoryUI`, e `UltimateUI`. Vamos seguir o mesmo padrão para manter consistência.

O jogo já tem:
- `GameManager` com `LoadGameLevel()` que inicia a run e carrega `GameScene`
- Cena `BaseLab` como hub/base do jogador
- `MusicManager` para controle de áudio
- Diversas keybinds espalhadas pelos scripts

---

## Sistema 1: Menu Principal (MainMenu)

### Visão Geral

Uma tela de menu que aparece ao abrir o jogo (cena dedicada `MainMenu`), com visual espacial/sci-fi coerente com a estética do jogo.

**Botões:**
- **▶ JOGAR** → Carrega a cena `BaseLab` (a base do jogador)
- **⚙ OPÇÕES** → Abre subpainel com configurações simuladas
- **★ CRÉDITOS** → Abre subpainel com informações do estúdio

**Subpainel de Opções (visual simulado):**
- Slider de **Volume Geral** (conecta ao `MusicManager.masterVolume` se disponível)
- Slider de **Volume de Efeitos** (visual, sem funcionalidade)
- Slider de **Sensibilidade do Mouse** (visual)
- Toggle de **Tela Cheia** (funcional via `Screen.fullScreen`)
- Dropdown simulado de **Resolução** (visual)
- Dropdown simulado de **Qualidade Gráfica** (visual)
- Botão **Voltar**

**Subpainel de Créditos:**
- Nome do estúdio/entertainment
- Nomes da equipe (configurável via Inspector)
- Versão do jogo
- Botão **Voltar**

### Arquitetura

```
MainMenuUI : MonoBehaviour
├── BuildMainPanel()      → Jogar, Opções, Créditos
├── BuildOptionsPanel()   → Sliders, toggles simulados
├── BuildCreditsPanel()   → Info do estúdio
├── ShowPanel(panel)      → Transição entre painéis
└── OnPlayClicked()       → SceneManager.LoadScene("BaseLab")
```

> **IMPORTANTE:** Cena MainMenu — Será necessário criar uma cena nova chamada `MainMenu` e adicioná-la ao Build Settings do Unity. O script será adicionado a um GameObject vazio nessa cena.

---

## Sistema 2: Menu de Referência de Controles (ControlsReferenceMenu)

### Visão Geral

Um painel in-game que lista todos os comandos/keybinds do jogo, organizado por categorias. Aberto com **F2** (tecla configurável).

### Categorias e Controles

| Categoria | Ação | Tecla |
|-----------|------|-------|
| **🎮 Movimentação** | Mover | WASD |
| | Sprint | Shift (sempre correndo) |
| | Dash | E |
| **⚔ Combate** | Ataque Primário | Q / Mouse Esquerdo |
| | Ultimate | U |
| **📦 Interface** | Inventário | Tab |
| | HUD de Atributos | F5 |
| | HUD de Economia | F3 |
| **🔧 Debug/Dev** | Cheat Console | / (barra) |
| | (Info) | F1 reservado |
| | (Info) | ; reservado |

### Arquitetura

```
ControlsReferenceMenu : MonoBehaviour
├── toggleKey = KeyCode.F2
├── BuildUI()             → Cria canvas + painel com categorias
├── BuildCategory()       → Header de categoria + linhas de keybind
├── BuildKeybindRow()     → [Tecla]  Descrição
└── Toggle()              → Mostra/esconde
```

---

## Proposta de Arquivos

### [NEW] MainMenuUI.cs (`Assets/Scripts/UI/MainMenuUI.cs`)

Script completo do menu principal. Cria toda a UI por código:
- Painel principal com título do jogo e 3 botões
- Subpainel de opções com sliders e toggles
- Subpainel de créditos
- Visual escuro/sci-fi com paleta roxa/cyan (coerente com EconomyHUD)
- Transição suave entre painéis (alpha fade)

### [NEW] ControlsReferenceMenu.cs (`Assets/Scripts/UI/ControlsReferenceMenu.cs`)

Script do menu de controles in-game:
- Abre/fecha com F2
- Lista todas as keybinds por categoria
- Visual compacto estilo HUD, parecido com os outros HUDs do jogo
- Não congela o gameplay (apenas overlay informativo)
- DontDestroyOnLoad para persistir entre cenas

---

## Decisões de Design

| Decisão | Escolha | Motivo |
|---------|---------|--------|
| Tecla do menu de controles | **F2** | F1, F3, F5 já estão em uso |
| UI criada por código | Sim | Padrão do projeto inteiro |
| Pasta dos scripts | `Scripts/UI/` | Nova pasta para organização |
| MainMenu como cena separada | Sim | Separação clara de responsabilidades |
| Paleta de cores | Roxa/cyan escuro | Coerente com EconomyHUD e InventoryUI |

---

## Verification Plan

### Manual Verification
1. Criar a cena `MainMenu` no Unity e adicionar um GameObject com `MainMenuUI`
2. Testar os 3 botões (Jogar, Opções, Créditos) e navegação entre painéis
3. Verificar que "Jogar" carrega a `BaseLab`
4. Na `BaseLab`/`GameScene`, testar F2 para abrir/fechar o menu de controles
5. Verificar que o menu de controles lista todas as keybinds corretas
6. Confirmar visual coerente com o restante dos HUDs

### Automated Tests
- Verificar compilação do projeto: nenhum erro de compilação nos novos scripts
