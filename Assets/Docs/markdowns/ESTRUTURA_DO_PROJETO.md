# 🧭 Guia da Nova Arquitetura do Projeto

Nossa organização de arquivos evoluiu. Para evitar dor de cabeça, infinitos conflitos com o Git e mistura com pacotes Unity corrompidos, blindamos nosso trabalho no núcleo `_Project`.

**Regra de Ouro:** Absolutamente tudo o que é de autoria da nossa equipe mora MUDOU PARA A PASTA `Assets/_Project/`. Não guarde **nada** seu na raiz!

Abaixo, um mapa rápido do seu novo ambiente de trabalho:

---

## 📦 1. A Caixa Forte (`Assets/_Project/`)

Onde todo o desenvolvimento e produção acontece.

*   `📁 Art/`: Todos os recursos visuais brutos. Dentro dela as subpastas dividem *Modelos 3D, Texturas, Materiais, Sprites e VFX*.
*   `📁 Scripts/`: Todo o código C#. Mantenham bem destrinchado em subpastas (*Player, MapGen, Enemy*...).
*   `📁 Prefabs/`: As entidades do nosso jogo já com seus scripts anexados. Para não virar bagunça, estão separados por domínios fechados:
    *   `/Player`: Só entra coisas do herói VIP (Astronauta e mecânicas).
    *   `/Enemies`: Onde mora a Aranha, Golem, Totens, Crystalseek...
    *   `/NPCs`: O Lojista (Merchant) e as ajudas.
    *   `/Environment`: Tilesets, Dungeons, Portais e salas inteiras.
    *   `/Items`: Mesas de Craft, as Adagas, os 32 itens de Drops, etc.
    *   `/UI`: Menus de tela, In-Game HUD, barras de HP.
*   `📁 Scenes/`, `Animations/`, `Audio/`: Propósitos autoexplicativos do workflow Unity.

---

## 🚫 2. Terceirizados (`Assets/ThirdParty/`)

*Plugins Gringos da Asset Store*. Alojamos o `Effects`, o pacote Espacial de Menus (`Space_Exploration_GUI...`) e o `TextMesh Pro`.
**NÃO MEXA NOS CÓDIGOS AQUI DENTRO TENTANDO PROGRAMAR SEU JOGO.** Se o criador atualizar o pacote no mês que vem e puxarmos no projeto, todas as coisas que você programou na pasta dele vão ser apagadas!

---

## 📚 3. Documentação (`Assets/Docs/`)

Aqui vive nosso Game Design Doc (GDD) em PDF além de vários arquivos curtos em `.md`. Foi criado um sistema novo de Drops? O manual (feito pela Anny ou Devs) vive aqui, assim todos tem uma biblioteca pronta de conhecimento cruzado. Sempre verifiquem esta pasta.

---

## 🧪 4. Sujeira Isolada (`Assets/_Sandbox/`)

Sede de Testes Nucleares. Quer descobrir se um shader bugado funciona testando um monstro feito do zero com código pela metade? Faça na subpasta com seu NOME, dentro do `_Sandbox`. Essa pasta não quebra o jogo de ninguém ao lado, use e abuse, mas lembre-se: limpe antes de querer passar algo em definitivo para o `_Project`.
