# Análise Técnica e Diagnóstico do Bumerangue (Opção B - Boomerang)

> **Documento de Referência:** `B - Boomerang.pdf`  
> **Data da Análise:** 15 de Novembro de 2026  
> **Objetivo:** Fornecer à próxima IA / Desenvolvedor um mapeamento completo do que já está implementado, diagnóstico detalhado dos bugs encontrados e o plano de ação exato para as correções e evolução da arma.

---

## 1. Visão Geral da Arquitetura do Bumerangue

O bumerangue foi estruturado em uma Máquina de Estados desacoplada (`BoomerangWeapon.cs`) com módulos especializados de VFX em `Assets/_Project/Scripts/VFX/`:

```
Boomerang (GameObject Raiz)
 ├── BoomerangWeapon.cs (Máquina de Estados: Stowed, Charging, Throw, Returning, SpinningShield)
 ├── BoomerangAirSpinVFX.cs (Giro da lâmina, anéis de vento e rastros das pontas)
 ├── BoomerangElectricCrystalVFX.cs (Variante 1: Cristal Desacoplante Elétrico)
 ├── BoomerangEchoVFX.cs (Variante 2: Clones na volta & Ecos autônomos no bounce)
 ├── BoomerangShieldVFX.cs (Habilidade Melee: Escudo giratório, Parry de 0.2s e Paralisia)
 ├── WeaponOffset.cs (Offsets na Mão Direita e Costas Spine2)
 └── VisualPivot -> boomerang (Modelo FBX normalizado para rotação no centro)
```

---

## 2. Comparativo: GDD (`B - Boomerang.pdf`) vs Código Implementado

| Página do PDF | Conceito no PDF | Status Atual no Código | Detalhes Técnicos |
| :--- | :--- | :--- | :--- |
| **Págs. 1–3** | **Arte Conceitual & Animações**<br>Visual shuriken/bumerangue, animações de sacar das costas, guardar e defesa. | ✅ **Implementado** | • Modelo `Assets/_Project/Items_and_Crafting/Boomerang/boomerang.fbx`<br>• Encaixe na mão (`mixamorig:RightHand`) e costas (`mixamorig:Spine2`) gerenciado via `WeaponOffset.cs`.<br>• `BoomerangAnimation.controller` configurado com `Astronaut_tirandodascostas_boomerang`, `Astronaut_guardando_boomerang` e `Astronaut_defense`. |
| **Pág. 4** | **Habilidade Base - Longo Alcance**<br>Lançamento padrão, quica nos inimigos/paredes e retorna para a mão do jogador. | ✅ **Implementado (Protótipo funcional)** | • Estados `Charging`, `Throw` e `Returning` em `BoomerangWeapon.cs`.<br>• Mira orientada pelo mouse (`GetMouseAimDirection`).<br>• Cálculo de ricochetes (`RegisterBounce`, de 2 a 6 quiques baseados no tempo de carga).<br>• Retorno com perseguição dinâmica (`DynamicHoming`), seguindo o jogador mesmo se ele der Dash. |
| **Pág. 5** | **Upgrade 1 - Cristal Desacoplante Elétrico**<br>Pontas se separam no ar com feixes elétricos, aumentam a área de acerto e paralisam (estilo *Istar*). | ✅ **Implementado** | • `BoomerangElectricCrystalVFX.cs` desacopla radialmente os sub-objetos `piramide_superior` e `piramide_inferior` durante o voo.<br>• Feixes de `LineRenderer` conectam a base às pontas e geram arcos externos.<br>• Expande o raio de acerto (`GetExpandedHitRadius`).<br>• Aplica status de lentidão e choque elétrico. |
| **Pág. 6** | **Upgrade 2 - Bounce + Ecos**<br>Cria clones translúcidos temporários (estilo *SharpBlur*) na volta e ecos fantasmas autônomos nos quiques. | ✅ **Implementado** | • `BoomerangEchoVFX.cs` captura snapshots de malha (`Graphics.DrawMesh`) gerando afterimages holográficas translúcidas na volta.<br>• Spawna ecos fantasmas independentes (`SpawnBounceEchoClone`) que voam e giram após quicar em paredes ou inimigos. |
| **Pág. 7** | **Habilidade Base - Melee: Escudo Giratório**<br>Gira o bumerangue em alta velocidade criando uma barreira. Leve dano/repulsão a quem encostar. | ✅ **Implementado** | • Estado `SpinningShield` ativado segurando o **Botão Direito do Mouse** ou tecla **R**.<br>• `BoomerangShieldVFX.cs` gera discos de energia concêntricos e ativa a pose defensiva (`Astronaut_defense`).<br>• Possui janela de **Parry de 0.2s** (`IsInParryWindow`) que inverte a trajetória de projéteis inimigos. |
| **Pág. 8** | **Upgrade Melee - Paralisia no Escudo**<br>Chance de causar paralisia em inimigos que colidirem com o escudo. | ✅ **Implementado** | • Integrado em `BoomerangShieldVFX.TriggerShieldMeleeParalysis()`. Aplica atordoamento (`StunVFX`), lentidão de 90% e choque elétrico. |

