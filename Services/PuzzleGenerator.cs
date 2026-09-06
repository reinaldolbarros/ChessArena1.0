using ChessMAUI.Models;

namespace ChessMAUI.Services;

/// <summary>
/// Gera puzzles proceduralmente usando a engine existente.
/// 8 tipos: mate, garfo de cavalo, garfo de rainha, espeto, captura livre,
/// xeque revelado, defesa, última fileira, promoção.
/// Seed determinística: mesmo dia → mesmos puzzles.
/// </summary>
public static class PuzzleGenerator
{
    // ── Offsets ──────────────────────────────────────────────────────────────
    private static readonly (int dr, int dc)[] Knight =
        [(-2,-1),(-2,1),(-1,-2),(-1,2),(1,-2),(1,2),(2,-1),(2,1)];
    private static readonly (int dr, int dc)[] Diag  = [(-1,-1),(-1,1),(1,-1),(1,1)];
    private static readonly (int dr, int dc)[] Straight = [(-1,0),(1,0),(0,-1),(0,1)];
    private static readonly (int dr, int dc)[] All =
        [(-1,-1),(-1,1),(1,-1),(1,1),(-1,0),(1,0),(0,-1),(0,1)];

    // ── Pesos de cada tipo ────────────────────────────────────────────────────
    private static readonly (int weight, Func<Random, Puzzle?> gen, string label)[] _types =
    [
        (18, TryEndgameMate,      "Mate Endgame"),
        (16, TryMateIn2,          "Mate em 2"),
        (12, TryKnightFork,       "Garfo Cavalo"),
        (10, TryHangingCapture,   "Captura Livre"),
        (10, TrySkewer,           "Espeto"),
        ( 9, TryQueenFork,        "Garfo Rainha"),
        ( 8, TryDefensive,        "Defesa"),
        ( 7, TryBackRank,         "Última Fileira"),
        ( 5, TryDiscoveredCheck,  "Xeque Revelado"),
        ( 3, TryPromotionWin,     "Promoção"),
        ( 2, TryPinExploit,       "Pino"),
    ];

    // =========================================================================
    // API pública
    // =========================================================================
    public static List<Puzzle> GeneratePool(long baseSeed, int count)
    {
        var results = new List<Puzzle>(count);
        long attempt = 0;
        int  maxTry  = count * 200;
        while (results.Count < count && attempt < maxTry)
        {
            var p = TryGenerate(baseSeed + attempt);
            if (p != null) results.Add(p);
            attempt++;
        }
        return results;
    }

    // =========================================================================
    // Dispatcher por peso
    // =========================================================================
    private static Puzzle? TryGenerate(long seed)
    {
        var rng   = new Random((int)(seed & 0x7FFFFFFF));
        int total = _types.Sum(t => t.weight);
        int roll  = rng.Next(total);
        int acc   = 0;
        foreach (var (w, gen, _) in _types)
        {
            acc += w;
            if (roll < acc) return gen(rng);
        }
        return _types[0].gen(rng);
    }

    // =========================================================================
    // 1. MATE EM 1 — endgame (K+Q, K+R, K+2R, K+Q+R vs K)
    // =========================================================================
    private static Puzzle? TryEndgameMate(Random rng)
    {
        var b = EmptyBoard();
        int choice = rng.Next(4);

        bool ok = choice switch
        {
            0 => PlaceKQK(rng, b),
            1 => PlaceKRK(rng, b),
            2 => PlaceKRRK(rng, b),
            _ => PlaceKQRvsK(rng, b),
        };

        if (!ok || !ValidPosition(b)) return null;

        var mate = FindUniqueMate(b);
        if (mate == null) return null;

        string hint = choice switch
        {
            0 => "Rei e Rainha — mate em 1",
            1 => "Rei e Torre — mate em 1",
            2 => "Duas Torres — mate em 1",
            _ => "Rainha e Torre — mate em 1"
        };
        int diff = choice == 1 ? 2 : 1;

        return Make(b, [MoveToUci(mate)], PieceColor.White, "Xeque-mate", diff, hint);
    }

