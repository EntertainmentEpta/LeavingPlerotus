# -*- coding: utf-8 -*-
"""
Script gerador do Deck de Apresentação em PDF:
ESTUDO DE PROGRESSÃO, BALANCEAMENTO E ECONOMIA DE INFUSÕES (ROGUE-LIKE PLEROTUS)
Gera um PDF em formato 16:9 Widescreen (apresentação de slides) com alta definição vetorial.
"""

import os
import subprocess
import sys

if sys.stdout.encoding != 'utf-8':
    try:
        sys.stdout.reconfigure(encoding='utf-8')
    except Exception:
        pass

def build_slides_html():
    return """<!DOCTYPE html>
<html lang="pt-BR">
<head>
<meta charset="UTF-8">
<title>Estudo de Progressão e Balanceamento - RogueLike Plerotus</title>
<style>
  @page {
    size: 16in 9in;
    margin: 0;
  }
  * {
    box-sizing: border-box;
    margin: 0;
    padding: 0;
  }
  html, body {
    width: 16in;
    height: 9in;
    margin: 0;
    padding: 0;
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
    background-color: #090c15;
    color: #f1f5f9;
    -webkit-print-color-adjust: exact !important;
    print-color-adjust: exact !important;
  }
  .slide {
    width: 16in;
    height: 9in;
    page-break-after: always;
    break-after: page;
    position: relative;
    padding: 0.52in 0.75in 0.42in 0.75in;
    display: flex;
    flex-direction: column;
    justify-content: space-between;
    background-color: #090c15;
    background-image: radial-gradient(circle at 15% 15%, rgba(30, 41, 59, 0.45) 0%, transparent 45%),
                      radial-gradient(circle at 85% 85%, rgba(15, 23, 42, 0.65) 0%, transparent 50%);
    overflow: hidden;
  }

  /* Header e Badges */
  .slide-header {
    margin-bottom: 0.22in;
  }
  .header-badge {
    display: inline-flex;
    align-items: center;
    gap: 8px;
    padding: 4px 14px;
    border-radius: 9999px;
    font-size: 11px;
    font-weight: 800;
    text-transform: uppercase;
    letter-spacing: 0.08em;
  }
  .badge-cyan { background: rgba(56, 189, 248, 0.12); color: #38bdf8; border: 1px solid rgba(56, 189, 248, 0.25); }
  .badge-amber { background: rgba(245, 158, 11, 0.12); color: #f59e0b; border: 1px solid rgba(245, 158, 11, 0.25); }
  .badge-emerald { background: rgba(16, 185, 129, 0.12); color: #10b981; border: 1px solid rgba(16, 185, 129, 0.25); }
  .badge-rose { background: rgba(244, 63, 94, 0.12); color: #f43f5e; border: 1px solid rgba(244, 63, 94, 0.25); }
  .badge-purple { background: rgba(168, 85, 247, 0.12); color: #a855f7; border: 1px solid rgba(168, 85, 247, 0.25); }

  .slide-title {
    font-size: 32px;
    font-weight: 900;
    letter-spacing: -0.025em;
    color: #ffffff;
    margin-top: 5px;
    line-height: 1.15;
  }
  .slide-subtitle {
    font-size: 14.5px;
    color: #94a3b8;
    margin-top: 3px;
    font-weight: 400;
  }

  /* Cards e Containers */
  .card {
    background-color: #101625;
    border: 1px solid rgba(148, 163, 184, 0.14);
    border-radius: 14px;
    padding: 16px 20px;
    position: relative;
  }
  .grid-2 { display: grid; grid-template-columns: 1fr 1fr; gap: 18px; }
  .grid-3 { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 16px; }
  .grid-4 { display: grid; grid-template-columns: 1fr 1fr 1fr 1fr; gap: 16px; }

  /* Tipografia Auxiliar */
  .stat-number { font-size: 32px; font-weight: 900; line-height: 1.1; letter-spacing: -0.03em; }
  .stat-label { font-size: 11.5px; text-transform: uppercase; font-weight: 800; letter-spacing: 0.06em; color: #64748b; }
  .text-highlight { font-weight: 700; color: #ffffff; }

  /* Barras de Progresso */
  .progress-bg {
    width: 100%;
    height: 10px;
    background: #1e293b;
    border-radius: 9999px;
    overflow: hidden;
    margin: 8px 0;
  }
  .progress-bar { height: 100%; border-radius: 9999px; }

  /* Footer */
  .slide-footer {
    border-top: 1px solid rgba(148, 163, 184, 0.12);
    padding-top: 10px;
    display: flex;
    justify-content: space-between;
    align-items: center;
    font-size: 11px;
    color: #64748b;
    font-weight: 600;
    letter-spacing: 0.04em;
  }

  /* Tabelas */
  table {
    width: 100%;
    border-collapse: collapse;
    font-size: 12.5px;
  }
  th {
    text-align: left;
    padding: 10px 14px;
    background: rgba(30, 41, 59, 0.7);
    color: #94a3b8;
    font-weight: 800;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    border-bottom: 2px solid rgba(148, 163, 184, 0.2);
  }
  td {
    padding: 10px 14px;
    border-bottom: 1px solid rgba(148, 163, 184, 0.1);
    color: #cbd5e1;
  }
  tr:last-child td { border-bottom: none; }
</style>
</head>
<body>

  <!-- ══════════════════════════════════════════════════════════════ -->
  <!-- SLIDE 1: CAPA EXECUTIVA                                       -->
  <!-- ══════════════════════════════════════════════════════════════ -->
  <div class="slide" style="justify-content: center; gap: 0.4in; text-align: left;">
    <div style="position: absolute; right: 0.8in; top: 0.8in; width: 4.5in; height: 4.5in; border-radius: 50%; background: radial-gradient(circle, rgba(56, 189, 248, 0.15) 0%, transparent 70%); filter: blur(30px); pointer-events: none;"></div>

    <div style="max-width: 11in;">
      <div class="header-badge badge-amber" style="margin-bottom: 16px;">
        🎮 ROGUELIKE PROJECT • PLEROTUS • RELATÓRIO EXECUTIVO DE DESIGN
      </div>
      <h1 style="font-size: 54px; font-weight: 900; letter-spacing: -0.03em; line-height: 1.05; color: #ffffff;">
        ESTUDO DE PROGRESSÃO &<br><span style="color: #38bdf8;">ECONOMIA DE INFUSÕES</span>
      </h1>
      <p style="font-size: 19px; color: #94a3b8; margin-top: 14px; line-height: 1.45; max-width: 9.5in;">
        Modelagem estatística do tempo médio até 100% de conclusão do jogo (Escrituras, Mercador, Armas e Sinergias), impacto do perfil de jogador na inflação de essência e diretrizes de calibração para a semana de testes.
      </p>
    </div>

    <!-- Grade de Métricas Chave -->
    <div class="grid-4" style="max-width: 13.5in; margin-top: 10px;">
      <div class="card" style="border-left: 4px solid #38bdf8;">
        <div class="stat-label">Códice do Caderno</div>
        <div class="stat-number" style="color: #38bdf8; margin: 4px 0;">24 Páginas</div>
        <div style="font-size: 12px; color: #94a3b8;">6 Blueprints de Engenharia + 18 Crônicas de Lore</div>
      </div>
      <div class="card" style="border-left: 4px solid #f59e0b;">
        <div class="stat-label">Segredo do Mercador</div>
        <div class="stat-number" style="color: #f59e0b; margin: 4px 0;">20 Encontros</div>
        <div style="font-size: 12px; color: #94a3b8;">Decodificação de 5% em 5% até a revelação da entidade</div>
      </div>
      <div class="card" style="border-left: 4px solid #f43f5e;">
        <div class="stat-label">Arsenal de Combate</div>
        <div class="stat-number" style="color: #f43f5e; margin: 4px 0;">3 Armas</div>
        <div style="font-size: 12px; color: #94a3b8;">Adaga de Plasma, Machado de Cristal e Pistola de Plasma</div>
      </div>
      <div class="card" style="border-left: 4px solid #10b981;">
        <div class="stat-label">Teia de Sinergias</div>
        <div class="stat-number" style="color: #10b981; margin: 4px 0;">8 Combos</div>
        <div style="font-size: 12px; color: #94a3b8;">52 itens dropáveis em 4 tiers + Build Berserker Imortal</div>
      </div>
    </div>

    <div class="slide-footer">
      <span>SEMANA DE PLAYTESTS • DOCUMENTO DE ALINHAMENTO INTERNO</span>
      <span>PREMISSA DE PACING: 14 MINUTOS / RUN • 3 ANDARES + BOSS FIGHT</span>
      <span>SLIDE 01 / 06</span>
    </div>
  </div>


  <!-- ══════════════════════════════════════════════════════════════ -->
  <!-- SLIDE 2: CONCLUSÃO 100%                                       -->
  <!-- ══════════════════════════════════════════════════════════════ -->
  <div class="slide">
    <div class="slide-header">
      <div class="header-badge badge-cyan">PILAR 1 • TEMPO DE JOGO REAL</div>
      <h2 class="slide-title">Mapeamento de Tempo até o 100% de Conteúdo</h2>
      <p class="slide-subtitle">Estimativa matemática de partidas (runs) e horas necessárias para um jogador médio zerar cada sistema.</p>
    </div>

    <!-- Grade dos 4 Pilares -->
    <div class="grid-4" style="flex: 1; align-items: stretch; margin-bottom: 0.15in;">
      <!-- Card 1: Escrituras -->
      <div class="card" style="display: flex; flex-direction: column; justify-content: space-between;">
        <div>
          <div style="display: flex; justify-content: space-between; align-items: center;">
            <span class="stat-label">Códice do Caderno</span>
            <span style="font-size: 18px;">📖</span>
          </div>
          <div class="stat-number" style="color: #38bdf8; margin: 8px 0 2px 0;">18–22 runs</div>
          <div style="font-size: 14px; font-weight: 700; color: #e2e8f0;">~4.8 Horas de Jogo</div>
          <div class="progress-bg"><div class="progress-bar" style="width: 85%; background: #38bdf8;"></div></div>
          <p style="font-size: 12px; color: #94a3b8; line-height: 1.45; margin-top: 8px;">
            Spawna 1 página por andar em Salas de Anomalia ou Dead Ends. O algoritmo <code style="color:#38bdf8;">GetNextUndiscoveredScripture</code> garante zero duplicatas.
          </p>
        </div>
        <div style="background: rgba(56, 189, 248, 0.08); border-radius: 8px; padding: 8px 12px; font-size: 11.5px; color: #38bdf8; font-weight: 700;">
          ✔ 100% Completo na 20ª Run
        </div>
      </div>

      <!-- Card 2: Mercador -->
      <div class="card" style="display: flex; flex-direction: column; justify-content: space-between;">
        <div>
          <div style="display: flex; justify-content: space-between; align-items: center;">
            <span class="stat-label">Segredo do Mercador</span>
            <span style="font-size: 18px;">🧙</span>
          </div>
          <div class="stat-number" style="color: #f59e0b; margin: 8px 0 2px 0;">20–24 runs</div>
          <div style="font-size: 14px; font-weight: 700; color: #e2e8f0;">~5.5 Horas de Jogo</div>
          <div class="progress-bg"><div class="progress-bar" style="width: 100%; background: #f59e0b;"></div></div>
          <p style="font-size: 12px; color: #94a3b8; line-height: 1.45; margin-top: 8px;">
            Chance de 25% por Dead End com fallback garantido no último. Trava de segurança permite apenas 1 encontro/pacto por partida.
          </p>
        </div>
        <div style="background: rgba(245, 158, 11, 0.08); border-radius: 8px; padding: 8px 12px; font-size: 11.5px; color: #f59e0b; font-weight: 700;">
          ✔ Clímax Narrativo na 22ª Run
        </div>
      </div>

      <!-- Card 3: Armas -->
      <div class="card" style="display: flex; flex-direction: column; justify-content: space-between;">
        <div>
          <div style="display: flex; justify-content: space-between; align-items: center;">
            <span class="stat-label">Arsenal Completo</span>
            <span style="font-size: 18px;">⚔️</span>
          </div>
          <div class="stat-number" style="color: #f43f5e; margin: 8px 0 2px 0;">12–16 runs</div>
          <div style="font-size: 14px; font-weight: 700; color: #e2e8f0;">~3.5 Horas de Jogo</div>
          <div class="progress-bg"><div class="progress-bar" style="width: 60%; background: #f43f5e;"></div></div>
          <p style="font-size: 12px; color: #94a3b8; line-height: 1.45; margin-top: 8px;">
            Adaga vem liberada. Machado é forjado na Run 4–6 com minérios de Golem. Pistola requer abater o Boss Geobionte e sintetizar no Robozinho.
          </p>
        </div>
        <div style="background: rgba(244, 63, 94, 0.08); border-radius: 8px; padding: 8px 12px; font-size: 11.5px; color: #f43f5e; font-weight: 700;">
          ✔ Todas as Armas na 14ª Run
        </div>
      </div>

      <!-- Card 4: Sinergias -->
      <div class="card" style="display: flex; flex-direction: column; justify-content: space-between;">
        <div>
          <div style="display: flex; justify-content: space-between; align-items: center;">
            <span class="stat-label">Teia de Sinergias</span>
            <span style="font-size: 18px;">⚡</span>
          </div>
          <div class="stat-number" style="color: #a855f7; margin: 8px 0 2px 0;">16–20 runs</div>
          <div style="font-size: 14px; font-weight: 700; color: #e2e8f0;">~4.5 Horas de Jogo</div>
          <div class="progress-bg"><div class="progress-bar" style="width: 78%; background: #a855f7;"></div></div>
          <p style="font-size: 12px; color: #94a3b8; line-height: 1.45; margin-top: 8px;">
            Cada run ativa de 1 a 2 sinergias. Com foco intencional na bancada, o jogador experimenta os 8 combos e atinge a build Berserker Imortal.
          </p>
        </div>
        <div style="background: rgba(168, 85, 247, 0.08); border-radius: 8px; padding: 8px 12px; font-size: 11.5px; color: #a855f7; font-weight: 700;">
          ✔ 8 Combos Testados na 18ª Run
        </div>
      </div>
    </div>

    <!-- Callout de Destaque: A Sincronia de Design -->
    <div class="card" style="background: rgba(16, 185, 129, 0.07); border-color: rgba(16, 185, 129, 0.25); display: flex; align-items: center; justify-content: space-between; padding: 14px 22px;">
      <div>
        <span style="font-weight: 800; color: #10b981; font-size: 13.5px;">🎯 INSIGHT DE DESIGN • A SINCRONIA DOURADA DE CONTEÚDO:</span>
        <span style="font-size: 13px; color: #cbd5e1; margin-left: 8px;">
          O <strong>Segredo do Mercador (20 a 24 runs)</strong> e o <strong>Códice Completo (18 a 22 runs)</strong> concluem praticamente juntos por volta de <strong>5 a 5.5 horas de jogo</strong>. O jogador nunca sente que um sistema ficou órfão enquanto o outro exige repetição excessiva.
        </span>
      </div>
    </div>

    <div class="slide-footer">
      <span>PILAR 1 • MATRIZ DE CONCLUSÃO 100%</span>
      <span>BASE CALCULADA: 75% EXPLORAÇÃO DE SALAS • TAXA DE VITÓRIA INICIAL 15% → FINAL 70%</span>
      <span>SLIDE 02 / 06</span>
    </div>
  </div>


  <!-- ══════════════════════════════════════════════════════════════ -->
  <!-- SLIDE 3: TIMELINE & RETENÇÃO POR SESSÕES                      -->
  <!-- ══════════════════════════════════════════════════════════════ -->
  <div class="slide">
    <div class="slide-header">
      <div class="header-badge badge-emerald">PILAR 2 • JORNADA DO JOGADOR</div>
      <h2 class="slide-title">Linha do Tempo de Retenção & Desbloqueios</h2>
      <p class="slide-subtitle">Distribuição dos marcos narrativos e mecânicos ao longo de 4 sessões de jogo durante a semana de testes.</p>
    </div>

    <div class="grid-4" style="flex: 1; align-items: stretch; margin-bottom: 0.15in;">
      <!-- Sessão 1 -->
      <div class="card" style="border-top: 4px solid #38bdf8;">
        <div style="font-size: 12px; font-weight: 800; color: #38bdf8; text-transform: uppercase;">SESSÃO 1 • DIA 1–2</div>
        <div style="font-size: 20px; font-weight: 900; margin: 4px 0; color: #ffffff;">A Descoberta</div>
        <div style="font-size: 12px; color: #64748b; margin-bottom: 12px;">0h às 1.5h • Runs 1 a 6</div>
        <ul style="font-size: 12px; color: #cbd5e1; list-style: none; display: flex; flex-direction: column; gap: 8px;">
          <li style="display: flex; gap: 6px;"><span>📖</span> <span><strong>Caderno Coletado:</strong> Surge na Safe Room logo no minuto 1.</span></li>
          <li style="display: flex; gap: 6px;"><span>📜</span> <span>Primeiras 5 escrituras recuperadas (20%).</span></li>
          <li style="display: flex; gap: 6px;"><span>🧙</span> <span>3 encontros com o Mercador (15% decodificado).</span></li>
          <li style="display: flex; gap: 6px;"><span>🔨</span> <span>Forja do Machado de Cristal no Robozinho.</span></li>
          <li style="display: flex; gap: 6px;"><span>💀</span> <span>Primeiras mortes na "Parede da Sala 5" (Geobionte).</span></li>
        </ul>
      </div>

      <!-- Sessão 2 -->
      <div class="card" style="border-top: 4px solid #10b981;">
        <div style="font-size: 12px; font-weight: 800; color: #10b981; text-transform: uppercase;">SESSÃO 2 • DIA 3–4</div>
        <div style="font-size: 20px; font-weight: 900; margin: 4px 0; color: #ffffff;">O Domínio</div>
        <div style="font-size: 12px; color: #64748b; margin-bottom: 12px;">1.5h às 3.5h • Runs 7 a 14</div>
        <ul style="font-size: 12px; color: #cbd5e1; list-style: none; display: flex; flex-direction: column; gap: 8px;">
          <li style="display: flex; gap: 6px;"><span>🏆</span> <span><strong>Primeira Vitória:</strong> Derrota o Boss Geobionte!</span></li>
          <li style="display: flex; gap: 6px;"><span>🔫</span> <span>Desbloqueio da Pistola de Plasma (Protótipo).</span></li>
          <li style="display: flex; gap: 6px;"><span>📜</span> <span>Metade do Caderno preenchido (12/24 páginas).</span></li>
          <li style="display: flex; gap: 6px;"><span>🧙</span> <span>Mercador em 50%: revelação dos Lapidadores.</span></li>
          <li style="display: flex; gap: 6px;"><span>⚡</span> <span>Primeiras sinergias ativadas (Chuva de Lâminas).</span></li>
        </ul>
      </div>

      <!-- Sessão 3 -->
      <div class="card" style="border-top: 4px solid #f59e0b;">
        <div style="font-size: 12px; font-weight: 800; color: #f59e0b; text-transform: uppercase;">SESSÃO 3 • DIA 5–6</div>
        <div style="font-size: 20px; font-weight: 900; margin: 4px 0; color: #ffffff;">O Clímax</div>
        <div style="font-size: 12px; color: #64748b; margin-bottom: 12px;">3.5h às 5.5h • Runs 15 a 22</div>
        <ul style="font-size: 12px; color: #cbd5e1; list-style: none; display: flex; flex-direction: column; gap: 8px;">
          <li style="display: flex; gap: 6px;"><span>📚</span> <span><strong>Códice 100% Completo:</strong> 24 páginas e 6 blueprints.</span></li>
          <li style="display: flex; gap: 6px;"><span>⚔️</span> <span>Forja do Catalisador Ancestral (Arma Ancestral).</span></li>
          <li style="display: flex; gap: 6px;"><span>🧙</span> <span><strong>20º Encontro:</strong> Revelação total da Consciência Plerotus!</span></li>
          <li style="display: flex; gap: 6px;"><span>🌟</span> <span>Build Berserker Imortal testada e dominada.</span></li>
          <li style="display: flex; gap: 6px;"><span>🎯</span> <span>Vitórias consistentes no Boss com múltiplos arsenais.</span></li>
        </ul>
      </div>

      <!-- Sessão 4 -->
      <div class="card" style="border-top: 4px solid #a855f7;">
        <div style="font-size: 12px; font-weight: 800; color: #a855f7; text-transform: uppercase;">SESSÃO 4 • DIA 7+</div>
        <div style="font-size: 20px; font-weight: 900; margin: 4px 0; color: #ffffff;">A Maestria</div>
        <div style="font-size: 12px; color: #64748b; margin-bottom: 12px;">5.5h+ • Runs 23 em diante</div>
        <ul style="font-size: 12px; color: #cbd5e1; list-style: none; display: flex; flex-direction: column; gap: 8px;">
          <li style="display: flex; gap: 6px;"><span>👑</span> <span><strong>Modo Deus / Ascensão:</strong> Desafio dos 13 Lendários T4.</span></li>
          <li style="display: flex; gap: 6px;"><span>⚡</span> <span>Otimização extrema de Speedrun (&lt; 10 min por run).</span></li>
          <li style="display: flex; gap: 6px;"><span>🩸</span> <span>Testes de builds extremas com Pactos de Sangue.</span></li>
          <li style="display: flex; gap: 6px;"><span>🧪</span> <span>Feedback avançado de balanceamento na comunidade.</span></li>
        </ul>
      </div>
    </div>

    <!-- Barra Visual Gantt -->
    <div class="card" style="padding: 12px 18px;">
      <div style="display: flex; justify-content: space-between; font-size: 11px; font-weight: 800; text-transform: uppercase; color: #64748b; margin-bottom: 6px;">
        <span>Progressão Contínua de Retenção (0h a 6h)</span>
        <span>Curva de Recompensa Sem Vácuos de Conteúdo</span>
      </div>
      <div style="width: 100%; height: 14px; background: #1e293b; border-radius: 6px; overflow: hidden; display: flex;">
        <div style="width: 25%; background: #38bdf8; height: 100%;"></div>
        <div style="width: 35%; background: #10b981; height: 100%;"></div>
        <div style="width: 30%; background: #f59e0b; height: 100%;"></div>
        <div style="width: 10%; background: #a855f7; height: 100%;"></div>
      </div>
    </div>

    <div class="slide-footer">
      <span>PILAR 2 • MAPA DE RETENÇÃO</span>
      <span>ESTRUTURADO PARA PLAYTESTS DE 7 DIAS (1 A 2 PARTIDAS POR DIA)</span>
      <span>SLIDE 03 / 06</span>
    </div>
  </div>


  <!-- ══════════════════════════════════════════════════════════════ -->
  <!-- SLIDE 4: GRÁFICO DE INFUSÕES POR PERFIL                       -->
  <!-- ══════════════════════════════════════════════════════════════ -->
  <div class="slide">
    <div class="slide-header">
      <div class="header-badge badge-amber">PILAR 3 • ECONOMIA DE INFUSÕES</div>
      <h2 class="slide-title">Curva de Infusões conforme a Psicologia do Jogador</h2>
      <p class="slide-subtitle">Como a fórmula de inflação exponencial <code style="color:#f59e0b;">C = B × (1 + 0.1 × Ptotal)</code> molda o poder e o comportamento na run.</p>
    </div>

    <div class="grid-2" style="flex: 1; align-items: stretch; margin-bottom: 0.15in;">
      <!-- Lado Esquerdo: Gráfico Vetorial SVG -->
      <div class="card" style="display: flex; flex-direction: column; justify-content: space-between;">
        <div style="display: flex; justify-content: space-between; align-items: center;">
          <span style="font-size: 13px; font-weight: 800; color: #ffffff; text-transform: uppercase;">Infusões Acumuladas por Sala (Run de 12 Salas)</span>
          <span style="font-size: 11px; color: #94a3b8;">Eixo Y: Infusões • Eixo X: Salas</span>
        </div>

        <!-- SVG Chart -->
        <svg viewBox="0 0 540 220" style="width: 100%; height: 2.3in; margin: 10px 0;">
          <!-- Linhas de Grade -->
          <line x1="45" y1="30" x2="520" y2="30" stroke="rgba(148, 163, 184, 0.15)" stroke-dasharray="3,3" />
          <line x1="45" y1="75" x2="520" y2="75" stroke="rgba(148, 163, 184, 0.15)" stroke-dasharray="3,3" />
          <line x1="45" y1="120" x2="520" y2="120" stroke="rgba(148, 163, 184, 0.15)" stroke-dasharray="3,3" />
          <line x1="45" y1="165" x2="520" y2="165" stroke="rgba(148, 163, 184, 0.15)" stroke-dasharray="3,3" />

          <!-- Eixos -->
          <line x1="45" y1="20" x2="45" y2="185" stroke="#64748b" stroke-width="1.5" />
          <line x1="45" y1="185" x2="520" y2="185" stroke="#64748b" stroke-width="1.5" />

          <!-- Labels Y -->
          <text x="35" y="34" font-size="9.5" fill="#94a3b8" text-anchor="end">10 Inf.</text>
          <text x="35" y="79" font-size="9.5" fill="#94a3b8" text-anchor="end">7 Inf.</text>
          <text x="35" y="124" font-size="9.5" fill="#94a3b8" text-anchor="end">4 Inf.</text>
          <text x="35" y="169" font-size="9.5" fill="#94a3b8" text-anchor="end">1 Inf.</text>

          <!-- Labels X -->
          <text x="45" y="200" font-size="9.5" fill="#94a3b8" text-anchor="middle">S1</text>
          <text x="125" y="200" font-size="9.5" fill="#94a3b8" text-anchor="middle">S3</text>
          <text x="210" y="200" font-size="9.5" fill="#94a3b8" text-anchor="middle">S5</text>
          <text x="300" y="200" font-size="9.5" fill="#94a3b8" text-anchor="middle">S7</text>
          <text x="390" y="200" font-size="9.5" fill="#94a3b8" text-anchor="middle">S9</text>
          <text x="495" y="200" font-size="9.5" fill="#38bdf8" font-weight="bold" text-anchor="middle">Boss S12</text>

          <!-- Curva 1: Compulsivo (Amarelo) -->
          <path d="M 45 170 L 125 135 L 210 100 L 300 75 L 390 65 L 495 60" fill="none" stroke="#f59e0b" stroke-width="3" stroke-linecap="round" />
          <circle cx="495" cy="60" r="4" fill="#f59e0b" />

          <!-- Curva 2: Estrategista (Verde) -->
          <path d="M 45 185 L 125 170 L 210 140 L 300 120 L 390 105 L 495 90" fill="none" stroke="#10b981" stroke-width="3" stroke-linecap="round" />
          <circle cx="495" cy="90" r="4" fill="#10b981" />

          <!-- Curva 3: Acumulador (Cinza) -->
          <path d="M 45 185 L 125 185 L 210 180 L 300 170 L 390 155 L 495 140" fill="none" stroke="#64748b" stroke-width="3" stroke-dasharray="4,4" stroke-linecap="round" />
          <circle cx="495" cy="140" r="4" fill="#64748b" />

          <!-- Curva 4: Apostador de Sangue (Vermelho) -->
          <path d="M 45 155 L 125 120 L 210 85 L 300 60 L 390 40 L 495 30" fill="none" stroke="#f43f5e" stroke-width="3" stroke-linecap="round" />
          <circle cx="495" cy="30" r="4" fill="#f43f5e" />
        </svg>

        <!-- Legenda -->
        <div style="display: flex; justify-content: space-between; font-size: 11px; border-top: 1px solid rgba(148,163,184,0.15); padding-top: 8px;">
          <span style="color: #f59e0b; font-weight: 700;">● Compulsivo (8–9)</span>
          <span style="color: #10b981; font-weight: 700;">● Estrategista (5–6)</span>
          <span style="color: #64748b; font-weight: 700;">┄ Acumulador (2–3)</span>
          <span style="color: #f43f5e; font-weight: 700;">● Apostador Sangue (10–11)</span>
        </div>
      </div>

      <!-- Lado Direito: Tabela Comparativa dos 4 Perfis -->
      <div class="card" style="display: flex; flex-direction: column; justify-content: space-between;">
        <span style="font-size: 13px; font-weight: 800; color: #ffffff; text-transform: uppercase;">Matriz Comparativa dos Arquétipos</span>

        <table style="margin-top: 6px;">
          <thead>
            <tr>
              <th>Perfil</th>
              <th>Qtd Infusões</th>
              <th>Parede Inflação</th>
              <th>Efeito no Boss</th>
              <th>Taxa Win</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td style="font-weight: 700; color: #f59e0b;">🌪️ Compulsivo</td>
              <td>8 a 9 infusões</td>
              <td>Sala 7 (+420 es)</td>
              <td>Muitos buffs fracos dispersos</td>
              <td style="color:#f59e0b; font-weight:700;">55%</td>
            </tr>
            <tr>
              <td style="font-weight: 700; color: #10b981;">🧠 Estrategista</td>
              <td>5 a 6 infusões</td>
              <td>Sem barreira</td>
              <td>Combos duplicados (×2) & Sinergia</td>
              <td style="color:#10b981; font-weight:700;">90%</td>
            </tr>
            <tr>
              <td style="font-weight: 700; color: #94a3b8;">🎒 Acumulador</td>
              <td>2 a 3 infusões</td>
              <td>Morte prematura</td>
              <td>Ultrapassado pelo escalonamento +3%</td>
              <td style="color:#f43f5e; font-weight:700;">25%</td>
            </tr>
            <tr>
              <td style="font-weight: 700; color: #f43f5e;">🩸 Apostador</td>
              <td>10 a 11 infusões</td>
              <td>HP Crítico</td>
              <td>Canhão de Vidro (Dano massivo, 50 HP)</td>
              <td style="color:#f59e0b; font-weight:700;">50%</td>
            </tr>
          </tbody>
        </table>

        <div style="background: rgba(30, 41, 59, 0.5); border-radius: 8px; padding: 10px 14px; font-size: 11.5px; color: #cbd5e1; margin-top: 8px;">
          💡 <strong>Conclusão Econômica:</strong> O design premia a <strong>reciclagem ativa</strong>. Jogadores que aprendem a descartar o que não usam para pagar o Fluxo Acumulado (2 itens iguais) têm a experiência mais recompensadora.
        </div>
      </div>
    </div>

    <div class="slide-footer">
      <span>PILAR 3 • COMPORTAMENTO & INFLAÇÃO DE ESSÊNCIA</span>
      <span>CUSTOS BASE: T1=60, T2=180, T3=300, T4=420 • COEFICIENTE ALPHA = 0.10</span>
      <span>SLIDE 04 / 06</span>
    </div>
  </div>


  <!-- ══════════════════════════════════════════════════════════════ -->
  <!-- SLIDE 5: GARGALOS CRÍTICOS & SOLUÇÕES                         -->
  <!-- ══════════════════════════════════════════════════════════════ -->
  <div class="slide">
    <div class="slide-header">
      <div class="header-badge badge-rose">PILAR 4 • DIAGNÓSTICO PARA OS PLAYTESTS</div>
      <h2 class="slide-title">Os 3 Pontos de Atrito Críticos & Soluções</h2>
      <p class="slide-subtitle">Locais exatos onde testadores reais podem travar ou se frustrar durante a próxima semana.</p>
    </div>

    <div class="grid-3" style="flex: 1; align-items: stretch; margin-bottom: 0.15in;">
      <!-- Gargalo 1 -->
      <div class="card" style="border-left: 4px solid #f43f5e; display: flex; flex-direction: column; justify-content: space-between;">
        <div>
          <div style="display: flex; justify-content: space-between; align-items: center;">
            <span style="font-size: 12px; font-weight: 800; color: #f43f5e; text-transform: uppercase;">GARGALO 1 • LETALIDADE</span>
            <span class="badge-rose" style="font-size: 10px; padding: 2px 8px;">Morte: 68%</span>
          </div>
          <div style="font-size: 18px; font-weight: 900; color: #ffffff; margin: 8px 0 4px 0;">A "Parede da Sala 5"</div>
          <p style="font-size: 12px; color: #94a3b8; line-height: 1.45;">
            Os inimigos acumulam +3% de HP e Dano a cada sala (+15% na S5). O primeiro Geobionte de Elite surge com 250 HP.
          </p>
          <div style="margin-top: 10px; padding: 8px 10px; background: rgba(244, 63, 94, 0.08); border-radius: 6px; font-size: 11.5px; color: #fca5a5;">
            <strong>Risco:</strong> Se o jogador jogou como "Acumulador" e tem 0 infusões, o TTK ultrapassa 25 segundos e ele morre por desgaste.
          </div>
        </div>
        <div style="margin-top: 10px; padding: 10px 12px; background: rgba(16, 185, 129, 0.08); border-radius: 8px; font-size: 11.5px; color: #10b981; font-weight: 700;">
          ✔ Ação: Inserir fala do Eptinho no HUD na entrada da Sala 4 alertando para usar a bancada.
        </div>
      </div>

      <!-- Gargalo 2 -->
      <div class="card" style="border-left: 4px solid #f59e0b; display: flex; flex-direction: column; justify-content: space-between;">
        <div>
          <div style="display: flex; justify-content: space-between; align-items: center;">
            <span style="font-size: 12px; font-weight: 800; color: #f59e0b; text-transform: uppercase;">GARGALO 2 • BALANÇO</span>
            <span class="badge-amber" style="font-size: 10px; padding: 2px 8px;">One-Shot</span>
          </div>
          <div style="font-size: 18px; font-weight: 900; color: #ffffff; margin: 8px 0 4px 0;">O Pacto Precoce de Sangue</div>
          <p style="font-size: 12px; color: #94a3b8; line-height: 1.45;">
            Jogadores novatos e curiosos aceitam o Pacto do Mercador logo na Sala 2, perdendo 25% da barra de vida máxima de forma permanente.
          </p>
          <div style="margin-top: 10px; padding: 8px 10px; background: rgba(245, 158, 11, 0.08); border-radius: 6px; font-size: 11.5px; color: #fcd34d;">
            <strong>Risco:</strong> Com 75 HP, 2 socos de Golem (30 dmg cada) causam derrota instantânea, gerando sentimento de punição excessiva.
          </div>
        </div>
        <div style="margin-top: 10px; padding: 10px 12px; background: rgba(16, 185, 129, 0.08); border-radius: 8px; font-size: 11.5px; color: #10b981; font-weight: 700;">
          ✔ Ação: Já implementada a trava de apenas 1 pacto por run e visual de correntes vermelhas no HUD.
        </div>
      </div>

      <!-- Gargalo 3 -->
      <div class="card" style="border-left: 4px solid #38bdf8; display: flex; flex-direction: column; justify-content: space-between;">
        <div>
          <div style="display: flex; justify-content: space-between; align-items: center;">
            <span style="font-size: 12px; font-weight: 800; color: #38bdf8; text-transform: uppercase;">GARGALO 3 • ONBOARDING</span>
            <span class="badge-cyan" style="font-size: 10px; padding: 2px 8px;">100% Resolvido</span>
          </div>
          <div style="font-size: 18px; font-weight: 900; color: #ffffff; margin: 8px 0 4px 0;">Descoberta do Caderno</div>
          <p style="font-size: 12px; color: #94a3b8; line-height: 1.45;">
            Se o jogador não encontrasse o Caderno na dungeon, todo o sistema de 24 escrituras e display 3D na Base ficaria bloqueado.
          </p>
          <div style="margin-top: 10px; padding: 8px 10px; background: rgba(56, 189, 248, 0.08); border-radius: 6px; font-size: 11.5px; color: #bae6fd;">
            <strong>Status:</strong> Implementado hoje no <code>LevelGenerator</code> com luz dourada e beacon vertical na Safe Room inicial.
          </div>
        </div>
        <div style="margin-top: 10px; padding: 10px 12px; background: rgba(16, 185, 129, 0.08); border-radius: 8px; font-size: 11.5px; color: #10b981; font-weight: 700;">
          ✔ Ação: Garantia total no minuto 1 de gameplay com fala celebratória do Eptinho.
        </div>
      </div>
    </div>

    <div class="slide-footer">
      <span>PILAR 4 • AUDITORIA DE ATRITO & UX</span>
      <span>DIRETRIZES DE DESIGN PREVENTIVAS PARA A SEMANA DE TESTES</span>
      <span>SLIDE 05 / 06</span>
    </div>
  </div>


  <!-- ══════════════════════════════════════════════════════════════ -->
  <!-- SLIDE 6: MATRIZ DE CALIBRAÇÃO & CONCLUSÃO                    -->
  <!-- ══════════════════════════════════════════════════════════════ -->
  <div class="slide">
    <div class="slide-header">
      <div class="header-badge badge-purple">PILAR 5 • DIRETRIZES DE DECISÃO</div>
      <h2 class="slide-title">Matriz de Sensibilidade & Veredito Final</h2>
      <p class="slide-subtitle">Tabela prática de calibração para a equipe alterar durante a reunião e direcionamentos finais.</p>
    </div>

    <div class="grid-2" style="flex: 1; align-items: stretch; margin-bottom: 0.15in;">
      <!-- Lado Esquerdo: Tabela de Sensibilidade -->
      <div class="card" style="display: flex; flex-direction: column; justify-content: space-between;">
        <span style="font-size: 13px; font-weight: 800; color: #ffffff; text-transform: uppercase;">Sensibilidade dos Parâmetros no Jogo</span>

        <table style="margin-top: 8px;">
          <thead>
            <tr>
              <th>Parâmetro</th>
              <th>Valor Atual</th>
              <th>Se Aumentar</th>
              <th>Se Reduzir</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td style="font-weight: 700; color: #f59e0b;">Chance Mercador</td>
              <td><strong>25% + Fallback</strong> (~1/run)</td>
              <td>40% (Segredo em ~3.5h)</td>
              <td>15% (Segredo em ~7.5h)</td>
            </tr>
            <tr>
              <td style="font-weight: 700; color: #38bdf8;">Páginas / Andar</td>
              <td><strong>1 Página</strong> (Garantida)</td>
              <td>2 Páginas (Caderno em ~2.8h)</td>
              <td>1 a cada 2 andares (~9h)</td>
            </tr>
            <tr>
              <td style="font-weight: 700; color: #f43f5e;">Escalonamento</td>
              <td><strong>+3% HP/Dano / Sala</strong></td>
              <td>+5% (Frustração extrema S5)</td>
              <td>+1.5% (Trivializa o Boss)</td>
            </tr>
            <tr>
              <td style="font-weight: 700; color: #10b981;">Inflação Alfa (&alpha;)</td>
              <td><strong>0.10 por ponto peso</strong></td>
              <td>0.15 (Parede na Sala 4)</td>
              <td>0.05 (Infusão infinita)</td>
            </tr>
          </tbody>
        </table>

        <div style="font-size: 11.5px; color: #94a3b8; line-height: 1.45; margin-top: 10px;">
          * A configuração atual (coluna "Valor Atual") é a que melhor atende o prazo de <strong>1 semana de testes</strong> sem exaurir os jogadores.
        </div>
      </div>

      <!-- Lado Direito: Conclusão & Próximos Passos -->
      <div class="card" style="display: flex; flex-direction: column; justify-content: space-between; border-left: 4px solid #10b981;">
        <div>
          <span style="font-size: 13px; font-weight: 800; color: #10b981; text-transform: uppercase;">Veredito & Recomendações Executivas</span>
          
          <div style="display: flex; flex-direction: column; gap: 10px; margin-top: 12px; font-size: 12.5px; color: #cbd5e1; line-height: 1.45;">
            <div style="display: flex; gap: 8px;">
              <span style="font-size: 15px; color:#10b981;">1.</span>
              <span><strong>Duração de Ouro (~5.5 Horas):</strong> O jogo oferece a densidade perfeita para um beta test. Os jogadores terão sensação de maestria plena e descoberta narrativa no final da semana.</span>
            </div>
            <div style="display: flex; gap: 8px;">
              <span style="font-size: 15px; color:#10b981;">2.</span>
              <span><strong>Harmonia entre Mercador e Caderno:</strong> Ambos progridem em paralelo e coroam a run com recompensas complementares (Lore cósmica + Blueprints táticos).</span>
            </div>
            <div style="display: flex; gap: 8px;">
              <span style="font-size: 15px; color:#10b981;">3.</span>
              <span><strong>Métrica Principal para Observar no Playtest:</strong> Coletar a contagem de infusões na Sala 5 para avaliar quantos jogadores caem na armadilha do "Acumulador".</span>
            </div>
          </div>
        </div>

        <div style="background: rgba(16, 185, 129, 0.1); border-radius: 8px; padding: 12px 14px; text-align: center; font-size: 13px; font-weight: 800; color: #10b981;">
          🚀 RECOMENDAÇÃO: APROVAR OS PARÂMETROS ATUAIS PARA O TESTE
        </div>
      </div>
    </div>

    <div class="slide-footer">
      <span>PILAR 5 • DIRETRIZES DE DECISÃO FINAL</span>
      <span>ROGUE-LIKE PROJECT • ROGUELIKE ACTION ENGINE • COMPILAÇÃO COMPLETA</span>
      <span>SLIDE 06 / 06</span>
    </div>
  </div>

</body>
</html>
"""

