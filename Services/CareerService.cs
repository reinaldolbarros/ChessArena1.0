using System.Text.Json;
using ChessMAUI.Models;

namespace ChessMAUI.Services;

public class CareerService
{
    private const string Key = "career_v3";

    private static readonly string[] Letters = ["A","B","C","D","E"];

    private static readonly string[][] CopaOpponentNames =
    [
        ["Korobov",  "Vachier-Lagrave", "Aronian"],
        ["Duda",     "Rapport",         "Nakamura"],
        ["So",       "Mamedyarov",      "Ding Liren"],
    ];

    // Elenco fixo dos Candidatos — sempre os mesmos 5, cada um com sua origem de
    // classificação. Fixo, não sorteado — só dá sentido a quem são os outros 5 jogadores,
    // sem precisar simular os outros torneios jogo a jogo. Difícil (3) é o teto aqui —
    // Hard (4) fica exclusivo do Campeonato Mundial.
    private static readonly (string Name, string Label, int Difficulty)[] CandidatosLineup =
    [
        ("Caruana",       "Campeão da Copa do Mundo",   3),
        ("Nepomniachtchi","Classificado do Grand Swiss", 3),
        ("Firouzja",      "Classificado do Grand Swiss", 3),
        ("Gukesh",        "Campeão do Grand Prix",      3),
        ("Carlsen",       "Maior rating médio",         3),
    ];

    // ── Persistence ───────────────────────────────────────────────────────────

    public CareerProgress Progress
    {
        get
        {
            var json = Preferences.Default.Get(Key, "");
            if (string.IsNullOrEmpty(json)) return new CareerProgress();
            try { return JsonSerializer.Deserialize<CareerProgress>(json) ?? new(); }
            catch { return new(); }
        }
    }

    public void Save(CareerProgress p)
        => Preferences.Default.Set(Key, JsonSerializer.Serialize(p));

    // ── Level configs ─────────────────────────────────────────────────────────

    // Escala de dificuldade: 1=Fácil, 2=Médio, 3=Difícil, 4=Hard (ver GetSkillLevel).
    // Local e Zonal só usam Fácil/Médio — nunca Difícil nem Hard. As fases seguintes
    // (Copa, Grand Swiss, Grand Prix, Candidatos) podem chegar até Difícil. Hard fica
    // reservado exclusivamente pro Campeonato Mundial (CreateMundial).
    private static int[] GetDiffs(CareerLevel level) => level switch
    {
        CareerLevel.Local      => [1,1,1,2,2],
        CareerLevel.Zonal      => [1,1,2,2,2],
        CareerLevel.GrandSwiss => [2,2,3,3,3],
        CareerLevel.GrandPrix  => [3,3,3,3,3],
        _                      => [2,2,3,3,3]
    };

    private static string[] GetPlayerNames(CareerLevel level) => Letters;

    private static int GetAdvancementSpots(CareerLevel level) => level switch
    {
        CareerLevel.Local      => 2,
        CareerLevel.Zonal      => 2,
        CareerLevel.GrandSwiss => 2,
        _                      => 1
    };

    // ── Creation ──────────────────────────────────────────────────────────────

    public CareerTournamentState CreateSwissTournament(CareerLevel level)
    {
        var diffs   = GetDiffs(level);
        var names   = GetPlayerNames(level);
        var players = new List<CareerPlayer>();
        for (int i = 0; i < diffs.Length; i++)
            players.Add(new CareerPlayer { Name = names[i], Difficulty = diffs[i] });
        players.Add(new CareerPlayer { Name = "Você", IsHuman = true, Difficulty = 3 });

        return new CareerTournamentState
        {
            Level            = level,
            Format           = CareerFormat.Swiss,
            TotalRounds      = diffs.Length,
            CurrentRound     = 1,
            AdvancementSpots = GetAdvancementSpots(level),
            Players          = players
        };
    }