    // =========================================================================
    // 2. MATE EM 2 — único primeiro lance força mate independente da defesa
    // =========================================================================
    private static Puzzle? TryMateIn2(Random rng)
    {
        // Posições menores geram mate-em-2 mais rápido que middlegames densos
        var b = rng.Next(3) == 0
            ? BuildEndgameForMate2(rng)
            : BuildRandomMiddlegame(rng, 4, 10);
        if (b == null || !ValidPosition(b)) return null;

        var color = b.CurrentTurn;
        var opp   = Opponent(color);
        var allMoves = ChessEngine.GetAllLegalMoves(b, color);

        ChessMove?   winMove1   = null;
        ChessMove?   oppReply   = null;
        ChessMove?   winMove2   = null;
        int uniqueCount = 0;

        foreach (var m1 in allMoves)
        {
            var cl1 = b.Clone();
            ChessEngine.ApplyMove(cl1, m1);

            var st1 = ChessEngine.GetGameState(cl1);
            if (st1 == GameState.Checkmate) continue; // já seria mate-em-1
            if (st1 is GameState.Stalemate or GameState.Draw) continue;

            var oppMoves = ChessEngine.GetAllLegalMoves(cl1, opp);
            if (oppMoves.Count == 0 || oppMoves.Count > 6) continue;

            bool allMated    = true;
            ChessMove? bestReply = null;
            ChessMove? mate2     = null;
            int        bestOppOptions = -1;

            foreach (var oR in oppMoves)
            {
                var cl2 = cl1.Clone();
                ChessEngine.ApplyMove(cl2, oR);
                var m2 = FindUniqueMate(cl2);
                if (m2 == null) { allMated = false; break; }
                // Prefere a resposta que dá mais opções ao oponente (desafio maior)
                int opts = ChessEngine.GetAllLegalMoves(cl2, color).Count;
                if (opts > bestOppOptions) { bestOppOptions = opts; bestReply = oR; mate2 = m2; }
            }

            if (!allMated || bestReply == null || mate2 == null) continue;

            uniqueCount++;
            if (uniqueCount > 1) return null; // solução não única
            winMove1 = m1; oppReply = bestReply; winMove2 = mate2;
        }

        if (winMove1 == null) return null;

        return Make(b,
            [MoveToUci(winMove1), MoveToUci(oppReply!), MoveToUci(winMove2!)],
            color, "Mate em 2", 3,
            "Force o xeque-mate em 2 lances!");
    }

    private static ChessBoard? BuildEndgameForMate2(Random rng)
    {
        var b = EmptyBoard();
        // Rei branco + Rainha + Torre vs Rei negro: gera posições densas o suficiente para mate-2
        if (!TryPlace(rng, b, PieceType.King,  PieceColor.White, out int wkr, out int wkc)) return null;
        if (!TryPlace(rng, b, PieceType.King,  PieceColor.Black, out _, out _,
            minDist: 2, refR: wkr, refC: wkc)) return null;
        if (!TryPlace(rng, b, PieceType.Queen, PieceColor.White, out _, out _)) return null;
        if (!TryPlace(rng, b, PieceType.Rook,  PieceColor.White, out _, out _)) return null;
        b.CurrentTurn = PieceColor.White;
        return b;
    }

    // =========================================================================
    // 4. GARFO DE CAVALO — check + peça valiosa
    // =========================================================================
    private static Puzzle? TryKnightFork(Random rng)
    {
        // Quadrado do garfo (onde o cavalo chega)
        for (int attempt = 0; attempt < 30; attempt++)
        {
            int fr = rng.Next(1, 7), fc = rng.Next(1, 7);

            // Quadrados atacados a partir de F
            var fromF = KnightAttacks(fr, fc);
            if (fromF.Count < 3) continue;

            Shuffle(rng, fromF);

            // Rei negro em fromF[0], peça valiosa em fromF[1]
            var (bkr, bkc) = fromF[0];
            var (bvr, bvc) = fromF[1];
            if (Adjacent(bkr, bkc, bvr, bvc)) continue;

            // Cavalo branco: quadrado que pode MOVER para F
            var starts = KnightAttacks(fr, fc)
                .Where(s => s != (bkr, bkc) && s != (bvr, bvc))
                .ToList();
            if (!starts.Any()) continue;
            var (wnr, wnc) = starts[rng.Next(starts.Count)];

            // Rei branco: longe de tudo
            var wkPos = SafeKingSquare(rng, bkr, bkc,
                [(bkr,bkc),(bvr,bvc),(wnr,wnc),(fr,fc)]);
            if (wkPos == null) continue;
            var (wkr, wkc) = wkPos.Value;

            var b = EmptyBoard();
            b.SetPiece(bkr, bkc, Piece(PieceType.King,   PieceColor.Black));
            b.SetPiece(bvr, bvc, Piece(rng.Next(2) == 0 ? PieceType.Queen : PieceType.Rook, PieceColor.Black));
            b.SetPiece(wnr, wnc, Piece(PieceType.Knight, PieceColor.White));
            b.SetPiece(wkr, wkc, Piece(PieceType.King,   PieceColor.White));
            b.CurrentTurn = PieceColor.White;
            if (!ValidPosition(b)) continue;

            // Movimento do garfo: cavalo vai para F
            var legal = ChessEngine.GetAllLegalMoves(b, PieceColor.White);
            var forkMove = legal.FirstOrDefault(m => m.ToRow == fr && m.ToCol == fc
                                                  && m.FromRow == wnr && m.FromCol == wnc);
            if (forkMove == null) continue;

            // Após o movimento: deve estar em check E o cavalo ataca a peça valiosa
            var clone = b.Clone();
            ChessEngine.ApplyMove(clone, forkMove);
            if (ChessEngine.GetGameState(clone) != GameState.Check) continue;
            if (!KnightAttacks(fr, fc).Contains((bvr, bvc))) continue;

            // Solução única? (apenas esse garfo deve ser o melhor)
            var vt = b.GetPiece(bvr, bvc)!;
            string tgt = vt.Type == PieceType.Queen ? "rainha" : "torre";
            return Make(b, [MoveToUci(forkMove)], PieceColor.White,
                "Garfo", 2, $"Cavalo faz garfo — rei e {tgt} sob ataque!");
        }
        return null;
    }

