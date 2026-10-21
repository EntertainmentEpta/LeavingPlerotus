using System;
using UnityEngine;

/// <summary>
/// Gerencia os 20 encontros progressivos com o Mercador das Sombras diretamente no HUD.
/// Ao acionar a interação, dois popups surgem simultaneamente no canto direito:
///   1. Topo: EptinhoPopupController (Eptinho reagindo e comentando a tradução).
///   2. Abaixo: MerchantPopupController (Mercador falando com dialeto mineral em decodificação).
/// </summary>
public static class MerchantEncounterDialogueSystem
{
    public struct EncounterLine
    {
        public string percentageBadge;
        public string eptinhoComment;
        public string merchantSpeech;

        public EncounterLine(string badge, string eptinho, string merchant)
        {
            percentageBadge = badge;
            eptinhoComment = eptinho;
            merchantSpeech = merchant;
        }
    }

    private static readonly EncounterLine[] ENIGMAS_MERCADOR = new EncounterLine[]
    {
        // Encontro 1 (5%)
        new EncounterLine(
            "5% DECODIFICADO",
            "Au! Que chiado é esse? Ele não usa cordas vocais, o som vem do atrito de anéis de silício nos dedos! Só consigo captar fragmentos...",
            "*estalos ásperos de silício* Mais um bípede de carne na escuridão da pedra. O solo de Plerotus acolhe os de passos pesados..."
        ),

        // Encontro 2 (10%)
        new EncounterLine(
            "10% DECODIFICADO",
            "Decodificação em 10%... Ele parece ter visto nossa nave colidir na cratera!",
            "*fricção mineral* Mais uma carcaça fumegante. Os que vieram antes também olhavam para o céu esperando salvação..."
        ),

        // Encontro 3 (15%)
        new EncounterLine(
            "15% DECODIFICADO",
            "Ele usa termos de mineração arcaicos! A língua dele parece esculpida em pedras velhas.",
            "Você escava como um verme cego. Acha que as gemas pertencem a você? Elas são dentes de um animal muito maior..."
        ),

        // Encontro 4 (20%)
        new EncounterLine(
            "20% DECODIFICADO",
            "O tom dele é de deboche... Ele está falando da civilização que habitava Plerotus!",
            "Eles também vestiam trajes brancos. Construíram torres altas de silício... e viraram adubo no mesmo lugar onde você pisa."
        ),

        // Encontro 5 (25%)
        new EncounterLine(
            "25% DECODIFICADO",
            "A voz dele é como pedras sendo trituradas... Ele tem uma fixação perturbadora pelo seu sangue!",
            "O sangue quente é um desperdício em corpos frágeis. Na pedra, ele é uma chave... mas você prefere queimá-lo por moedas."
        ),

        // Encontro 6 (30%)
        new EncounterLine(
            "30% DECODIFICADO",
            "Ele chamou os antigos mineradores das escrituras de 'Os Lapidadores'!",
            "Os Lapidadores achavam que poderiam dissecar o micélio do planeta. Tolos. Não se desseca a boca que está engolindo você."
        ),

        // Encontro 7 (35%)
        new EncounterLine(
            "35% DECODIFICADO",
            "Cada palavra dele parece carregada de escárnio. Ele não é um aliado...",
            "Cada gota vermelha que você derrama alimenta a sede que nunca cessa. Agradeço pela sua generosidade involuntária, engenheiro."
        ),

        // Encontro 8 (40%)
        new EncounterLine(
            "40% DECODIFICADO",
            "Ele reconheceu a forja do nosso Robozinho de Síntese na Base!",
            "Seu pequeno autômato tenta reconstruir as ferramentas deles. Eles também tentaram forjar uma lâmina definitiva... Acabaram cortando as próprias gargantas."
        ),

        // Encontro 9 (45%)
        new EncounterLine(
            "45% DECODIFICADO",
            "Quase metade da estrutura sintática compreendida! A matriz fonética está se alinhando.",
            "Você não se cansa de renascer na poeira? O planeta tem indigestão com você, mas até o estômago mais fraco aprende a digerir com o tempo."
        ),

        // Encontro 10 (50%)
        new EncounterLine(
            "50% DECODIFICADO",
            "50% da tradução concluída! Ele sabe sobre a fórmula que estamos buscando nas escrituras!",
            "Eles procuravam um catalisador. Um rio que não corria em suas veias de rocha. Se soubessem que o rio viria do céu em um foguete quebrado..."
        ),

        // Encontro 11 (55%)
        new EncounterLine(
            "55% DECODIFICADO",
            "As frases de silício estão ficando assustadoramente nítidas no tradutor...",
            "Você se sente esperto acoplando espinhos à sua ferramenta? Cada fusão aproxima sua carne da pedra definitiva."
        ),

        // Encontro 12 (60%)
        new EncounterLine(
            "60% DECODIFICADO",
            "Ele está rindo com o ranger dos dedos... Ele presenciou a loucura dos Lapidadores!",
            "A loucura deles começou devagar. Primeiro uma broca, depois um irmão... no fim, as fendas estavam cheias de silêncio e cinzas."
        ),

        // Encontro 13 (70%)
        new EncounterLine(
            "70% DECODIFICADO",
            "70%! Ele sabe da existência das salas de anomalia onde o fungo necrosou!",
            "Você já sentiu o tremor nas salas de anomalia? As partes onde o planeta adoece... são meus lugares favoritos. Cicatrizes perfeitas."
        ),

        // Encontro 14 (80%)
        new EncounterLine(
            "80% DECODIFICADO",
            "80% de precisão! Ele nos vê como parasitas menores limpando o caminho!",
            "Você acha que está limpando as salas? Você é apenas um anticorpo cego abrindo caminho para a próxima fase digestiva de Plerotus."
        ),

        // Encontro 15 (85%)
        new EncounterLine(
            "85% DECODIFICADO",
            "A tradução está em 85%! O tom dele mudou... não há mais nenhum disfarce de neutralidade.",
            "Você quase montou o quebra-cabeça deles, não é? Ferramentas precisam de mestres, operário. E a carne sempre apodrece antes da pedra."
        ),

        // Encontro 16 (90%)
        new EncounterLine(
            "90% DECODIFICADO",
            "90%! Ele acabou de admitir que estava presente no dia em que o parasita foi semeado!",
            "Eles dormiam em paz até a semente ser plantada. Bastou um sussurro no ouvido do arquiteto-mor para que cavassem o próprio túmulo."
        ),

        // Encontro 17 (95%)
        new EncounterLine(
            "95% DECODIFICADO",
            "Engenheiro... Ele não estava apenas assistindo, ele sussurrou para os líderes da civilização!",
            "Eles imploraram para que eu bebesse suas lágrimas quando o ar virou vidro fosco. Eu apenas observei enquanto a poeira assentava."
        ),

        // Encontro 18 (98%)
        new EncounterLine(
            "98% DECODIFICADO",
            "Falta apenas calibrar uma última frequência... A tradução está quase perfeita!",
            "Você tem o catalisador nas veias, engenheiro. Mas você é fraco demais para empunhá-lo contra o coração de Plerotus."
        ),

        // Encontro 19 (99%)
        new EncounterLine(
            "99% DECODIFICADO",
            "TRADUÇÃO COMPLETA IMINENTE! Na próxima visita, a linguagem dele será 100% nítida!",
            "Chegamos ao fim da charada, operário de macacão sujo. Da próxima vez, não haverá mercadorias... apenas contas a acertar."
        ),

        // Encontro 20 (100% - REVELAÇÃO DO BOSS SECRETO)
        new EncounterLine(
            "100% — IDENTIDADE REVELADA",
            "🚨 ALERTA CRÍTICO! TRADUÇÃO 100%! ELE NÃO É UM COMERCIANTE, O DISFARCE CAIU!",
            "Chega de fingir comércio, astronauta imbecil! Fui EU quem trouxe o esporo de Plerotus para este mundo! Fui EU quem manipulou os Lapidadores a se destruírem procurando um sangue que não tinham! Este planeta é o meu jardim carnívoro, e você NUNCA sairá vivo com essa arma ancestral!"
        )
    };