    public CareerTournamentState CreateCandidatosTournament()
    {
        var players = CandidatosLineup
            .Select(p => new CareerPlayer { Name = p.Name, Difficulty = p.Difficulty, QualifiedVia = p.Label })
            .ToList();

        players.Add(new CareerPlayer { Name = "Você", IsHuman = true, Difficulty = 3, QualifiedVia = "Sua campanha" });

        return new CareerTournamentState
        {
            Level            = CareerLevel.Candidatos,
            Format           = CareerFormat.Swiss,
            TotalRounds      = players.Count - 1,
            CurrentRound     = 1,
            AdvancementSpots = GetAdvancementSpots(CareerLevel.Candidatos),
            Players          = players
        };
    }

    public CareerTournamentState CreateCopaMundo()
    {
        var set = CopaOpponentNames[Random.Shared.Next(CopaOpponentNames.Length)];
        // All 3 opponents stored upfront. Points = 0 means "current", -1 = "upcoming", -2 = "played".
        return new CareerTournamentState
        {
            Level        = CareerLevel.CopaMundo,
            Format       = CareerFormat.Elimination,
            TotalRounds  = 3,
            CurrentRound = 1,
            Players      =
            [
                new CareerPlayer { Name = "Você",   IsHuman = true },
                new CareerPlayer { Name = set[0], Difficulty = 2, Points = 0  },
                new CareerPlayer { Name = set[1], Difficulty = 2, Points = -1 },
                new CareerPlayer { Name = set[2], Difficulty = 3, Points = -1 }
            ]
        };
    }

    public CareerTournamentState CreateMundial()
    {
        return new CareerTournamentState
        {
            Level        = CareerLevel.Mundial,
            Format       = CareerFormat.BestOfN,
            TotalRounds  = 7,
            CurrentRound = 1,
            WinsNeeded   = 3,
            Players      =
            [
                new CareerPlayer { Name = "Você",   IsHuman = true, Difficulty = 4 },
                new CareerPlayer { Name = "Magnus",  Difficulty = 4 }
            ]
        };
    }

    // ── Pairing ───────────────────────────────────────────────────────────────

    public CareerPlayer GetNextOpponent(CareerTournamentState t)
    {
        if (t.Format == CareerFormat.Elimination)
        {
            return t.Players.FirstOrDefault(p => !p.IsHuman && p.Points >= 0)
                ?? t.Players.FirstOrDefault(p => !p.IsHuman)
                ?? new CareerPlayer { Name = "Oponente", Difficulty = 3 };
        }
        if (t.Format == CareerFormat.BestOfN)
            return t.Players.FirstOrDefault(p => !p.IsHuman)
                ?? new CareerPlayer { Name = "Magnus", Difficulty = 4 };

        double pts    = t.Human.Points;
        var    faced  = t.Rounds.Select(r => r.Opponent).ToHashSet();
        var    ai     = t.Players.Where(p => !p.IsHuman).ToList();
        if (ai.Count == 0) return new CareerPlayer { Name = "Oponente", Difficulty = 2 };
        var    unused = ai.Where(p => !faced.Contains(p.Name))
                         .OrderBy(p => Math.Abs(p.Points - pts))
                         .ThenByDescending(p => p.Points)
                         .ToList();
        return unused.Count > 0
            ? unused[0]
            : ai.OrderBy(p => Math.Abs(p.Points - pts)).First();
    }

    // ── Grava resultado e retorna próximo oponente (null = torneio encerrado) ──

    public (bool hasNext, CareerPlayer? nextOpponent) ProcessRoundResult(
        bool humanWon, bool isDraw, string opponentName)
    {
        var prog = Progress;
        if (prog.ActiveTournament == null) return (false, null);

        var result = humanWon ? CareerRoundResult.Win
                   : isDraw   ? CareerRoundResult.Draw
                   : CareerRoundResult.Loss;

        RecordRound(prog.ActiveTournament, opponentName, result);
        Save(prog);

        var fresh = Progress;
        var t     = fresh.ActiveTournament;
        if (t == null || t.IsCompleted) return (false, null);

        return (true, GetNextOpponent(t));
    }