    // =========================================================================
    // 3. CAPTURA LIVRE — peça desprotegida de alto valor
    // =========================================================================
    private static Puzzle? TryHangingCapture(Random rng)
    {
        var b = BuildRandomMiddlegame(rng, 6, 14);
        if (b == null) return null;

        var color = b.CurrentTurn;
        var opp   = Opponent(color);

        var allMoves = ChessEngine.GetAllLegalMoves(b, color);
        var winning  = new List<(ChessMove move, int gain, PieceType captured)>();

        foreach (var m in allMoves)
        {
            var target = b.GetPiece(m.ToRow, m.ToCol);
            if (target == null || target.Type == PieceType.King) continue;
            int val = PVal(target.Type);
            if (val < 3) continue; // só bispo/cavalo ou superior

            // Peça está desprotegida?
            if (ChessEngine.IsSquareAttacked(b, m.ToRow, m.ToCol, opp)) continue;

            // Quem captura não fica pendurado?
            var clone = b.Clone();
            ChessEngine.ApplyMove(clone, m);
            if (ChessEngine.IsSquareAttacked(clone, m.ToRow, m.ToCol, opp))
            {
                // Verifica se a troca ainda é lucrativa
                int capturerVal = PVal(b.GetPiece(m.FromRow, m.FromCol)!.Type);
                if (capturerVal >= val) continue; // perderia material
            }

            winning.Add((m, val, target.Type));
        }

        if (winning.Count != 1) return null; // precisa ter UMA opção vencedora clara

        var (move, _, ct) = winning[0];
        string name = ct switch
        {
            PieceType.Queen  => "rainha",  PieceType.Rook   => "torre",
            PieceType.Bishop => "bispo",   PieceType.Knight => "cavalo",
            _                => "peça"
        };
        return Make(b, [MoveToUci(move)], color, "Captura", 1,
            $"{name.ToUpper()[0]}{name[1..]} desprotegida — capture antes que seja tarde!");
    }

    // =========================================================================
    // 4. ESPETO — ataque em raio força peça valiosa a revelar outra
    // =========================================================================
    private static Puzzle? TrySkewer(Random rng)
    {
        var b = EmptyBoard();

        // Escolhe direção do espeto
        bool isDiag = rng.Next(2) == 0;
        var dirs = isDiag ? Diag : Straight;
        var (dr, dc) = dirs[rng.Next(dirs.Length)];

        // Peça atacante (rainha/torre/bispo)
        PieceType atkType = isDiag
            ? (rng.Next(2) == 0 ? PieceType.Queen : PieceType.Bishop)
            : (rng.Next(2) == 0 ? PieceType.Queen : PieceType.Rook);

        for (int attempt = 0; attempt < 40; attempt++)
        {
            int ar = rng.Next(8), ac = rng.Next(8);

            // Rei negro em alguma posição ao longo do raio
            int dist1 = rng.Next(1, 5);
            int bkr   = ar + dr * dist1;
            int bkc   = ac + dc * dist1;
            if (!ChessBoard.IsInBounds(bkr, bkc)) continue;

            // Peça valiosa atrás do rei
            int dist2 = rng.Next(1, 4);
            int bvr   = bkr + dr * dist2;
            int bvc   = bkc + dc * dist2;
            if (!ChessBoard.IsInBounds(bvr, bvc)) continue;

            // Validar que não estão sobrepostos
            if (bkr == ar && bkc == ac) continue;
            if (bvr == ar && bvc == ac) continue;
            if (bvr == bkr && bvc == bkc) continue;

            // Reis brancos e negros longe
            var wkPos = SafeKingSquare(rng, bkr, bkc,
                [(ar,ac),(bkr,bkc),(bvr,bvc)]);
            if (wkPos == null) continue;
            var (wkr, wkc) = wkPos.Value;

            var vt = rng.Next(2) == 0 ? PieceType.Queen : PieceType.Rook;

            b.ClearBoard();
            b.SetPiece(ar,  ac,  Piece(atkType, PieceColor.White));
            b.SetPiece(bkr, bkc, Piece(PieceType.King, PieceColor.Black));
            b.SetPiece(bvr, bvc, Piece(vt,              PieceColor.Black));
            b.SetPiece(wkr, wkc, Piece(PieceType.King,  PieceColor.White));
            b.CurrentTurn = PieceColor.White;
            if (!ValidPosition(b)) continue;

            // Verifica que o atacante pode atacar o rei ao longo do raio
            var legal = ChessEngine.GetAllLegalMoves(b, PieceColor.White);
            // Espeto: a peça atacante se move para ATACAR o rei ao longo da mesma diagonal/linha
            // Ela já está no raio — busca move que resulte em xeque
            var skMoves = legal
                .Where(m => m.FromRow == ar && m.FromCol == ac)
                .Where(m =>
                {
                    var cl = b.Clone();
                    ChessEngine.ApplyMove(cl, m);
                    return ChessEngine.GetGameState(cl) == GameState.Check;
                })
                .ToList();

            if (skMoves.Count != 1) continue;

            // Após o xeque, o rei deve fugir e deixar a peça atrás vulnerável
            var clone = b.Clone();
            ChessEngine.ApplyMove(clone, skMoves[0]);
            // O rei preto deve ter escape (não é mate, é espeto)
            if (ChessEngine.GetGameState(clone) == GameState.Checkmate) continue;

            string vName = vt == PieceType.Queen ? "rainha" : "torre";
            string aName = atkType switch
            {
                PieceType.Queen  => "Rainha", PieceType.Rook => "Torre",
                _ => "Bispo"
            };
            return Make(b, [MoveToUci(skMoves[0])], PieceColor.White,
                "Espeto", 2, $"{aName} faz espeto — rei cede e perde a {vName}!");
        }
        return null;
    }

