# Material de Apoio: Gui Serralha (Sinergias & Drops)

Para a sua tarefa de implementar o sistema de sinergias e montar a apresentacao sobre as skills e infusoes, aqui esta todo o material extraido da documentacao do projeto que voce precisara usar:

## 1. Lista Oficial de Sinergias (Teia de Sinergias)

O sistema deve prever a criacao destas sinergias compostas ao juntar os drops corretos nas infusoes:

1. Chuva de Laminas (Multi-Tiro do Goblin + Dano Base do SharpBlur)
2. Parede Intransponivel (Armor Max do Golem + Armor Colossal do Sentinela)
3. Corte Congelante (Lentidao do Cristalus + Dano/Crit do SharpBlur)
4. Assassino Fantasma (Dash da Aranha + Esquiva da Pedra Magica)
5. Dominio de Espaco (Knockback do Totem + Range Melee do Watcher)
6. Tempestade Focada (Quicar do Tuner + Mult. Critico do Swarm)
7. Aura Espinhosa (Espinhos do Dragao + Regen Armor do Cristalus)
8. Fuga Explosiva (Cargas de Dash do Totem + Dispersao do Goblin)
9. BERSERKER IMORTAL (Sinergia Especial/Secreta de 4 partes)
10. A ASCENSAO - Modo Deus (Requer as 13 partes T4 Lendarias juntas)

## 2. Itens de Drop dos Inimigos (Usados nas Infusoes)

Estes sao os pedacos/partes corporais que os inimigos dropam para voce infundir na arma (separados por Tier 1 Comum a Tier 4 Lendario):

- Aranha: T1 (Vel. Ataque) | T2 (-CD do Dash) | T3 (Vel+Dash) | T4 (Ataques envenenam)
- SharpBlur: T1 (+Dano Base) | T2 (+Chance Critica) | T3 (Dano+Crit) | T4 (Sangramento nos Criticos)
- Dragao: T1 (Espinhos reflete dano) | T2 (+Knockback) | T3 (Esp+Recuo) | T4 (Dano Refletido atordoa)
- Goblin: T1 (Projeteis extras) | T2 (Spread de tiros) | T3 (Tiro+Spread) | T4 (Dash solta bomba)
- Watcher: T1 (+Alcance Projetil) | T2 (+Alcance Melee) | T3 (Alcance M+P) | T4 (Tiros teleguiados)
- Tuner: T1 (Perfuracao) | T2 (Quicar) | T3 (Furar+Quicar) | T4 (Marca de Dano no alvo)
- Cristalus: T1 (Regen Armor) | T2 (Slow no hit) | T3 (Regen+Slow) | T4 (Escudo nao tem delay)
- Golem: T1 (+Armor Max) | T2 (Chance negar dano) | T3 (Armor+Nega) | T4 (Ganha escudo com HP baixo)
- P. Magica: T1 (+HP Max) | T2 (+Esquiva) | T3 (HP+Esquiva) | T4 (Cura ao limpar a sala)
- Swarm: T1 (I-Frames no Dash) | T2 (Mult. Critico) | T3 (I-Frame+Crit) | T4 (Hit solta cristais)
- Totem: T1 (+Cargas Dash) | T2 (Empurrar inimigos) | T3 (Dash+Knock) | T4 (Dash da Escudo)
- Bismutado: T1 (Dano Base++) | T2 (Crit Mult++) | T3 (Dano+Crit++) | T4 (Cleave/Ataque em area)
- Sentinela: T1 (HP Max++) | T2 (Armor Max++) | T3 (HP+Armor++) | T4 (Roubo de vida)

## 3. Itens da Bolsa Sintetica (Base Resources)

Lembrando que os itens de Drop (lista acima) sao perdidos se nao retornarem. O que efetivamente entra na Bolsa Sintetica e persiste na base para construir os upgrades do robo sao os 17 Recursos-Base (returnsToBase):

- Flora: Cogumelo Oceano, Melocactus, Musgo Bioluminescente, Fibra, Fibra Silenciosa, Resta de Geobionte.
- Minerais: Quartzo Basico, Po de Cristal, Geodo, Prismalita, Cristal Explosivo, Lithos.
- Fauna: Caracol Geodo, Larva Lapidadora, Ovo de Cristal Crawler, Peixe de Lagoa, Vagalume Cristalizado.
