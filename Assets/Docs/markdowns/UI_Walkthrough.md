# Walkthrough — Sistemas de UI: Menu Principal + Controles

## Arquivos Criados

| Arquivo | Descrição |
|---------|-----------|
| `Assets/Scripts/UI/MainMenuUI.cs` | Menu principal: Jogar, Opções, Créditos |
| `Assets/Scripts/UI/ControlsReferenceMenu.cs` | Menu de controles in-game (F2) |

---

## Setup Manual no Unity — Passo a Passo

### Parte 1: Cena MainMenu

#### Passo 1 — Criar a cena

1. No Unity, vá em **File → New Scene** (ou `Ctrl+N`)
2. Escolha **Basic (Built-in)** e clique **Create**
3. Salve a cena: **File → Save As** (ou `Ctrl+Shift+S`)
4. Navegue até `Assets/Scenes/`
5. Nomeie como **`MainMenu`** e clique **Save**

#### Passo 2 — Criar o GameObject do Menu

1. Com a cena `MainMenu` aberta, no painel **Hierarchy**, clique com botão direito
2. Selecione **Create Empty**
3. Renomeie o GameObject para **`MainMenuManager`** (clique duplo no nome)
4. No painel **Inspector** (com o MainMenuManager selecionado):
   - Clique em **Add Component**
   - Digite **`MainMenuUI`** e selecione o script

#### Passo 3 — Configurar no Inspector

Com o `MainMenuManager` selecionado, você verá no Inspector:

```
MainMenuUI (Script)
├── Navegação
│   └── Play Scene Name: "BaseLab"    ← cena que carrega ao clicar Jogar
├── Créditos
│   ├── Studio Name: "EPTA Entertainment"
│   ├── Team Members: [lista de nomes]  ← adicione nomes da equipe aqui
│   └── Game Version: "v0.1 Alpha"
```

> **DICA:** Para adicionar membros da equipe: clique no **+** no array `Team Members` e escreva cada nome.

#### Passo 4 — Deletar objetos desnecessários

A cena veio com câmera e luz padrão. Como o menu é UI pura (ScreenSpace Overlay), você pode:
1. **Manter a Main Camera** (ela não atrapalha, mas também não é usada)
2. **Deletar Directional Light** se quiser (opcional, não afeta UI)

#### Passo 5 — Adicionar ao Build Settings

> **IMPORTANTE:** Este é o passo mais importante! Sem isso o jogo não sabe que a cena existe.

1. Vá em **File → Build Settings** (ou `Ctrl+Shift+B`)
2. Com a cena `MainMenu` **aberta no Editor**, clique em **Add Open Scenes**
3. A cena aparecerá na lista. Agora organize a **ordem**:
   - **`MainMenu`** deve ser o **índice 0** (primeira da lista)
   - Arraste ela para o topo se necessário
4. A lista deve ficar mais ou menos assim:

```
Scenes In Build:
  ☑ Scenes/MainMenu          0  ← PRIMEIRA!
  ☑ Scenes/BaseLab           1
  ☑ Scenes/GameScene          2
  ☑ ...outras cenas...
```

5. Feche a janela (não precisa clicar Build)

> **CUIDADO:** Se `MainMenu` não estiver no índice 0, o jogo vai iniciar em outra cena ao invés do menu.

---

### Parte 2: Menu de Controles (F2)

#### Passo 1 — Adicionar ao Player

1. Abra a cena **`BaseLab`** (ou a cena onde seu Player está)
2. No **Hierarchy**, encontre o GameObject do seu **Player** (o astronaut)
3. Selecione o Player
4. No **Inspector**, clique em **Add Component**
5. Digite **`ControlsReferenceMenu`** e selecione

#### Pronto!

O script cria tudo sozinho e persiste entre cenas com `DontDestroyOnLoad`. Não precisa configurar mais nada.

> **DICA:** Se quiser mudar a tecla de F2 para outra, basta alterar o campo **Toggle Key** no Inspector.

---

### Parte 3: Testar

1. **Abra a cena MainMenu** (`Assets/Scenes/MainMenu`)
2. Clique **Play** ▶
3. Teste:
   - **Jogar** → deve carregar a BaseLab
   - **Opções** → mostra sliders e toggles (os sliders de áudio mexem mas são visuais por enquanto)
   - **Créditos** → mostra EPTA Entertainment
   - **Sair** → para o Play Mode no Editor
4. Na **BaseLab**, pressione **F2** → deve aparecer o menu de controles
5. Pressione **F2** de novo → fecha

---

## Como Adicionar Novos Controles no Futuro

Abra `ControlsReferenceMenu.cs` e encontre o método `PopulateControls()`. Para adicionar uma nova keybind:

```csharp
// Dentro de PopulateControls():

// Nova seção:
y = AddSection(parent, "🆕  NOVA CATEGORIA", y);

// Novo controle:
y = AddKeybind(parent, "TECLA", "Descrição da ação", y);

// Com cor customizada:
y = AddKeybind(parent, "X", "Ação especial", y, C_GOLD);
```

## Como Modificar o Menu Principal

No `MainMenuUI.cs`:

- **Cores**: altere as constantes `C_BG`, `C_ACCENT`, etc. no topo da classe
- **Novo botão**: adicione um `MakeButton()` no `BuildMainPanel()`
- **Nova opção**: use `AddSliderRow()`, `AddToggleRow()` ou `AddDropdownRow()` no `BuildOptionsPanel()`