    // =========================================================================
    // 5. GARFO DE RAINHA — ataca dois alvos desprotegidos simultaneamente
    // =========================================================================
    private static Puzzle? TryQueenFork(Random rng)
    {
        var b = BuildRandomMiddlegame(rng, 5, 12);
        if (b == null) return null;

        var color = b.CurrentTurn;
        var opp   = Opponent(color);
        var allMoves = ChessEngine.GetAllLegalMoves(b, color);

        var forkMoves = new List<ChessMove>();

        foreach (var m in allMoves)
        {
            var movingPiece = b.GetPiece(m.FromRow, m.FromCol);
            if (movingPiece?.Type != PieceType.Queen &&
                movingPiece?.Type != PieceType.Rook  &&
                movingPiece?.Type != PieceType.Bishop) continue;

            var cl = b.Clone();
            ChessEngine.ApplyMove(cl, m);

            // Conta alvos valiosos desprotegidos que a peça agora ataca
            int targets = CountValuableUndefendedTargets(cl, m.ToRow, m.ToCol,
                movingPiece.Type, opp, color);

            if (targets >= 2) forkMoves.Add(m);
        }

        if (forkMoves.Count != 1) return null;

        var fm    = forkMoves[0];
        var piece = b.GetPiece(fm.FromRow, fm.FromCol)!;
        string pn = piece.Type switch
        {
            PieceType.Queen => "Rainha", PieceType.Rook => "Torre", _ => "Bispo"
        };
        return Make(b, [MoveToUci(fm)], color, "Garfo", 2,
            $"{pn} ataca dois alvos — escolha um, perde o outro!");
    }

    // =========================================================================
    // 6. DEFESA — posição ameaçada, encontre o único lance salvador
    // =========================================================================
    private static Puzzle? TryDefensive(Random rng)
    {
        var b = BuildRandomMiddlegame(rng, 8, 18);
        if (b == null) return null;

        var color = b.CurrentTurn;
        var opp   = Opponent(color);

        // Deve haver uma ameaça clara: mate em 1 ou peça pendurada de alto valor
        bool hasThreats = HasImmediateThreat(b, opp, color);
        if (!hasThreats) return null;

        var allMoves = ChessEngine.GetAllLegalMoves(b, color);
        if (allMoves.Count < 2 || allMoves.Count > 6) return null; // muito fácil ou sem escolha

        // Encontra movimentos que SALVAM (o oponente não tem mais ameaça após eles)
        var saving = allMoves
            .Where(m =>
            {
                var cl = b.Clone();
                ChessEngine.ApplyMove(cl, m);
                return !HasImmediateThreat(cl, opp, color); // opp não tem mais ameaça
            })
            .ToList();

        if (saving.Count != 1) return null; // precisa de exatamente 1 lance salvador

        return Make(b, [MoveToUci(saving[0])], color, "Defesa", 2,
            "Ameaça iminente — encontre o único lance que salva!");
    }