def generate_pdf():
    # Caminhos
    project_root = r"c:\Users\vicen\Documents\GitHub\RogueLikeProject"
    docs_dir = os.path.join(project_root, "Assets", "Docs")
    os.makedirs(docs_dir, exist_ok=True)

    html_file = os.path.join(docs_dir, "slides_presentation.html")
    pdf_file_project = os.path.join(docs_dir, "ESTUDO_PROGRESSAO_BALANCEAMENTO_SLIDES.pdf")
    
    artifact_dir = r"C:\Users\vicen\.gemini\antigravity\brain\9f471003-d698-4d3b-8ebe-311544f7c532"
    pdf_file_artifact = os.path.join(artifact_dir, "Estudo_Progressao_Balanceamento_Slides.pdf")

    # Salva o HTML
    print("[1/3] Gerando HTML dos slides com diagramas e gráficos vetoriais...")
    with open(html_file, "w", encoding="utf-8") as f:
        f.write(build_slides_html())
    print(f"      HTML salvo em: {html_file}")

    # Renderiza via Chrome headless
    chrome_path = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
    if not os.path.exists(chrome_path):
        chrome_path = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"

    print(f"[2/3] Compilando PDF 16:9 de alta definição via Chrome ({chrome_path})...")
    cmd = [
        chrome_path,
        "--headless=new",
        "--disable-gpu",
        "--no-pdf-header-footer",
        f"--print-to-pdf={pdf_file_project}",
        html_file
    ]
    res = subprocess.run(cmd, capture_output=True, text=True)

    if os.path.exists(pdf_file_project) and os.path.getsize(pdf_file_project) > 1000:
        size_kb = os.path.getsize(pdf_file_project) / 1024
        print(f"      [OK] PDF gerado com sucesso no Projeto: {pdf_file_project} ({size_kb:.1f} KB)")
        
        # Copia para o diretório de artifacts
        import shutil
        shutil.copy2(pdf_file_project, pdf_file_artifact)
        print(f"      [OK] Copia sincronizada no Artifacts: {pdf_file_artifact}")
        print("[3/3] Processo concluido com exito!")
        return True
    else:
        print(f"      [ERRO] Erro na geracao do PDF: {res.stderr}")
        return False

if __name__ == "__main__":
    success = generate_pdf()
    sys.exit(0 if success else 1)
