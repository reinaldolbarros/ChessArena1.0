using ChessMAUI.Models;

namespace ChessMAUI.Services;

public enum TournamentAlert { None, Bubble, InTheMoney }

public class TournamentService
{
    private static readonly string[] AiNames =
    [
        "Magnus Bot",    "Fischer AI",    "Kasparov X",    "Tal Ghost",
        "Deep Blue Jr",  "Capablanca AI", "Morphy Bot",    "Karpov Engine",
        "Anand AI",      "Kramnik Bot",   "Petrosian AI",  "Botvinnik Bot",
        "Smyslov AI",    "Spassky X",     "Alekhine Ghost","Nakamura Bot",
        "Caruana AI",    "Giri X",        "Nepo Bot",      "Mamedyarov AI",
        "Aronian X",     "Topalov Ghost", "Leko Bot",      "Ivanchuk AI",
        "Shirov Bot",    "Adams AI",      "Morozevich X",  "Polgar AI",
        "Kamsky Bot",    "Short AI",      "Gelfand X",     "Bareev Ghost",
        "Khalifman Bot", "Dreev AI",      "Svidler X",     "Grischuk AI",
        "Lputian Bot",   "Rublevsky AI",  "Timofeev X",    "Sjugirov Bot",
        "Sakaev AI",     "Korobov X",     "Vitiugov Bot",  "Lysyj AI",
        "Jakovenko X",   "Eljanov Bot",   "Fedoseev AI",   "Cheparinov X",
        "Nisipeanu Bot", "Motylev AI",    "Tomashevsky X", "Efimenko Bot",
        "Malakhov AI",   "Inarkiev X",    "Riazantsev Bot","Potkin AI",
        "Khismatullin X","Nepomniachtchi","Dubov Bot",     "Vidit AI",
        "Praggnanandhaa","Abdusattorov",  "Firouzja Bot",  "Esipenko AI"
    ];

    // -------------------------------------------------------------------------
    // Criação a partir da sala de matchmaking (fluxo online)
    // -------------------------------------------------------------------------
    public Tournament CreateFromRoom(List<RoomPlayer> roomPlayers, decimal buyIn,
                                     TournamentType type = TournamentType.Standard,
                                     decimal satelliteTarget = 0,
                                     decimal bountyPerPlayer = 0)
    {
        int size        = roomPlayers.Count;
        int totalRounds = (int)Math.Log2(size);

        var t = new Tournament
        {
            Size = size, BuyIn = buyIn, PrizePool = buyIn * size,
            TotalRounds = totalRounds, CurrentRound = 1,
            IsHeadsUp = size == 2,
            Status = TournamentStatus.Active,
            PrizeTable = BuildPrizeTable(size, buyIn * size),
            Type = type,
            SatelliteTarget = satelliteTarget,
            BountyPerPlayer = bountyPerPlayer
        };

        foreach (var rp in roomPlayers)
            t.Players.Add(new TournamentPlayer
            {
                Name     = rp.Name,
                IsHuman  = rp.IsHuman,
                Strength = rp.Strength,
                Avatar   = rp.IsHuman ? rp.Avatar : PickBotAvatar()
            });

        GenerateRound(t, t.Players.OrderBy(_ => Random.Shared.Next()).ToList(), 1);
        SimulateAIMatches(t);
        return t;
    }

    // -------------------------------------------------------------------------
    // Criação direta (legado)
    // -------------------------------------------------------------------------
    public Tournament Create(string humanName, int size, decimal buyIn,
                             string humanAvatar = "♟")
    {
        int totalRounds = (int)Math.Log2(size);
        var t = new Tournament
        {
            Size = size, BuyIn = buyIn, PrizePool = buyIn * size,
            TotalRounds = totalRounds, CurrentRound = 1,
            Status = TournamentStatus.Active,
            PrizeTable = BuildPrizeTable(size, buyIn * size)
        };

        t.Players.Add(new TournamentPlayer
        {
            Name = humanName, IsHuman = true, Strength = 6,
            Avatar = humanAvatar
        });

        var names = AiNames.OrderBy(_ => Random.Shared.Next()).Take(size - 1).ToArray();
        for (int i = 0; i < size - 1; i++)
        {
            int str = Random.Shared.Next(3, 10);
            t.Players.Add(new TournamentPlayer
            {
                Name = names[i], Strength = str,
                Avatar = PickBotAvatar()
            });
        }

        GenerateRound(t, t.Players.OrderBy(_ => Random.Shared.Next()).ToList(), 1);
        SimulateAIMatches(t);
        return t;
    }

