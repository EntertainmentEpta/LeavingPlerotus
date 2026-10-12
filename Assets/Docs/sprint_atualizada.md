# Quadro de Atividades e Sprint: Living Plerotus

Status: Sem merges imediatos. Desenvolvimentos parciais deverao ser mantidos em branches individuais isoladas ate a integracao controlada.

## 1. Lucas (Arma de Fogo, Alcance e Sistema de Municao Consciente)
- Bugfix de Animacao: Diagnosticar e corrigir o estado do Animator Controller da arma de longa distancia que esta travando/quebrando a animacao de tiro ao se clicar em posicoes arbitrarias na tela.
- Aumento de Alcance (Range): O limite atual do Raycast ou colisor esta muito curto. Escalonar o calculo da trajetoria para que a arma possa atingir alvos cobrindo a extensao total do cenario/mapa.
- Tiro Teleguiado (Loot T4): Implementar a troca de municao para um comportamento de projeteis guiados (Aimbot). Ao coletar e equipar um drop Tier 4 especifico de um mob, os projeteis instanciados devem utilizar a transform do inimigo mais proximo dentro do raio de deteccao para alterar a trajetoria dinamicamente em tempo real.
- Sistema de Municao Consciente e Recarga Punitiva:
  - Regeneracao Passiva: Programar uma rotina (Coroutine ou FixedUpdate) que recarregue a municao automaticamente com o passar do tempo, condicionada estritamente a nao deixar o valor da arma zerar (municao atual > 0).
  - Punicao de Esvaziamento (100% Vazio): Se o jogador zerar completamente a municao por atirar sem cadencia (spam), a arma trava e a regeneracao automatica entra em hiato ou e interrompida.
  - Recarga Manual (Reload): O input de Reload devera servir exclusivamente para recuperar a arma do estado zerado. Ao ser pressionado nesse estado, uma animacao inteira de recarga deve tocar sem a configuracao de "Exit Time", de forma a proibir interrupcoes. O destravamento dos tiros so ocorrera via Animation Event no exato ultimo frame da acao, forcando o jogador a ser punido pela falta de gerenciamento.
  - Feedback UI: Vincular o valor atual de balas a um componente visual nativo (como um Slider ou grupo de imagens ativaveis) no Canvas/HUD, atualizando-se de forma sincronizada ao disparo e recarga para conscientizacao imediata.

## 2. Isaac (Base do Mapa Constelacao e Estrutura de Sinergias)
- Modelo de Constelacao: Estruturar a base, classes e fluxos para a geracao de fases em formato de Constelacao, utilizando exatamente a arquitetura especificada na ultima reuniao, integrando-a ou sobrepondo-a a geracao procedural existente.
- Banco e Dicas de Sinergias: Expandir os dicionarios ou classes de ScriptableObjects responsaveis pelo armazenamento das Sinergias para que aceitem nativamente campo "Nome Proprio" e uma "Dica de Descoberta" associada. O escopo desta semana e inteiramente voltado a estruturacao desses campos logicos de armazenamento e interfaceamento via script, ignorando as descricoes e textos reais.
- Atencao - Base da Infusao: Ao mexer na estrutura logica da base que conversa com sinergias, ter em mente que o fluxo da infusao esta bloqueado/falho. Garanta que a arquitetura das sinergias possa consultar os dados limpos das infusoes (o Matheus tratara o processamento do bug de fundir paralelamente na branch dele).

## 3. Gui Serralha (Sinergias, Refatoracao da Adaga e Pitch de Mecanicas)
- Implementacao Mecanica das Sinergias: Transformar em logica de combate funcional as sinergias que estao mapeadas no fluxo logico da teia (ex: combinar bonus do Goblin e SharpBlur gera fisicamente multiplos projeteis com dano acrescido). As relacoes e origens dos itens base estao no arquivo isolado de apoio (material_gui_serralha.md).
- Rebalanceamento e Game Feel da Adaga: Refatorar o comportamento da Adaga. Isso abrange manipular as Hitboxes para consistencia visual, ajustar as taxas de recuperacao (Recovery Frames), o intervalo de ataque ativo e possivelmente efeitos visuais associados. A missao central e atestar a usabilidade real e responsividade da arma em batalhas de alta intensidade.
- Pitch de Game Design: Estruturar uma apresentacao ou documento textual apresentando seu ponto de vista sistemico e criativo sobre a mecanica geral de utilizacao de "Skills" combinadas com a tela de Infusoes. Esse material sera a base de discussao estrutural da equipe em reuniao.