---

## 3. Diagnóstico Detalhado dos Problemas Encontrados

### ⚠️ Bug 1: Jogador tomando dano próprio ao lançar o bumerangue (CRÍTICO)

* **Sintoma:** Ao arremessar o bumerangue, o jogador toma dano instantâneo por tick, sofre lentidão e fica com efeito de eletricidade no corpo.
* **Causa Raiz Identificada:**
  1. A Variante 1 do PDF (Cristal Desacoplante Elétrico) vem ativada por padrão: `enableElectricCrystalVariant = true` em `BoomerangWeapon.cs`.
  2. Durante o voo, `BoomerangElectricCrystalVFX.cs` chama `UpdateAirElectricTrails()`, gerando rastros no ar via:
     ```csharp
     ElectricTrailVFX.CreateTrailSegment(prevPos, curPos, 3, 0.35f);
     ```
  3. O script `ElectricTrailVFX.cs` foi reaproveitado do chefe/mob inimigo **Istar** (`ShardSwarm_AI.cs`). O método de colisão dele (`OnTriggerEnter` e `OnTriggerStay`) faz:
     ```csharp
     private void TriggerStatusOnPlayer(Collider other)
     {
         if (other.CompareTag("Player") || (other.transform.root != null && other.transform.root.CompareTag("Player")))
         {
             int actualDamage = (damagePerTick > 0) ? damagePerTick : 5;
             ElectrocutedStatus.ApplyElectrocuted(other.gameObject, actualDamage, 0.50f, 3.0f);
         }
     }
     ```
  4. Como o bumerangue é lançado a partir do corpo do jogador (`throwOriginPos = originTr.position + Vector3.up * 1.1f`), os primeiros segmentos de `ElectricTrailVFX` colidem com o próprio astronauta! O rastro assume que o jogador é o alvo e aplica `ElectrocutedStatus`, causando dano por segundo e 50% de lentidão a si mesmo.
* **Solução:**
  * No script `ElectricTrailVFX.cs`, adicionar uma flag `public bool isHostileToPlayer = true;`. Quando chamado pelo bumerangue do jogador, passar `isHostileToPlayer = false` e ignorar colisões com a tag `"Player"` (atingindo apenas quem tiver `DummyHealth` ou tag `"Enemy"`).
  * Como alternativa imediata, desmarcar `spawnAirElectricTrailSegments = false` em `BoomerangElectricCrystalVFX.cs`.

---

### ⚠️ Bug 2: Animação de ataque com mãos vazias se clicar enquanto o bumerangue está no ar

* **Sintoma:** Enquanto o bumerangue está voando ou voltando, dar um clique com o Botão Esquerdo faz o astronauta disparar a animação de ataque corpo a corpo com as mãos vazias, sem lançar nada.
* **Causa Raiz Identificada:**
  1. Em `Player_WeaponManager.cs` (linhas 69–70 e 102–103), a checagem que decide se a arma empunhada é melee faz apenas:
     ```csharp
     bool isMelee = offset.weaponType != WeaponType.Gun;
     attackScript.hasWeapon = isMelee;
     ```
     *(Faltou verificar se é diferente de `WeaponType.Boomerang`, como já é feito na linha 244).*
  2. Como `WeaponType.Boomerang != WeaponType.Gun` é verdadeiro, o script `PrimaryAttackKnife.cs` assume que há uma faca/machado na mão e mantém `hasWeapon = true`.
  3. Ao clicar com o mouse enquanto o bumerangue está fora, o `BoomerangWeapon` ignora o arremesso, mas o `PrimaryAttackKnife` captura o clique e executa `PerformNextAttack()`, tocando a animação `Attack1` (`Side.anim`).
