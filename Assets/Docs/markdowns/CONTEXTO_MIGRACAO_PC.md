# Contexto Completo de Migracao - Transicao Notebook para PC

Arquivo mestre de alinhamento e contexto geral do projeto Leaving Plerotus para a sessao no PC Desktop. Este documento unifica as duas conversas de trabalho realizadas no Notebook (Planejamento de Sprint e Sistema de Quests/Dialogos).

---

## 1. Diretrizes de Versionamento e Branches
- Branch de trabalho ativa: VicenzoWS (Nao commitar na main e nao realizar merges no momento).
- Regra de equipe: Nao realizar nenhum merge por enquanto; todos os membros trabalham isolados em suas branches (Lucass, matheus, pedro, isaac, etc.).
- Git LFS: Ao clonar ou sincronizar o projeto no PC, rodar obrigatoriamente:
  ```bash
  git lfs install
  git lfs pull
  ```
  para restaurar texturas pesadas e modelos FBX/GLB do Blender sem quebrar com erro de ponteiro.

---

## 2. Frente A: Planejamento das Tarefas da Sprint (Equipe de 6 Pessoas)
Documento detalhado disponivel em: Assets/Docs/sprint_atualizada.md

- Lucas:
  - Corrigir bug do Animator da arma ranged que quebra ao clicar em qualquer lugar.
  - Aumentar alcance (range) para cobrir o mapa todo.
  - Sistema de municao consciente: recarrega automatico com o tempo; se esvaziar 100%, trava e obriga reload manual longo sem exit time.
  - Loot T4 que altera o projetil para tiro teleguiado (aimbot).
  - Feedback visual no HUD (indicador de balas).

- Isaac:
  - Construir a base do Modelo de Constelacao para o mapa.
  - Preparar a estrutura de dados (ScriptableObjects/dicionarios) para Sinergias terem nomes proprios e dicas descritivas (apenas campos logicos por enquanto).
  - Cuidado com o bug central de infusao (Matheus cuidara do processamento pesado).

- Gui Serralha:
  - Implementar no codigo as 10 sinergias oficiais da Teia de Sinergias (detalhadas em Assets/Docs/material_gui_serralha.md).
  - Refatorar a Adaga (game feel, hitboxes, tempos de ataque e recovery).
  - Montar apresentacao/pitch de game design com sua visao sobre o sistema de skills e infusoes para a reuniao.
  - Material de apoio e itens da bolsa sintetica disponiveis em Assets/Docs/itens_bolsa_sintetica.md.

- Matheus:
  - Trabalhar exclusivamente na sua branch, sem merges.
  - Resolver o bug de que nao esta sendo possivel fundir armas: selecionar 1 item funde; selecionar 2 itens dobra a fusao, recalculando os pesos de inflacao.
  - Criar um modulo visual legivel do Historico de Infusoes dentro do Inventario do jogador.
  - Implementar no Mercador a "Cirurgia de Remocao": lista as infusoes atreladas e permite retirar uma peca (nao devolve essencia, mas abate o peso e recalcula a inflacao para baixo).

- Pedro:
  - Pitch de teoria sobre Variantes "Elite" de todos os inimigos (exceto Geobionte e Bismutado).
  - Implementar 3 variantes na Unity usando Prefab Variants.
  - Arquitetura de geracao por Dupla Rolagem (Sub-Roll): o Room Generator sorteia o mob base por pontos normalmente, e logo em seguida roda uma probabilidade interna (mutationChance) para instanciar a variante ou a versao base. Codigo de referencia presente em Assets/Docs/sprint_atualizada.md.
  - Loot Table da variante modificada com mais chance de T3 e T4.

- Vicenzo:
  - Lideranca, acompanhamento das branches, validacao da adaga e do pitch do Gui Serralha, e definicao do nome comercial oficial do jogo.

---

## 3. Frente B: Sistema de Quests e Dialogos do Tutorial (Outra Conversa)
Documentos detalhados disponiveis em: 
- Assets/Docs/Missoes_e_Dialogos_Tutorial.md
- Assets/Docs/textos_tutorial_eptinho.md

- Arquitetura de Quests Criada:
  - Scripts envolvidos: QuestManager.cs, QuestData.cs, DialogManager.cs, DialogTrigger.cs, ObjectiveMarker.cs, FloatingIndicator.cs.
  - Logica de Funcionamento: O DialogManager aceita um callback (onComplete) que avisa o DialogTrigger para concluir a quest no QuestManager.
  - Quest 0 (Introducao do Eptinho): O dialogo inicial do Corgi Eptinho na base funciona como a "Quest 0" (Quest_Intro). Uma vez lido pelo jogador, ele marca como concluido no SaveManager e nunca mais reaparece (a menos que seja resetado com Shift+R).
  - Quest 1 (Consertar a Porta da Run): Ao concluir o dialogo inicial, a Quest 1 se ativa, apontando a exclamacao para o console/porta da Run.
  - Proximas Missoes Planejadas: Entrada na Run, Coleta da 1ª parte de monstro (Sinergia), Encontro com o Sintonizador e Encontro com o Mercador.

---

## 4. Como o Antigravity do PC Deve Assumir o Trabalho
Ao iniciar no PC Desktop, basta pedir ao Antigravity:
"Leia o arquivo Assets/Docs/CONTEXTO_MIGRACAO_PC.md para carregar todo o contexto das tarefas da equipe e do sistema de Quests/Dialogos desenvolvidos no notebook."
Ele estara 100% atualizado e pronto para continuar o desenvolvimento.