    // =========================================================================
    // 7. ÚLTIMA FILEIRA — ataque na fileira 1 ou 8
    // =========================================================================
    private static Puzzle? TryBackRank(Random rng)
    {
        var b = EmptyBoard();

        // Rei negro na última fileira (row 0 = rank 8) cercado de peões
        bool blackOnTop = rng.Next(2) == 0;
        int bkRow = blackOnTop ? 0 : 7;
        int bkCol = rng.Next(1, 7);

        // Peões bloqueando o rei
        int pawnRow = blackOnTop ? 1 : 6;
        var pawnColor = PieceColor.Black;
        for (int c = Math.Max(0, bkCol - 1); c <= Math.Min(7, bkCol + 1); c++)
            b.SetPiece(pawnRow, c, Piece(PieceType.Pawn, pawnColor));

        // Torre ou rainha branca que vai dar mate na última fileira
        var atkType = rng.Next(2) == 0 ? PieceType.Rook : PieceType.Queen;
        int atkCol  = rng.Next(8);
        int atkRow  = rng.Next(1, 7); // não na última fileira ainda

        // Rei branco longe
        var wkPos = SafeKingSquare(rng, bkRow, bkCol,
            [(bkRow,bkCol),(atkRow,atkCol),(pawnRow,bkCol)]);
        if (wkPos == null) return null;
        var (wkr, wkc) = wkPos.Value;

        b.SetPiece(bkRow, bkCol, Piece(PieceType.King, PieceColor.Black));
        b.SetPiece(atkRow, atkCol, Piece(atkType,     PieceColor.White));
        b.SetPiece(wkr, wkc,      Piece(PieceType.King, PieceColor.White));
        b.CurrentTurn = PieceColor.White;
        if (!ValidPosition(b)) return null;

        var mate = FindUniqueMate(b);
        if (mate == null) return null;

        string an = atkType == PieceType.Rook ? "Torre" : "Rainha";
        return Make(b, [MoveToUci(mate)], PieceColor.White,
            "Última Fileira", 1,
            $"{an} na 8ª fileira — rei sem escape!");
    }

    // =========================================================================
    // 8. XEQUE REVELADO — peça se move e descobre ataque letal
    // =========================================================================
    private static Puzzle? TryDiscoveredCheck(Random rng)
    {
        var b = BuildRandomMiddlegame(rng, 6, 14);
        if (b == null) return null;

        var color = b.CurrentTurn;
        var opp   = Opponent(color);
        var allMoves = ChessEngine.GetAllLegalMoves(b, color);

        var discovered = new List<ChessMove>();
        foreach (var m in allMoves)
        {
            var movingPiece = b.GetPiece(m.FromRow, m.FromCol)!;

            // Peça que se move NÃO deve ser a que dá xeque (senão é xeque direto)
            var cl = b.Clone();
            ChessEngine.ApplyMove(cl, m);
            if (ChessEngine.GetGameState(cl) != GameState.Check) continue;

            // Quem está dando xeque no clone? Se for a peça que se moveu, é xeque direto
            var (kr, kc) = cl.FindKing(opp);
            bool directCheck = ChessEngine.IsSquareAttacked(cl, kr, kc,
                color) && IsAttackFromSquare(cl, m.ToRow, m.ToCol, kr, kc);

            if (directCheck) continue; // queremos revelado, não direto

            // Verifica que a peça movida ganha material ou tem algum valor
            var captured = b.GetPiece(m.ToRow, m.ToCol);
            if (captured != null && PVal(captured.Type) >= 3)
                discovered.Add(m); // captura + xeque revelado
            else if (cl.GetPiece(m.ToRow, m.ToCol)?.Type == PieceType.Pawn)
                continue; // move de peão sem captura é fraco
            else
                discovered.Add(m);
        }

        if (discovered.Count != 1) return null;

        var dm = discovered[0];
        return Make(b, [MoveToUci(dm)], color, "Xeque Revelado", 3,
            "Mova a peça e revele um ataque devastador!");
    }

    // =========================================================================
    // 9. PROMOÇÃO VENCEDORA — peão promove e ganha material ou dá mate
    // =========================================================================
    private static Puzzle? TryPromotionWin(Random rng)
    {
        var b = EmptyBoard();

        // Peão branco na penúltima fileira
        bool whitePawn = rng.Next(2) == 0;
        var pColor = whitePawn ? PieceColor.White : PieceColor.Black;
        var oColor = Opponent(pColor);

        int pawnRow  = whitePawn ? 1 : 6; // um passo do tabuleiro final
        int pawnCol  = rng.Next(8);
        int promoRow = whitePawn ? 0 : 7;

        // Rei do oponente perto da casa de promoção (para mate ou garfo)
        int bkCol  = Math.Clamp(pawnCol + rng.Next(-2, 3), 0, 7);
        int bkRow  = promoRow;
        if (bkRow == promoRow && bkCol == pawnCol) bkCol = (bkCol + 1) % 8;

        // Rei do pawn-side longe
        var wkPos = SafeKingSquare(rng, bkRow, bkCol,
            [(bkRow,bkCol),(pawnRow,pawnCol),(promoRow,pawnCol)]);
        if (wkPos == null) return null;
        var (wkr, wkc) = wkPos.Value;

        // Peça extra do oponente que pode ser atacada após promoção
        int exRow = rng.Next(8), exCol = rng.Next(8);
        var exType = rng.Next(2) == 0 ? PieceType.Queen : PieceType.Rook;

        b.SetPiece(pawnRow, pawnCol, Piece(PieceType.Pawn, pColor));
        b.SetPiece(bkRow,  bkCol,   Piece(PieceType.King, oColor));
        b.SetPiece(wkr,    wkc,     Piece(PieceType.King, pColor));
        if (exRow != bkRow || exCol != bkCol)
            b.SetPiece(exRow, exCol, Piece(exType, oColor));
        b.CurrentTurn = pColor;
        if (!ValidPosition(b)) return null;

        // Tenta encontrar promoção com solução única (mate ou captura)
        var mate = FindUniqueMate(b);
        if (mate != null && mate.PromotionPiece.HasValue)
            return Make(b, [MoveToUci(mate)], pColor, "Promoção", 1,
                "Promova o peão e dê xeque-mate!");

        // Ou promoção que ganha material
        var legal = ChessEngine.GetAllLegalMoves(b, pColor);
        var promoWins = legal
            .Where(m => m.PromotionPiece == PieceType.Queen)
            .Where(m =>
            {
                var cl = b.Clone();
                ChessEngine.ApplyMove(cl, m);
                // A rainha promovida ataca peça valiosa desprotegida?
                return CountValuableUndefendedTargets(cl, m.ToRow, m.ToCol,
                    PieceType.Queen, oColor, pColor) >= 1;
            })
            .ToList();

        if (promoWins.Count != 1) return null;

        return Make(b, [MoveToUci(promoWins[0])], pColor, "Promoção", 2,
            "Promova e ataque — a rainha decide o jogo!");
    }