* **Solução:**
  * Atualizar as checagens em `Player_WeaponManager.cs` para:
    ```csharp
    bool isMelee = offset != null && offset.weaponType != WeaponType.Gun && offset.weaponType != WeaponType.Boomerang;
    attackScript.hasWeapon = isMelee;
    ```
  * Adicionalmente, quando `currentState == BoomerangState.Throw`, um novo clique pode opcionalmente acelerar o retorno imediato (Recall), sem disparar animações de corte do player.

---

### ℹ️ Esclarecimento 3: O que são as "Habilidades Desbloqueáveis ao Melhorar"?

* **Conceito de Design:**
  No GDD original (`B - Boomerang.pdf`), o bumerangue foi projetado para ter uma árvore de progressão roguelike:
  * **Nível 1 (Básico):** Apenas arremesso com quique padrão e escudo sem efeitos.
  * **Ao Melhorar (Upgrades):**
    * *Upgrade Longo Alcance 1:* Cristal Desacoplante Elétrico (Pág. 5).
    * *Upgrade Longo Alcance 2:* Ecos e Clones nos Quiques (Pág. 6).
    * *Upgrade Melee 1:* Paralisia no Escudo Giratório (Pág. 8).
* **Situação Atual no Projeto:**
  O sistema da base (`PlayerUpgrades.cs` / `CraftingManager.cs`) ainda não possui os nós/botões de compra específicos para essas melhorias do bumerangue.
  Por essa razão, para fins de teste no desenvolvimento, **todas as três melhorias foram deixadas pré-ativadas no código** em `BoomerangWeapon.cs`:
  ```csharp
  public bool enableElectricCrystalVariant = true;       // Pág. 5 (Cristal Elétrico)
  public bool enableTranslucentClonesOnReturn = true;   // Pág. 6 (Clones na volta)
  public bool enableBounceEchoSpawning = true;          // Pág. 6 (Ecos nos ricochetes)
  public bool enableShieldMeleeParalysis = true;         // Pág. 8 (Paralisia Melee)
  ```
  *(Nota: Há atalhos no editor pressionando `Ctrl + 1`, `Ctrl + 2` e `Ctrl + 3` para ligar/desligar cada uma dessas variantes em tempo real).*

---

## 4. Arquivos Envolvidos e Suas Responsabilidades

1. `Assets/_Project/Scripts/Player/BoomerangWeapon.cs`  
   Máquina de Estados central (`Stowed`, `Charging`, `Throw`, `Returning`, `SpinningShield`), mira, quiques e retorno inteligente.
2. `Assets/_Project/Scripts/VFX/BoomerangAirSpinVFX.cs`  
   Efeito visual de lâmina girando no ar, linhas ciano procedurais e rastro das pontas.
3. `Assets/_Project/Scripts/VFX/BoomerangElectricCrystalVFX.cs`  
   Desacoplamento radial dos cristais, feixes de raio estilo Istar e rastro elétrico.
4. `Assets/_Project/Scripts/VFX/BoomerangEchoVFX.cs`  
   Renderização de clones translúcidos (afterimages) e ecos fantasmas de ricochete.
5. `Assets/_Project/Scripts/VFX/BoomerangShieldVFX.cs`  
   Escudo circular giratório, janela de parry de 0.2s e choque elétrico melee.
6. `Assets/_Project/Scripts/VFX/ElectricTrailVFX.cs`  
   Rastro elétrico gerado no chão/ar com detecção de colisão (origem do bug de dano próprio).