    // -------------------------------------------------------------------------
    // Gera confrontos de uma rodada
    // -------------------------------------------------------------------------
    private static void GenerateRound(Tournament t, List<TournamentPlayer> players, int round)
    {
        for (int i = 0; i < players.Count; i += 2)
            t.Matches.Add(new TournamentMatch
            {
                Round = round, Player1 = players[i], Player2 = players[i + 1]
            });
    }

    // -------------------------------------------------------------------------
    // Simula partidas IA vs IA
    // -------------------------------------------------------------------------
    public void SimulateAIMatches(Tournament t)
    {
        foreach (var m in t.Matches.Where(m => m.Round == t.CurrentRound
                                            && !m.IsHumanMatch
                                            && m.Status == MatchStatus.Pending))
            Resolve(m, Simulate(m.Player1, m.Player2));
    }

    // -------------------------------------------------------------------------
    // Registra resultado humano
    // -------------------------------------------------------------------------
    public void RecordHumanResult(Tournament t, bool humanWon)
    {
        var m = t.CurrentHumanMatch;
        if (m == null) return;

        var human    = m.HumanPlayer!;
        var opponent = m.Opponent(human)!;

        // ── Heads-Up: melhor de 3 ─────────────────────────────────
        if (t.IsHeadsUp)
        {
            // Marca o jogo como completo sem eliminar ainda
            m.Winner = humanWon ? human : opponent;
            m.Loser  = humanWon ? opponent : human;
            m.Status = MatchStatus.Completed;

            if (humanWon) t.HumanSeriesWins++;
            else          t.OpponentSeriesWins++;

            if (t.HeadsUpSeriesDecided)
            {
                // Série decidida — elimina o perdedor definitivamente
                m.Loser.IsEliminated = true;
                if (!humanWon)
                {
                    t.Status = TournamentStatus.HumanEliminated;
                    SetFinalPosition(t, human);
                }
                else
                {
                    t.Status = TournamentStatus.HumanWon;
                    human.FinalPosition   = 1;
                    opponent.FinalPosition = 2;
                }
            }
            else
            {
                // Série não decidida — adiciona próximo jogo
                t.Matches.Add(new TournamentMatch
                {
                    Round   = t.CurrentRound,
                    Player1 = human,
                    Player2 = opponent
                });
            }
            return;
        }

        // ── Formato padrão (eliminação direta) ───────────────────
        Resolve(m, humanWon ? human : opponent);

        if (!humanWon)
        {
            t.Status = TournamentStatus.HumanEliminated;
            SetFinalPosition(t, human);
        }
    }

    // -------------------------------------------------------------------------
    // Avança rodada
    // -------------------------------------------------------------------------
    public bool AdvanceRound(Tournament t)
    {
        if (!t.CurrentRoundMatches.All(m => m.Status == MatchStatus.Completed)) return false;
        if (t.CurrentRound >= t.TotalRounds) return false;

        var winners = t.CurrentRoundMatches.Select(m => m.Winner!).ToList();
        t.CurrentRound++;
        GenerateRound(t, winners.OrderBy(_ => Random.Shared.Next()).ToList(), t.CurrentRound);
        SimulateAIMatches(t);
        return true;
    }

    // -------------------------------------------------------------------------
    // Verifica conclusão
    // -------------------------------------------------------------------------
    public bool CheckCompletion(Tournament t)
    {
        // HeadsUp: status já definido em RecordHumanResult
        if (t.IsHeadsUp)
            return t.Status is TournamentStatus.HumanWon or TournamentStatus.HumanEliminated;

        if (t.CurrentRound < t.TotalRounds) return false;
        if (t.CurrentRoundMatches.Any(m => m.Status == MatchStatus.Pending)) return false;

        var finalMatch = t.Matches.Last();
        if (finalMatch.Winner?.IsHuman == true)
        {
            t.Status = TournamentStatus.HumanWon;
            finalMatch.Winner.FinalPosition = 1;
            finalMatch.Loser!.FinalPosition = 2;
        }
        else
        {
            var human = t.HumanPlayer!;
            if (human.FinalPosition == 0) human.FinalPosition = 2;
        }
        return true;
    }