    // =========================================================================
    // 10. PINO — peça presa na frente do rei
    // =========================================================================
    private static Puzzle? TryPinExploit(Random rng)
    {
        // Cria posição com pino: Bishop/Queen/Rook pina peça na frente do rei inimigo
        var b = EmptyBoard();

        bool isDiag = rng.Next(2) == 0;
        var dirs = isDiag ? Diag : Straight;
        var (dr, dc) = dirs[rng.Next(dirs.Length)];

        for (int attempt = 0; attempt < 30; attempt++)
        {
            // Peça atacante
            int ar = rng.Next(1, 7), ac = rng.Next(1, 7);
            // Peça pinada do inimigo
            int pr = ar + dr, pc = ac + dc;
            // Rei inimigo atrás da peça pinada
            int bkr = pr + dr, bkc = pc + dc;
            if (!ChessBoard.IsInBounds(pr, pc) || !ChessBoard.IsInBounds(bkr, bkc)) continue;

            // Rei branco longe
            var wkPos = SafeKingSquare(rng, bkr, bkc,
                [(ar,ac),(pr,pc),(bkr,bkc)]);
            if (wkPos == null) continue;
            var (wkr, wkc) = wkPos.Value;

            PieceType pinnerType = isDiag
                ? (rng.Next(2) == 0 ? PieceType.Queen : PieceType.Bishop)
                : (rng.Next(2) == 0 ? PieceType.Queen : PieceType.Rook);
            PieceType pinnedType = rng.Next(2) == 0 ? PieceType.Rook : PieceType.Knight;

            b.ClearBoard();
            b.SetPiece(ar, ac, Piece(pinnerType, PieceColor.White));
            b.SetPiece(pr, pc, Piece(pinnedType, PieceColor.Black));
            b.SetPiece(bkr, bkc, Piece(PieceType.King,  PieceColor.Black));
            b.SetPiece(wkr, wkc, Piece(PieceType.King,  PieceColor.White));
            // Peça extra branca que captura a pinada
            var capturer = rng.Next(3) switch
            {
                0 => PieceType.Rook,
                1 => PieceType.Bishop,
                _ => PieceType.Queen
            };
            // Posiciona o capturador adjacente à peça pinada em rota de ataque
            var capPos = KingNeighbors(pr, pc)
                .Where(s => s != (ar,ac) && s != (bkr,bkc) && s != (wkr,wkc))
                .OrderBy(_ => rng.Next()).FirstOrDefault();
            if (capPos == default) continue;
            b.SetPiece(capPos.Item1, capPos.Item2, Piece(capturer, PieceColor.White));

            b.CurrentTurn = PieceColor.White;
            if (!ValidPosition(b)) continue;

            // Encontra captura da peça pinada com solução única
            var legal = ChessEngine.GetAllLegalMoves(b, PieceColor.White);
            var pinCaptures = legal
                .Where(m => m.ToRow == pr && m.ToCol == pc)
                .Where(m =>
                {
                    var cl = b.Clone();
                    ChessEngine.ApplyMove(cl, m);
                    // A peça pinada não pode recapturar (pino!)
                    return !ChessEngine.GetAllLegalMoves(cl, PieceColor.Black)
                        .Any(bm => bm.ToRow == m.ToRow && bm.ToCol == m.ToCol);
                })
                .ToList();

            if (pinCaptures.Count != 1) continue;

            string pinnedName = pinnedType == PieceType.Rook ? "torre" : "cavalo";
            return Make(b, [MoveToUci(pinCaptures[0])], PieceColor.White,
                "Pino", 2, $"A {pinnedName} está presa — capture sem medo!");
        }
        return null;
    }

