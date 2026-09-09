using System.Collections.Concurrent;
using ChessMAUI.Services;
using ChessMAUI.ViewModels;
using Microsoft.Maui.Graphics.Platform;

namespace ChessMAUI.Views;

public class BoardDrawable : IDrawable
{
    public SquareViewModel[,]? Squares  { get; set; }
    public bool                IsFlipped { get; set; }

    private static readonly Color WhitePiece    = Colors.White;
    private static readonly Color BlackPiece    = Color.FromArgb("#1A1209");
    private static readonly Color GlowColor     = Colors.White;
    private static readonly Color ShadowColor   = Colors.Black;
    private static readonly Color LastMoveColor = Color.FromArgb("#8090EE90");

    private const string KingSymbol      = "♚";
    private const float  KingScale       = 1.20f;
    private const string WhitePawnSymbol = "♙";
    private const string BlackPawnSymbol = "♟";

    // ── Imagens das peças (arte customizada) ─────────────────────────────────
    private static readonly ConcurrentDictionary<string, Microsoft.Maui.Graphics.IImage> PieceImages = new();
    private static readonly string[] PieceCodes =
        { "wk", "wq", "wr", "wb", "wn", "wp", "bk", "bq", "br", "bb", "bn", "bp" };
    private static Task? _loadTask;

    public static Task EnsureImagesLoadedAsync() => _loadTask ??= LoadImagesAsync();

    private static async Task LoadImagesAsync()
    {
        // 1) Lê os bytes de cada arquivo (I/O simples, tanto faz a thread).
        var buffers = new Dictionary<string, byte[]>();
        foreach (var code in PieceCodes)
        {
            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync($"pieces/piece_{code}.png");
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                buffers[code] = ms.ToArray();
            }
            catch
            {
                // Sem imagem para esta peça: cai no desenho por símbolo Unicode.
            }
        }