    private static readonly EncounterLine DIALOGO_POS_REVELACAO = new EncounterLine(
        "100% — ENTIDADE HOSTIL",
        "Ele está observando cada passo... O confronto final está próximo!",
        "Ainda respirando, carcaça de carne? Plerotus já está sentindo o cheiro do seu sangue. Logo sua lâmina será apenas mais um fóssil no solo deste mundo!"
    );

    public static void CheckAndTriggerEncounterDialogue(Action onComplete = null)
    {
        if (SaveManager.instance == null || SaveManager.instance.CachedData == null)
        {
            onComplete?.Invoke();
            return;
        }

        SaveManager.instance.CachedData.merchantEncounterCount++;
        int encounter = SaveManager.instance.CachedData.merchantEncounterCount;
        SaveManager.instance.SavePersistentData();

        Debug.Log($"🧙 [MERCADOR ENCONTRO #{encounter}] Disparando diálogo duplo no HUD (Eptinho acima, Mercador abaixo)...");

        EncounterLine line;
        if (encounter <= ENIGMAS_MERCADOR.Length)
        {
            line = ENIGMAS_MERCADOR[encounter - 1];
        }
        else
        {
            line = DIALOGO_POS_REVELACAO;
        }

        // 1. Popup do Eptinho no Topo Direito do HUD
        if (EptinhoPopupController.instancia != null)
        {
            Sprite eptinhoSprite = EptinhoPopupController.instancia.eptinhoTensoSprite != null
                ? EptinhoPopupController.instancia.eptinhoTensoSprite
                : EptinhoPopupController.instancia.eptinhoNormalSprite;

            EptinhoPopupController.instancia.MostrarPopupCustomizado(eptinhoSprite, line.eptinhoComment);
        }

        // 2. Popup do Mercador no Canto Direito (Logo abaixo do Eptinho)
        if (MerchantPopupController.Instance != null)
        {
            MerchantPopupController.Instance.MostrarPopupMercador(line.merchantSpeech, line.percentageBadge, duracao: 9f);
        }

        onComplete?.Invoke();
    }

    /// <summary>
    /// Retorna a fala atual do Mercador com base no progresso salvo, sem avançar o contador.
    /// Usado para o diálogo ambiente ao se aproximar do Mercador.
    /// </summary>
    public static EncounterLine GetCurrentEncounterLine()
    {
        int encounter = 1;
        if (MerchantPopupController.Instance != null && MerchantPopupController.Instance.debugEncounterCount > 0)
        {
            encounter = Mathf.Clamp(MerchantPopupController.Instance.debugEncounterCount, 1, 20);
        }
        else if (SaveManager.instance != null && SaveManager.instance.CachedData != null)
        {
            encounter = Mathf.Max(1, SaveManager.instance.CachedData.merchantEncounterCount);
        }

        if (encounter <= ENIGMAS_MERCADOR.Length)
        {
            return ENIGMAS_MERCADOR[encounter - 1];
        }
        return DIALOGO_POS_REVELACAO;
    }
}