    // =========================================================================
    // Helpers de geração de posição
    // =========================================================================
    private static bool PlaceKQK(Random rng, ChessBoard b)
    {
        if (!TryPlace(rng, b, PieceType.King,  PieceColor.White, out int wkr, out int wkc)) return false;
        if (!TryPlace(rng, b, PieceType.King,  PieceColor.Black, out int bkr, out int bkc,
            minDist: 2, refR: wkr, refC: wkc)) return false;
        if (!TryPlace(rng, b, PieceType.Queen, PieceColor.White, out _, out _,
            minDist: 2, refR: bkr, refC: bkc)) return false;
        b.CurrentTurn = PieceColor.White;
        return true;
    }

    private static bool PlaceKRK(Random rng, ChessBoard b)
    {
        if (!TryPlace(rng, b, PieceType.King, PieceColor.White, out int wkr, out int wkc)) return false;
        if (!TryPlace(rng, b, PieceType.King, PieceColor.Black, out int bkr, out int bkc,
            minDist: 2, refR: wkr, refC: wkc, edgeOnly: true)) return false;
        if (!TryPlace(rng, b, PieceType.Rook, PieceColor.White, out _, out _)) return false;
        b.CurrentTurn = PieceColor.White;
        return true;
    }

    private static bool PlaceKRRK(Random rng, ChessBoard b)
    {
        if (!TryPlace(rng, b, PieceType.King, PieceColor.White, out int wkr, out int wkc)) return false;
        if (!TryPlace(rng, b, PieceType.King, PieceColor.Black, out _, out _,
            minDist: 2, refR: wkr, refC: wkc)) return false;
        if (!TryPlace(rng, b, PieceType.Rook, PieceColor.White, out _, out _)) return false;
        if (!TryPlace(rng, b, PieceType.Rook, PieceColor.White, out _, out _)) return false;
        b.CurrentTurn = PieceColor.White;
        return true;
    }

    private static bool PlaceKQRvsK(Random rng, ChessBoard b)
    {
        if (!TryPlace(rng, b, PieceType.King,  PieceColor.White, out int wkr, out int wkc)) return false;
        if (!TryPlace(rng, b, PieceType.King,  PieceColor.Black, out _, out _,
            minDist: 2, refR: wkr, refC: wkc)) return false;
        if (!TryPlace(rng, b, PieceType.Queen, PieceColor.White, out _, out _)) return false;
        if (!TryPlace(rng, b, PieceType.Rook,  PieceColor.White, out _, out _)) return false;
        b.CurrentTurn = PieceColor.White;
        return true;
    }