        // 2) Cria todos os bitmaps (Win2D CanvasBitmap no Windows) em UM ÚNICO
        // despacho para a thread de UI. Criar cada imagem em um "await
        // MainThread..." separado devolve o controle à fila de mensagens entre
        // uma peça e outra — se um evento de maximizar a janela for processado
        // nesse intervalo, ocorre uma falha nativa (reentrância no Win2D/WinUI).
        // Fazendo tudo de uma vez, sem ceder o controle no meio, evitamos essa janela.
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            foreach (var (code, bytes) in buffers)
            {
                try
                {
                    using var ms = new MemoryStream(bytes);
                    PieceImages[code] = PlatformImage.FromStream(ms);
                }
                catch
                {
                    // Sem imagem para esta peça: cai no desenho por símbolo Unicode.
                }
            }
        });
    }

    private static string? PieceImageCode(SquareViewModel sq)
    {
        char t = sq.PieceSymbol switch
        {
            "♚" => 'k',
            "♛" => 'q',
            "♜" => 'r',
            "♝" => 'b',
            "♞" => 'n',
            "♟" => 'p',
            _   => '\0'
        };
        if (t == '\0' || sq.PieceIsWhite == null) return null;
        return $"{(sq.PieceIsWhite == true ? 'w' : 'b')}{t}";
    }

    // Altura-alvo de cada tipo de peça, como fração da altura da casa. Rei,
    // rainha, torre, bispo e cavalo dividem a mesma altura (mesmo "porte
    // físico") — só o peão é menor, como num jogo de xadrez real. Os recortes
    // de origem vieram de artes diferentes e não guardam essa proporção entre
    // si sozinhos.
    private static readonly Dictionary<char, float> PieceHeightFrac = new()
    {
        ['k'] = 0.94f,
        ['q'] = 0.94f,
        ['r'] = 0.84f,
        ['b'] = 0.94f,
        ['n'] = 0.94f,
        ['p'] = 0.80f,
    };

    public void Draw(ICanvas canvas, RectF bounds)
    {
        if (Squares == null) return;

        float cw       = bounds.Width  / 8f;
        float ch       = bounds.Height / 8f;
        float fontSize = MathF.Min(cw, ch) * 0.80f;
        float off      = MathF.Max(1.5f, fontSize * 0.055f);

        var (coordLight, coordDark) = BoardThemeService.CoordColors;
        canvas.Antialias = true;

        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
        {
            var   sq = Squares[r, c];
            int   vr = IsFlipped ? 7 - r : r;   // visual row
            int   vc = IsFlipped ? 7 - c : c;   // visual col
            float x  = vc * cw;
            float y  = vr * ch;

            // ── Fundo base ────────────────────────────────────────────
            canvas.FillColor = sq.BackgroundColor;
            canvas.FillRectangle(x, y, cw, ch);

            // ── Overlay verde transparente — somente última jogada ────
            if (sq.IsLastMove && !sq.IsSelected && !sq.IsInCheck)
            {
                canvas.FillColor = LastMoveColor;
                canvas.FillRectangle(x, y, cw, ch);
            }

            // ── Coordenadas ───────────────────────────────────────────
            float cs = MathF.Max(7f, cw * 0.17f);
            canvas.FontSize  = cs;
            canvas.FontColor = sq.IsLight ? coordLight : coordDark;
            if (vc == 0)  // coluna visual mais à esquerda
                canvas.DrawString(((char)('8' - r)).ToString(),
                    x + 2, y + 1, cw * 0.3f, ch * 0.3f,
                    HorizontalAlignment.Left, VerticalAlignment.Top);
            if (vr == 7)  // linha visual mais abaixo
                canvas.DrawString(((char)('a' + c)).ToString(),
                    x, y + ch - cs * 1.4f, cw - 2, cs * 1.4f,
                    HorizontalAlignment.Right, VerticalAlignment.Bottom);

            // ── Indicador de movimento válido ─────────────────────────
            if (sq.IsValidMove && string.IsNullOrEmpty(sq.PieceSymbol))
            {
                canvas.FillColor = Color.FromArgb("#9028A745");
                canvas.FillCircle(x + cw * 0.5f, y + ch * 0.5f, cw * 0.17f);
            }

            // ── Peça ──────────────────────────────────────────────────
            if (string.IsNullOrEmpty(sq.PieceSymbol)) continue;

            string? imgCode = PieceImageCode(sq);
            if (imgCode != null && PieceImages.TryGetValue(imgCode, out var pieceImg))
            {
                char  type       = imgCode[1];
                float heightFrac = PieceHeightFrac.TryGetValue(type, out var hf) ? hf : 0.7f;
                float aspect     = pieceImg.Width / (float)pieceImg.Height;

                float dh = ch * heightFrac;
                float dw = dh * aspect;

                float maxW = cw * 0.96f;
                if (dw > maxW)
                {
                    float shrink = maxW / dw;
                    dw *= shrink;
                    dh *= shrink;
                }

                if (type == 'k')
                    dw *= 1.16f;   // rei um pouco mais largo pro "porte" da coroa

                // Centralizada na horizontal; na vertical, todas as peças assentam
                // na mesma "linha do chão" perto da base da casa — centralizar
                // pelo meio faz peças de alturas diferentes (rei x peão) parecerem
                // flutuando em posições diferentes, o que lia como desalinhado.
                float dx        = x + (cw - dw) / 2f;
                float baselineY = y + ch * 0.95f;
                float dy        = baselineY - dh;
                canvas.DrawImage(pieceImg, dx, dy, dw, dh);

                if (sq.IsValidMove)
                {
                    canvas.StrokeColor = Color.FromArgb("#9028A745");
                    canvas.StrokeSize  = cw * 0.09f;
                    canvas.DrawCircle(x + cw * 0.5f, y + ch * 0.5f, cw * 0.42f);
                }
                continue;
            }

            bool  isWhite       = sq.PieceIsWhite == true;
            bool  isKing        = sq.PieceSymbol == KingSymbol;
            float pieceFontSize = isKing ? fontSize * KingScale : fontSize;

            canvas.FontSize = pieceFontSize;

            if (isWhite)
            {
                // Contorno escuro externo (3 camadas para parecer mais grosso)
                canvas.FontColor = ShadowColor.WithAlpha(0.30f);
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    canvas.DrawString(sq.PieceSymbol,
                        x + dx * off * 1.8f, y + dy * off * 1.8f, cw, ch,
                        HorizontalAlignment.Center, VerticalAlignment.Center);
                }
                canvas.FontColor = ShadowColor.WithAlpha(0.18f);
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    canvas.DrawString(sq.PieceSymbol,
                        x + dx * off * 3.2f, y + dy * off * 3.2f, cw, ch,
                        HorizontalAlignment.Center, VerticalAlignment.Center);
                }
                // Peça principal: branca sólida
                canvas.FontColor = WhitePiece;
                canvas.DrawString(sq.PieceSymbol,
                    x, y, cw, ch,
                    HorizontalAlignment.Center, VerticalAlignment.Center);
            }
            else
            {
                // Glow branco ao redor (2 camadas para definir borda)
                canvas.FontColor = GlowColor.WithAlpha(0.80f);
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    canvas.DrawString(sq.PieceSymbol,
                        x + dx * off * 1.2f, y + dy * off * 1.2f, cw, ch,
                        HorizontalAlignment.Center, VerticalAlignment.Center);
                }
                canvas.FontColor = GlowColor.WithAlpha(0.35f);
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    canvas.DrawString(sq.PieceSymbol,
                        x + dx * off * 2.5f, y + dy * off * 2.5f, cw, ch,
                        HorizontalAlignment.Center, VerticalAlignment.Center);
                }
                // Peça principal: quase preta
                canvas.FontColor = BlackPiece;
                canvas.DrawString(sq.PieceSymbol,
                    x, y, cw, ch,
                    HorizontalAlignment.Center, VerticalAlignment.Center);
            }

            // ── Detalhe interno nos peões: curva no corpo + linha na base ─
            bool isPawn = sq.PieceSymbol == WhitePawnSymbol || sq.PieceSymbol == BlackPawnSymbol;
            if (isPawn)
            {
                float cx  = x + cw * 0.5f;
                float sw  = MathF.Max(0.6f, cw * 0.020f);
                var   ink = isWhite ? ShadowColor.WithAlpha(0.26f) : GlowColor.WithAlpha(0.24f);

                canvas.StrokeSize  = sw;
                canvas.StrokeColor = ink;

                // Curva suave no corpo (bezier cúbico vertical com leve inflexão)
                float bodyTop = y + ch * 0.43f;
                float bodyBot = y + ch * 0.68f;
                float bodyMid = y + ch * 0.555f;
                float bulge   = cw * 0.055f;   // quanto a curva desvia para o lado

                var path = new PathF();
                path.MoveTo(cx, bodyTop);
                path.CurveTo(
                    cx - bulge, bodyTop + (bodyMid - bodyTop) * 0.4f,
                    cx + bulge, bodyTop + (bodyMid - bodyTop) * 1.6f,
                    cx, bodyBot);
                canvas.DrawPath(path);

                // Linha horizontal na base
                float baseY  = y + ch * 0.78f;
                float halfW  = cw * 0.18f;
                canvas.DrawLine(cx - halfW, baseY, cx + halfW, baseY);
            }

            // Indicador de captura: anel ao redor da peça adversária
            if (sq.IsValidMove)
            {
                canvas.StrokeColor = Color.FromArgb("#9028A745");
                canvas.StrokeSize  = cw * 0.09f;
                canvas.DrawCircle(x + cw * 0.5f, y + ch * 0.5f, cw * 0.42f);
            }
        }
    }
}