7. `Assets/_Project/Scripts/Player/Player_WeaponManager.cs`  
   Gerenciamento de empunhar/guardar armas e alternância de Animator Controller.
8. `Assets/_Project/Scripts/Player/PrimaryAttackKnife.cs`  
   Script legado de ataque básico do player (origem do bug da animação fantasma).
9. `Assets/_Project/Props/WeaponRack/Resources/Boomerang.prefab`  
   Prefab oficial da arma gerado para coleta no Weapon Rack.

---

## 5. Histórico e Resolução dos 5 Ajustes Solicitados (15/11/2026)

| # | Item Solicitado | Status | Solução Implementada |
| :--- | :--- | :--- | :--- |
| **1** | **Nomes no Inspector ("Lucas" / "Isaac")** | ✅ **Concluído** | Todos os atributos `[Header]`, `[Tooltip]`, comentários de código e regiões que continham nomes de pessoas em `BoomerangWeapon.cs`, `BoomerangSetupHelper.cs`, `BoomerangShieldVFX.cs`, `BoomerangEchoVFX.cs` e `BoomerangElectricCrystalVFX.cs` foram substituídos por termos técnicos e descritivos limpos. |
| **2** | **Alinhamento nas costas (entrando na cabeça)** | ✅ **Concluído** | O offset `backMountPosition` e `stowedBackPosition` foi refinado para `(0f, -0.08f, -0.20f)` com rotação `(0f, 0f, 45f)` em `BoomerangWeapon.cs`, `WeaponOffset.cs`, `BoomerangSetupHelper.cs` e nos prefabs. O bumerangue agora fica repousado nas costas sem penetrar no capacete. |
| **3** | **Prefab da base não atualiza ao editar o prefab da pasta** | ✅ **Concluído** | Identificado que `WeaponRack.cs` carrega em tempo de execução de `Resources/Boomerang.prefab`. O prefab oficial de runtime é o de `Resources/`, sendo o que deve ser editado. Prefabs alinhados. |
| **4** | **Animação de ataque ao clicar com bumerangue no ar** | ✅ **Concluído** | Corrigido em `Player_WeaponManager.cs` (linhas 69 e 102), garantindo que armas do tipo `WeaponType.Boomerang` nunca ativem `attackScript.hasWeapon = true`, impedindo a reprodução da animação melee de mãos vazias. |
| **5** | **Dano próprio ao lançar com a variante elétrica** | ✅ **Concluído** | Adicionada flag `isHostileToPlayer` em `ElectricTrailVFX.cs` (padrão `true` para inimigos como *ShardSwarm/Istar*). Os métodos geradores em `BoomerangElectricCrystalVFX.cs` e `BoomerangShieldVFX.cs` agora passam `hostileToPlayer: false`, prevenindo qualquer dano ou lentidão ao astronauta. |
| **6** | **Encaixe na mão atravessando o corpo (Idle / Charging)** | ✅ **Concluído** | O `handMountRotation` anterior `(0, 90, 90)` apontava a lâmina transversalmente cortando o quadril do astronauta. Foi alterado para rotação `(0, 0, 90)` e posição `(-0.14, 0.08, 0.05)`, alinhando a lâmina longitudinalmente ao lado da coxa na mão direita. |
| **7** | **Mecânica de Carregamento (Hold Attack)** | ✅ **Documentado** | Segurar o botão esquerdo carrega o arremesso de 0 a 100% (0.85s). Aumenta a velocidade de 14 para 26, alcance de 5.5m para 13.5m e quiques de 2 para 6. Acima de 70% de carga, o bumerangue vibra na mão como feedback tátil antes do lançamento. |

---

## 6. Próximos Passos Recomendados

1. **Refinar a Física e Combate:**
   Completar os métodos na região `PHYSICS_AND_COMBAT_HOOKS` de `BoomerangWeapon.cs` para ajustar knockback vetorial, dano escalonado e contagem exata de quiques entre inimigos próximos.
2. **Conectar Upgrades à Progressão do Jogo:**
   Expor os booleanos de melhoria (`enableElectricCrystalVariant`, `enableBounceEchoSpawning`, etc.) para serem desbloqueados através da árvore de upgrades da base ou drops durante a run.