    private static bool TryPlace(Random rng, ChessBoard b, PieceType type, PieceColor color,
        out int row, out int col,
        int minDist = 0, int refR = -1, int refC = -1, bool edgeOnly = false)
    {
        row = col = -1;
        var cands = new List<(int r, int c)>(64);
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                if (b.GetPiece(r, c) != null) continue;
                if (edgeOnly && r != 0 && r != 7 && c != 0 && c != 7) continue;
                if (minDist > 0 && refR >= 0 &&
                    Math.Abs(r - refR) < minDist && Math.Abs(c - refC) < minDist) continue;
                cands.Add((r, c));
            }
        if (!cands.Any()) return false;
        (row, col) = cands[rng.Next(cands.Count)];
        b.SetPiece(row, col, new ChessPiece(type, color) { HasMoved = true });
        return true;
    }

    // Posição de meio-jogo por random walk
    private static ChessBoard? BuildRandomMiddlegame(Random rng, int minMoves, int maxMoves)
    {
        var b     = new ChessBoard();
        int moves = rng.Next(minMoves, maxMoves);

        for (int i = 0; i < moves; i++)
        {
            var legal = ChessEngine.GetAllLegalMoves(b, b.CurrentTurn);
            if (!legal.Any()) return null;
            ChessEngine.ApplyMove(b, legal[rng.Next(legal.Count)]);
            var st = ChessEngine.GetGameState(b);
            if (st != GameState.Normal && st != GameState.Check) return null;
        }
        return b;
    }

    // =========================================================================
    // Detecção de táticas
    // =========================================================================
    private static ChessMove? FindUniqueMate(ChessBoard board)
    {
        var all  = ChessEngine.GetAllLegalMoves(board, board.CurrentTurn);
        ChessMove? found = null;
        foreach (var m in all)
        {
            var cl = board.Clone();
            ChessEngine.ApplyMove(cl, m);
            if (ChessEngine.GetGameState(cl) == GameState.Checkmate)
            {
                if (found != null) return null;
                found = m;
            }
        }
        return found;
    }

    private static int CountValuableUndefendedTargets(ChessBoard board,
        int fromR, int fromC, PieceType pt, PieceColor enemy, PieceColor mySide)
    {
        int count = 0;
        // Simula os ataques da peça no clone (turno = mySide)
        board.CurrentTurn = mySide;
        var attacks = ChessEngine.GetAllLegalMoves(board, mySide)
            .Where(m => m.FromRow == fromR && m.FromCol == fromC)
            .Select(m => (m.ToRow, m.ToCol))
            .Distinct();

        foreach (var (tr, tc) in attacks)
        {
            var target = board.GetPiece(tr, tc);
            if (target == null || target.Color != enemy) continue;
            if (PVal(target.Type) < 3) continue;
            if (!ChessEngine.IsSquareAttacked(board, tr, tc, enemy))
                count++;
        }
        board.CurrentTurn = enemy; // restaura
        return count;
    }

    private static bool HasImmediateThreat(ChessBoard board, PieceColor attacker, PieceColor defender)
    {
        var saved = board.CurrentTurn;
        board.CurrentTurn = attacker;
        try
        {
            var atMoves = ChessEngine.GetAllLegalMoves(board, attacker);
            foreach (var m in atMoves)
            {
                var cl = board.Clone();
                ChessEngine.ApplyMove(cl, m);
                if (ChessEngine.GetGameState(cl) == GameState.Checkmate) return true;

                // Captura peça valiosa desprotegida?
                var tgt = board.GetPiece(m.ToRow, m.ToCol);
                if (tgt != null && PVal(tgt.Type) >= 5 &&
                    !ChessEngine.IsSquareAttacked(board, m.ToRow, m.ToCol, defender))
                    return true;
            }
            return false;
        }
        finally
        {
            board.CurrentTurn = saved;
        }
    }

    private static bool IsAttackFromSquare(ChessBoard board, int fr, int fc, int tr, int tc)
    {
        // Simplificado: verifica se a peça em (fr,fc) está na mesma linha/diagonal/L de (tr,tc)
        int dr = tr - fr, dc = tc - fc;
        if (dr == 0 || dc == 0 || Math.Abs(dr) == Math.Abs(dc)) return true; // rainha/torre/bispo
        if (KnightAttacks(fr, fc).Contains((tr, tc))) return true; // cavalo
        return false;
    }

    // =========================================================================
    // Utilitários
    // =========================================================================
    private static ChessBoard EmptyBoard()
    {
        var b = new ChessBoard
        {
            EnPassantCol  = -1, EnPassantRow  = -1,
            HalfMoveClock = 0,  FullMoveNumber = 1
        };
        b.ClearBoard();
        b.PositionHistory.Clear();
        return b;
    }

    private static bool ValidPosition(ChessBoard b)
    {
        var waiting = Opponent(b.CurrentTurn);
        return !ChessEngine.IsInCheck(b, waiting) &&
               !ChessEngine.IsInCheck(b, b.CurrentTurn);
    }

    private static Puzzle Make(ChessBoard board, string[] solution, PieceColor side,
        string category, int diff, string hint) =>
        new($"gen_{Guid.NewGuid():N}", board.ToFen(), solution, side, category, diff, hint);

    private static string MoveToUci(ChessMove m)
    {
        string uci = $"{(char)('a' + m.FromCol)}{8 - m.FromRow}{(char)('a' + m.ToCol)}{8 - m.ToRow}";
        if (m.PromotionPiece.HasValue)
            uci += m.PromotionPiece.Value switch
            {
                PieceType.Rook => "r", PieceType.Bishop => "b",
                PieceType.Knight => "n", _ => "q"
            };
        return uci;
    }

    private static ChessPiece Piece(PieceType t, PieceColor c) =>
        new(t, c) { HasMoved = true };

    private static PieceColor Opponent(PieceColor c) =>
        c == PieceColor.White ? PieceColor.Black : PieceColor.White;

    private static int PVal(PieceType t) => t switch
    {
        PieceType.Queen  => 9, PieceType.Rook   => 5,
        PieceType.Bishop => 3, PieceType.Knight => 3,
        PieceType.Pawn   => 1, _                => 100
    };

    private static List<(int r, int c)> KnightAttacks(int row, int col) =>
        Knight.Select(o => (row + o.dr, col + o.dc))
              .Where(s => ChessBoard.IsInBounds(s.Item1, s.Item2))
              .ToList();

    private static List<(int r, int c)> KingNeighbors(int row, int col) =>
        All.Select(o => (row + o.dr, col + o.dc))
           .Where(s => ChessBoard.IsInBounds(s.Item1, s.Item2))
           .ToList();

    private static (int r, int c)? SafeKingSquare(Random rng, int oppKr, int oppKc,
        IEnumerable<(int, int)> occupied)
    {
        var occ = occupied.ToHashSet();
        var cands = new List<(int r, int c)>();
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                if (occ.Contains((r, c))) continue;
                if (Math.Abs(r - oppKr) <= 1 && Math.Abs(c - oppKc) <= 1) continue;
                cands.Add((r, c));
            }
        if (!cands.Any()) return null;
        return cands[rng.Next(cands.Count)];
    }

    private static bool Adjacent(int r1, int c1, int r2, int c2) =>
        Math.Abs(r1 - r2) <= 1 && Math.Abs(c1 - c2) <= 1;

    private static void Shuffle<T>(Random rng, List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