    // Nome de verdade do título (Bicampeão, Tricampeão...), em vez de um genérico
    // "Nx Campeão" — vocabulário real do esporte/xadrez pra isso.
    public static string ChampionTitleName(int titles) => titles switch
    {
        1 => "Campeão Mundial",
        2 => "Bicampeão Mundial",
        3 => "Tricampeão Mundial",
        4 => "Tetracampeão Mundial",
        5 => "Pentacampeão Mundial",
        6 => "Hexacampeão Mundial",
        7 => "Heptacampeão Mundial",
        _ => $"{titles}× Campeão Mundial"
    };

    // Força real da IA (Skill Level do Stockfish, 0-20) — por ADVERSÁRIO, não por tempo de
    // busca nem por fase inteira. Fácil/Médio/Difícil/Hard é o mesmo conceito de dificuldade
    // já usado no modo casual, só que em 4 degraus em vez de 3 — Hard fica reservado pro
    // Campeonato Mundial (ver GetDiffs).
    public static int GetSkillLevel(int diff) => diff switch
    {
        1 => 3,   // Fácil
        2 => 10,  // Médio
        3 => 16,  // Difícil
        _ => 20   // Hard
    };

    public string DiffLabel(int diff) => diff switch
    {
        1 => "Fácil", 2 => "Médio", 3 => "Difícil", _ => "Hard"
    };

    // Estilo do adversário — determinístico a partir do nome, pra ficar sempre o mesmo
    // durante todo o torneio (e em qualquer revanche), sem precisar guardar mais nenhum
    // dado novo em CareerPlayer/no save do torneio.
    public static BotPersonality GetPersonality(string name)
    {
        int bucket = Math.Abs(name.GetHashCode()) % 3;
        return bucket switch
        {
            0 => BotPersonality.Aggressive,
            1 => BotPersonality.Solid,
            _ => BotPersonality.Balanced
        };
    }

    public static string PersonalityLabel(BotPersonality p) => p switch
    {
        BotPersonality.Aggressive => "Agressivo",
        BotPersonality.Solid      => "Defensivo",
        _                         => "Clássico"
    };

    public static string PersonalityIcon(BotPersonality p) => p switch
    {
        BotPersonality.Aggressive => "🔥",
        BotPersonality.Solid      => "🛡",
        _                         => "⚖"
    };

    public static string PersonalityColorHex(BotPersonality p) => p switch
    {
        BotPersonality.Aggressive => "#D85A30",
        BotPersonality.Solid      => "#639922",
        _                         => "#7F77DD"
    };

    // ── Recording ─────────────────────────────────────────────────────────────

    public void RecordRound(CareerTournamentState t, string opponentName, CareerRoundResult result)
    {
        switch (t.Format)
        {
            case CareerFormat.Swiss:       RecordSwissRound(t, opponentName, result);       break;
            case CareerFormat.Elimination: RecordEliminationRound(t, opponentName, result); break;
            case CareerFormat.BestOfN:     RecordBestOfNRound(t, result);                   break;
        }
    }

