using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gerencia a descoberta de escrituras ancestrais e o desbloqueio de blueprints no Robozinho de Síntese.
/// Singleton — persiste entre cenas via DontDestroyOnLoad.
/// </summary>
public class LoreManager : MonoBehaviour
{
    public static LoreManager Instance { get; private set; }

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
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
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
            "\"O solo parou de corroer nosso maquinário quando combinamos sais alcalinos de bismuto. O parasita de Plerotus cospe ácido, mas toda secreção tem um ponto de neutralização química.\"",
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
            "\"Nossas brocas batiam nos colossos minerais e quebravam. Eles não são de rocha maciça; são conchas com nervuras ocas de silício. Se vibrar na frequência oposta, a couraça estilhaça.\"",
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
            "\"Os filamentos do fungo tentavam nos prender ao piso. Aplicamos graxa de quartzo nas juntas de locomoção e o micélio escorregava sem travar nossa marcha.\"",
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
            "\"A rede de nós aéreos amplifica as hordas. Criamos um ressonador que quebra o feixe bio-energético e cega a sincronia agressiva das aberrações.\"",
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
            "\"A fórmula da cura estava completa. Mas precisava de um rio que não corria em nós. Nossas veias de sílica secaram enquanto irmãos abriam irmãos procurando o calor vermelho. O mercador de olhos amarelos ria enquanto nosso povo morria.\"",
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
            "\"Para quem vier depois das cinzas: esta arma não destrói a terra, ela dissolve a casca do parasita para que o núcleo respire. Entregue seu ferro e liberte este mundo da digestão eterna.\"",
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
            "\"Dia 04. Pouso estabilizado no vale dos monólitos. As sondas atmosféricas acusam riqueza mineral sem precedentes. Cristais de bismuto e prismalita brotam do chão como espinhos. Construímos nossa base no setor alto.\"",
            false, null, null, null, null
        );

        EnsureCanonicalScripture(
            "scripture_lore_02",
            "Registro Geológico: O Fenômeno da Prismalita",
            "\"Dia 18. A prismalita não é rocha inerte. Ela vibra em frequências subsônicas quando tocada por calor biológico. O chefe da expedição ordenou escavações triplas. O mercado estelar pagará fortunas por cada grama.\"",
            false, null, null, null, null
        );

        EnsureCanonicalScripture(
            "scripture_lore_03",
            "Relatório Operacional: A Profundidade da Fissura",
            "\"Dia 32. Rompemos a câmara 7. O maquinário pesado encontrou cavernas com formações biológicas que desafiam a botânica conhecida. As paredes parecem respirar uma névoa arroxeada durante a noite geológica.\"",
            false, null, null, null, null
        );

        EnsureCanonicalScripture(
            "scripture_lore_04",
            "Carta aos Financiadores: Lucros Incalculáveis",
            "\"Dia 41. Enviamos o primeiro lote de prismalita refinada. A taxa de pureza alcançou 99,4%. Nossos brocadores reclamam de enxaquecas e sussurros na rocha, mas os bônus contratuais manterão as equipes trabalhando.\"",
            false, null, null, null, null
        );

        EnsureCanonicalScripture(
            "scripture_lore_05",
            "Anotações Médicas: O Primeiro Mal-Estar",
            "\"Dia 53. Três engenheiros apresentaram cristalização nos tecidos periféricos das mãos. O contato direto com o pó mineral parece alterar a polaridade celular. Recomendei trajes selados nível 4, mas a gerência recusou o atraso.\"",
            false, null, null, null, null
        );

        EnsureCanonicalScripture(
            "scripture_lore_06",
            "Manuscrito Manchado: Sons nas Paredes",
            "\"Dia 60. As brocas pararam sozinhas às 03:00. O som que vem das galerias mais baixas não é eco. É ritmo. Como se um coração colossal estivesse bombeando quilômetros abaixo dos nossos pés.\"",
            false, null, null, null, null
        );

        // ─── ARCO 2: A INFECÇÃO E O SOLO PARASITA (Dias 61 a 140) ───
        EnsureCanonicalScripture(
            "scripture_lore_07",
            "Alerta de Quarentena: A Praga de Esporos Silicosos",
            "\"Dia 74. O ar da galeria norte saturou de esporos que corrompem até as juntas de titânio dos robôs. As portas de segurança foram travadas por dentro. Quem ficou para trás não responde mais pelo rádio.\"",
            false, null, null, null, null
        );

        EnsureCanonicalScripture(
            "scripture_lore_08",
            "Necrópsia Mineral: O Tecido não é Rocha",
            "\"Dia 89. Analisei um dos monstros que atacou a perfuratriz. Sob a carapaça de quartzo e bismuto, há órgãos bombeadores de hemolinfa ácida. Os monstros deste planeta não nasceram na rocha: o planeta os digere e remolda.\"",
            false, null, null, null, null
        );

        EnsureCanonicalScripture(
            "scripture_lore_09",
            "Comunicação Quebrada: Falha nos Rádios",
            "\"Dia 102. A torre de transmissão desabou. Uma colônia de hifas ácidas derreteu os cabos de alta tensão. Estamos ilhados. O módulo de fuga na superfície foi engolido por uma cratera viva.\"",
            false, null, null, null, null
        );

        EnsureCanonicalScripture(
            "scripture_lore_10",
            "Registro de Fuga: As Galerias Estão Fechando",
            "\"Dia 115. Os corredores da mina mudam de lugar. O mapa que desenhamos há três dias agora dá de cara com paredes de pedra viva. Plerotus não quer nos expulsar; quer nos manter dentro de suas câmaras digestivas.\"",
            false, null, null, null, null
        );

        EnsureCanonicalScripture(
            "scripture_lore_11",
            "Estudo de Campo: A Simbiose dos Colossos",
            "\"Dia 128. Vimos um Golem gigante alimentar uma ninhada de vermes cristalinos com seu próprio corpo estilhaçado. Há uma inteligência terrível operando na escuridão. Eles se comunicam através da rede de nós aéreos.\"",
            false, null, null, null, null
        );

        EnsureCanonicalScripture(
            "scripture_lore_12",
            "Página Queimada: O Sacrifício do Setor B",
            "\"Dia 139. Explodimos as cargas de demolição do túnel principal para conter o avanço das criaturas. Oitenta companheiros ficaram do outro lado. O cheiro de enxofre e bismuto queimado não sai mais do meu traje.\"",
            false, null, null, null, null
        );

        // ─── ARCO 3: O MERCADOR E O PACTO FATAL (Dias 141 ao Fim) ───
        EnsureCanonicalScripture(
            "scripture_lore_13",
            "Avistamento: A Figura na Escuridão",
            "\"Dia 152. Encontrei uma criatura encapuzada parada no meio das ruínas. Ela não nos ataca. Carrega lanternas antigas e cartas marcadas com sangue seco. Ela me chamou pelo nome antes que eu dissesse uma palavra.\"",
            false, null, null, null, null
        );

        EnsureCanonicalScripture(
            "scripture_lore_14",
            "Contrato Impossível: Ouro em Troca de Sangue",
            "\"Dia 166. O Mercador nos ofereceu armas que quebram qualquer couraça e poções que fecham feridas na hora. Mas ele não aceita moedas nem prismalita. Ele exige uma gota do nosso vigor vital. Uma fatia da nossa vida.\"",
            false, null, null, null, null
        );

        EnsureCanonicalScripture(
            "scripture_lore_15",
            "Delírio de Febre: As Moedas Sugam o Calor",
            "\"Dia 178. Dois sobreviventes aceitaram os pactos. Seus corpos ficaram leves e suas armas disparavam com fúria destrutiva. Mas nos dias seguintes, suas veias secaram. O frio tomou conta e eles não acordaram mais.\"",
            false, null, null, null, null
        );

        EnsureCanonicalScripture(
            "scripture_lore_16",
            "Última Refeição: Alquimia da Fome",
            "\"Dia 190. A comida acabou há duas semanas. Estamos destilando musgo luminescente e seiva de melocactus para sobreviver. Cada gole queima como brasa, mas mantém nossos corações batendo por mais um dia.\"",
            false, null, null, null, null
        );

        EnsureCanonicalScripture(
            "scripture_lore_17",
            "Testamento do Geólogo: Não Confie no Mercador",
            "\"Dia 204. Se você encontrou estas anotações, não caia nas promessas do ser encapuzado. Ele foi o primeiro habitante a cair neste mundo, e a eternidade o transformou no cobrador das almas de Plerotus.\"",
            false, null, null, null, null
        );

        EnsureCanonicalScripture(
            "scripture_lore_18",
            "Página Final: O Planeta é um Ser Vivo",
            "\"Dia 211. Finalmente compreendi. Plerotus não é um pedregulho no espaço. É um embrião ancestral em sono profundo. Nossas brocas o acordaram. Se você quiser escapar deste abismo... precisará perfurar o próprio coração da terra.\"",
            false, null, null, null, null
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

    private void EnsureCanonicalScripture(string id, string title, string lore, bool hasBlueprint, string bpEquipId, string bpTitle, string bpDesc, List<CraftingIngredient> ingredients)
    {
        if (allScriptures.Exists(s => s != null && s.scriptureId == id)) return;

        LoreScriptureData sc = ScriptableObject.CreateInstance<LoreScriptureData>();
        sc.scriptureId = id;
        sc.title = title;
        sc.excerptLore = lore;
        sc.hasBlueprint = hasBlueprint;
        sc.blueprintEquipmentId = bpEquipId;
        sc.blueprintTitle = bpTitle;
        sc.blueprintDescription = bpDesc;
        sc.ingredients = ingredients != null ? ingredients : new List<CraftingIngredient>();

        allScriptures.Add(sc);
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
        string header = isFirstTime ? "📜 <color=#FFE500>NOVA ESCRITURA CATALOGADA!</color>" : "📜 <color=#4DEEEA>REGISTRO ANCESTRAL REVISITADO</color>";
        string blueprintNotice = data.hasBlueprint ? $"\n<color=#4DEEEA><b>[BLUEPRINT DO ROBOZINHO DESBLOQUEADO]</b></color>\n<size=85%>{data.blueprintTitle}: {data.blueprintDescription}</size>" : "";
        string message = $"{header}\n<b>{data.title}</b>\n<size=80%><i>{data.excerptLore}</i></size>{blueprintNotice}";

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
        DiscoverResearcherBook();

        foreach (var sc in allScriptures)
        {
            if (sc != null)
            {
                CollectScripture(sc);
            }
        }

        FloatingBookDisplay.Instance?.CheckBookVisibility();

        string msg = "📖 <color=#FFE500>TODAS AS ESCRITURAS E O CADERNO FORAM DESBLOQUEADOS!</color>\n" +
                     $"<size=85%>18 escrituras catalogadas ({DiscoveredScripturesCount}/{TotalScripturesCount}) e Códice liberado no Laboratório da Base!</size>";

        if (EptinhoPopupController.instancia != null)
        {
            EptinhoPopupController.instancia.MostrarPopupAviso(msg);
        }
        else
        {
            Debug.Log($"🐕 [EPTINHO POPUP] {msg}");
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
