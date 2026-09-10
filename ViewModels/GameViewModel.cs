using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ChessMAUI.Models;
using ChessMAUI.Services;

namespace ChessMAUI.ViewModels;

// ============================================================
// SquareViewModel — representa cada casa do tabuleiro
// ============================================================
public class SquareViewModel : INotifyPropertyChanged
{
    private string _pieceSymbol  = "";
    private bool?  _pieceIsWhite;          // null = casa vazia
    private bool   _isLight;
    private bool   _isSelected;
    private bool   _isValidMove;
    private bool   _isLastMove;
    private bool   _isInCheck;

    public int Row { get; }
    public int Col { get; }

    public string PieceSymbol
    {
        get => _pieceSymbol;
        set { _pieceSymbol = value; OnPC(); }
    }

    // Cor da peça — null quando a casa está vazia
    public bool? PieceIsWhite
    {
        get => _pieceIsWhite;
        set { _pieceIsWhite = value; OnPC(); OnPC(nameof(PieceTextColor)); OnPC(nameof(PieceShadowColor)); }
    }

    // Peças brancas: marfim com sombra escura. Pretas: carvão com sombra clara.
    public Color PieceTextColor => _pieceIsWhite == true
        ? Color.FromArgb("#F5F0DC")   // marfim
        : Color.FromArgb("#1C1C2C");  // carvão

    public Color PieceShadowColor => _pieceIsWhite == true
        ? Color.FromArgb("#AA3B2000") // sombra castanha semitransparente
        : Color.FromArgb("#88FFFFFF"); // brilho branco semitransparente

