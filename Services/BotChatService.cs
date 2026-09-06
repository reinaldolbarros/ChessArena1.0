namespace ChessMAUI.Services;

/// <summary>Gera mensagens de chat simuladas do bot adversário.</summary>
public class BotChatService
{
    private static readonly string[] OnStart =
        ["Boa sorte! 🤝", "Vamos jogar!", "Que a melhor IA vença 😄", "GL HF!", "Preparado?"];

    private static readonly string[] OnGoodMove =
        ["Boa jogada!", "Não esperava isso 😮", "Interessante...", "Hmm 🤔", "Bem jogado!"];

    private static readonly string[] OnCapture =
        ["Obrigado pela peça 😏", "Capturei!", "Minha 😈", "Era armadilha!", "Obrigado!"];

    private static readonly string[] OnCheck =
        ["Xeque! ♟", "Cuidado com seu rei!", "Sente a pressão? 😤", "Xeque!", "😈 Xeque!"];

    private static readonly string[] OnWin =
        ["GG! Boa partida 🤝", "Gg wp!", "Foi difícil! Parabéns 👏", "Até a próxima! 👋"];

    private static readonly string[] OnLoss =
        ["GG! Você jogou bem 👏", "Boa partida!", "Revancha? 🤝", "Parabéns! 🏆", "Bem jogado!"];

    private static readonly string[] OnThinking =
        ["Calculando...", "🤔", "Hmm...", "Deixa eu pensar...", "Processando..."];

    private static readonly Random _rng = Random.Shared;

    public event Action<string>? MessageReceived;

    public void SendStart()    => Fire(OnStart,    delay: 1000);
    public void SendGoodMove() => Fire(OnGoodMove, delay: 800,  chance: 0.3);
    public void SendCapture()  => Fire(OnCapture,  delay: 600,  chance: 0.5);
    public void SendCheck()    => Fire(OnCheck,    delay: 500,  chance: 0.8);
    public void SendWin()      => Fire(OnWin,      delay: 1200);
    public void SendLoss()     => Fire(OnLoss,     delay: 1200);
    public void SendThinking() => Fire(OnThinking, delay: 200,  chance: 0.2);

    private void Fire(string[] pool, int delay = 0, double chance = 1.0)
    {
        if (_rng.NextDouble() > chance) return;
        string msg = pool[_rng.Next(pool.Length)];
        _ = SendAsync(msg, delay);
    }

    private async Task SendAsync(string msg, int delay)
    {
        if (delay > 0) await Task.Delay(delay);
        MainThread.BeginInvokeOnMainThread(() => MessageReceived?.Invoke(msg));
    }
}