    // -------------------------------------------------------------------------
    // Bubble / ITM detection
    // -------------------------------------------------------------------------
    /// <summary>
    /// Retorna o tipo de alerta baseado na posição atual do humano.
    /// Bubble = próximo a ser eliminado antes das premiações.
    /// ITM    = acabou de entrar na zona de prêmios.
    /// </summary>
    public TournamentAlert CheckAlert(Tournament t)
    {
        var human = t.HumanPlayer;
        if (human == null || human.IsEliminated) return TournamentAlert.None;

        int remaining    = t.PlayersRemaining;
        int paidPositions = t.PrizeTable.Count;

        // ITM: restam exatamente as posições premiadas
        if (remaining <= paidPositions) return TournamentAlert.InTheMoney;

        // Bubble: próximo eliminado não recebe prêmio
        if (remaining == paidPositions + 1) return TournamentAlert.Bubble;

        return TournamentAlert.None;
    }

    public decimal GetHumanPrize(Tournament t)
    {
        var human = t.HumanPlayer;
        if (human == null || human.FinalPosition == 0) return 0;
        t.PrizeTable.TryGetValue(human.FinalPosition, out decimal prize);
        return prize;
    }

    public int GetAIDepth(Tournament t)
    {
        int roundsLeft = t.TotalRounds - t.CurrentRound;
        return roundsLeft switch { 0 => 3, 1 => 3, 2 => 2, _ => 1 };
    }

    // -------------------------------------------------------------------------
    // Sem rake — redistribuição total (fichas fictícias, casa lucra via assinatura)
    // -------------------------------------------------------------------------
    internal const decimal RakePct = 0m;

    public static decimal GetRakeAmount(int size, decimal buyIn) => 0m;

    // -------------------------------------------------------------------------
    // Tabela de prêmios — 100% do pool redistribuído
    // 3º lugar em 8j recebe o buy-in de volta; último pagante sempre ≥ 1× buy-in
    // -------------------------------------------------------------------------
    private static Dictionary<int, decimal> BuildPrizeTable(int size, decimal pool)
    {
        return size switch
        {
            2   => Dist(pool, (1,1m)),
            8   => Dist(pool, (1,.60m),(2,.26m),(3,.14m)),
            16  => Dist(pool, (1,.55m),(2,.25m),(3,.13m),(4,.07m)),
            32  => Dist(pool, (1,.45m),(2,.22m),(3,.13m),(4,.09m),(5,.06m),(6,.05m)),
            64  => Dist(pool, (1,.38m),(2,.22m),(3,.14m),(4,.10m),(5,.04m),(6,.04m),(7,.04m),(8,.04m)),
            128 => Dist(pool, (1,.28m),(2,.16m),(3,.11m),(4,.08m),(5,.06m),(6,.05m),
                              (7,.03m),(8,.03m),(9,.025m),(10,.025m),(11,.025m),(12,.025m),
                              (13,.025m),(14,.025m),(15,.025m),(16,.025m)),
            _   => Dist(pool, (1,1m))
        };
    }

    private static Dictionary<int, decimal> Dist(decimal pool, params (int pos, decimal pct)[] entries)
        => entries.ToDictionary(e => e.pos, e => Math.Round(pool * e.pct, 2));

    private static TournamentPlayer Simulate(TournamentPlayer p1, TournamentPlayer p2)
    {
        double p1Win = (double)p1.Strength / (p1.Strength + p2.Strength);
        return Random.Shared.NextDouble() < p1Win ? p1 : p2;
    }

    private static void Resolve(TournamentMatch m, TournamentPlayer winner)
    {
        m.Winner = winner;
        m.Loser  = m.Opponent(winner);
        m.Loser!.IsEliminated = true;
        m.Status = MatchStatus.Completed;
    }

    private static void SetFinalPosition(Tournament t, TournamentPlayer player)
        => player.FinalPosition = t.Players.Count(p => !p.IsEliminated) + 1;

    private static readonly string[] BotAvatars = ["🤖","🦾","⚙️","🔩","💻","🎯","🧠","🦿","🕹️","👾"];
    private static string PickBotAvatar() => BotAvatars[Random.Shared.Next(BotAvatars.Length)];
}