    private static void RecordSwissRound(CareerTournamentState t, string opponentName, CareerRoundResult result)
    {
        var opp = t.Players.FirstOrDefault(p => p.Name == opponentName)
               ?? new CareerPlayer { Name = opponentName, Difficulty = 2 };

        double h = result == CareerRoundResult.Win  ? 1.0
                 : result == CareerRoundResult.Draw ? 0.5 : 0.0;

        t.Human.Points += h;
        opp.Points     += 1.0 - h;
        t.Human.Faced.Add(opponentName);
        opp.Faced.Add("Você");

        t.Rounds.Add(new CareerRound
        {
            Number = t.CurrentRound, Opponent = opponentName,
            Difficulty = opp.Difficulty, Result = result
        });

        SimulateAIRound(t, opponentName);

        if (t.CurrentRound >= t.TotalRounds)
        {
            t.IsCompleted = true;
            int pos  = t.Standings.FindIndex(p => p.IsHuman) + 1;
            t.Outcome = pos <= t.AdvancementSpots
                ? CareerStageOutcome.Advanced
                : CareerStageOutcome.Eliminated;
        }
        else
        {
            t.CurrentRound++;
        }
    }

    // Cada rodada (Oitavas/Semi/Final) é uma mini-partida de melhor-de-2 — se empatar 1-1
    // nos 2 jogos, decide num desempate (jogo 3). Mais fiel à Copa do Mundo FIDE real (que
    // usa mini-match com rápidas/blitz de desempate) e menos brutal que "perdeu 1 jogo, saiu".
    private static void RecordEliminationRound(CareerTournamentState t, string opponentName, CareerRoundResult result)
    {
        var opp = t.Players.FirstOrDefault(p => p.Name == opponentName)
               ?? new CareerPlayer { Name = opponentName, Difficulty = 3 };

        bool isTiebreak = t.MiniMatchGame >= 3;

        if (isTiebreak)
        {
            // Desempate decisivo: só vitória avança, sem margem pra empate (tipo Armageddon)
            bool wonTiebreak = result == CareerRoundResult.Win;
            FinishMiniMatch(t, opp, opponentName, wonTiebreak);
            return;
        }

        double myScore = result == CareerRoundResult.Win  ? 1.0
                       : result == CareerRoundResult.Draw ? 0.5 : 0.0;
        t.MiniMatchMyScore  += myScore;
        t.MiniMatchOppScore += 1.0 - myScore;

        if (t.MiniMatchGame == 1)
        {
            // Sempre segue pro 2º jogo da mini-partida
            t.MiniMatchGame = 2;
            return;
        }

        // 2º jogo jogado — decide a mini-partida
        if (t.MiniMatchMyScore > t.MiniMatchOppScore)      FinishMiniMatch(t, opp, opponentName, won: true);
        else if (t.MiniMatchMyScore < t.MiniMatchOppScore) FinishMiniMatch(t, opp, opponentName, won: false);
        else t.MiniMatchGame = 3; // 1-1 — vai pro desempate
    }

    private static void FinishMiniMatch(CareerTournamentState t, CareerPlayer opp, string opponentName, bool won)
    {
        t.Rounds.Add(new CareerRound
        {
            Number = t.CurrentRound, Opponent = opponentName,
            Difficulty = opp.Difficulty, Result = won ? CareerRoundResult.Win : CareerRoundResult.Loss
        });

        // Reseta o placar da mini-partida pra próxima rodada
        t.MiniMatchGame      = 1;
        t.MiniMatchMyScore   = 0;
        t.MiniMatchOppScore  = 0;

        if (!won)
        {
            t.IsCompleted = true;
            t.Outcome     = CareerStageOutcome.Eliminated;
            return;
        }

        if (t.CurrentRound >= t.TotalRounds)
        {
            t.IsCompleted = true;
            t.Outcome     = CareerStageOutcome.AdvancedDirect;
        }
        else
        {
            // Mark current opponent as played (Points = -2 sentinel) and activate next
            opp.Points = -2;
            t.CurrentRound++;
            var next = t.Players.FirstOrDefault(p => !p.IsHuman && p.Points == -1);
            if (next != null) next.Points = 0;
        }
    }