## 4. Matheus (Logica Rigorosa de Infusao e Inventario do Mercador)
- Isolamento: Trabalhar integralmente em branch especifica. Merges estao suspensos.
- Bug Central de Infusao: Diagnosticar e consertar a falha que impede fundir as armas atualmente. A mecanica deve ser muito clara para o jogador. O fluxo requer processar a interacao ao clicar em 1 item para efetivar 1 processo de infusao padrao, e ser logico ao processar 2 itens para fundi-los acumulados (dobrados), repassando o multiplicador progressivo instantaneamente ao codigo e somando na equacao de inflacao.
- Modulo Visual do Historico de Infusoes: Criar um painel logico legivel, exigindo senso critico e design visual, nativamente integrado ao Inventario. Esse painel tera que mostrar visualmente quais infusoes/itens ja estao atrelados a arma do jogador naquela run para garantir ciencia imediata dos proprios buffs.
- Cirurgia de Remocao (O Mercador): Integrar a logica do encontro do Mercador para que ele leia essa exata lista visual de infusoes. O sistema precisa permitir ao jogador clicar na peca atrelada e pagar para retira-la do arsenal. O ato da remocao destruira a peca sem devolver nenhum custo de essencia para o jogador. O ganho dessa cirurgia sera apenas invocar o recalculo do sistema para subtrair o peso atual de inflacao daquela peca, tornando as futuras infusoes mais baratas de se adquirir.

## 5. Pedro (Concepcao de Variantes Elite e Arquitetura de Spawn Duplo)
- Pitch Criativo (Teoria): Projetar o conceito de variacoes "Elite" para todos os inimigos do bestiario (excluindo Geobionte e Bismutado). Determinar alteracoes visuais (escala, matiz do material, cor da barra de vida) e aprimoramentos de status (ex: maior HP, ataque acelerado, forca de stun elevada). O objetivo e apresentar a visao geral dessas variantes na reuniao.
- Implementacao Pratica (Prototipagem): Escolher 3 inimigos dessa lista e implementa-los completamente no motor da Unity.
- Arquitetura Visual Obrigatoria (Prefab Variants): E estritamente proibido duplicar os prefabs originais para criar os elites. Utilize obrigatoriamente a funcao "Create > Prefab Variant" da Unity a partir do inimigo base. Isso garante que logicas fundamentais da IA sejam herdadas automaticamente sem duplicacao de codigo. Modifique apenas os componentes visuais, de UI e atributos no Variant.
- Arquitetura de Geracao de Salas (Spawn Duplo/Sub-Roll): Nao insira as Variantes diretamente na mesma pool geral de probabilidades do Room Generator. Isso quebrara o balanceamento e o orcamento de pontos do jogo (impossibilitando o controle de orcamento entre mobs leves e tanques). A arquitetura exigida e a Interceptacao de Dupla Rolagem:

  Crie a classe de configuracao base:
  ```csharp
  [System.Serializable]
  public class EnemySpawnSetup 
  {
      public GameObject basePrefab;
      public GameObject variantPrefab;
      [Range(0f, 1f)]
      public float mutationChance; // Ex: 0.15 para 15% de chance de virar elite
      public int costInPoints;
  }
  ```

  No laco de instanciacao do Spawner, apos o sistema original decidir qual inimigo foi sorteado (pagando os pontos normalmente), adicione a interceptacao:
  ```csharp
  // "selectedSetup" e o inimigo escolhido pela probabilidade normal da sala
  GameObject prefabToSpawn = selectedSetup.basePrefab;

  // Interceptacao da Rolagem de Mutacao
  if (selectedSetup.variantPrefab != null && UnityEngine.Random.value <= selectedSetup.mutationChance) 
  {
      prefabToSpawn = selectedSetup.variantPrefab;
  }

  Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
  ```

  Esta logica garante intrinsecamente que variantes de inimigos com altas taxas de spawn globais continuem aparecendo com maior frequencia que as variantes de elites raros, mantendo a estabilidade da fauna.
- Rebalanceamento da Loot Table (Drops): No prefab da Variante, sobrescreva os parametros da tabela de loot do componente original. Sendo uma ameaca rara, diminua drasticamente ou zere a probabilidade de drops Tier 1, e infle a probabilidade de drop dos itens corporais de Tier 3 e Tier 4 correspondentes a este inimigo.
