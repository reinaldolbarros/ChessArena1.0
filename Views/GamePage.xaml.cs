using ChessMAUI.Models;
using ChessMAUI.Services;
using ChessMAUI.ViewModels;

namespace ChessMAUI.Views;

public partial class GamePage : ContentPage
{
    private readonly GameViewModel _vm;

    private CancellationTokenSource? _chatCts;
    private double _squareSize;
    private bool   _resultShownForGame;
    private bool   _nextTurnIsBlack;
    private bool   _resultExpanded = true;

    private int _selectedDiff = 0;
    private int _selectedTimeMinutes = 0;
    // Tempo de raciocínio (não representa mais dificuldade sozinho — ver Skill Level
    // abaixo). Difícil e Hard usam o mesmo tempo; o que diferencia os dois é a força
    // real da IA via CareerService.GetSkillLevel.
    private static readonly int[]    DiffDepths  = [1, 3, 5, 5];
    private static readonly string[] DiffLabels  = ["Fácil", "Médio", "Difícil", "Hard"];

    public GamePage()
    {
        InitializeComponent();
        _vm = new GameViewModel();
        BindingContext = _vm;

        _vm.PromotionRequested  += OnPromotionRequested;
        _vm.ChatMessageReceived += OnChatMessageReceived;
        _vm.TournamentGameEnded += OnTournamentGameEnded;
        _vm.ResignRequested     += OnResignRequested;
        _vm.DrawOfferRequested  += OnDrawOfferRequested;
        _vm.PropertyChanged     += OnVmPropertyChanged;
        _vm.RequestHandoff      += ShowHandoffOverlay;

        _selectedDiff        = Preferences.Default.Get("AiDifficulty", 0);
        _selectedTimeMinutes = Preferences.Default.Get("GameTimeMinutes", 0);
        BuildBoard();
        BoardThemeService.ThemeChanged += OnThemeChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        AdminBar.IsVisible = AppState.Current.IsAdminMode;

        // Carrega as imagens das peças já com o GraphicsView anexado à tela
        // (evita criar o bitmap Win2D antes de existir um CanvasControl ativo,
        // o que causava um fail-fast nativo mais tarde, ao maximizar a janela).
        _ = BoardDrawable.EnsureImagesLoadedAsync()
            .ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() => BoardView.Invalidate()));

        SelectDiff(_selectedDiff);
        SelectTime(_selectedTimeMinutes);


        var state = AppState.Current;

        // Consome a flag UMA ÚNICA VEZ — evita reiniciar o jogo em cada OnAppearing
        if (state.PendingCareerGame)
        {
            state.PendingCareerGame  = false;
            SetupPanel.IsVisible     = false;
            ResultPanel.IsVisible    = false;
            Title = "Modo Carreira";
            _vm.StartTournamentGame(
                state.CareerOpponentName,
                state.CareerTimeMinutes,
                state.CareerAIDepth,
                state.CareerSkillLevel);
        }
        else if (state.PendingTournamentGame)
        {
            state.PendingTournamentGame = false;
            SetupPanel.IsVisible        = false;
            ResultPanel.IsVisible       = false;
            Title = $"vs {state.TournamentOpponentName}";
            _vm.StartTournamentGame(
                state.TournamentOpponentName,
                state.TournamentTimeMinutes,
                state.TournamentAIDepth);
        }
        else if (state.PendingOnlineGame)
        {
            state.PendingOnlineGame = false;
            SetupPanel.IsVisible    = false;
            ResultPanel.IsVisible   = false;
            Title = $"vs {state.OnlineOpponentName}";
            string myName  = AppState.Current.Profile.Name;
            string oppName = state.OnlineOpponentName;
            WhitePlayerLabel.Text = state.OnlinePlayerIsWhite
                ? $"♙ {myName} (Brancas)"
                : $"♙ {oppName} (Brancas)";
            BlackPlayerLabel.Text = state.OnlinePlayerIsWhite
                ? $"♟ {oppName} (Pretas)"
                : $"♟ {myName} (Pretas)";
            _vm.StartTournamentGame(oppName, state.OnlineTimeMinutes, 3);
        }
        else if (state.PendingFriendGame)
        {
            state.PendingFriendGame = false;
            string p1 = string.IsNullOrWhiteSpace(state.FriendPlayer1Name) ? "Jogador 1" : state.FriendPlayer1Name;
            string p2 = string.IsNullOrWhiteSpace(state.FriendOpponentName) ? "Jogador 2" : state.FriendOpponentName;

            // Desafiante (p1) começa com brancas no 1º jogo; cores invertem a cada partida
            bool p1IsWhite = state.FriendGameCount % 2 == 0;
            state.FriendGameCount++;

            string white = p1IsWhite ? p1 : p2;
            string black = p1IsWhite ? p2 : p1;

            Title = $"{p1} vs {p2}";
            _vm.StartFriendGame(white, black, state.FriendTimeMinutes);
            WhitePlayerLabel.Text  = $"♙ {white} (Brancas)";
            BlackPlayerLabel.Text  = $"♟ {black} (Pretas)";
            SetupPanel.IsVisible   = false;
            HandoffPanel.IsVisible = false;
            _drawable.IsFlipped    = false;
        }
        else if (!_vm.IsTournamentMode && !_vm.IsFriendMode && _vm.GameOver)
        {
            ResultPanel.IsVisible = false;
            SetupPanel.IsVisible  = true;
            SelectDiff(_selectedDiff);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        var state = AppState.Current;

        // Fallback para carreira: captura resultado real mesmo que TournamentGameEnded não tenha disparado
        if (state.IsCareerGame && _vm.GameOver && !state.MatchResultReady)
        {
            bool isDraw = _vm.StatusMessage.Contains("Empate") || _vm.StatusMessage.Contains("Afogamento");
            state.LastMatchHumanWon = _vm.HumanWon == true;
            state.LastMatchWasDraw  = isDraw;
            state.MatchResultReady  = true;
        }
        // Fallback original para torneios de bracket
        else if (_vm.IsTournamentMode && _vm.GameOver && !state.MatchResultReady)
        {
            state.MatchResultReady = true;
        }
    }

    // -----------------------------------------------------------------------
    // Torneio — navega automaticamente ao fim da partida
    // -----------------------------------------------------------------------
    private void OnTournamentGameEnded(bool humanWon)
    {
        if (_resultShownForGame) return;
        _resultShownForGame = true;

        var state = AppState.Current;
        int starsEarned = 0;
        starsEarned += state.Daily.RecordGamePlayed();

        bool isDraw = _vm.StatusMessage.Contains("Empate") || _vm.StatusMessage.Contains("Afogamento");
        int  eloDelta = 0;

        if (state.IsCareerGame)
        {
            // O rating/Elo do perfil não é alterado pelo Modo Carreira — a progressão de
            // carreira já tem seu próprio placar (pontos Suíço, fases, título), e os
            // adversários de carreira são IA em níveis controlados, não uma amostra justa
            // pra calibrar o rating "de verdade" (que reflete jogos contra outros usuários).
            state.LastMatchHumanWon = humanWon;
            state.LastMatchWasDraw  = isDraw;
            state.MatchResultReady  = true;
        }
        else if (state.IsOnlineGame)
        {
            int oppElo = state.OnlineMatch.State.OpponentRating;
            eloDelta   = state.Profile.UpdateElo(oppElo, humanWon, isDraw);
            if (humanWon)     { state.Profile.RecordWin(); starsEarned += state.Daily.RecordWin(); }
            else if (!isDraw)   state.Profile.RecordLoss();
        }

        if (starsEarned > 0) state.Stars.Add(starsEarned);

        bool   hasNextRound = false;
        int    nextRoundNum = 1;
        string nextBtnText  = "Próxima";
        if (state.IsCareerGame)
        {
            var prog = state.Career.Progress;
            var t    = prog.ActiveTournament;
            if (t != null)
            {
                nextRoundNum = t.CurrentRound + 1;
                hasNextRound = t.Format switch
                {
                    CareerFormat.Swiss => t.CurrentRound < t.TotalRounds,
                    // Elimination: only continue if human won (loss = eliminated)
                    CareerFormat.Elimination => humanWon && !isDraw && t.CurrentRound < t.TotalRounds,
                    // BestOfN: continue if neither player reached win threshold
                    CareerFormat.BestOfN =>
                        (t.HumanWins   + (humanWon && !isDraw ? 1 : 0)) < t.WinsNeeded &&
                        (t.HumanLosses + (!humanWon && !isDraw ? 1 : 0)) < t.WinsNeeded &&
                        t.CurrentRound < t.TotalRounds,
                    _ => false
                };
                nextBtnText = t.Format switch
                {
                    CareerFormat.Elimination => nextRoundNum == 3 ? "Jogar a Final" : $"Fase {nextRoundNum}",
                    CareerFormat.BestOfN     => $"Partida {nextRoundNum}",
                    _                        => $"Rodada {nextRoundNum}"
                };
            }
        }

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            ApplyResultColors(humanWon, isDraw);
            ResultTitle.Text  = humanWon ? "Vitória!" : isDraw ? "Empate" : "Derrota";
            ResultDetail.Text = _vm.StatusMessage;

            if (state.IsOnlineGame)
            {
                int oldPoints = state.Profile.Points - eloDelta;
                string eloSign2 = eloDelta >= 0 ? "+" : "";
                ResultOldRating.Text        = $"{oldPoints}";
                ResultNewRating.Text        = $"{state.Profile.Points}";
                ResultRatingDelta.Text      = $"{eloSign2}{eloDelta}";
                ResultRatingDelta.TextColor = eloDelta >= 0 ? Color.FromArgb("#4CAF50") : Color.FromArgb("#FF5252");
                ResultRatingRow.IsVisible   = true;
            }
            else
            {
                ResultRatingRow.IsVisible = false;
            }
            ResultSetupBtn.IsVisible = false;

            if (state.IsCareerGame)
            {
                ResultActionBtn.IsVisible      = hasNextRound;
                ResultActionBtn.Text           = nextBtnText;
                ResultSecondaryLabel.IsVisible = true;
                ResultSecondaryLabel.Text      = "Ver resultado da fase";
            }
            else if (state.IsOnlineGame)
            {
                ResultActionBtn.IsVisible       = true;
                ResultActionBtn.Text            = "🔄 Revanche";
                ResultActionBtn.BackgroundColor = Color.FromArgb("#1A4A7A");
                NewOnlineBtn.IsVisible          = true;
                NewOnlineBtn.Text              = $"🔍 Nova Partida · {state.OnlineTimeMinutes}min";
                ResultSecondaryLabel.IsVisible  = true;
                ResultSecondaryLabel.Text       = "← Voltar à Arena";
            }
            else
            {
                ResultActionBtn.IsVisible  = true;
                ResultActionBtn.Text       = "Voltar ao Torneio";
                ResultSecondaryLabel.Text  = "← Voltar à Arena";
            }
            await Task.Delay(2000);
            _resultExpanded            = true;
            ResultExpandable.IsVisible = true;
            ResultChevron.Text         = "▲";
            ResultPanel.IsVisible      = true;
        });
    }

    // -----------------------------------------------------------------------
    // Missões: registra partidas casuais
    // -----------------------------------------------------------------------
    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(_vm.GameOver) || !_vm.GameOver) return;
        if (_vm.IsTournamentMode) return;

        if (_vm.IsFriendMode)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                HandoffPanel.IsVisible = false;
                bool isDraw   = _vm.StatusMessage.Contains("Empate") || _vm.StatusMessage.Contains("Afogamento");
                bool whiteWon = _vm.StatusMessage.Contains(_vm.WhitePlayerName) && _vm.StatusMessage.Contains("vence");
                ApplyResultColors(!isDraw, isDraw);
                ResultTitle.Text  = isDraw ? "Empate" : whiteWon ? $"{_vm.WhitePlayerName} vence!" : $"{_vm.BlackPlayerName} vence!";
                ResultDetail.Text = _vm.StatusMessage;
                ResultRatingRow.IsVisible = false;
                ResultSetupBtn.IsVisible  = false;
                ResultActionBtn.Text   = "← Voltar à Arena";
                ResultSecondaryLabel.IsVisible = false;
                ResultPanel.IsVisible  = true;
            });
            return;
        }

        // HumanWon é setado pelo admin (ForceWin/ForceLoss); senão, lê o StatusMessage
        bool humanWon = _vm.HumanWon.HasValue
            ? _vm.HumanWon == true
            : _vm.StatusMessage.Contains("Brancas vencem");

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var state = AppState.Current;

            // Registra W/L e atualiza Elo
            bool isDraw2 = _vm.StatusMessage.Contains("Empate") || _vm.StatusMessage.Contains("Afogamento");
            int  starsNow = 0;
            starsNow += state.Daily.RecordGamePlayed();
            if (humanWon)      { state.Profile.RecordWin();  starsNow += state.Daily.RecordWin(); }
            else if (!isDraw2)   state.Profile.RecordLoss();

            if (starsNow > 0) state.Stars.Add(starsNow);

            int aiDepth   = DiffDepths[_selectedDiff];
            int oppElo    = ProfileService.EloRatingForAI(aiDepth);
            int eloChange = state.Profile.UpdateElo(oppElo, humanWon, isDraw2);

            // Painel de resultado
            ApplyResultColors(humanWon, isDraw2);
            ResultTitle.Text  = humanWon ? "Vitória!" : isDraw2 ? "Empate" : "Derrota";
            ResultDetail.Text = _vm.StatusMessage;
            string eloSign = eloChange >= 0 ? "+" : "";
            int oldPoints = state.Profile.Points - eloChange;
            ResultOldRating.Text        = $"{oldPoints}";
            ResultNewRating.Text        = $"{state.Profile.Points}";
            ResultRatingDelta.Text      = $"{eloSign}{eloChange}";
            ResultRatingDelta.TextColor = eloChange >= 0 ? Color.FromArgb("#4CAF50") : Color.FromArgb("#FF5252");
            ResultRatingRow.IsVisible   = true;
            string timeLabel = _selectedTimeMinutes == 0 ? "sem relógio" : $"{_selectedTimeMinutes} min";
            ResultActionBtn.Text           = $"Nova partida\n{timeLabel}";
            ResultActionBtn.HeightRequest  = 58;
            ResultSetupBtn.IsVisible       = true;
            ResultSecondaryLabel.IsVisible = true;
            ResultSecondaryLabel.Text      = "← Voltar à Arena";

            // Frase de resumo da partida
            int moveCount = _vm.MoveCount;
            ResultSummaryLabel.Text    = GenerateGameSummary(humanWon, isDraw2, moveCount, _vm.StatusMessage);
            ResultSummaryRow.IsVisible = true;

            // 2s para o jogador ver o tabuleiro com o lance final
            await Task.Delay(2000);
            _resultExpanded            = true;
            ResultExpandable.IsVisible = true;
            ResultChevron.Text         = "▲";
            ResultPanel.IsVisible      = true;
        });
    }

    private void OnResultToggle(object? sender, EventArgs e)
    {
        _resultExpanded = !_resultExpanded;
        ResultExpandable.IsVisible = _resultExpanded;
        ResultChevron.Text         = _resultExpanded ? "▲" : "▼";
    }

    private void ApplyResultColors(bool won, bool draw)
    {
        var accent = won  ? Color.FromArgb("#3FB950")
                   : draw ? Color.FromArgb("#D29922")
                          : Color.FromArgb("#F85149");
        var bgColor = won  ? Color.FromArgb("#0D2A15")
                    : draw ? Color.FromArgb("#1A1500")
                           : Color.FromArgb("#2A0D0D");
        var stroke  = won  ? Color.FromArgb("#238636")
                    : draw ? Color.FromArgb("#9E6A03")
                           : Color.FromArgb("#B91C1C");

        ResultTopBar.BackgroundColor  = accent;
        ResultIcon.Text      = won ? "✓" : draw ? "½" : "✗";
        ResultIcon.TextColor = accent;
        ResultIconBorder.BackgroundColor = bgColor;
        ResultIconBorder.Stroke          = stroke;
        ResultTitle.TextColor = accent;
    }

    private static string GenerateGameSummary(bool humanWon, bool isDraw, int moves, string status)
    {
        bool byMate      = status.Contains("xeque-mate",  StringComparison.OrdinalIgnoreCase);
        bool byTime      = status.Contains("tempo",       StringComparison.OrdinalIgnoreCase);
        bool byResign    = status.Contains("desistiu",    StringComparison.OrdinalIgnoreCase)
                        || status.Contains("desistência", StringComparison.OrdinalIgnoreCase);
        bool byStalemate = status.Contains("afogamento",  StringComparison.OrdinalIgnoreCase);

        bool earlyGame = moves < 20;
        bool midGame   = moves >= 20 && moves < 40;
        bool lateGame  = moves >= 40;

        if (byStalemate) return Pick(
            "O rei adversário ficou sem casas disponíveis sem estar em xeque — afogamento. A vantagem existia, mas o cálculo final não considerou essa saída.",
            "Afogamento: o rei do adversário não tinha lances legais, mas não estava em xeque. Uma jogada diferente teria evitado o empate.",
            "A vitória escapou por afogamento. Na posição final, qualquer lance deixava o rei adversário em xeque — a saída de empate ficou aberta.");

        if (isDraw)
        {
            if (earlyGame) return Pick(
                "As trocas precoces eliminaram o desequilíbrio antes de qualquer lado criar ameaças reais. A partida terminou empatada sem tensão.",
                "O equilíbrio foi estabelecido cedo demais. As simplificações na abertura deixaram a posição sem tensão suficiente para qualquer lado vencer.",
                "Empate resultante de trocas precoces. Com poucas peças no tabuleiro desde cedo, nenhum lado teve material para criar pressão real.");
            if (midGame) return Pick(
                "O equilíbrio material se manteve durante todo o meio-jogo. Nenhum dos lados encontrou um erro explorável para converter em vitória.",
                "Posição tecnicamente equilibrada no meio-jogo. As oportunidades de criar desequilíbrio apareceram, mas nenhum lado as aproveitou de forma decisiva.",
                "O jogo foi bem disputado, mas sem erros graves de ambos os lados. O empate reflete a qualidade mútua nesta partida.");
            return Pick(
                "Final tecnicamente equilibrado. Peças simétricas e estrutura de peões idêntica tornaram a conversão impossível para ambos os lados.",
                "No final, o equilíbrio de material e de estrutura de peões não deixou margem para vitória. Empate justo pelo que a posição oferecia.",
                "As peças restantes não eram suficientes para forçar a decisão. Um final bem defendido dos dois lados resultou no empate.");
        }

        if (humanWon)
        {
            if (byMate && earlyGame) return Pick(
                "Xeque-mate na abertura. O adversário não completou o desenvolvimento e deixou o rei no centro — alvo direto do ataque nas colunas abertas.",
                "Vitória rápida por xeque-mate. O rei adversário ficou preso no centro desde cedo, sem tempo para fazer o roque antes do ataque se concretizar.",
                "O adversário atrasou o desenvolvimento e pagou o preço: xeque-mate antes do jogo chegar ao meio-jogo.");
            if (byMate && midGame) return Pick(
                "O ataque foi construído explorando o rei mal posicionado do adversário. A falta de coordenação das peças defensivas abriu caminho para o xeque-mate.",
                "Xeque-mate no meio-jogo. As fraquezas em torno do rei adversário foram exploradas com precisão, sem deixar tempo para reorganizar a defesa.",
                "O rei adversário ficou sem cobertura no momento crítico. O ataque foi montado com peças coordenadas e terminou em xeque-mate.");
            if (byMate && lateGame) return Pick(
                "Superioridade convertida em xeque-mate no final. O rei adversário ficou isolado enquanto as peças pesadas coordenaram o ataque decisivo.",
                "A vantagem material do final foi convertida em xeque-mate. O rei adversário não tinha como escapar das peças coordenadas.",
                "Xeque-mate no final de jogo. A atividade do rei e das peças pesadas foi suficiente para encurralar o adversário sem saída.");
            if (byTime && earlyGame) return Pick(
                "A pressão constante na abertura forçou o adversário a gastar tempo calculando cada resposta. O relógio se esgotou antes de estabilizar a posição.",
                "Vitória pelo relógio. A abertura criou posição complexa que exigiu muito tempo do adversário para calcular as respostas corretas.",
                "O adversário perdeu no tempo durante a abertura. A pressão nas primeiras jogadas consumiu o relógio antes de qualquer estabilização.");
            if (byTime) return Pick(
                "A posição complexa exigiu muito tempo do adversário. A pressão contínua deixou poucas opções simples, e o relógio foi decisivo.",
                "Vitória pelo tempo. A posição foi mantida tensa o suficiente para forçar o adversário a calcular lances longos que consumiram o relógio.",
                "O relógio foi o árbitro final. A pressão acumulada ao longo da partida deixou o adversário sem tempo para calcular a defesa correta.");
            if (earlyGame) return Pick(
                "O adversário cometeu erros de desenvolvimento na abertura que foram aproveitados imediatamente — peças centralizadas criaram pressão irresistível.",
                "Vitória na abertura. Erros do adversário foram punidos com precisão, sem deixar espaço para recuperação.",
                "A abertura foi resolvida rapidamente. O adversário não conseguiu completar o desenvolvimento antes de a posição se tornar insustentável.");
            if (midGame) return Pick(
                "Coordenação de peças superior no meio-jogo. As torres e peças menores trabalharam juntas para criar ameaças que o adversário não conseguiu neutralizar.",
                "A vantagem foi construída gradualmente no meio-jogo. Cada lance aumentou a pressão até o adversário não ter mais defesa.",
                "Domínio tático no meio-jogo. A posição das peças foi superior em todos os momentos críticos, até a vantagem se tornar decisiva.");
            if (lateGame) return Pick(
                "Técnica de final precisa. A vantagem de peões foi convertida metodicamente, sem conceder contra-jogo ao adversário.",
                "O final foi gerenciado com cuidado. A superioridade foi convertida passo a passo, sem permitir ao adversário criar complicações.",
                "Vitória técnica no final. A vantagem material foi aproveitada com precisão, mantendo o adversário passivo até o fim.");
            return Pick(
                "Domínio ao longo de toda a partida. A vantagem acumulada aos poucos foi convertida com consistência no final.",
                "Uma partida bem gerenciada. A vantagem foi construída gradualmente e convertida sem grandes complicações.",
                "Vitória sólida. O jogo foi controlado do início ao fim, com a vantagem sendo consolidada a cada fase da partida.");
        }
        else
        {
            if (byMate && earlyGame) return Pick(
                "O rei ficou preso no centro durante toda a abertura. Com as colunas abertas e as peças adversárias desenvolvidas, o xeque-mate foi inevitável.",
                "Xeque-mate precoce. O desenvolvimento incompleto deixou o rei exposto antes de qualquer proteção ser montada.",
                "A abertura não foi concluída a tempo. Com o rei no centro e as colunas abertas, o adversário encontrou o xeque-mate rapidamente.");
            if (byMate && midGame) return Pick(
                "Uma fraqueza na estrutura de peões em torno do rei se tornou o alvo principal do adversário. O ataque foi montado lance a lance até o xeque-mate.",
                "O rei ficou mal protegido no meio-jogo. O adversário explorou as fraquezas ao redor com peças coordenadas e chegou ao xeque-mate.",
                "O ataque adversário foi construído com paciência. As fraquezas em torno do rei acumularam até não haver mais defesa possível.");
            if (byMate && lateGame) return Pick(
                "No final, a coordenação entre rei e peões pesados do adversário foi superior. O rei foi encurralado progressivamente até não restar escapatória.",
                "O final de jogo revelou a superioridade adversária. O rei foi afastado das peças aliadas e encurralado sem saída.",
                "Xeque-mate no final. A desvantagem material acumulada não deixou peças suficientes para montar defesa adequada.");
            if (byTime && earlyGame) return Pick(
                "A abertura criou posição complexa com muitas opções táticas. O tempo foi consumido calculando as respostas corretas, e o relógio decidiu antes da posição.",
                "Derrota pelo tempo na abertura. A complexidade das posições iniciais exigiu muito tempo de cálculo, esgotando o relógio cedo.",
                "O relógio foi adverso desde a abertura. Com tantas opções táticas para calcular, o tempo se esgotou antes de encontrar o melhor caminho.");
            if (byTime) return Pick(
                "Posição tecnicamente equilibrada, mas o relógio foi o fator decisivo. O tempo se esgotou em um momento de alta tensão tática.",
                "Derrota pelo tempo. A posição era defensável, mas calcular a resposta certa consumiu o tempo restante no momento decisivo.",
                "O relógio decidiu em um momento de alta pressão. Com pouco tempo e posição complexa, a decisão correta não veio a tempo.");
            if (earlyGame) return Pick(
                "A abertura saiu do controle antes do desenvolvimento estar completo. Uma peça ficou sem defensor e o adversário aproveitou a sequência tática imediatamente.",
                "Os erros vieram cedo demais. Com o desenvolvimento incompleto, o adversário explorou as fraquezas antes de qualquer organização ser possível.",
                "A abertura não correu bem. Um erro tático precoce criou desvantagem que se revelou impossível de recuperar.");
            if (midGame) return Pick(
                "Um lance tático no meio-jogo criou desequilíbrio material que se revelou decisivo. A partir dali, o adversário converteu a vantagem com consistência.",
                "O meio-jogo foi o ponto de virada. Uma imprecisão tática entregou a iniciativa ao adversário, que não desperdiçou a oportunidade.",
                "A desvantagem surgiu no meio-jogo. Um único erro custou material suficiente para o adversário converter a partida com tranquilidade.");
            if (lateGame) return Pick(
                "A diferença apareceu na técnica de final. Peões passados e atividade do rei foram os fatores que determinaram o resultado nas últimas jogadas.",
                "O final foi tecnicamente superior ao adversário. A atividade do rei e dos peões passados foi determinante para o resultado.",
                "A desvantagem ficou clara no final de jogo. A técnica adversária na conversão não deixou margem para complicar a posição.");
            return Pick(
                "A partida foi disputada, mas pequenas imprecisões acumuladas ao longo do jogo criaram vantagem que o adversário soube preservar até o fim.",
                "Derrota por acúmulo de imprecisões. Cada pequeno erro cedeu um pouco de vantagem até o adversário ter o suficiente para converter.",
                "O jogo foi equilibrado, mas os erros pesaram. A vantagem adversária foi construída gradualmente e mantida com consistência até o final.");
        }
    }

    private static string Pick(params string[] options)
        => options[Random.Shared.Next(options.Length)];

    // -----------------------------------------------------------------------
    // Handoff pass-and-play — exibe overlay entre turnos
    // -----------------------------------------------------------------------
    private void ShowHandoffOverlay(string nextPlayerName)
    {
        // Sem painel — o relógio controla a vez. Só inverte a perspectiva com fade.
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            _nextTurnIsBlack = nextPlayerName == _vm.BlackPlayerName;
            await BoardView.FadeTo(0, 110);
            _drawable.IsFlipped = _nextTurnIsBlack;
            BoardView.Invalidate();
            await BoardView.FadeTo(1, 110);
        });
    }

    private void OnHandoffDismissed(object? sender, EventArgs e)
    {
        HandoffPanel.IsVisible = false;
    }

    // -----------------------------------------------------------------------
    // Chat do bot — exibe balão e some após 3 s
    // -----------------------------------------------------------------------
    private void OnChatMessageReceived(string message)
    {
        _chatCts?.Cancel();
        _chatCts = new CancellationTokenSource();
        var token = _chatCts.Token;

        ChatLabel.Text       = $"🤖  {message}";
        ChatBubble.IsVisible = true;

        Task.Run(async () =>
        {
            await Task.Delay(3000, token);
            if (!token.IsCancellationRequested)
                MainThread.BeginInvokeOnMainThread(() => ChatBubble.IsVisible = false);
        }, token);
    }

    // -----------------------------------------------------------------------
    // Admin: forçar resultado
    // -----------------------------------------------------------------------
    private void OnAdminWin(object? sender, EventArgs e)  => _vm.ForceWin();
    private void OnAdminLose(object? sender, EventArgs e) => _vm.ForceLoss();

    // -----------------------------------------------------------------------
    // Confirmação: Desistir
    // -----------------------------------------------------------------------
    private Task<bool> OnResignRequested()
        => DisplayAlert("Desistir", "Tem certeza que quer desistir?", "Sim, desistir", "Cancelar");

    // -----------------------------------------------------------------------
    // Confirmação: Propor empate — simula resposta da IA (~30% aceita)
    // -----------------------------------------------------------------------
    private async Task<bool> OnDrawOfferRequested()
    {
        bool aiAccepts = Random.Shared.NextDouble() < 0.30;
        string msg = aiAccepts
            ? "A IA aceita o empate."
            : "A IA recusa o empate.";
        await DisplayAlert("Proposta de Empate", msg, "OK");
        return aiAccepts;
    }

    private async void OnNewOnlineClicked(object? sender, EventArgs e)
    {
        var state = AppState.Current;

        NewOnlineBtn.IsEnabled    = false;
        ResultActionBtn.IsEnabled = false;
        ResultDetail.Text         = "🔍 Buscando novo oponente...";

        await state.OnlineMatch.StartSearchingAsync(state.OnlineTimeMinutes, state.Profile.Points);

        if (state.OnlineMatch.State.Phase != OnlineMatchPhase.Confirmed)
        {
            NewOnlineBtn.IsEnabled    = true;
            ResultActionBtn.IsEnabled = true;
            return;
        }

        var s      = state.OnlineMatch.State;
        string myName  = state.Profile.Name;
        string oppName = s.OpponentName;

        state.OnlineOpponentName  = oppName;
        state.OnlinePlayerIsWhite = s.PlayerIsWhite;

        WhitePlayerLabel.Text = s.PlayerIsWhite
            ? $"♙ {myName} (Brancas)"
            : $"♙ {oppName} (Brancas)";
        BlackPlayerLabel.Text = s.PlayerIsWhite
            ? $"♟ {oppName} (Pretas)"
            : $"♟ {myName} (Pretas)";

        NewOnlineBtn.IsEnabled    = true;
        ResultActionBtn.IsEnabled = true;
        NewOnlineBtn.IsVisible    = false;
        ResultPanel.IsVisible     = false;
        _resultShownForGame       = false;
        Title = $"vs {oppName}";
        _vm.StartTournamentGame(oppName, state.OnlineTimeMinutes, 3);
    }


    private void OnResultSetupClicked(object? sender, EventArgs e)
    {
        ResultPanel.IsVisible          = false;
        ResultSetupBtn.IsVisible       = false;
        NewOnlineBtn.IsVisible         = false;
        WhitePlayerLabel.Text          = "♙ Você (Brancas)";
        BlackPlayerLabel.Text          = "♟ IA (Pretas)";
        Title                          = "ChessArena";
        AppState.Current.IsCareerGame  = false;
        SelectDiff(_selectedDiff);
        SetupPanel.IsVisible           = true;
    }

    private async void OnResultBackTapped(object? sender, TappedEventArgs e)
    {
        AppState.Current.IsOnlineGame   = false;
        ResultPanel.IsVisible           = false;
        ResultSetupBtn.IsVisible        = false;
        NewOnlineBtn.IsVisible          = false;
        await Shell.Current.GoToAsync("..");
    }

    private void SelectDiff(int idx)
    {
        _selectedDiff = idx;
        Preferences.Default.Set("AiDifficulty", idx);
        DiffLabel.Text = $"Dificuldade: {DiffLabels[idx]}";
    }

    // 0 = sem relógio; até 30 min, mesmo teto do Jogar Online (ver RandomMatchPage).
    private const int MinCasualTime = 0;
    private const int MaxCasualTime = 30;

    private void SelectTime(int minutes)
    {
        _selectedTimeMinutes = Math.Clamp(minutes, MinCasualTime, MaxCasualTime);
        Preferences.Default.Set("GameTimeMinutes", _selectedTimeMinutes);
        TimeValueLabel.Text      = _selectedTimeMinutes == 0 ? "Sem relógio" : $"{_selectedTimeMinutes} min";
        TimeBtnDecrease.IsEnabled = _selectedTimeMinutes > MinCasualTime;
        TimeBtnIncrease.IsEnabled = _selectedTimeMinutes < MaxCasualTime;
    }

    private void OnTimeDecrease(object? sender, EventArgs e) => SelectTime(_selectedTimeMinutes - 1);
    private void OnTimeIncrease(object? sender, EventArgs e) => SelectTime(_selectedTimeMinutes + 1);

    private void OnDiffSettingsClicked(object? sender, TappedEventArgs e)
    {
        DiffOverlay.IsVisible  = true;
        DiffDropdown.IsVisible = true;
    }

    private void OnDiffOverlayDismiss(object? sender, TappedEventArgs e)
    {
        DiffOverlay.IsVisible  = false;
        DiffDropdown.IsVisible = false;
    }

    private void OnDiffEasyTapped(object? sender, TappedEventArgs e)
    {
        OnDiffOverlayDismiss(sender, e);
        SelectDiff(0);
    }

    private void OnDiffMediumTapped(object? sender, TappedEventArgs e)
    {
        OnDiffOverlayDismiss(sender, e);
        SelectDiff(1);
    }

    private void OnDiffHardTapped(object? sender, TappedEventArgs e)
    {
        OnDiffOverlayDismiss(sender, e);
        SelectDiff(2);
    }

    private void OnDiffHardcoreTapped(object? sender, TappedEventArgs e)
    {
        OnDiffOverlayDismiss(sender, e);
        SelectDiff(3);
    }

    private void OnSetupNewGameClicked(object? sender = null, EventArgs? e = null)
    {
        ResultPanel.IsVisible          = false;
        SetupPanel.IsVisible           = false;
        ResultSetupBtn.IsVisible       = false;
        ResultActionBtn.HeightRequest  = 48;
        WhitePlayerLabel.Text         = "♙ Você (Brancas)";
        BlackPlayerLabel.Text         = "♟ IA (Pretas)";
        Title                         = "ChessArena";
        AppState.Current.IsCareerGame = false;
        // Skill Level real por dificuldade (mesma escala 1-4 do Modo Carreira: Fácil=3,
        // Médio=10, Difícil=16, Hard=20) — antes a IA sempre jogava em força máxima (20)
        // no modo casual, só variando o tempo de raciocínio, o que fazia até o "Fácil"
        // continuar difícil de vencer de verdade.
        int skillLevel = CareerService.GetSkillLevel(_selectedDiff + 1);
        _vm.StartNewGame(_selectedTimeMinutes, DiffDepths[_selectedDiff], skillLevel: skillLevel);
    }

    private async void OnResultActionClicked(object? sender, EventArgs e)
    {
        var state = AppState.Current;

        if (state.IsCareerGame)
        {
            if (!state.MatchResultReady)
            {
                ResultPanel.IsVisible = false;
                await Shell.Current.GoToAsync("..");
                return;
            }

            state.MatchResultReady = false;
            var (hasNext, opp) = state.Career.ProcessRoundResult(
                state.LastMatchHumanWon, state.LastMatchWasDraw, state.CareerOpponentName);

            if (!hasNext || opp == null)
            {
                // Torneio encerrado — sem próxima rodada. Sem isso, IsCareerGame ficava preso
                // em "true", e uma partida Online jogada logo em seguida (sem passar por
                // "Nova Partida") acabava sendo processada por engano como rodada de Carreira.
                state.IsCareerGame    = false;
                ResultPanel.IsVisible = false;
                await Shell.Current.GoToAsync("..");
                return;
            }

            state.CareerOpponentName = opp.Name;
            state.CareerAIDepth      = 3; // tempo de raciocínio fixo — não representa mais dificuldade
            // CareerTimeMinutes não muda entre rodadas — é o tempo que o jogador escolheu
            // na CareerPage antes de começar o torneio, não varia mais com a dificuldade.
            state.CareerSkillLevel   = CareerService.GetSkillLevel(opp.Difficulty);
            ResultPanel.IsVisible = false;

            // Copa do Mundo (eliminação): cada rodada agora é melhor-de-2, então perder o
            // 1º jogo não encerra nada — tem um 2º jogo (e desempate, se 1-1) contra o MESMO
            // adversário. Sem isso, o app pulava direto pro próximo jogo sem avisar, e parecia
            // bug ("perdi e continuou jogando a mesma fase?"). Aqui, volta pra CareerPage
            // pra mostrar "Jogo 2/2" ou "Desempate" antes de continuar.
            bool isElimination = state.Career.Progress.ActiveTournament?.Format == CareerFormat.Elimination;
            if (isElimination)
            {
                await Shell.Current.GoToAsync("..");
                return;
            }

            // Sem isso, OnTournamentGameEnded barra na guarda "if (_resultShownForGame) return;"
            // pro resto do torneio inteiro — a rodada 2 em diante terminava sem nunca exibir o
            // painel de resultado nem o botão de continuar, já que essa mesma GamePage é
            // reaproveitada entre rodadas (sem recriar a página).
            _resultShownForGame = false;
            _vm.StartTournamentGame(opp.Name, state.CareerTimeMinutes, state.CareerAIDepth, state.CareerSkillLevel);
            return;
        }

        if (state.IsOnlineGame)
        {
            if (ResultActionBtn.Text.Contains("Revanche"))
            {
                ResultActionBtn.IsEnabled = false;
                NewOnlineBtn.IsEnabled    = false;
                ResultActionBtn.Text      = "Aguardando...";
                ResultDetail.Text         = "Oponente aceitou! Preparando nova partida...";
                await Task.Delay(1500);

                // Inverte as cores para a revanche
                state.OnlinePlayerIsWhite = !state.OnlinePlayerIsWhite;

                string myName  = state.Profile.Name;
                string oppName = state.OnlineOpponentName;
                WhitePlayerLabel.Text = state.OnlinePlayerIsWhite
                    ? $"♙ {myName} (Brancas)"
                    : $"♙ {oppName} (Brancas)";
                BlackPlayerLabel.Text = state.OnlinePlayerIsWhite
                    ? $"♟ {oppName} (Pretas)"
                    : $"♟ {myName} (Pretas)";

                ResultActionBtn.IsEnabled = true;
                NewOnlineBtn.IsEnabled    = true;
                NewOnlineBtn.IsVisible    = false;
                ResultPanel.IsVisible     = false;
                _resultShownForGame       = false;
                Title = $"Revanche vs {oppName}";
                _vm.StartTournamentGame(oppName, state.OnlineTimeMinutes, 3);
            }
            else
            {
                NewOnlineBtn.IsVisible = false;
                state.IsOnlineGame     = false;
                ResultPanel.IsVisible  = false;
                await Shell.Current.GoToAsync("..");
            }
            return;
        }

        ResultPanel.IsVisible = false;
        if (_vm.IsTournamentMode || _vm.IsFriendMode)
        {
            await Shell.Current.GoToAsync("..");
        }
        else
            OnSetupNewGameClicked();
    }

    // -----------------------------------------------------------------------
    // Botão: Som — alterna mudo/ativo
    // -----------------------------------------------------------------------
    private void OnSoundToggled(object? sender, EventArgs e)
    {
        _vm.SoundEnabled = !_vm.SoundEnabled;
        SoundBtn.Text    = _vm.SoundEnabled ? "🔊" : "🔇";
    }

    // -----------------------------------------------------------------------
    // Promoção de peão — exibe popup de escolha
    // -----------------------------------------------------------------------
    private async void OnPromotionRequested(string color)
    {
        string title  = "Promover Peão";
        string? choice = await DisplayActionSheet(title, null, null,
            "♛ Rainha", "♜ Torre", "♝ Bispo", "♞ Cavalo");

        string key = choice?.Split(' ')[1].ToLower() switch
        {
            "rainha" => "queen",
            "torre"  => "rook",
            "bispo"  => "bishop",
            "cavalo" => "knight",
            _        => "queen"
        };

        _vm.PromoteCommand.Execute(key);
    }

    // -----------------------------------------------------------------------
    // Configura o GraphicsView do tabuleiro
    // -----------------------------------------------------------------------
    private readonly BoardDrawable _drawable = new();

    private void BuildBoard()
    {
        _drawable.Squares = _vm.Squares;
        BoardView.Drawable = _drawable;

        _vm.BoardChanged += () =>
            MainThread.BeginInvokeOnMainThread(() => BoardView.Invalidate());

        var tap = new TapGestureRecognizer();
        tap.Tapped += OnBoardTapped;
        BoardView.GestureRecognizers.Add(tap);
    }

    private void OnThemeChanged()
    {
        MainThread.BeginInvokeOnMainThread(() => BoardView.Invalidate());
    }

    private async void OnThemePaletteClicked(object? sender, EventArgs e)
    {
        string? choice = await DisplayActionSheet(
            "Tema do tabuleiro", "Cancelar", null,
            BoardThemeService.ThemeLabels);

        if (choice == null || choice == "Cancelar") return;

        int idx = Array.IndexOf(BoardThemeService.ThemeLabels, choice);
        if (idx >= 0)
            BoardThemeService.SetTheme((BoardThemeService.Theme)idx);
    }

    private void OnBoardTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            if (_squareSize <= 0) return;
            var pos = e.GetPosition(BoardView);
            if (pos is null) return;
            int col = Math.Clamp((int)(pos.Value.X / _squareSize), 0, 7);
            int row = Math.Clamp((int)(pos.Value.Y / _squareSize), 0, 7);
            if (_drawable.IsFlipped) { col = 7 - col; row = 7 - row; }
            _vm.SquareTappedCommand.Execute(_vm.Squares[row, col]);
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
                await DisplayAlert("Erro no tabuleiro", ex.ToString(), "OK"));
        }
    }

    // -----------------------------------------------------------------------
    // Adapta o tamanho do tabuleiro à tela
    // -----------------------------------------------------------------------
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        double used = _vm.TimerVisible ? 280 : 180;
        double available = Math.Min(width - 16, height - used);
        if (available <= 0) return;

        _squareSize = available / 8.0;

        BoardView.WidthRequest  = available;
        BoardView.HeightRequest = available;
        BoardView.Invalidate();
    }
}
