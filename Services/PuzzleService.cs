using ChessMAUI.Models;

namespace ChessMAUI.Services;

public record Puzzle(
    string Id,
    string Fen,
    string[] Solution,    // UCI moves, e.g. ["e2e4", "d7d5"]
    PieceColor PlayerColor,
    string Category,      // "Xeque-mate", "Garfo", "Forca", "Promoção", etc.
    int    Difficulty,    // 1=fácil 2=médio 3=difícil
    string Description);  // hint shown to user

public class PuzzleService
{
    public const int FreeLimit        = 4;
    private const int GeneratedTarget = 100; // puzzles gerados em background por lote

    private const string KeyDayDate  = "puzzle_day_date";
    private const string KeyDayStart = "puzzle_day_start";
    private const string KeyDayCount = "puzzle_day_count";

    public readonly LichessPuzzleService Online = new();

    // Pool gerado — mesclado com os pré-definidos
    private List<Puzzle>? _generatedPool;
    private Task?         _genTask;

    // Inicia geração assíncrona (chame ao inicializar AppState)
    public void BeginBackgroundGeneration()
    {
        if (_genTask != null) return;
        Online.BeginPrefetch();
        long seed = DateTime.Today.Ticks / TimeSpan.TicksPerDay;
        _genTask = Task.Run(() =>
        {
            _generatedPool = PuzzleGenerator.GeneratePool(seed * 997L, GeneratedTarget);
        });
    }

    // Pool completo: pré-definidos + gerados (disponíveis)
    private IReadOnlyList<Puzzle> FullPool =>
        _generatedPool is { Count: > 0 }
            ? [.. _puzzles, .. _generatedPool]
            : _puzzles;

    private static readonly Puzzle[] _puzzles =
    [
        new("p01",
            "r1bqkb1r/pppp1ppp/2n2n2/4p2Q/2B1P3/8/PPPP1PPP/RNB1K1NR w KQkq - 4 4",
            ["h5f7"],
            PieceColor.White,
            "Xeque-mate",  1,
            "Mate do Escolar — a rainha decide!"),

        new("p02",
            "r1b1kb1r/pppp1ppp/2n5/4p3/2BqP3/5N2/PPPP1PPP/RNBQK2R w KQkq - 0 5",
            ["f3d4"],
            PieceColor.White,
            "Garfo",  1,
            "Cavalo faz garfo — dois ataques de uma vez"),

        new("p03",
            "6k1/5ppp/8/8/8/8/5PPP/4R1K1 w - - 0 1",
            ["e1e8"],
            PieceColor.White,
            "Xeque-mate",  1,
            "Torre domina a coluna aberta"),

        new("p04",
            "r3k2r/ppp2ppp/2n5/3qp3/8/2N5/PPPP1PPP/R1BQK2R b KQkq - 0 1",
            ["d5d1"],
            PieceColor.Black,
            "Xeque-mate",  2,
            "Rainha invade a 1ª fileira"),

        new("p05",
            "2r3k1/5ppp/8/8/8/8/5PPP/2R3K1 b - - 0 1",
            ["c8c1"],
            PieceColor.Black,
            "Xeque-mate",  1,
            "Troca fatal na coluna c"),

        new("p06",
            "rnbqkbnr/ppp2ppp/8/3pp3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq d6 0 3",
            ["f3e5"],
            PieceColor.White,
            "Garfo",  2,
            "Cavalo captura e ameaça o rei"),

        new("p07",
            "4k3/8/4K3/8/8/8/8/4R3 w - - 0 1",
            ["e1e8"],
            PieceColor.White,
            "Xeque-mate",  1,
            "Mate de torre com rei auxiliar"),

        new("p08",
            "r2q1rk1/ppp2ppp/2n1bn2/3pp3/2B1P3/2NP1N2/PPP2PPP/R1BQR1K1 w - - 0 1",
            ["c4f7"],
            PieceColor.White,
            "Forca",  3,
            "Bispo sacrifica para ganhar material"),

        new("p09",
            "6k1/pp4pp/5n2/3r4/8/8/5PPP/6K1 b - - 0 1",
            ["d5d1"],
            PieceColor.Black,
            "Xeque-mate",  2,
            "Torre corta a fuga do rei"),

        new("p10",
            "8/6kp/6p1/8/8/6PP/7K/8 w - - 0 1",
            ["h3h4"],
            PieceColor.White,
            "Promoção",  2,
            "Crie um peão passado decisivo"),

        new("p11",
            "r1b2rk1/pppp1ppp/2n2n2/2b1p1B1/2B1P3/2N2N2/PPPP1PPP/R2QK2R w KQ - 0 1",
            ["d1d8"],
            PieceColor.White,
            "Xeque-mate",  3,
            "Rainha mergulha no labirinto negro"),

        new("p12",
            "5rk1/pp4pp/3b4/8/3n4/8/PP4PP/R4RK1 b - - 0 1",
            ["d4f3"],
            PieceColor.Black,
            "Garfo",  2,
            "Cavalo faz garfo na posição crítica"),

        new("p13",
            "r4rk1/ppp2ppp/2n5/3Np3/8/8/PPP2PPP/R4RK1 w - - 0 1",
            ["d5f6"],
            PieceColor.White,
            "Garfo",  2,
            "Cavalo ataca rei e torre ao mesmo tempo"),

        new("p14",
            "8/p7/8/8/8/8/P7/K1k5 w - - 0 1",
            ["a2a4"],
            PieceColor.White,
            "Promoção",  3,
            "Corrida de peões — quem chega primeiro?"),

        new("p15",
            "3r2k1/pp3ppp/8/3R4/8/8/PP3PPP/6K1 w - - 0 1",
            ["d5d8"],
            PieceColor.White,
            "Xeque-mate",  2,
            "Troca de torres leva ao mate"),

        new("p16",
            "8/8/8/3k4/8/3K4/8/3Q4 w - - 0 1",
            ["d1d5"],
            PieceColor.White,
            "Xeque-mate",  1,
            "Rainha e rei trabalham juntos"),

        new("p17",
            "r3r1k1/ppp2ppp/2n5/3q4/8/2N5/PPP2PPP/R2QR1K1 b - - 0 1",
            ["d5h1"],
            PieceColor.Black,
            "Xeque-mate",  3,
            "Diagonal mortal — rainha encontra seu alvo"),

        new("p18",
            "6k1/ppp2ppp/8/8/8/8/PPP2PPP/6K1 w - - 0 1",
            ["g1f2"],
            PieceColor.White,
            "Rei Ativo",  1,
            "Ative seu rei no final de jogo"),

        new("p19",
            "r1bqr1k1/pppp1ppp/2n2n2/4p3/2B1P3/2N2N2/PPPPQPPP/R1B1K2R w KQ - 0 1",
            ["e2e5"],
            PieceColor.White,
            "Garfo",  2,
            "Rainha captura e ameaça múltiplas peças"),

        new("p20",
            "8/8/1k6/8/8/1K6/8/1R6 w - - 0 1",
            ["b1b6"],
            PieceColor.White,
            "Xeque-mate",  1,
            "Mate de borda com torre"),

        new("p21",
            "r2q1rk1/pp1b1ppp/2p1pn2/3pN3/3P1B2/2N5/PPQ1PPPP/R4RK1 w - - 0 1",
            ["e5f7"],
            PieceColor.White,
            "Forca",  3,
            "Cavalo sacrifica para ganhar a rainha"),

        new("p22",
            "8/8/8/4k3/8/4K3/4P3/8 w - - 0 1",
            ["e2e4"],
            PieceColor.White,
            "Promoção",  2,
            "Oposição — avance seu peão com técnica"),

        new("p23",
            "6k1/5pp1/7p/8/8/8/5PP1/4Q1K1 w - - 0 1",
            ["e1e8"],
            PieceColor.White,
            "Xeque-mate",  1,
            "Rainha na 8ª fileira encerra o jogo"),

        new("p24",
            "6kr/8/8/6q1/8/8/7P/5RK1 w - - 0 1",
            ["g1f2"],
            PieceColor.White,
            "Defesa",  1,
            "Rei em cheque — fuja para a única casa segura"),

        new("p25",
            "r4rk1/1pp2ppp/p1n5/4q3/8/2N5/PPP2PPP/R1BQR1K1 w - - 0 1",
            ["c3e4"],
            PieceColor.White,
            "Garfo",  2,
            "Cavalo faz garfo na rainha e na torre"),
    ];

