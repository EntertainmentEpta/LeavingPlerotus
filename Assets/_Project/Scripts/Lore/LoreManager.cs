using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gerencia a descoberta de escrituras ancestrais e o desbloqueio de blueprints no Robozinho de Síntese.
/// Singleton — persiste entre cenas via DontDestroyOnLoad.
/// </summary>
public class LoreManager : MonoBehaviour
{
    private static LoreManager _instance;
    public static LoreManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<LoreManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("LoreManager_Auto");
                    _instance = go.AddComponent<LoreManager>();
                    DontDestroyOnLoad(go);
                    Debug.Log("📜 [LORE] LoreManager auto-criado como singleton persistente!");
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstanceOnLoad()
    {
        var unused = Instance;
    }

    [Header("Escrituras Registradas")]
    public List<LoreScriptureData> allScriptures = new List<LoreScriptureData>();

    private Dictionary<string, LoreScriptureData> scriptureLookup = new Dictionary<string, LoreScriptureData>();

    public static event Action<LoreScriptureData> OnScriptureDiscovered;
    public static event Action OnResearcherBookDiscovered;

    public bool HasFoundResearcherBook => SaveManager.instance != null && SaveManager.instance.CachedData != null && SaveManager.instance.CachedData.hasFoundResearcherBook;
    public int DiscoveredScripturesCount => (SaveManager.instance?.CachedData?.discoveredScriptureIds?.Count) ?? 0;
    public int TotalScripturesCount => allScriptures.Count;
    public float CompletionPercentage => TotalScripturesCount > 0 ? ((float)DiscoveredScripturesCount / TotalScripturesCount) * 100f : 0f;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeDefaultScriptures();
    }

    private void InitializeDefaultScriptures()
    {
        // 1. Carrega assets de Resources se existirem
        LoreScriptureData[] loaded = Resources.LoadAll<LoreScriptureData>("Scriptures");
        foreach (var sc in loaded)
        {
            if (sc != null && !allScriptures.Contains(sc))
            {
                allScriptures.Add(sc);
            }
        }

        // 2. Garante a criação em memória das 6 escrituras canônicas da civilização ancestral
        EnsureCanonicalScripture(
            "scripture_acid_filter",
            "Placa Mineral: Decomposição Ácida",
            "\"Durante o primeiro mês na mina, o solo das galerias inferiores começou a corroer as esteiras das brocas e os trajes da tripulação. As criaturas de Plerotus não atacam por ódio: elas cospem ácido digestivo para dissolver matéria alienígena e alimentar a rocha. Descobrimos que combinando sais alcalinos de bismuto bruto com poeira de cristal, criamos um neutralizador celular capaz de blindar os filtros do traje contra qualquer secreção tóxica.\"",
            true,
            "equip_filtro_acido",
            "Filtro Químico de Decomposição",
            "Reduz em 40% o dano sofrido por veneno e poças ácidas no mapa todo.",
            new List<CraftingIngredient>
            {
                new CraftingIngredient { itemId = "po_cristal", quantity = 3 },
                new CraftingIngredient { itemId = "quartzo_basico", quantity = 2 }
            }
        );

        EnsureCanonicalScripture(
            "scripture_shell_breaker",
            "Fragmento de Autômato: Fratura de Casca",
            "\"Quando atingimos a câmara de basalto, as pontas das perfuratrizes industriais quebraram contra os primeiros guardiões minerais. Os Golems não são rocha inerte; são exoesqueletos biomecânicos com nervuras ocas de silício vivo que protegem os nós energéticos da crosta. Ao calibrar um solenoide de impacto para ressoar na frequência oposta à sua malha cristalina, a couraça externa entra em colapso e estilhaça com facilidade.\"",
            true,
            "equip_quebra_casca",
            "Solenoide de Quebra-Casca",
            "Ataques causam +20% de dano contra armaduras e couraças pesadas de monstros (Golems e Peixe).",
            new List<CraftingIngredient>
            {
                new CraftingIngredient { itemId = "geodo", quantity = 2 },
                new CraftingIngredient { itemId = "prismalita", quantity = 2 }
            }
        );

        EnsureCanonicalScripture(
            "scripture_hyphae_dilution",
            "Fóssil Cristalino: Diluição de Hifas",
            "\"À medida que descíamos para as fendas úmidas, teias fúngicas e micélios bioluminescentes começaram a brotar nas frestas do piso, ancorando as botas dos engenheiros à rocha para que as aranhas de sílica pudessem enxamear. O fungo busca imobilizar a presa para que a terra a absorva viva. Desenvolvemos uma graxa à base de quartzo e fibras silenciosas que repele esses filamentos e impede que as juntas mecânicas sejam travadas.\"",
            true,
            "equip_anti_hifas",
            "Agente de Diluição de Hifas",
            "Reduz em 30% a intensidade e a duração de lentidão (Slow) e teias.",
            new List<CraftingIngredient>
            {
                new CraftingIngredient { itemId = "musgo_bioluminescente", quantity = 3 },
                new CraftingIngredient { itemId = "fibra_silenciosa", quantity = 2 }
            }
        );

        EnsureCanonicalScripture(
            "scripture_tuner_disruptor",
            "Diapasão Antigo: Dispersão Harmônica",
            "\"Os monstros de Plerotus não agem individualmente: eles são regidos por nós aéreos flutuantes, os Sintonizadores de Cristal, que funcionam como neurônios externos da colmeia planetária. O feixe bio-ressonante que emitem estimula o metabolismo dos monstros próximos e coordena emboscadas letais. Construímos este disruptor de frequência para quebrar o enlace harmônico e deixar os enxames desorientados e vulneráveis.\"",
            true,
            "equip_estabilizador_sintonia",
            "Estabilizador de Frequência",
            "Inimigos buffados pelo Sintonizador não recebem bônus de cadência de ataque e perdem defesa.",
            new List<CraftingIngredient>
            {
                new CraftingIngredient { itemId = "cristal_explosivo", quantity = 2 },
                new CraftingIngredient { itemId = "prismalita", quantity = 3 }
            }
        );

        EnsureCanonicalScripture(
            "scripture_blood_tragedy",
            "Tabuleta Trincada: A Procura do Sangue",
            "\"A infecção de silício começava nas pontas dos dedos e transformava sangue humano em vidro inerte. Nossos biólogos formularam um agente de reversão, mas havia um preço trágico: a síntese exigia sangue puro e aquecido para estabilizar, recurso que se esgotou nos mineradores doentes. Quando o desespero atingiu o ápice, a loucura tomou conta da enfermaria. O Mercador encapuzado observava das sombras, acumulando o calor vital dos homens enquanto a expedição se autodestruía.\"",
            true,
            "equip_sonda_anomalia",
            "Sonda de Fissura Mineral",
            "Revela a localização de Salas de Anomalia e veios de recursos raros no mini-mapa.",
            new List<CraftingIngredient>
            {
                new CraftingIngredient { itemId = "lithos", quantity = 2 },
                new CraftingIngredient { itemId = "resta_geobionte", quantity = 2 }
            }
        );

        EnsureCanonicalScripture(
            "scripture_ancestral_weapon",
            "O Códice Final: A Arma Ancestral",
            "\"Deixo este esquema codificado para o piloto ou sobrevivente que despertar nos restos da nossa nave. A anomalia que nos prendeu não é mera gravidade: é o Geobionte, o órgão vivo no epicentro de Plerotus que consome toda matéria que entra em sua órbita. Armas convencionais apenas arranham sua couraça de bismuto. Este catalisador ancestral converte energia mineral em pulsos de desagregação celular, capazes de expor o coração do planeta e abrir a rota de escape para o espaço.\"",
            true,
            "equip_arma_ancestral",
            "O Catalisador Ancestral",
            "Amplifica o dano crítico em +50% e o dano base da arma em +25% contra o coração de Plerotus.",
            new List<CraftingIngredient>
            {
                new CraftingIngredient { itemId = "prismalita", quantity = 5 },
                new CraftingIngredient { itemId = "lithos", quantity = 3 },
                new CraftingIngredient { itemId = "cristal_explosivo", quantity = 3 }
            }
        );

        // ─── ARCO 1: A FEBRE DO OURO MINERAL (Expedição Plerotus - Dias 01 a 60) ───
        EnsureCanonicalScripture(
            "scripture_lore_01",
            "Diário de Expedição: Pouso no Setor Primário",
            "\"Dia 04. A nave de pesquisa e prospecção 'Aethelgard' pousou com sucesso no vale dos monólitos de Plerotus. As leituras dos sensores acusam concentrações minerais jamais vistas em qualquer setor mapeado: bismuto iridescente, lithos e veios densos de prismalita pura aflorando da rocha como espinhos. Montamos nosso posto de comando e a oficina do Robozinho de Síntese no setor alto da caverna.\"",
            false, null, null, null, null,
            "BACIA DE POUSO SUPERIOR",
            "<b>Missão:</b> Mapear veios de prismalita e instalar brocas industriais.\n<b>Leitura Atmosférica:</b> Pressão estável, porém o ar carrega micropartículas de silício em suspensão.\n<b>Base Estabelecida:</b> Módulo Safe Room ancorado na rocha firme.",
            "Utilize a Safe Room para refinar minérios e forjar melhorias antes de se aprofundar nas galerias."
        );

        EnsureCanonicalScripture(
            "scripture_lore_02",
            "Registro Geológico: O Fenômeno da Prismalita",
            "\"Dia 18. Nossos ensaios laboratoriais comprovaram: a prismalita não é um mineral inerte. Suas facetas emitem pulsações piezoelétricas de frequência subsônica sempre que detectam calor biológico ou proximidade orgânica. Em vez de cautela, a diretoria da mineradora exigiu triplicar as cotas diárias de extração. O mercado estelar pagará fortunas por cada grama deste condutor vivo.\"",
            false, null, null, null, null,
            "PROPRIEDADES DA PRISMALITA",
            "<b>Comportamento Físico:</b> Ressonância magnética ativa na faixa subsônica.\n<b>Afinidade Celular:</b> O mineral atrai e amplifica calor corporal.\n<b>Alerta Operacional:</b> A extração acelerada gera microvibrações que ecoam por toda a crosta.",
            "A prismalita é o reagente base para sintetizar armaduras e armas avançadas na bancada."
        );

        EnsureCanonicalScripture(
            "scripture_lore_03",
            "Relatório Operacional: A Profundidade da Fissura",
            "\"Dia 32. As brocas romperam a abóbada da Câmara 7 e revelaram um ecossistema subterrâneo impossível. Não há luz estelar aqui, mas fungos bioluminescentes e tubos de alimentação mineral brotam das fendas, pulsando em sincronia com o terreno. Durante os períodos de resfriamento geológico, as paredes parecem respirar uma névoa alcalina arroxeada que dissolve borrachas e ligas plásticas industriais.\"",
            false, null, null, null, null,
            "BIOMA DA FISSURA FÚNGICA",
            "<b>Ecossistema Subterrâneo:</b> Micélios bioluminescentes com metabolismo quimiotrófico.\n<b>Gás Ambiente:</b> Vapores alcalinos com pH corrosivo.\n<b>Observação:</b> A flora se alimenta de traços ferrosos deixados pelas nossas ferramentas.",
            "Desvie de poças roxas e névoas cáusticas; elas corroem escudos e armaduras com extrema rapidez."
        );

        EnsureCanonicalScripture(
            "scripture_lore_04",
            "Carta aos Financiadores: Lucros Incalculáveis",
            "\"Dia 41. Despachamos a primeira cápsula de carga com toneladas de prismalita refinada em direção à órbita. A pureza mineral atingiu 99,4%, quebrando qualquer recorde corporativo. Os operadores das perfuratrizes continuam reclamando de enxaquecas agudas e de uma sensação de vibração nos ossos do crânio, mas as promessas de bônus contratuais calaram os pedidos de paralisação preventiva.\"",
            false, null, null, null, null,
            "RELATÓRIO FINANCEIRO CORPORATIVO",
            "<b>Volume Exportado:</b> 120 toneladas de minério puro.\n<b>Incidência Médica:</b> 42% da tripulação de escavação apresenta fadiga neuro-acústica.\n<b>Decisão da Gerência:</b> Manter a perfuração em regime ininterrupto.",
            "Recursos raros acumulam-se em profundidades maiores, mas a agressividade do bioma cresce na mesma proporção."
        );

        EnsureCanonicalScripture(
            "scripture_lore_05",
            "Anotações Médicas: O Primeiro Mal-Estar",
            "\"Dia 53. Casos alarmantes na enfermaria: três técnicos que manuseavam fragmentos de quartzo sem luvas reforçadas apresentaram endurecimento epidérmico. Sob microscópio, as células epiteliais estão sendo substituídas por lâminas hexagonais de silício vítreo. Alertei a coordenação de que a poeira de Plerotus reescreve a matéria orgânica por contato prolongado, mas meu pedido de quarentena estrita foi engavetado para não interromper a produção.\"",
            false, null, null, null, null,
            "PATOLOGIA DE CRISTALIZAÇÃO",
            "<b>Diagnóstico:</b> Síndrome de Metamorfose Silicosa Primária.\n<b>Vetor de Contágio:</b> Inalação de poeira e contato dérmico com prismalita fraturada.\n<b>Evolução:</b> Perda de sensibilidade tátil e rigidez muscular progressiva.",
            "Preserve a integridade dos seus trajes e evite permanecer imóvel em áreas saturadas de poeira mineral."
        );

        EnsureCanonicalScripture(
            "scripture_lore_06",
            "Manuscrito Manchado: Sons nas Paredes",
            "\"Dia 60. Às 03:00 da madrugada local, todas as perfuratrizes pesadas desligaram simultaneamente por sobrecarga térmica nos sensores de terra. Quando o maquinário silenciou, o som que subiu pelas galerias mais profundas não era eco nem choque tectônico. Era um ritmo cardíaco nítido e compassado, com 42 batimentos por minuto. Algo colossal vive, dorme e respira a quilômetros sob os nossos pés.\"",
            false, null, null, null, null,
            "MEDIDAS SISMOGRÁFICAS",
            "<b>Frequência do Pulso:</b> 0,7 Hz rítmico a 8,4 km de profundidade.\n<b>Conclusão Geológica:</b> O interior do planeta não possui núcleo de ferro fundido, mas um bolsão oco com circulação bio-fluídica.",
            "O som dos batimentos guia o caminho até as profundezas onde o coração de Plerotus repousa."
        );

        // ─── ARCO 2: A INFECÇÃO E O SOLO PARASITA (Dias 61 a 140) ───
        EnsureCanonicalScripture(
            "scripture_lore_07",
            "Alerta de Quarentena: A Praga de Esporos Silicosos",
            "\"Dia 74. O ar da galeria norte atingiu saturação crítica com esporos cristalinos. O pó fino penetrou nas juntas hidráulicas dos autômatos de mineração, fundindo as articulações mecânicas em blocos sólidos de pedra. A ordem de quarentena chegou tarde demais: os mineradores do Nível 3 foram trancados atrás das portas de contenção para impedir que a praga subisse até o módulo habitacional da nave. Quem ficou para trás não responde mais pelo rádio.\"",
            false, null, null, null, null,
            "ORDEM DE CONTENÇÃO NÍVEL 4",
            "<b>Setores Isolados:</b> Galerias Norte e Dutos de Ventilação Secundários.\n<b>Perdas Mecânicas:</b> 4 escavadeiras autônomas e 16 trajes pressurizados inutilizados.\n<b>Sobreviventes Isolados:</b> Sem confirmação de sinais vitais.",
            "Autômatos corrompidos perdem a capacidade de distinguir aliados e tornam-se cascas agressivas."
        );

        EnsureCanonicalScripture(
            "scripture_lore_08",
            "Necrópsia Mineral: O Tecido não é Rocha",
            "\"Dia 89. Consegui dissecar os restos de um ser quadrúpede abatido pela segurança junto aos geradores. Por fora, tem a carapaça facetada de um monstro nativo; por dentro, encontrei tubos pulsantes conduzindo hemolinfa ácida e hastes de titânio pertencentes a um dos nossos cães-robôs perdidos há um mês. Plerotus não gera monstros do nada: o planeta assimila nossa tecnologia e carne, revestindo tudo com quartzo e transformando os invasores em seus próprios anticorpos.\"",
            false, null, null, null, null,
            "ANATOMIA DAS ABERRAÇÕES",
            "<b>Composição dos Monstros:</b> 60% silicatos cristalinos nativos, 40% componentes sintetizados da expedição.\n<b>Sistema Circulatório:</b> Seiva ácida em alta pressão que atua como sangue e veneno.",
            "Ao abater monstros, colete seus fragmentos de quartzo e carapaça para infundir suas próprias armas com resistências nativas."
        );

        EnsureCanonicalScripture(
            "scripture_lore_09",
            "Comunicação Quebrada: Falha nos Rádios",
            "\"Dia 102. Perdemos contato definitivo com a frota em órbita. A torre de transmissão principal no cume da colina foi corroída por colônias de hifas ácidas e desabou na ravina. Nossos motores de ascensão foram danificados pela expansão súbita de agulhas de cristal que perfuraram as linhas de combustível. Estamos encalhados na Safe Room da nave sem qualquer possibilidade de decolagem imediata.\"",
            false, null, null, null, null,
            "ESTADO DA NAVE AETHELGARD",
            "<b>Propulsores Principais:</b> Bloqueados por calcificação mineral externa.\n<b>Transmissor Hiperespacial:</b> Destruído.\n<b>Status da Tripulação:</b> Confinados em modo de sobrevivência defensiva.",
            "Conserte os subsistemas da nave (como a porta do laboratório) para reativar utilitários que ampliam suas chances de fuga."
        );

        EnsureCanonicalScripture(
            "scripture_lore_10",
            "Registro de Fuga: As Galerias Estão Fechando",
            "\"Dia 115. Os corredores da mina não obedecem à estabilidade tectônica. Mapeamos uma rota de fuga em direção aos depósitos da superfície, mas ao retornarmos 48 horas depois, a passagem havia cicatrizado como uma ferida fechando, substituída por paredes de pedra viva e veios de bismuto. Plerotus não quer apenas nos expulsar: o planeta se comporta como um estômago fechando seus esfíncteres para iniciar a digestão lenta de tudo o que trouxemos.\"",
            false, null, null, null, null,
            "TOPOGRAFIA MUTÁVEL",
            "<b>Anomalia Espacial:</b> Reconfiguração dinâmica das câmaras e corredores a cada ciclo geológico.\n<b>Velocidade de Regeneração da Rocha:</b> 24 a 72 horas para fechar fissuras artificiais.",
            "As saídas e salas mudam a cada incursão; consulte o mini-mapa com frequência para não ser cercado em becos sem saída."
        );

        EnsureCanonicalScripture(
            "scripture_lore_11",
            "Estudo de Campo: A Simbiose dos Colossos",
            "\"Dia 128. Ocultei meu traje entre as estalagmites e testemunhei o ritual das criaturas: um Golem descomunal esmagou propositalmente seu próprio braço de silício contra o piso para alimentar uma ninhada de vermes cristalinos com seus fragmentos ferrosos. Há uma inteligência simbiótica regendo a fauna da escuridão. Os Sintonizadores de Cristal pairam no ar como maestros bio-elétricos, redistribuindo energia para onde as defesas forem atacadas.\"",
            false, null, null, null, null
            ,"HIERARQUIA DO ENXAME",
            "<b>Comportamento Social:</b> Trofalaxia mineral (alimentação sacrificial compartilhada).\n<b>Elo de Comando:</b> Os Sintonizadores flutuantes coordenam a cadência e a fúria das hordas.",
            "Priorize abater os Sintonizadores de Cristal no início do combate para desmantelar a sinergia dos outros inimigos."
        );

        EnsureCanonicalScripture(
            "scripture_lore_12",
            "Página Queimada: O Sacrifício do Setor B",
            "\"Dia 139. Quando os enxames de vespas e colossos romperam a barreira de contenção do Setor B, não houve alternativa. Ativei as cargas de dinamite que sustentavam o teto do túnel secundário. Sepultei os monstros sob mil toneladas de granito... e com eles, deixei trancados meus últimos dezoito colegas de turno. Seus gritos pelo rádio ainda ecoam no meu capacete. O cheiro de enxofre e bismuto incandescente nunca mais sairá deste traje.\"",
            false, null, null, null, null,
            "REGISTRO DO INCIDENTE SETOR B",
            "<b>Ação Tática:</b> Detonação controlada de contenção.\n<b>Vítimas:</b> Equipe de Mineração Beta (18 membros dados como desaparecidos).\n<b>Resultado:</b> Avanço da horda contido temporariamente por 14 dias.",
            "Em momentos de cerco extremo, utilize esquivas de dash para reposicionar antes de ser encurralado contra o cenário."
        );

        // ─── ARCO 3: O MERCADOR E O PACTO FATAL (Dias 141 ao Fim) ───
        EnsureCanonicalScripture(
            "scripture_lore_13",
            "Avistamento: A Figura na Escuridão",
            "\"Dia 152. Nas ruínas do reator auxiliar, deparei-me com uma figura encapuzada e silenciosa, cercada por brasas fumaçantes e lanternas de mercúrio. As criaturas de silício passam a centímetros dele e não o atacam, como se ele pertencesse ao solo. Ele abriu as mãos ossudas e colocou sobre uma laje caixas de munição limpa e relíquias de trajes reforçados. Ele sabia meu nome de registro antes mesmo que eu apontasse a lanterna.\"",
            false, null, null, null, null,
            "AVALIAÇÃO DA ENTIDADE NÔMADE",
            "<b>Codinome:</b> O Mercador de Plerotus.\n<b>Comportamento:</b> Neutro e desprovido de agressividade física direta.\n<b>Imunidade Biológica:</b> O ecossistema cristalino não o reconhece como intruso.",
            "O Mercador surge em salas de descanso isoladas; aproveite para examinar suas ofertas quando sua saúde estiver alta."
        );

        EnsureCanonicalScripture(
            "scripture_lore_14",
            "Contrato Impossível: Ouro em Troca de Sangue",
            "\"Dia 166. Tentei negociar com o Mercador usando lingotes de ouro e lascas puríssimas de prismalita, mas ele apenas empurrou as pedras para longe com desdém. Ele sussurrou que minerais não têm valor para quem já viveu eras neste túmulo. O que ele exige é vitae pura: uma fatia irreversível do sangue orgânico do explorador, selada em seus frascos escuros em troca de armas avassaladoras. Um dízimo de vida máxima para continuar de pé.\"",
            false, null, null, null, null,
            "TERMOS DE TRANSAÇÃO DO MERCADOR",
            "<b>Moeda Rejeitada:</b> Minérios, orbs convencionais e metais preciosos.\n<b>Moeda Exigida:</b> Sangue vital orgânico (redução permanente de Max HP).\n<b>Mercadoria Oferecida:</b> Artefatos de alto impacto bélico.",
            "Aceitar um Pacto de Sangue concede poder devastador, mas tranca uma porção da sua barra de vida máxima permanentemente."
        );

        EnsureCanonicalScripture(
            "scripture_lore_15",
            "Delírio de Febre: As Moedas Sugam o Calor",
            "\"Dia 178. Dois dos nossos últimos atiradores aceitaram os pactos do Mercador em troca de núcleos energéticos para suas armas. No combate seguinte, seus projéteis desintegravam qualquer carapaça com facilidade aterradora. Mas a euforia durou pouco: a cada disparo, marcas pretas e veios de corrupção se espalhavam pelo peito deles. Na terceira manhã, suas veias haviam colapsado e os corpos amanheceram frios como mármore polido.\"",
            false, null, null, null, null,
            "MANIFESTAÇÃO DA CORRUPÇÃO",
            "<b>Efeito Visual:</b> Garras de sombra e fluido corrompido cobrindo a silhueta do usuário.\n<b>Degeneração Biológica:</b> Aumento exponencial da vulnerabilidade a golpes críticos dos monstros.",
            "Calcule com cuidado: acumular múltiplos pactos sem melhorias defensivas pode tornar qualquer erro de posicionamento fatal."
        );

        EnsureCanonicalScripture(
            "scripture_lore_16",
            "Última Refeição: Alquimia da Fome",
            "\"Dia 190. Nossas rações sintéticas terminaram há duas semanas. Aprendi a triturar musgo bioluminescente e ferver seiva alcalina de melocactus nos cadinhos da forja para criar um caldo espesso. O gosto lembra solvente químico e queima a garganta como brasa, mas contém aminoácidos suficientes para impedir que a cristalização avance sobre meus pulmões. Cada refeição é uma pequena vitória contra a fome da terra.\"",
            false, null, null, null, null,
            "FARMACOLOGIA DE EMERGÊNCIA",
            "<b>Ingredientes:</b> Musgo bioluminescente refinado e seiva de cactos alcalinos.\n<b>Propriedade Terapêutica:</b> Desacelera a petrificação do trato respiratório e restaura estamina.",
            "Colete musgo bioluminescente nas salas verdes para forjar filtros que atenuam penalidades de terreno."
        );

        EnsureCanonicalScripture(
            "scripture_lore_17",
            "Testamento do Geólogo: Não Confie no Mercador",
            "\"Dia 204. Se você é o tripulante sobrevivente que recuperou meu caderno de anotações, guarde este conselho: não se iluda com as promessas fáceis do Mercador. Ele não é um aliado nem um náufrago buscando resgate. Ele foi o primeiro pesquisador a desembarcar aqui séculos atrás; para não perecer, entregou sua humanidade ao planeta e agora atua como agiota biológico, coletando o sangue dos incautos para alimentar a gestação de Plerotus.\"",
            false, null, null, null, null,
            "ARQUIVO HISTÓRICO DA EXPEDIÇÃO ZERO",
            "<b>Identidade Original:</b> Comandante da primeira expedição de prospecção orbital.\n<b>Papel Atual:</b> Agente simbiótico e intermediário de sacrifício vital da entidade planetária.",
            "Não dependa exclusivamente dos pactos para vencer; fortaleça suas defesas naturais na mesa do Robozinho."
        );

        EnsureCanonicalScripture(
            "scripture_lore_18",
            "Página Final: O Planeta é um Ser Vivo",
            "\"Dia 211. Finalmente juntei todas as peças antes que meus olhos se fechem. Plerotus não é uma rocha inanimada à deriva no vácuo: é um embrião cósmico ancestral, um Geobionte adormecido que nossa ganância mineradora despertou antes da hora. A gravidade que nos prende e os monstros que nos caçam são seu sistema imunológico tentando nos dissolver. Para ligar os propulsores da nossa nave e quebrar o campo de atração... você terá que descer até a câmara mais profunda e perfurar o coração vivo do planeta.\"",
            false, null, null, null, null,
            "DIAGNÓSTICO FINAL DA MISSÃO",
            "<b>A Verdadeira Natureza de Plerotus:</b> Macro-organismo biocristalino em fase de gestação.\n<b>Objetivo de Fuga:</b> Destruir o Geobionte na câmara central para dissipar o bloqueio gravitacional e liberar a decolagem.",
            "Desça pelas galerias até o covil do Geobionte. Somente abatendo o núcleo vivo a nave conseguirá escapar de Plerotus."
        );

        scriptureLookup.Clear();
        foreach (var sc in allScriptures)
        {
            if (sc != null && !string.IsNullOrEmpty(sc.scriptureId))
            {
                scriptureLookup[sc.scriptureId] = sc;
            }
        }

        Debug.Log($"[LORE] LoreManager inicializado com {allScriptures.Count} escrituras canônicas.");
    }

    private void EnsureCanonicalScripture(
        string id, 
        string title, 
        string lore, 
        bool hasBlueprint, 
        string bpEquipId, 
        string bpTitle, 
        string bpDesc, 
        List<CraftingIngredient> ingredients,
        string fieldHeader = null,
        string fieldNotes = null,
        string survivalTip = null)
    {
        LoreScriptureData sc = allScriptures.Find(s => s != null && s.scriptureId == id);
        if (sc == null)
        {
            sc = ScriptableObject.CreateInstance<LoreScriptureData>();
            sc.scriptureId = id;
            allScriptures.Add(sc);
        }

        sc.title = title;
        sc.excerptLore = lore;
        sc.hasBlueprint = hasBlueprint;
        sc.blueprintEquipmentId = bpEquipId;
        sc.blueprintTitle = bpTitle;
        sc.blueprintDescription = bpDesc;
        sc.ingredients = ingredients != null ? ingredients : new List<CraftingIngredient>();
        sc.fieldAnalysisHeader = !string.IsNullOrEmpty(fieldHeader) ? fieldHeader : "ANÁLISE DE CAMPO";
        sc.fieldAnalysisNotes = fieldNotes ?? "";
        sc.tacticalSurvivalTip = survivalTip ?? "";
    }

    public LoreScriptureData GetScripture(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return scriptureLookup.TryGetValue(id, out var data) ? data : null;
    }

    public bool IsScriptureDiscovered(string id)
    {
        if (SaveManager.instance == null || SaveManager.instance.CachedData == null) return false;
        return SaveManager.instance.CachedData.discoveredScriptureIds != null &&
               SaveManager.instance.CachedData.discoveredScriptureIds.Contains(id);
    }

    public bool IsBlueprintUnlocked(string blueprintEquipmentId)
    {
        if (SaveManager.instance == null || SaveManager.instance.CachedData == null) return false;
        return SaveManager.instance.CachedData.unlockedRobotBlueprints != null &&
               SaveManager.instance.CachedData.unlockedRobotBlueprints.Contains(blueprintEquipmentId);
    }

    /// <summary>
    /// Retorna a próxima escritura ainda não descoberta pelo jogador.
    /// Dá prioridade para blueprints primeiro, e depois crônicas de lore.
    /// Se todas já foram descobertas, retorna uma aleatória para releitura.
    /// </summary>
    public LoreScriptureData GetNextUndiscoveredScripture()
    {
        if (allScriptures == null || allScriptures.Count == 0) return null;

        // 1. Tenta encontrar uma com blueprint ainda não descoberta
        foreach (var sc in allScriptures)
        {
            if (sc != null && sc.hasBlueprint && !IsScriptureDiscovered(sc.scriptureId))
            {
                return sc;
            }
        }

        // 2. Tenta encontrar qualquer crônica de lore ainda não descoberta
        foreach (var sc in allScriptures)
        {
            if (sc != null && !IsScriptureDiscovered(sc.scriptureId))
            {
                return sc;
            }
        }

        // 3. Fallback: todas já descobertas, retorna uma aleatória
        return allScriptures[UnityEngine.Random.Range(0, allScriptures.Count)];
    }

    /// <summary>
    /// Coleta uma escritura encontrada no mapa pelo jogador.
    /// Registra no save, desbloqueia o blueprint no Robozinho e dispara o pop-up de Eptinho.
    /// </summary>
    public void CollectScripture(LoreScriptureData data)
    {
        if (data == null) return;

        bool isFirstTime = false;

        if (SaveManager.instance != null && SaveManager.instance.CachedData != null)
        {
            if (SaveManager.instance.CachedData.discoveredScriptureIds == null)
                SaveManager.instance.CachedData.discoveredScriptureIds = new List<string>();

            if (!SaveManager.instance.CachedData.discoveredScriptureIds.Contains(data.scriptureId))
            {
                SaveManager.instance.CachedData.discoveredScriptureIds.Add(data.scriptureId);
                isFirstTime = true;
            }

            if (data.hasBlueprint && !string.IsNullOrEmpty(data.blueprintEquipmentId))
            {
                if (SaveManager.instance.CachedData.unlockedRobotBlueprints == null)
                    SaveManager.instance.CachedData.unlockedRobotBlueprints = new List<string>();

                if (!SaveManager.instance.CachedData.unlockedRobotBlueprints.Contains(data.blueprintEquipmentId))
                {
                    SaveManager.instance.CachedData.unlockedRobotBlueprints.Add(data.blueprintEquipmentId);
                    Debug.Log($"🤖 [LORE] Blueprint desbloqueado para o Robozinho: {data.blueprintTitle} ({data.blueprintEquipmentId})");
                }
            }

            SaveManager.instance.SavePersistentData();
        }

        // Toca notificação do Eptinho no HUD
        ShowEptinhoScriptureNotification(data, isFirstTime);

        // Dispara evento
        OnScriptureDiscovered?.Invoke(data);
    }

    private void ShowEptinhoScriptureNotification(LoreScriptureData data, bool isFirstTime)
    {
        string header = isFirstTime 
            ? "📜 <color=#FFE500>FOLHA DO CADERNO ENCONTRADA!</color>" 
            : "📜 <color=#4DEEEA>PÁGINA JÁ CATALOGADA</color>";

        string speech = isFirstTime
            ? "<i>\"Au! Mais uma página do antigo pesquisador! Salvei a anotação para você ler com calma no caderno da base.\"</i>"
            : "<i>\"Já temos uma cópia dessas anotações salva no nosso caderno de campo.\"</i>";

        string blueprintNotice = (data.hasBlueprint && !string.IsNullOrEmpty(data.blueprintTitle))
            ? $"\n<color=#4DEEEA><b>[NOVO BLUEPRINT NO ROBOZINHO: {data.blueprintTitle}]</b></color>"
            : "";

        string message = $"{header}\n<b>{data.title}</b>\n<size=85%>{speech}</size>{blueprintNotice}";

        if (EptinhoPopupController.instancia != null)
        {
            EptinhoPopupController.instancia.MostrarPopupAviso(message);
        }
        else
        {
            Debug.Log($"🐕 [EPTINHO POPUP] {message}");
        }
    }

    /// <summary>
    /// Desbloqueia o Caderno do Pesquisador quando coletado pelo jogador na dungeon.
    /// Salva o estado no disco e ativa a exibição flutuante no laboratório da Base.
    /// </summary>
    public void DiscoverResearcherBook()
    {
        if (SaveManager.instance != null && SaveManager.instance.CachedData != null)
        {
            SaveManager.instance.CachedData.hasFoundResearcherBook = true;
            SaveManager.instance.SavePersistentData();
        }

        string msg = "📖 <color=#FFE500>CADERNO DE CAMPO ENCONTRADO!</color>\n" +
                     "<size=85%><i>\"Nossa, você encontrou o diário de um antigo pesquisador... Está bem danificado e com páginas faltando. Deixe-me analisá-lo e levá-lo de volta para a bancada do laboratório!\"</i></size>";

        if (EptinhoPopupController.instancia != null)
        {
            EptinhoPopupController.instancia.MostrarPopupAviso(msg);
        }
        else
        {
            Debug.Log($"🐕 [EPTINHO POPUP] {msg}");
        }

        OnResearcherBookDiscovered?.Invoke();
    }

    /// <summary>
    /// Desbloqueia instantaneamente o Caderno do Pesquisador e todas as 18 escrituras / blueprints para testes.
    /// Chamado por comando de cheat (/unlocklore, /escrituras, /caderno), atalho F8, ou botão na DevCheatConsole.
    /// </summary>
    public void UnlockAllScripturesAndBook()
    {
        if (allScriptures == null || allScriptures.Count == 0)
        {
            InitializeDefaultScriptures();
        }

        if (SaveManager.instance != null && SaveManager.instance.CachedData != null)
        {
            SaveManager.instance.CachedData.hasFoundResearcherBook = true;

            if (SaveManager.instance.CachedData.discoveredScriptureIds == null)
                SaveManager.instance.CachedData.discoveredScriptureIds = new List<string>();

            if (SaveManager.instance.CachedData.unlockedRobotBlueprints == null)
                SaveManager.instance.CachedData.unlockedRobotBlueprints = new List<string>();

            foreach (var sc in allScriptures)
            {
                if (sc != null && !string.IsNullOrEmpty(sc.scriptureId))
                {
                    if (!SaveManager.instance.CachedData.discoveredScriptureIds.Contains(sc.scriptureId))
                    {
                        SaveManager.instance.CachedData.discoveredScriptureIds.Add(sc.scriptureId);
                    }

                    if (sc.hasBlueprint && !string.IsNullOrEmpty(sc.blueprintEquipmentId))
                    {
                        if (!SaveManager.instance.CachedData.unlockedRobotBlueprints.Contains(sc.blueprintEquipmentId))
                        {
                            SaveManager.instance.CachedData.unlockedRobotBlueprints.Add(sc.blueprintEquipmentId);
                            Debug.Log($"🤖 [LORE] Blueprint desbloqueado para o Robozinho: {sc.blueprintTitle} ({sc.blueprintEquipmentId})");
                        }
                    }
                }
            }

            SaveManager.instance.SavePersistentData();
        }

        OnResearcherBookDiscovered?.Invoke();

        if (FloatingBookDisplay.Instance != null)
        {
            FloatingBookDisplay.Instance.CheckBookVisibility();
        }

        string msg = "<b>[CODICE DE PLEROTUS]</b>\n" +
                     $"<color=#FFE500>TODAS AS {allScriptures.Count} ESCRITURAS E O CADERNO FORAM DESBLOQUEADOS!</color>\n" +
                     $"<size=85%>18 capitulos catalogados ({DiscoveredScripturesCount}/{TotalScripturesCount}) e blueprints integrados ao Robozinho!</size>";

        if (EptinhoPopupController.instancia != null)
        {
            EptinhoPopupController.instancia.MostrarPopupAviso(msg);
        }
        else
        {
            Debug.Log($"[EPTINHO POPUP] {msg}");
        }
    }

    // ─── FERRAMENTAS DE TESTE (INSPECTOR) ───────────────────────────────────

    [ContextMenu("📖 Testar Desbloqueio: Caderno do Pesquisador")]
    public void TestarDesbloqueioCaderno()
    {
        DiscoverResearcherBook();
    }

    [ContextMenu("📜 Testar Desbloqueio: Todas as 18 Escrituras e Caderno")]
    public void TestarDesbloqueioTodasEscriturasECaderno()
    {
        UnlockAllScripturesAndBook();
    }

    [ContextMenu("🧪 Testar Coleta: Decomposição Ácida (Blueprint 1)")]
    public void TestarColetaAcido()
    {
        LoreScriptureData sc = GetScripture("scripture_acid_filter");
        if (sc != null) CollectScripture(sc);
    }

    [ContextMenu("🧪 Testar Coleta: Quebra-Casca (Blueprint 2)")]
    public void TestarColetaQuebraCasca()
    {
        LoreScriptureData sc = GetScripture("scripture_shell_breaker");
        if (sc != null) CollectScripture(sc);
    }

    [ContextMenu("🧪 Testar Coleta: Todas as 6 Escrituras Canônicas")]
    public void TestarColetaTodas()
    {
        foreach (var sc in allScriptures)
        {
            if (sc != null) CollectScripture(sc);
        }
    }

    [ContextMenu("🗑️ Limpar Escrituras e Blueprints Salvos")]
    public void LimparEscriturasSalvas()
    {
        if (SaveManager.instance != null && SaveManager.instance.CachedData != null)
        {
            SaveManager.instance.CachedData.discoveredScriptureIds?.Clear();
            SaveManager.instance.CachedData.unlockedRobotBlueprints?.Clear();
            SaveManager.instance.SavePersistentData();
            Debug.Log("[LORE] 🧹 Save resetado: todas as escrituras e blueprints foram bloqueados.");
        }
    }
}