    private static void RecordBestOfNRound(CareerTournamentState t, CareerRoundResult result)
    {
        var opp = t.Players.FirstOrDefault(p => !p.IsHuman)
               ?? new CareerPlayer { Name = "Magnus", Difficulty = 4 };

        if (result == CareerRoundResult.Win)       t.HumanWins++;
        else if (result == CareerRoundResult.Loss) t.HumanLosses++;
        // Draw: neither wins — extends the series

        t.Rounds.Add(new CareerRound
        {
            Number = t.CurrentRound, Opponent = opp.Name,
            Difficulty = opp.Difficulty, Result = result
        });
        t.CurrentRound++;

        if (t.HumanWins >= t.WinsNeeded)
        {
            t.IsCompleted = true;
            t.Outcome     = CareerStageOutcome.Advanced;
        }
        else if (t.HumanLosses >= t.WinsNeeded)
        {
            t.IsCompleted = true;
            t.Outcome     = CareerStageOutcome.Eliminated;
        }
        else if (t.CurrentRound > t.TotalRounds)
        {
            t.IsCompleted = true;
            t.Outcome     = t.HumanWins >= t.HumanLosses
                ? CareerStageOutcome.Advanced
                : CareerStageOutcome.Eliminated;
        }
    }

    private static void SimulateAIRound(CareerTournamentState t, string justPlayed)
    {
        var pool = t.Players.Where(p => !p.IsHuman && p.Name != justPlayed).ToList();
        for (int i = 0; i + 1 < pool.Count; i += 2)
        {
            double prob  = Math.Clamp(0.5 + (pool[i].Difficulty - pool[i+1].Difficulty) * 0.12, 0.15, 0.85);
            bool   aWins = Random.Shared.NextDouble() < prob;
            pool[i].Points   += aWins ? 1.0 : 0.0;
            pool[i+1].Points += aWins ? 0.0 : 1.0;
        }
        if (pool.Count % 2 == 1) pool.Last().Points += 0.5;
    }

    // ── Career start & progression ────────────────────────────────────────────

    public void StartCareer()
    {
        var p = new CareerProgress
        {
            CurrentLevel     = CareerLevel.Local,
            CycleYear        = 2024,
            ActiveTournament = CreateSwissTournament(CareerLevel.Local)
        };
        Save(p);
    }

    public void StartNewCycle(CareerProgress p)
    {
        p.IsCareerCompleted = false;
        p.CurrentLevel      = CareerLevel.Local;
        p.ZonalRetries      = 0;
        p.CycleYear         = p.EffectiveCycleYear + 2;
        p.TitlesWon++;
        p.ActiveTournament  = CreateSwissTournament(CareerLevel.Local);
        Save(p);
    }