    // -----------------------------------------------------------------------
    // Persistência diária
    // -----------------------------------------------------------------------
    private void EnsureDay()
    {
        string today   = DateTime.Today.ToString("yyyy-MM-dd");
        string lastDay = Preferences.Default.Get(KeyDayDate, "");
        if (lastDay == today) return;

        // Novo dia: avança o índice inicial
        int prevStart = Preferences.Default.Get(KeyDayStart, 0);
        int poolSize  = FullPool.Count;
        int newStart  = poolSize > 0 ? (prevStart + FreeLimit) % poolSize : 0;
        Preferences.Default.Set(KeyDayStart, newStart);
        Preferences.Default.Set(KeyDayDate,  today);
        Preferences.Default.Set(KeyDayCount, 0);
    }

    public int GetDailyCount()
    {
        EnsureDay();
        return Preferences.Default.Get(KeyDayCount, 0);
    }

    public Puzzle GetCurrentPuzzle()
    {
        EnsureDay();
        var lichess = Online.GetCurrent();
        if (lichess != null) return lichess;

        var pool  = FullPool;
        int start = Preferences.Default.Get(KeyDayStart, 0);
        int count = Preferences.Default.Get(KeyDayCount, 0);
        return pool.Count > 0
            ? pool[(start + count) % pool.Count]
            : _puzzles[0]; // fallback
    }

    public void IncrementDailyCount()
    {
        Online.Advance(); // avança para o próximo puzzle Lichess inédito
        int count = Preferences.Default.Get(KeyDayCount, 0);
        Preferences.Default.Set(KeyDayCount, count + 1);
    }

    // Gratuito: até FreeLimit por dia. Assinantes e admins: ilimitado.
    public bool CanPlayMore(bool isSubscriber) =>
        isSubscriber || AppState.Current.IsAdminMode || GetDailyCount() < FreeLimit;

    // Tamanho do pool atual (para mostrar nas configurações/admin)
    public int PoolSize => FullPool.Count;
}