    public bool IsLight
    {
        get => _isLight;
        set { _isLight = value; OnPC(); OnPC(nameof(BackgroundColor)); }
    }
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPC(); OnPC(nameof(BackgroundColor)); }
    }
    public bool IsValidMove
    {
        get => _isValidMove;
        set { _isValidMove = value; OnPC(); OnPC(nameof(BackgroundColor)); }
    }
    public bool IsLastMove
    {
        get => _isLastMove;
        set { _isLastMove = value; OnPC(); OnPC(nameof(BackgroundColor)); }
    }
    public bool IsInCheck
    {
        get => _isInCheck;
        set { _isInCheck = value; OnPC(); OnPC(nameof(BackgroundColor)); }
    }

    public Color BackgroundColor
    {
        get
        {
            var (light, dark) = BoardThemeService.BoardColors;
            return (IsSelected, IsInCheck, IsLight) switch
            {
                (true, _, _) => Color.FromArgb("#F6F669"),
                (_, true, _) => Color.FromArgb("#FF4444"),
                (_, _, true) => light,
                _            => dark,
            };
        }
    }

    public void RefreshTheme()
    {
        OnPC(nameof(BackgroundColor));
    }

    public SquareViewModel(int row, int col)
    {
        Row = row; Col = col;
        IsLight = (row + col) % 2 == 0;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPC([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

// ============================================================
// GameViewModel — lógica principal do jogo
// ============================================================
public class GameViewModel : INotifyPropertyChanged
{
    // --- Estado do jogo ---
    private ChessBoard        _board = new();
    private SquareViewModel?  _selectedSquare;
    private List<ChessMove>   _validMoves   = [];
    private ChessMove?        _lastMove;
    private string            _statusMessage = "Toque em 'Novo Jogo' para começar";
    private bool              _gameOver      = true;
    private bool              _isAIThinking;
    private bool              _drawRefusedThisTurn;
    private bool              _awaitingPromotion;
    private ChessMove?        _pendingPromotion;

    // --- Temporizador ---
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    private IDispatcherTimer? _clock;
    private TimeSpan          _whiteTime;
    private TimeSpan          _blackTime;
    private bool              _timerEnabled;
    private TimeSpan          _moveTime;
    private TimeSpan          _moveTimeLimit;   // limite configurado conforme duração do jogo
    private bool              _moveTimerActive; // false quando jogo é de 1 min

    // --- Serviços ---
    private AIService                _ai           = new();
    private readonly SoundService    _sound        = new();
    private readonly BotChatService  _chat         = new();
    private CancellationTokenSource? _aiCts;
    private int                      _aiThinkMaxMs = 4000; // limite de tempo para a IA pensar
    private readonly List<string>    _uciMoveHistory = [];
    private int                      _sfSkillLevel   = 20;
    private int                      _sfMoveTimeMs   = 1000;
    private int                      _aiDepthLevel   = 3; // 1=Fácil, 3=Médio, 5=Difícil — só controla profundidade/tempo
    private BotPersonality           _aiPersonality  = BotPersonality.Balanced;
    private readonly Random          _personalityRng = new();

    // --- Peças capturadas e lista de lances ---
    private readonly List<ChessPiece> _capturedByWhite = []; // peças pretas capturadas pelo jogador
    private readonly List<ChessPiece> _capturedByBlack = []; // peças brancas capturadas pela IA
    private readonly List<string>     _moves            = [];

    // --- Análise pós-jogo: instantâneos antes de cada lance do jogador ---
    private record PlayerMoveRecord(ChessBoard BoardBefore, ChessMove Move, int HalfMoveIndex);
    private readonly List<PlayerMoveRecord> _playerMoveSnapshots = [];

    // --- Revisão completa: todos os lances (jogador + IA) ---
    private record AllMoveRecord(ChessBoard BoardBefore, ChessBoard BoardAfter, ChessMove Move, string Notation, bool IsPlayerMove, int HalfMoveIndex, int StaticEval = 0);
    private readonly List<AllMoveRecord> _allMoveSnapshots = [];

    public enum MoveQuality     { Good, Inaccuracy, Mistake, Blunder }
    public enum TacticalErrorType { MissedMate, MissedFreeCapture, HangingPiece, BlunderedPiece, General }

    public record MoveAnalysisItem(
        int              MoveNumber,
        string           Description,
        MoveQuality      Quality,
        TacticalErrorType ErrorType,
        ChessBoard       BoardBefore,
        ChessMove        PlayerMove,
        ChessMove        BestMove,
        int              CpLoss);

    // ----------------------------------------------------------------
    // Tabuleiro visual
    // ----------------------------------------------------------------
    public SquareViewModel[,] Squares   { get; } = new SquareViewModel[8, 8];
    public List<SquareViewModel> SquareList { get; } = [];

    // ----------------------------------------------------------------
    // Propriedades vinculadas ao XAML
    // ----------------------------------------------------------------
    public string StatusMessage
    {
        get => _statusMessage;
        private set { _statusMessage = value; OnPC(); }
    }

    public bool GameOver
    {
        get => _gameOver;
        private set { _gameOver = value; OnPC(); OnPC(nameof(ShowMoveTimer)); OnPC(nameof(ShowResignButton)); OnPC(nameof(CanOfferDraw)); OfferDrawCommand?.ChangeCanExecute(); }
    }

    public bool IsAIThinking
    {
        get => _isAIThinking;
        private set { _isAIThinking = value; OnPC(); OnPC(nameof(CanOfferDraw)); OfferDrawCommand?.ChangeCanExecute(); }
    }

    public bool AwaitingPromotion
    {
        get => _awaitingPromotion;
        private set { _awaitingPromotion = value; OnPC(); }
    }

    // Temporizador total
    public string  WhiteTimeDisplay => _timerEnabled ? FormatTime(_whiteTime) : "--:--";
    public string  BlackTimeDisplay => _timerEnabled ? FormatTime(_blackTime) : "--:--";
    public bool    TimerVisible     => _timerEnabled;
    public bool    IsWhiteLowTime   => _timerEnabled && _whiteTime.TotalSeconds < 30 && _whiteTime.TotalSeconds > 0;
    public bool    IsBlackLowTime   => _timerEnabled && _blackTime.TotalSeconds < 30 && _blackTime.TotalSeconds > 0;

    // Temporizador por jogada
    public string  MoveTimeDisplay  => FormatTime(_moveTime);
    public bool    IsMoveTimeLow    => _moveTime.TotalSeconds < (_moveTimeLimit.TotalSeconds * 0.25);
    public bool    ShowMoveTimer    => _moveTimerActive;

    // Modo torneio
    public bool   IsTournamentMode    { get; private set; }
    public string TournamentOpponent  { get; private set; } = "";
    public bool   ShowNewGameButton   => !IsTournamentMode;
    public bool?  HumanWon            { get; private set; }

    // Modo amigo (pass-and-play)
    public bool   IsFriendMode       { get; private set; }
    public string WhitePlayerName    { get; private set; } = "Você";
    public string BlackPlayerName    { get; private set; } = "IA";
    public event Action<string>? RequestHandoff;

    public int MoveCount => _allMoveSnapshots.Count;

    // Som
    public bool SoundEnabled
    {
        get => _sound.Enabled;
        set { _sound.Enabled = value; OnPC(); }
    }

    // ----------------------------------------------------------------
    // Comandos
    // ----------------------------------------------------------------
    public ICommand SquareTappedCommand { get; }
    public ICommand PromoteCommand      { get; }
    public ICommand ResignCommand       { get; }
    public Command  OfferDrawCommand    { get; }

    public bool ShowResignButton => !_gameOver && !IsFriendMode;
    public bool CanOfferDraw    => !IsFriendMode && !_gameOver && !_isAIThinking && !_drawRefusedThisTurn;

    // Peças capturadas e vantagem de material
    public string WhiteCapturesDisplay { get; private set; } = "";
    public string BlackCapturesDisplay { get; private set; } = "";
    public string MaterialDisplay      { get; private set; } = "";
    public Color  MaterialColor        { get; private set; } = Colors.White;

    // Lista de lances (notação algébrica)
    public string MoveListText { get; private set; } = "";

    public event Action?               BoardChanged;
    public event Action<string>?       PromotionRequested;
    public event Action<string>?       ChatMessageReceived;
    public event Action<bool>?         TournamentGameEnded;
    public event Func<Task<bool>>?     ResignRequested;          // retorna true se confirmado
    public event Func<Task<bool>>?     DrawOfferRequested;        // retorna true se aceito pela IA

    // ----------------------------------------------------------------
    // Construtor
    // ----------------------------------------------------------------
    public GameViewModel()
    {
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                var sq = new SquareViewModel(r, c);
                Squares[r, c] = sq;
                SquareList.Add(sq);
            }

        SquareTappedCommand = new Command<SquareViewModel>(OnSquareTapped);
        PromoteCommand      = new Command<string>(OnPromote);
        ResignCommand       = new Command(async () => await OnResign());
        OfferDrawCommand    = new Command(async () => await OnOfferDraw(), () => CanOfferDraw);
        _chat.MessageReceived += msg => ChatMessageReceived?.Invoke(msg);

        RefreshBoard();
    }

    // ----------------------------------------------------------------
    // ----------------------------------------------------------------
    // Modo torneio — chamado pela GamePage quando IsInTournamentMatch
    // ----------------------------------------------------------------
    public void StartTournamentGame(string opponentName, int minutes, int aiDepth, int? skillLevel = null, BotPersonality personality = BotPersonality.Balanced)
    {
        TournamentOpponent = opponentName;
        StartNewGame(minutes, aiDepth, isTournament: true, skillLevel: skillLevel, personality: personality);
    }

    public void StartFriendGame(string white, string black, int minutes)
    {
        WhitePlayerName = white;
        BlackPlayerName = black;
        StartNewGame(minutes, aiDepth: 1, isTournament: false, friendMode: true);
    }

    private static ChessMove? UciToMove(ChessBoard board, string uci)
    {
        if (uci.Length < 4) return null;
        int fc = uci[0] - 'a';
        int fr = 8 - (uci[1] - '0');
        int tc = uci[2] - 'a';
        int tr = 8 - (uci[3] - '0');
        if (fc < 0 || fc > 7 || fr < 0 || fr > 7 || tc < 0 || tc > 7 || tr < 0 || tr > 7) return null;

        var legal = ChessEngine.GetLegalMoves(board, fr, fc);
        ChessMove? move = legal.FirstOrDefault(m => m.ToRow == tr && m.ToCol == tc);

        if (move != null && uci.Length == 5)
        {
            move.PromotionPiece = uci[4] switch
            {
                'q' => PieceType.Queen,
                'r' => PieceType.Rook,
                'b' => PieceType.Bishop,
                'n' => PieceType.Knight,
                _   => PieceType.Queen
            };
        }

        return move;
    }

    // Usado só pela seleção de lance por personalidade (ver PersonalityMoveSelector) — diz se
    // um candidato do Stockfish é uma captura na posição atual, pra dar preferência a lances
    // "com a cara" de um bot Agressivo/Sólido.
    private bool IsCaptureUci(string uci)
    {
        var m = UciToMove(_board, uci);
        return m != null && (m.IsEnPassant || _board.GetPiece(m.ToRow, m.ToCol) != null);
    }

    // Conta quantas peças brancas ficam ameaçadas (atacadas por uma peça preta) depois de
    // jogar esse candidato — proxy simples de "quão ameaçador" o lance é, usado só pelo
    // Agressivo. Simula a jogada num clone do tabuleiro; não afeta a partida real.
    private int CountThreatsUci(string uci)
    {
        var m = UciToMove(_board, uci);
        if (m == null) return 0;

        var clone = _board.Clone();
        ChessEngine.ApplyMove(clone, m);

        int threats = 0;
        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
        {
            var p = clone.GetPiece(r, c);
            if (p != null && p.Color == PieceColor.White && ChessEngine.IsSquareAttacked(clone, r, c, PieceColor.Black))
                threats++;
        }
        return threats;
    }

    private static string MoveToUci(ChessMove move)
    {
        var sb = new System.Text.StringBuilder(5);
        sb.Append((char)('a' + move.FromCol));
        sb.Append((char)('0' + (8 - move.FromRow)));
        sb.Append((char)('a' + move.ToCol));
        sb.Append((char)('0' + (8 - move.ToRow)));
        if (move.PromotionPiece.HasValue)
            sb.Append(move.PromotionPiece.Value switch
            {
                PieceType.Queen  => 'q',
                PieceType.Rook   => 'r',
                PieceType.Bishop => 'b',
                _                => 'n'
            });
        return sb.ToString();
    }

    // ----------------------------------------------------------------
    // Novo jogo — chamado pela GamePage após o usuário escolher tempo e dificuldade
    // ----------------------------------------------------------------
    public void StartNewGame(int minutes, int aiDepth = 3, bool isTournament = false, bool friendMode = false, int? skillLevel = null, BotPersonality personality = BotPersonality.Balanced)
    {
        _aiPersonality = personality;
        _aiCts?.Cancel();
        _aiCts = null;
        _ai    = new AIService(aiDepth);

        // Limite de tempo de raciocínio por profundidade
        _aiThinkMaxMs = aiDepth switch
        {
            1 => 800,
            3 => 2500,
            _ => 8000,  // depth 5 → hard
        };

        _board            = new ChessBoard();
        _selectedSquare   = null;
        _lastMove         = null;
        _pendingPromotion = null;
        _validMoves.Clear();
        _capturedByWhite.Clear();
        _capturedByBlack.Clear();
        _moves.Clear();
        _playerMoveSnapshots.Clear();
        _allMoveSnapshots.Clear();
        _uciMoveHistory.Clear();
        _aiDepthLevel = aiDepth;
        // Skill Level sempre no máximo por padrão: a IA nunca ignora tática forçada nem mate
        // por "burrice" artificial. A dificuldade varia só pelo tempo de busca (profundidade
        // posicional) — ver ApplyStockfishMoveAsync para a exceção controlada do Fácil (raro
        // lance de peão solto). Exceção: Modo Carreira passa um skillLevel específico por fase
        // da pirâmide (ver CareerService.GetSkillLevel), pra ter uma curva real de dificuldade
        // entre o Torneio Local e o Campeonato Mundial.
        _sfSkillLevel = skillLevel ?? 20;
        // Com Skill Level sempre no máximo + hash/threads adequados, o Stockfish já joga muito
        // forte em buscas curtas — não precisa de vários segundos pra ser um adversário sério.
        _sfMoveTimeMs = aiDepth switch { 1 => 1000, 3 => 1500, _ => 2000 };
        UpdateCapturesDisplay();
        UpdateMoveList();
        AwaitingPromotion    = false;
        IsAIThinking         = false;
        _drawRefusedThisTurn = false;
        GameOver             = false;
        HumanWon          = null;
        IsTournamentMode  = isTournament;
        IsFriendMode      = friendMode;
        if (!friendMode) { WhitePlayerName = "Você"; BlackPlayerName = "IA"; }
        OnPC(nameof(IsTournamentMode));
        OnPC(nameof(TournamentOpponent));
        OnPC(nameof(ShowNewGameButton));
        OnPC(nameof(CanOfferDraw));
        OfferDrawCommand.ChangeCanExecute();

        // Configura temporizador
        _timerEnabled = minutes > 0;
        _whiteTime    = TimeSpan.FromMinutes(minutes);
        _blackTime    = TimeSpan.FromMinutes(minutes);
        NotifyTimerProperties();

        // Define limite por jogada conforme duração total:
        // 0 (sem limite) ou 1 min → sem contador por jogada
        // 2 min → 30 s por jogada
        // 3+ min → 2 min por jogada
        _moveTimerActive = minutes >= 2;
        _moveTimeLimit   = minutes == 2 ? TimeSpan.FromSeconds(30) : TimeSpan.FromMinutes(2);
        _moveTime        = _moveTimeLimit;
        OnPC(nameof(MoveTimeDisplay));
        OnPC(nameof(IsMoveTimeLow));
        OnPC(nameof(ShowMoveTimer));

        ClearHighlights();
        RefreshBoard();

        // Clock roda se há timer total OU contador por jogada
        if (_timerEnabled || _moveTimerActive)
            StartClock();
        else
            StopClock();

        StatusMessage = _timerEnabled
            ? (IsFriendMode ? $"{WhitePlayerName} começa — {minutes} min" : $"Brancas jogam — {minutes} min por lado")
            : (IsFriendMode ? $"Vez de {WhitePlayerName}" : "Vez das Brancas");

        if (IsTournamentMode) _chat.SendStart();
    }

    // ----------------------------------------------------------------
    // Relógio
    // ----------------------------------------------------------------
    private void StartClock()
    {
        StopClock();
        _clock          = Application.Current!.Dispatcher.CreateTimer();
        _clock.Interval = OneSecond;
        _clock.Tick    += OnClockTick;
        _clock.Start();
    }

    private void StopClock() { _clock?.Stop(); _clock = null; }

    private void OnClockTick(object? sender, EventArgs e)
    {
        try
        {
        if (_gameOver) return;

        // Contador por jogada — só corre na vez do jogador (brancas)
        if (_moveTimerActive && (IsFriendMode || _board.CurrentTurn == PieceColor.White))
        {
            _moveTime -= OneSecond;
            OnPC(nameof(MoveTimeDisplay));
            OnPC(nameof(IsMoveTimeLow));
            if (_moveTime <= TimeSpan.Zero)
            {
                _moveTime = TimeSpan.Zero;
                OnPC(nameof(MoveTimeDisplay));
                EndByMoveTimeout();
                return;
            }
        }

        if (!_timerEnabled) return;

        // Contador total de jogo
        if (_board.CurrentTurn == PieceColor.White)
        {
            _whiteTime -= OneSecond;
            if (_whiteTime <= TimeSpan.Zero)
            {
                _whiteTime = TimeSpan.Zero;
                NotifyTimerProperties();
                EndByTimeout();
                return;
            }
            OnPC(nameof(WhiteTimeDisplay));
            OnPC(nameof(IsWhiteLowTime));
        }
        else
        {
            _blackTime -= OneSecond;
            if (_blackTime <= TimeSpan.Zero)
            {
                _blackTime = TimeSpan.Zero;
                NotifyTimerProperties();
                EndByTimeout();
                return;
            }
            OnPC(nameof(BlackTimeDisplay));
            OnPC(nameof(IsBlackLowTime));
        }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CLOCK] Exceção no tick do relógio: {ex.Message}");
        }
    }

    private void EndByMoveTimeout()
    {
        StopClock();
        bool whiteFlagged  = _board.CurrentTurn == PieceColor.White;
        var  opponentColor = whiteFlagged ? PieceColor.Black : PieceColor.White;

        if (ChessEngine.SideHasInsufficientMatingMaterial(_board, opponentColor))
        {
            StatusMessage = "Tempo por jogada esgotado, mas o adversário não tem material para dar xeque-mate — Empate!";
            if (IsTournamentMode) SetTournamentResult(false);
        }
        else
        {
            var winner = whiteFlagged
                ? (IsFriendMode ? BlackPlayerName : IsTournamentMode ? TournamentOpponent : "Pretas (IA)")
                : (IsFriendMode ? WhitePlayerName : "Brancas");
            StatusMessage = $"Tempo por jogada esgotado! {winner} vence!";
            if (IsTournamentMode) SetTournamentResult(!whiteFlagged);
        }
        GameOver = true;
        _sound.PlayGameOver();
    }

    // Regra 6.9 da FIDE: se a bandeira cai mas o adversário não tem material suficiente pra
    // dar xeque-mate de jeito nenhum (só o rei, ou rei + 1 peça menor sozinha), é empate em
    // vez de vitória por tempo — ver ChessEngine.SideHasInsufficientMatingMaterial.
    private void EndByTimeout()
    {
        StopClock();
        bool whiteFlagged   = _board.CurrentTurn == PieceColor.White;
        var  opponentColor  = whiteFlagged ? PieceColor.Black : PieceColor.White;

        if (ChessEngine.SideHasInsufficientMatingMaterial(_board, opponentColor))
        {
            StatusMessage = "Tempo esgotado, mas o adversário não tem material para dar xeque-mate — Empate!";
            if (IsTournamentMode) SetTournamentResult(false); // mesma convenção de GameState.Draw
        }
        else
        {
            var winner = whiteFlagged
                ? (IsFriendMode ? BlackPlayerName : IsTournamentMode ? TournamentOpponent : "Pretas (IA)")
                : (IsFriendMode ? WhitePlayerName : "Brancas");
            StatusMessage = $"Tempo esgotado! {winner} vence!";
            if (IsTournamentMode) SetTournamentResult(!whiteFlagged);
        }
        GameOver = true;
        _sound.PlayGameOver();
    }

    private void ResetMoveTimer()
    {
        _moveTime = _moveTimeLimit;
        OnPC(nameof(MoveTimeDisplay));
        OnPC(nameof(IsMoveTimeLow));
    }

    // Registra resultado do torneio na AppState imediatamente (independente de qual botão o usuário clicar)
    private void SetTournamentResult(bool humanWon)
    {
        HumanWon = humanWon;
        AppState.Current.LastMatchHumanWon = humanWon;
        TournamentGameEnded?.Invoke(humanWon);
    }

    private void NotifyTimerProperties()
    {
        OnPC(nameof(WhiteTimeDisplay));
        OnPC(nameof(BlackTimeDisplay));
        OnPC(nameof(TimerVisible));
        OnPC(nameof(IsWhiteLowTime));
        OnPC(nameof(IsBlackLowTime));
    }

    // ----------------------------------------------------------------
    // Interação com o tabuleiro (somente turno das Brancas)
    // ----------------------------------------------------------------
    private void OnSquareTapped(SquareViewModel tapped)
    {
        if (_gameOver || _awaitingPromotion) return;

        var humanColor = IsFriendMode ? _board.CurrentTurn : PieceColor.White;
        bool isMyTurn  = IsFriendMode || _board.CurrentTurn == PieceColor.White;
        var piece      = _board.GetPiece(tapped.Row, tapped.Col);

        if (_selectedSquare != null)
        {
            if (isMyTurn && !_isAIThinking)
            {
                var move = _validMoves.FirstOrDefault(m => m.ToRow == tapped.Row && m.ToCol == tapped.Col);
                if (move != null)
                {
                    if (move.PromotionPiece.HasValue)
                    {
                        _pendingPromotion = move;
                        AwaitingPromotion = true;
                        ClearHighlights();
                        PromotionRequested?.Invoke(humanColor == PieceColor.White ? "white" : "black");
                        return;
                    }
                    ExecutePlayerMove(move);
                    return;
                }
            }

            if (piece?.Color == humanColor)
            {
                SelectSquare(tapped);
                return;
            }

            ClearHighlights();
            _selectedSquare = null;
            return;
        }

        if (piece?.Color == humanColor)
            SelectSquare(tapped);
    }

    private void SelectSquare(SquareViewModel sq)
    {
        ClearHighlights();
        _selectedSquare = sq;

        var piece = _board.GetPiece(sq.Row, sq.Col);
        bool isPlayerTurn = piece != null && piece.Color == _board.CurrentTurn;

        _validMoves = isPlayerTurn
            ? ChessEngine.GetLegalMoves(_board, sq.Row, sq.Col)
            : [];

        sq.IsSelected = true;
        foreach (var m in _validMoves)
            Squares[m.ToRow, m.ToCol].IsValidMove = true;
        BoardChanged?.Invoke();
    }

    private void ExecutePlayerMove(ChessMove move)
    {
        _drawRefusedThisTurn = false;
        OnPC(nameof(CanOfferDraw));
        OfferDrawCommand.ChangeCanExecute();

        var movingPiece   = _board.GetPiece(move.FromRow, move.FromCol)!;
        var movingColor   = movingPiece.Color;
        var capturedPiece = move.IsEnPassant
            ? new ChessPiece(PieceType.Pawn, movingColor == PieceColor.White ? PieceColor.Black : PieceColor.White)
            : _board.GetPiece(move.ToRow, move.ToCol);
        bool isCapture = capturedPiece != null;

        _lastMove       = move;
        ClearHighlights();
        _selectedSquare = null;
        _validMoves.Clear();

        var snapshotBeforeMove = _board.Clone();
        ChessEngine.ApplyMove(_board, move);
        RefreshBoard();

        var state = ChessEngine.GetGameState(_board);
        PlaySound(isCapture, state);

        if (capturedPiece != null)
        {
            if (movingColor == PieceColor.White) _capturedByWhite.Add(capturedPiece);
            else                                  _capturedByBlack.Add(capturedPiece);
            UpdateCapturesDisplay();
        }
        var notation = GetNotation(move, movingPiece, isCapture, state);
        _moves.Add(notation);
        UpdateMoveList();

        if (!IsFriendMode)
        {
            _uciMoveHistory.Add(MoveToUci(move));
            _playerMoveSnapshots.Add(new(snapshotBeforeMove, move, _moves.Count - 1));
            int pEval = AIService.EvaluateStatic(_board); // avalia posição atual (instantâneo)
            bool pBlackToMove = _board.CurrentTurn == PieceColor.Black;
            _allMoveSnapshots.Add(new(snapshotBeforeMove, _board.Clone(), move, notation, true, _moves.Count - 1,
                pBlackToMove ? -pEval : pEval));
        }

        if (IsTournamentMode) _chat.SendGoodMove();
        UpdateStatus(state);

        if (!_gameOver)
        {
            if (IsFriendMode)
            {
                var nextName = _board.CurrentTurn == PieceColor.White ? WhitePlayerName : BlackPlayerName;
                ResetMoveTimer();
                RequestHandoff?.Invoke(nextName);
            }
            else
                _ = RunAIAsync();
        }
        else
            ResetMoveTimer();
    }

    private void OnPromote(string pieceType)
    {
        if (_pendingPromotion == null) return;

        _pendingPromotion.PromotionPiece = pieceType switch
        {
            "queen"  => PieceType.Queen,
            "rook"   => PieceType.Rook,
            "bishop" => PieceType.Bishop,
            "knight" => PieceType.Knight,
            _        => PieceType.Queen
        };

        AwaitingPromotion = false;
        ExecutePlayerMove(_pendingPromotion);
        _pendingPromotion = null;
    }

    // ----------------------------------------------------------------
    // IA (Pretas)
    // ----------------------------------------------------------------
    private async Task RunAIAsync()
    {
        _aiCts?.Cancel();

        // Limit think time to 30% of remaining clock (min 500 ms, max _aiThinkMaxMs)
        int thinkMs = _aiThinkMaxMs;
        if (_timerEnabled && _blackTime.TotalMilliseconds > 0)
        {
            int budget = (int)(_blackTime.TotalMilliseconds * 0.30);
            thinkMs = Math.Clamp(budget, 500, _aiThinkMaxMs);
        }

        // Stockfish move time: the lesser of the clock budget and the configured difficulty time
        int sfMoveTime = Math.Min(thinkMs, _sfMoveTimeMs);

        // Na abertura, dá mais tempo pra busca de variedade (MultiPV) render candidatos com
        // profundidade comparável — com pouco tempo, os alternativos parecem artificialmente
        // "piores" só por terem sido menos explorados, e a IA nunca varia de fato. Só estende
        // quando não há relógio apertado, pra não gastar tempo do jogador à toa.
        int varietyMoveTime = _timerEnabled ? sfMoveTime : Math.Max(sfMoveTime, 1800);

        // Outer CTS must outlive internal AI + Stockfish safety timeout
        _aiCts       = new CancellationTokenSource(Math.Max(thinkMs, varietyMoveTime) + 6_000);
        IsAIThinking = true;

        int pendingReselectRow = -1;
        int pendingReselectCol = -1;

        try
        {
            ChessMove? move = null;

            // Try Stockfish first
            var sf = AppState.Current.Stockfish;
            System.Diagnostics.Debug.WriteLine($"[AI] Stockfish.IsAvailable={sf.IsAvailable}  sfMoveTime={sfMoveTime}  skill={_sfSkillLevel}");
            if (sf.IsAvailable)
            {
                string? uciStr;
                bool    isForcedMate = false;

                // Abertura: variedade escolhendo entre lances de força equivalente (mesmo MultiPV
                // em força máxima) — evita repetir sempre a mesma jogada na mesma posição inicial.
                // Depois de algumas jogadas, volta ao lance único mais forte (mais rápido e igualmente calculado).
                if (_uciMoveHistory.Count <= OpeningVarietyPlies)
                {
                    var top = await sf.GetTopMovesAsync(_uciMoveHistory, varietyMoveTime, 3, _aiCts.Token);
                    if (top.Count > 0)
                    {
                        var best = top[0];
                        isForcedMate = best.IsMate;
                        var choice = best;
                        if (!best.IsMate)
                        {
                            var closeOnes = top
                                .Where(t => !t.IsMate && Math.Abs(t.ScoreCp - best.ScoreCp) <= OpeningVarietyMarginCp)
                                .ToList();
                            if (closeOnes.Count > 0)
                                choice = closeOnes[_easyRng.Next(closeOnes.Count)];
                        }
                        uciStr = choice.Move;
                    }
                    else uciStr = null;
                }
                else if (_aiPersonality == BotPersonality.Balanced)
                {
                    (uciStr, isForcedMate) = await sf.GetBestMoveAsync(
                        _uciMoveHistory, sfMoveTime, _sfSkillLevel, _aiCts.Token);
                }
                else
                {
                    // Estilo de bot: escolhe entre os melhores candidatos (mesma força do
                    // Skill Level configurado, não força máxima) em vez de sempre o nº 1.
                    var styled = await sf.GetTopMovesAsync(
                        _uciMoveHistory, sfMoveTime, 3, _aiCts.Token, _sfSkillLevel);
                    if (styled.Count > 0)
                    {
                        isForcedMate = styled[0].IsMate;
                        // O bot (IA) sempre joga de Pretas nesse fluxo (ver WhitePlayerName/
                        // BlackPlayerName logo acima em StartNewGame).
                        uciStr = PersonalityMoveSelector.Choose(
                            styled, _aiPersonality, IsCaptureUci, CountThreatsUci,
                            botPlaysWhite: false, _personalityRng);
                    }
                    else uciStr = null;
                }

                System.Diagnostics.Debug.WriteLine($"[AI] Stockfish returned: {uciStr ?? "null"} mate={isForcedMate}");
                if (uciStr != null)
                {
                    move = UciToMove(_board, uciStr);
                    // Fácil: sem mate à vista, deixa passar um peão solto de vez em quando —
                    // nunca troca a jogada quando há mate forçado (a favor ou contra a IA).
                    if (_aiDepthLevel == 1 && !isForcedMate && move != null)
                        move = TryEasyPawnSlip(_board, move) ?? move;
                }
            }

            // Fallback para IA interna quando Stockfish não disponível (ex: emulador x86)
            if (move == null)
            {
                using var aiLimitCts = CancellationTokenSource.CreateLinkedTokenSource(_aiCts.Token);
                aiLimitCts.CancelAfter(thinkMs);
                move = await _ai.GetBestMoveAsync(_board, aiLimitCts.Token);
            }

            if (move == null || _gameOver || _board.CurrentTurn != PieceColor.Black)
            {
                // Nenhuma jogada — atualiza status para não ficar preso em "IA pensando..."
                if (!_gameOver && _board.CurrentTurn == PieceColor.Black)
                    StatusMessage = "Sua vez (Brancas)";
                return;
            }

            var aiPiece    = _board.GetPiece(move.FromRow, move.FromCol)!;
            var aiCaptured = move.IsEnPassant
                ? new ChessPiece(PieceType.Pawn, PieceColor.White)
                : _board.GetPiece(move.ToRow, move.ToCol);
            bool isCapture = aiCaptured != null;
            _lastMove = move;
            // Salva seleção que o usuário pode ter feito enquanto a IA pensava
            pendingReselectRow = _selectedSquare?.Row ?? -1;
            pendingReselectCol = _selectedSquare?.Col ?? -1;
            ClearHighlights();
            _selectedSquare = null;

            var aiSnapshotBefore = _board.Clone();
            ChessEngine.ApplyMove(_board, move);
            _uciMoveHistory.Add(MoveToUci(move));
            RefreshBoard();

            var state = ChessEngine.GetGameState(_board);
            PlaySound(isCapture, state);

            if (aiCaptured != null) { _capturedByBlack.Add(aiCaptured); UpdateCapturesDisplay(); }
            var aiNotation = GetNotation(move, aiPiece, isCapture, state);
            _moves.Add(aiNotation);
            int aEval = AIService.EvaluateStatic(_board);
            bool aBlackToMove = _board.CurrentTurn == PieceColor.Black;
            _allMoveSnapshots.Add(new(aiSnapshotBefore, _board.Clone(), move, aiNotation, false, _moves.Count - 1,
                aBlackToMove ? -aEval : aEval));
            UpdateMoveList();

            if (IsTournamentMode)
            {
                if (isCapture) _chat.SendCapture();
                if (state == GameState.Check) _chat.SendCheck();
                if (state is GameState.Checkmate or GameState.Stalemate) _chat.SendWin();
            }
            UpdateStatus(state);
            ResetMoveTimer();
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsAIThinking = false;
            // Restaura seleção de peça que o usuário fez enquanto a IA pensava
            if (pendingReselectRow >= 0 && !_gameOver)
            {
                var piece = _board.GetPiece(pendingReselectRow, pendingReselectCol);
                if (piece?.Color == PieceColor.White)
                    SelectSquare(Squares[pendingReselectRow, pendingReselectCol]);
            }
        }
    }

    // ----------------------------------------------------------------
    // Fácil: chance rara de soltar um peão (nunca uma peça maior, nunca quando há mate)
    // ----------------------------------------------------------------
    private static readonly Random _easyRng = new();
    private const double EasyPawnSlipChance = 0.08; // ~8% dos lances, só quando aplicável

    // Variedade de abertura: nas primeiras jogadas, sorteia entre lances de força equivalente
    // (mesmo MultiPV em força máxima) em vez de sempre repetir a mesma jogada "número 1".
    private const int OpeningVarietyPlies    = 10; // ~5 lances da IA no começo da partida
    private const int OpeningVarietyMarginCp = 60; // candidatos até 60 centipawns do melhor

    // Tenta substituir o melhor lance por um lance de peão que "pendura" o próprio peão
    // (fica atacado e sem defensor). Só chamado no Fácil e só quando o Stockfish não viu mate.
    // Retorna null se não há candidato ou se o sorteio não caiu — o chamador mantém o melhor lance.
    private ChessMove? TryEasyPawnSlip(ChessBoard board, ChessMove bestMove)
    {
        if (_easyRng.NextDouble() >= EasyPawnSlipChance) return null;

        var aiColor    = board.CurrentTurn;
        var humanColor = aiColor == PieceColor.White ? PieceColor.Black : PieceColor.White;

        var candidates = new List<ChessMove>();
        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
        {
            var p = board.GetPiece(r, c);
            if (p?.Color != aiColor || p.Type != PieceType.Pawn) continue;

            foreach (var pawnMove in ChessEngine.GetLegalMoves(board, r, c))
            {
                var clone = board.Clone();
                ChessEngine.ApplyMove(clone, pawnMove);

                bool hangs = ChessEngine.IsSquareAttacked(clone, pawnMove.ToRow, pawnMove.ToCol, humanColor)
                          && !ChessEngine.IsSquareAttacked(clone, pawnMove.ToRow, pawnMove.ToCol, aiColor);
                if (hangs) candidates.Add(pawnMove);
            }
        }

        if (candidates.Count == 0) return null;
        return candidates[_easyRng.Next(candidates.Count)];
    }

    // ----------------------------------------------------------------
    // Som
    // ----------------------------------------------------------------
    private void PlaySound(bool isCapture, GameState state)
    {
        if (state is GameState.Checkmate or GameState.Stalemate or GameState.Draw)
            _sound.PlayGameOver();
        else if (state == GameState.Check)
            _sound.PlayCheck();
        else if (isCapture)
            _sound.PlayCapture();
        else
            _sound.PlayMove();
    }

    // ----------------------------------------------------------------
    // Utilitários
    // ----------------------------------------------------------------
    private void ClearHighlights()
    {
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                Squares[r, c].IsSelected  = false;
                Squares[r, c].IsValidMove = false;
                Squares[r, c].IsInCheck   = false;
                Squares[r, c].IsLastMove  = false;
            }
    }

    private void RefreshBoard()
    {
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                var piece = _board.GetPiece(r, c);
                Squares[r, c].PieceSymbol  = piece?.Symbol ?? "";
                Squares[r, c].PieceIsWhite = piece == null ? (bool?)null : piece.Color == PieceColor.White;
                Squares[r, c].IsLastMove   = false;   // limpa antes de remarcar
            }

        if (_lastMove != null)
        {
            Squares[_lastMove.FromRow, _lastMove.FromCol].IsLastMove = true;
            Squares[_lastMove.ToRow,   _lastMove.ToCol  ].IsLastMove = true;
        }

        BoardChanged?.Invoke();
    }

    // ----------------------------------------------------------------
    // Admin: forçar resultado imediato
    // ----------------------------------------------------------------
    public void ForceWin()
    {
        if (_gameOver) return;
        StopClock();
        StatusMessage = IsTournamentMode
            ? $"[ADMIN] Vitória forçada vs {TournamentOpponent}!"
            : "[ADMIN] Vitória forçada!";
        if (IsTournamentMode) SetTournamentResult(true);
        HumanWon  = true;
        GameOver  = true;
        _sound.PlayGameOver();
    }

    public void ForceLoss()
    {
        if (_gameOver) return;
        StopClock();
        StatusMessage = IsTournamentMode
            ? $"[ADMIN] Derrota forçada. {TournamentOpponent} vence!"
            : "[ADMIN] Derrota forçada!";
        if (IsTournamentMode) SetTournamentResult(false);
        HumanWon  = false;
        GameOver  = true;
        _sound.PlayGameOver();
    }

    // ----------------------------------------------------------------
    // Desistir
    // ----------------------------------------------------------------
    private async Task OnResign()
    {
        if (_gameOver || ResignRequested == null) return;
        bool confirmed = await ResignRequested.Invoke();
        if (!confirmed) return;

        StopClock();
        StatusMessage = IsTournamentMode
            ? $"Você desistiu. {TournamentOpponent} vence!"
            : IsFriendMode ? "Partida encerrada por desistência."
            : "Você desistiu. Pretas (IA) vencem!";
        if (IsTournamentMode) SetTournamentResult(false);
        GameOver = true;
        _sound.PlayGameOver();
    }

    // ----------------------------------------------------------------
    // Oferta de empate (apenas fora de torneio)
    // ----------------------------------------------------------------
    private async Task OnOfferDraw()
    {
        if (_gameOver || DrawOfferRequested == null) return;
        if (_isAIThinking) return;

        // IA aceita empate com ~30% de chance (mais provável se estiver em desvantagem)
        bool aiAccepts = await DrawOfferRequested.Invoke();
        if (!aiAccepts)
        {
            _drawRefusedThisTurn = true;
            OnPC(nameof(CanOfferDraw));
            OfferDrawCommand.ChangeCanExecute();
            return;
        }

        StopClock();
        StatusMessage = "Empate acordado!";
        if (IsTournamentMode) SetTournamentResult(false);
        GameOver = true;
        _sound.PlayGameOver();
    }

    private void UpdateStatus(GameState state)
    {
        switch (state)
        {
            case GameState.Checkmate:
                // CurrentTurn = quem está em xeque-mate (perdeu)
                bool whiteCheckmated = _board.CurrentTurn == PieceColor.White;
                var winnerName = whiteCheckmated
                    ? (IsFriendMode ? BlackPlayerName : IsTournamentMode ? TournamentOpponent : "Pretas (IA)")
                    : (IsFriendMode ? WhitePlayerName : "Brancas");
                StatusMessage = $"Xeque-Mate! {winnerName} vencem!";
                if (IsTournamentMode) SetTournamentResult(!whiteCheckmated);
                GameOver = true;
                StopClock();
                break;

            case GameState.Stalemate:
                // CurrentTurn = quem ficou sem movimentos (o afogado)
                bool humanStalemated = _board.CurrentTurn == PieceColor.White;
                if (IsFriendMode)
                {
                    StatusMessage = humanStalemated
                        ? $"Afogamento! {WhitePlayerName} ficou sem movimentos. {BlackPlayerName} vence!"
                        : $"Afogamento! {BlackPlayerName} ficou sem movimentos. {WhitePlayerName} vence!";
                }
                else if (IsTournamentMode)
                {
                    if (humanStalemated)
                        StatusMessage = $"Afogamento! Você ficou sem movimentos. {TournamentOpponent} vence!";
                    else
                        StatusMessage = "Afogamento! Você afogou o adversário. Você vence!";
                    SetTournamentResult(!humanStalemated);
                }
                else
                {
                    // Casual contra Bots: mesma regra do Torneio/Amigo — quem afoga o
                    // adversário vence, em vez de empate (afogamento como derrota "roubada"
                    // de quem estava perdendo é considerado injusto neste app).
                    StatusMessage = humanStalemated
                        ? "Afogamento! Você ficou sem movimentos. Pretas (IA) vencem!"
                        : "Afogamento! Você afogou a IA. Brancas vencem!";
                    // Casual não passa por SetTournamentResult (só torneio/online) — sem isso,
                    // a tela de resultado cairia no fallback frágil de ler o texto da mensagem
                    // pra saber quem venceu.
                    HumanWon = !humanStalemated;
                }
                GameOver = true;
                StopClock();
                break;

            case GameState.Draw:
                // Em torneio: empate por 50 lances / repetição / material = derrota do jogador
                string drawReason = _board.HalfMoveClock >= 100
                    ? "Regra dos 50 lances (sem captura/peão)"
                    : ChessEngine.IsInsufficientMaterial(_board)
                        ? "Material insuficiente para xeque-mate"
                        : "Repetição de posição (3×)";
                StatusMessage = IsTournamentMode
                    ? $"⚠ Derrota por empate: {drawReason}. {TournamentOpponent} avança!"
                    : $"Empate! ({drawReason})";
                if (IsTournamentMode) SetTournamentResult(false);
                GameOver = true;
                StopClock();
                break;

            case GameState.Check:
                var (kr, kc) = _board.FindKing(_board.CurrentTurn);
                if (kr >= 0) Squares[kr, kc].IsInCheck = true;
                StatusMessage = _board.CurrentTurn == PieceColor.White
                    ? (IsFriendMode ? $"Xeque! Vez de {WhitePlayerName}" : "Xeque! Sua vez (Brancas)")
                    : (IsFriendMode ? $"Xeque! Vez de {BlackPlayerName}" : "Xeque! IA pensando...");
                break;

            default:
                StatusMessage = _board.CurrentTurn == PieceColor.White
                    ? (IsFriendMode ? $"Vez de {WhitePlayerName}" : "Sua vez (Brancas)")
                    : (IsFriendMode ? $"Vez de {BlackPlayerName}" : "IA pensando...");
                break;
        }
    }

    // ----------------------------------------------------------------
    // Peças capturadas e vantagem de material
    // ----------------------------------------------------------------
    private static int PieceValue(ChessPiece p) => p.Type switch
    {
        PieceType.Queen  => 9,
        PieceType.Rook   => 5,
        PieceType.Bishop => 3,
        PieceType.Knight => 3,
        _                => 1   // Peão (Rei nunca é capturado)
    };

    private void UpdateCapturesDisplay()
    {
        static string Fmt(List<ChessPiece> list) =>
            string.Join("", list.OrderByDescending(PieceValue).Select(p => p.Symbol));

        WhiteCapturesDisplay = Fmt(_capturedByWhite);
        BlackCapturesDisplay = Fmt(_capturedByBlack);

        int adv = _capturedByWhite.Sum(PieceValue) - _capturedByBlack.Sum(PieceValue);
        MaterialDisplay = adv > 0 ? $"+{adv}" : adv < 0 ? adv.ToString() : "";
        MaterialColor   = adv > 0 ? Color.FromArgb("#4CAF50")
                        : adv < 0 ? Color.FromArgb("#FF5252")
                        : Colors.Transparent;

        OnPC(nameof(WhiteCapturesDisplay));
        OnPC(nameof(BlackCapturesDisplay));
        OnPC(nameof(MaterialDisplay));
        OnPC(nameof(MaterialColor));
    }

    // ----------------------------------------------------------------
    // Lista de lances (notação algébrica com símbolos Unicode)
    // ----------------------------------------------------------------
    private void UpdateMoveList()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < _moves.Count; i++)
        {
            if (i % 2 == 0)
            {
                if (i > 0) sb.Append("  ");
                sb.Append($"{i / 2 + 1}.");
            }
            else
                sb.Append(' ');
            sb.Append(_moves[i]);
        }
        MoveListText = sb.ToString();
        OnPC(nameof(MoveListText));
    }

    private static string GetNotation(ChessMove move, ChessPiece piece, bool isCapture, GameState stateAfter)
    {
        if (move.IsCastling)
            return move.ToCol > move.FromCol ? "O-O" : "O-O-O";

        string dest        = $"{(char)('a' + move.ToCol)}{8 - move.ToRow}";
        string captureMark = isCapture ? "x" : "";

        string notation;
        if (piece.Type == PieceType.Pawn)
        {
            string fromFile = isCapture ? ((char)('a' + move.FromCol)).ToString() : "";
            notation = $"{fromFile}{captureMark}{dest}";
        }
        else
        {
            notation = $"{piece.Symbol}{captureMark}{dest}";
        }

        if (move.PromotionPiece.HasValue)
            notation += "=" + new ChessPiece(move.PromotionPiece.Value, piece.Color).Symbol;

        notation += stateAfter switch
        {
            GameState.Checkmate => "#",
            GameState.Check     => "+",
            _                   => ""
        };

        return notation;
    }

    private static string FormatTime(TimeSpan t) =>
        $"{(int)t.TotalMinutes:D2}:{t.Seconds:D2}";

    // ----------------------------------------------------------------
    // Revisão completa da partida — dois passes: depth 8 para todos,
    // depth 14 apenas para as 8 posições com maior perda de centipeões.
    // ----------------------------------------------------------------
    public async Task<Models.GameReviewData> GetReviewDataAsync(
        IProgress<(int Current, int Total)>? progress, CancellationToken ct)
    {
        var snapshots    = _allMoveSnapshots.ToList();
        var records      = new List<Models.ReviewMoveRecord>();
        var sf           = AppState.Current.Stockfish;
        var firstHuman   = snapshots.FirstOrDefault(s => s.IsPlayerMove);
        bool humanIsWhite = firstHuman != null && firstHuman.HalfMoveIndex % 2 == 0;
        int total     = snapshots.Count;

        // ── Fallback: motor interno ───────────────────────────────────
        if (!sf.IsAvailable)
        {
            for (int i = 0; i < snapshots.Count && !ct.IsCancellationRequested; i++)
            {
                var snap      = snapshots[i];
                progress?.Report((i + 1, total));

                int rawEval   = AIService.EvaluateStatic(snap.BoardAfter);
                int evalWhite = snap.BoardAfter.CurrentTurn == PieceColor.Black ? -rawEval : rawEval;

                int cpLoss = 0;
                ChessMove? bestMove = null;
                if (snap.IsPlayerMove)
                    (bestMove, cpLoss) = await Task.Run(
                        () => AIService.AnalyzeMoveDeep(snap.BoardBefore, snap.Move, ct), ct);

                var quality = cpLoss switch
                {
                    < 50  => MoveQuality.Good,
                    < 100 => MoveQuality.Inaccuracy,
                    < 300 => MoveQuality.Mistake,
                    _     => MoveQuality.Blunder
                };

                records.Add(new Models.ReviewMoveRecord
                {
                    HalfMoveIndex = snap.HalfMoveIndex,
                    BoardBefore   = snap.BoardBefore,
                    BoardAfter    = snap.BoardAfter,
                    Move          = snap.Move,
                    Notation      = snap.Notation,
                    Evaluation    = evalWhite,
                    CpLoss        = cpLoss,
                    Quality       = quality,
                    BestMove      = bestMove,
                    BestMoveUci   = null
                });
            }

            var candidatesFb = Models.GameReviewData.IdentifyKeyMoments(records, humanIsWhite);

            return new Models.GameReviewData
            {
                WhitePlayerName = WhitePlayerName,
                BlackPlayerName = BlackPlayerName,
                HumanWon        = HumanWon,
                HumanIsWhite    = humanIsWhite,
                Moves           = records,
                EvalHistory     = records.Select(r => r.Evaluation).ToList(),
                KeyMoments      = candidatesFb,
                MainLesson      = ""
            };
        }

        // ── Análise Stockfish: profundidade fixa para TODOS os lances humanos ──
        // Profundidade 14 garante que Stockfish veja sequências táticas de 7+ lances,
        // eliminando o "efeito horizonte" e falsos positivos por profundidade insuficiente.
        // Antes e depois do mesmo lance usam a MESMA profundidade → cpLoss comparável.
        // StaticEval serve apenas para o gráfico de avaliação dos lances do adversário.
        int humanMoveCount = snapshots.Count(s => s.IsPlayerMove);
        var sfResults = new Dictionary<int, (int EvalWhite, int EvalWhiteBefore, string? BestUci)>();
        {
            var uciList = new List<string>(snapshots.Count);
            int sfDone  = 0;
            for (int i = 0; i < snapshots.Count && !ct.IsCancellationRequested; i++)
            {
                if (snapshots[i].IsPlayerMove)
                {
                    // Call 1: ANTES — depth 14 → melhor UCI + eval consistente
                    var (scoreBefore, altUci) = await sf.AnalyzePositionAsync(uciList, 14, ct);
                    string? bestUci = (!string.IsNullOrEmpty(altUci) && altUci != "0000") ? altUci : null;
                    bool whiteToMove     = snapshots[i].BoardBefore.CurrentTurn == PieceColor.White;
                    int  evalWhiteBefore = whiteToMove ? scoreBefore : -scoreBefore;

                    uciList.Add(MoveToUci(snapshots[i].Move));

                    // Call 2: DEPOIS — depth 14 → eval pós-lance, mesma profundidade = cpLoss válido
                    var (scoreAfter, _) = await sf.AnalyzePositionAsync(uciList, 14, ct);
                    bool blackToMove = snapshots[i].BoardAfter.CurrentTurn == PieceColor.Black;
                    sfResults[i] = (blackToMove ? -scoreAfter : scoreAfter, evalWhiteBefore, bestUci);

                    sfDone++;
                    progress?.Report((sfDone * 2, humanMoveCount * 2));
                }
                else
                {
                    uciList.Add(MoveToUci(snapshots[i].Move));
                }
            }
        }

        // ── Montar registros finais ───────────────────────────────────────────
        for (int i = 0; i < snapshots.Count; i++)
        {
            var snap     = snapshots[i];
            int  cpLoss  = 0;
            string? bestUci = null;

            // Eval para o gráfico: Stockfish (lances humanos) ou StaticEval (adversário)
            int rawStatic = snap.StaticEval;
            int evalWhite = snap.HalfMoveIndex % 2 == 1 ? -rawStatic : rawStatic;

            if (sfResults.TryGetValue(i, out var sfr))
            {
                evalWhite = sfr.EvalWhite;
                bool isWhiteMove = snap.HalfMoveIndex % 2 == 0;
                cpLoss = isWhiteMove
                    ? Math.Max(0, sfr.EvalWhiteBefore - sfr.EvalWhite)
                    : Math.Max(0, sfr.EvalWhite - sfr.EvalWhiteBefore);
                cpLoss  = Math.Min(cpLoss, 1000);
                bestUci = sfr.BestUci;
            }

            // Thresholds calibrados: abertura muito tolerante, erros apenas com perda real
            bool opening = snap.HalfMoveIndex < 12;
            var quality  = cpLoss switch
            {
                _ when opening && cpLoss < 70  => MoveQuality.Good,
                < 20                           => MoveQuality.Good,
                < 70                           => MoveQuality.Inaccuracy,
                < 200                          => MoveQuality.Mistake,
                _                              => MoveQuality.Blunder
            };

            var bestMove = bestUci != null ? UciToMove(snap.BoardBefore, bestUci) : null;

            // Descarta bestMove que parece claramente errado visualmente
            if (bestMove != null && !bestMove.IsCastling)
            {
                var bMoving = snap.BoardBefore.GetPiece(bestMove.FromRow, bestMove.FromCol);
                var bTarget = snap.BoardBefore.GetPiece(bestMove.ToRow,   bestMove.ToCol);
                if (bMoving != null)
                {
                    static int PVal(PieceType t) => t switch
                    {
                        PieceType.Queen  => 900, PieceType.Rook   => 500,
                        PieceType.Bishop => 330, PieceType.Knight  => 320,
                        PieceType.Pawn   => 100, _                 => 0
                    };
                    PieceColor opp = bMoving.Color == PieceColor.White ? PieceColor.Black : PieceColor.White;

                    if (bTarget != null)
                    {
                        // Captura claramente perdedora: peça muito mais valiosa captura peça defendida
                        bool targetDefended = ChessEngine.IsSquareAttacked(
                            snap.BoardBefore, bestMove.ToRow, bestMove.ToCol, bTarget.Color);
                        if (targetDefended && PVal(bMoving.Type) > PVal(bTarget.Type) + 200)
                        {
                            bestMove = null;
                            bestUci  = null;
                        }
                    }
                    else
                    {
                        // Rainha ou torre se movendo para casa VAZIA sob ataque adversário
                        // (aparece como erro óbvio; Stockfish pode ver combinação profunda mas
                        //  mostrar a seta confunde o utilizador)
                        if (PVal(bMoving.Type) >= 500 &&
                            ChessEngine.IsSquareAttacked(snap.BoardBefore, bestMove.ToRow, bestMove.ToCol, opp))
                        {
                            bestMove = null;
                            bestUci  = null;
                        }
                    }
                }
            }

            records.Add(new Models.ReviewMoveRecord
            {
                HalfMoveIndex = snap.HalfMoveIndex,
                BoardBefore   = snap.BoardBefore,
                BoardAfter    = snap.BoardAfter,
                Move          = snap.Move,
                Notation      = snap.Notation,
                Evaluation    = evalWhite,
                CpLoss        = cpLoss,
                Quality       = quality,
                BestMove      = bestMove,
                BestMoveUci   = bestUci
            });
        }

        var candidates = Models.GameReviewData.IdentifyKeyMoments(records, humanIsWhite);

        return new Models.GameReviewData
        {
            WhitePlayerName = WhitePlayerName,
            BlackPlayerName = BlackPlayerName,
            HumanWon        = HumanWon,
            HumanIsWhite    = humanIsWhite,
            Moves           = records,
            EvalHistory     = records.Select(r => r.Evaluation).ToList(),
            KeyMoments      = candidates,
            MainLesson      = ""
        };
    }

    // ----------------------------------------------------------------
    // Análise pós-jogo
    // ----------------------------------------------------------------
    public async Task<List<MoveAnalysisItem>> AnalyzeGameAsync(CancellationToken ct)
    {
        var snapshots = _playerMoveSnapshots.ToList();
        var results   = new List<MoveAnalysisItem>();

        await Task.Run(() =>
        {
            foreach (var rec in snapshots)
            {
                if (ct.IsCancellationRequested) break;

                var (bestMove, cpLoss) = AIService.AnalyzeMove(rec.BoardBefore, rec.Move);
                if (bestMove == null) continue;

                MoveQuality quality = cpLoss switch
                {
                    < 50  => MoveQuality.Good,
                    < 100 => MoveQuality.Inaccuracy,
                    < 300 => MoveQuality.Mistake,
                    _     => MoveQuality.Blunder
                };

                if (quality == MoveQuality.Good) continue;

                int moveNumber  = rec.HalfMoveIndex / 2 + 1;
                var errorType   = ClassifyError(rec.BoardBefore, rec.Move, bestMove);
                string desc     = BuildDescription(errorType, rec.BoardBefore, rec.Move, bestMove, moveNumber);

                results.Add(new(moveNumber, desc, quality, errorType,
                    rec.BoardBefore, rec.Move, bestMove, cpLoss));
            }
        }, ct);

        return [.. results.OrderByDescending(r => r.CpLoss).Take(3)];
    }

    private static TacticalErrorType ClassifyError(ChessBoard boardBefore, ChessMove playerMove, ChessMove bestMove)
    {
        // 1. Melhor lance leva a xeque-mate?
        var afterBest = boardBefore.Clone();
        ChessEngine.ApplyMove(afterBest, bestMove);
        if (ChessEngine.GetGameState(afterBest) == GameState.Checkmate)
            return TacticalErrorType.MissedMate;

        // 2. Melhor lance captura peça sem defesa (captura grátis)?
        var victim = boardBefore.GetPiece(bestMove.ToRow, bestMove.ToCol);
        if (victim != null && !ChessEngine.IsSquareAttacked(afterBest, bestMove.ToRow, bestMove.ToCol, victim.Color))
            return TacticalErrorType.MissedFreeCapture;

        // 3. O lance do jogador colocou a peça num quadrado não defendido?
        var afterPlayer = boardBefore.Clone();
        ChessEngine.ApplyMove(afterPlayer, playerMove);

        bool isAttacked = ChessEngine.IsSquareAttacked(afterPlayer, playerMove.ToRow, playerMove.ToCol, PieceColor.Black);
        bool isDefended = ChessEngine.IsSquareAttacked(afterPlayer, playerMove.ToRow, playerMove.ToCol, PieceColor.White);
        if (isAttacked && !isDefended)
            return TacticalErrorType.HangingPiece;

        // 4. O lance expôs outra peça branca que estava segura?
        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
        {
            if (r == playerMove.ToRow && c == playerMove.ToCol) continue;
            var p = afterPlayer.GetPiece(r, c);
            if (p == null || p.Color != PieceColor.White) continue;

            bool wasSafe   = !ChessEngine.IsSquareAttacked(boardBefore,   r, c, PieceColor.Black)
                          ||  ChessEngine.IsSquareAttacked(boardBefore,   r, c, PieceColor.White);
            bool isHanging =  ChessEngine.IsSquareAttacked(afterPlayer, r, c, PieceColor.Black)
                          && !ChessEngine.IsSquareAttacked(afterPlayer, r, c, PieceColor.White);
            if (wasSafe && isHanging)
                return TacticalErrorType.BlunderedPiece;
        }

        return TacticalErrorType.General;
    }

    private static string BuildDescription(TacticalErrorType type, ChessBoard boardBefore,
        ChessMove playerMove, ChessMove bestMove, int moveNumber)
    {
        string mover  = PieceNamePt(boardBefore.GetPiece(playerMove.FromRow, playerMove.FromCol)?.Type);
        string better = PieceNamePt(boardBefore.GetPiece(bestMove.FromRow,   bestMove.FromCol  )?.Type);
        string target = PieceNamePt(boardBefore.GetPiece(bestMove.ToRow,     bestMove.ToCol    )?.Type);

        return type switch
        {
            TacticalErrorType.MissedMate =>
                $"Lance {moveNumber} — Havia xeque-mate disponível nessa posição! " +
                $"Com {better} você poderia ter encerrado a partida, mas deixou a chance escapar.",

            TacticalErrorType.MissedFreeCapture =>
                $"Lance {moveNumber} — Você podia capturar {target} adversári{ArticleO(target)} gratuitamente " +
                $"com {better}, sem nenhum risco. Uma peça ganha de graça foi ignorada.",

            TacticalErrorType.HangingPiece =>
                $"Lance {moveNumber} — Você jogou {mover} para uma casa sem proteção. " +
                $"O adversário podia capturá-l{ArticleA(mover)} sem risco, ganhando material de graça.",

            TacticalErrorType.BlunderedPiece =>
                $"Lance {moveNumber} — Seu movimento com {mover} tirou a proteção de outra peça sua. " +
                $"Com {better} você teria mantido a posição sólida e evitado essa perda.",

            _ =>
                $"Lance {moveNumber} — Havia uma jogada muito mais forte com {better} nessa posição, " +
                $"que teria dado uma vantagem decisiva. A escolha feita desperdiçou essa oportunidade."
        };
    }

    private static string PieceNamePt(PieceType? t) => t switch
    {
        PieceType.King   => "o Rei",
        PieceType.Queen  => "a Rainha",
        PieceType.Rook   => "a Torre",
        PieceType.Bishop => "o Bispo",
        PieceType.Knight => "o Cavalo",
        PieceType.Pawn   => "o Peão",
        _                => "a peça"
    };

    private static string ArticleO(string name) => name.StartsWith("a ") ? "a" : "o";
    private static string ArticleA(string name) => name.StartsWith("a ") ? "a" : "o";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPC([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