    public void ApplyStageResult(CareerProgress p)
    {
        var t       = p.ActiveTournament;
        if (t == null) return;

        var outcome = t.Outcome;
        var level   = p.CurrentLevel;

        switch (level)
        {
            case CareerLevel.Local:
                if (outcome == CareerStageOutcome.Advanced)
                {
                    p.CurrentLevel     = CareerLevel.Zonal;
                    p.ActiveTournament = CreateSwissTournament(CareerLevel.Zonal);
                }
                else
                {
                    p.ActiveTournament = CreateSwissTournament(CareerLevel.Local);
                }
                break;

            case CareerLevel.Zonal:
                if (outcome == CareerStageOutcome.Advanced)
                {
                    p.ZonalRetries     = 0;
                    p.CurrentLevel     = CareerLevel.CopaMundo;
                    p.ActiveTournament = CreateCopaMundo();
                }
                else
                {
                    p.ZonalRetries++;
                    if (p.ZonalRetries >= 2)
                    {
                        p.ZonalRetries     = 0;
                        p.CurrentLevel     = CareerLevel.Local;
                        p.ActiveTournament = CreateSwissTournament(CareerLevel.Local);
                    }
                    else
                    {
                        p.ActiveTournament = CreateSwissTournament(CareerLevel.Zonal);
                    }
                }
                break;

            case CareerLevel.CopaMundo:
                if (outcome == CareerStageOutcome.AdvancedDirect)
                {
                    p.CurrentLevel     = CareerLevel.Candidatos;
                    p.ActiveTournament = CreateCandidatosTournament();
                }
                else
                {
                    p.CurrentLevel     = CareerLevel.GrandSwiss;
                    p.ActiveTournament = CreateSwissTournament(CareerLevel.GrandSwiss);
                }
                break;

            case CareerLevel.GrandSwiss:
                if (outcome == CareerStageOutcome.Advanced)
                {
                    p.CurrentLevel     = CareerLevel.Candidatos;
                    p.ActiveTournament = CreateCandidatosTournament();
                }
                else
                {
                    p.CurrentLevel     = CareerLevel.GrandPrix;
                    p.ActiveTournament = CreateSwissTournament(CareerLevel.GrandPrix);
                }
                break;

            case CareerLevel.GrandPrix:
                if (outcome == CareerStageOutcome.Advanced)
                {
                    p.CurrentLevel     = CareerLevel.Candidatos;
                    p.ActiveTournament = CreateCandidatosTournament();
                }
                else
                {
                    p.CurrentLevel     = CareerLevel.CopaMundo;
                    p.ActiveTournament = CreateCopaMundo();
                }
                break;

            case CareerLevel.Candidatos:
                if (outcome == CareerStageOutcome.Advanced)
                {
                    p.CurrentLevel     = CareerLevel.Mundial;
                    p.ActiveTournament = CreateMundial();
                }
                else
                {
                    p.CurrentLevel     = CareerLevel.CopaMundo;
                    p.ActiveTournament = CreateCopaMundo();
                }
                break;

            case CareerLevel.Mundial:
                if (outcome == CareerStageOutcome.Advanced)
                {
                    p.IsCareerCompleted = true;
                    p.ActiveTournament  = null;
                    // CycleYear e TitlesWon incrementados em StartNewCycle quando o jogador iniciar o próximo ciclo
                }
                else
                {
                    p.CurrentLevel     = CareerLevel.Candidatos;
                    p.ActiveTournament = CreateCandidatosTournament();
                }
                break;
        }

        Save(p);
    }

    // ── Destination description ───────────────────────────────────────────────

    public string NextDestinationText(CareerLevel level, CareerStageOutcome outcome) =>
        (level, outcome) switch
        {
            (CareerLevel.Local,      CareerStageOutcome.Advanced)       => "→ Torneio Zonal",
            (CareerLevel.Local,      _)                                 => "→ Tentar novamente",
            (CareerLevel.Zonal,      CareerStageOutcome.Advanced)       => "→ Copa do Mundo FIDE",
            (CareerLevel.Zonal,      _)                                 => "→ Nova tentativa no Zonal (2ª falha = volta ao Local)",
            (CareerLevel.CopaMundo,  CareerStageOutcome.AdvancedDirect) => "→ Candidatos (classificação direta!)",
            (CareerLevel.CopaMundo,  _)                                 => "→ Grand Swiss",
            (CareerLevel.GrandSwiss, CareerStageOutcome.Advanced)       => "→ Candidatos",
            (CareerLevel.GrandSwiss, _)                                 => "→ Grand Prix FIDE",
            (CareerLevel.GrandPrix,  CareerStageOutcome.Advanced)       => "→ Candidatos",
            (CareerLevel.GrandPrix,  _)                                 => "→ Copa do Mundo (nova tentativa)",
            (CareerLevel.Candidatos, CareerStageOutcome.Advanced)       => "→ Campeonato Mundial!",
            (CareerLevel.Candidatos, _)                                 => "→ Copa do Mundo (nova tentativa)",
            (CareerLevel.Mundial,    CareerStageOutcome.Advanced)       => "🏆 Campeão Mundial!",
            (CareerLevel.Mundial,    _)                                 => "→ Candidatos (nova tentativa)",
            _                                                           => ""
        };
}
