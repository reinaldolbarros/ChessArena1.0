using Supabase;

namespace ChessMAUI.Services;

public sealed class SupabaseService
{
    public static SupabaseService Instance { get; } = new();

    public Client Client  { get; private set; } = null!;
    public bool   IsReady { get; private set; }

    private SupabaseService() { }

    public async Task InitializeAsync()
    {
        if (IsReady) return;

        var options = new SupabaseOptions
        {
            AutoRefreshToken    = true,
            // Necessário pro modo online real: sincronizar lances da partida (tabela
            // "games") e o criador de um desafio descobrir que foi aceito (tabela
            // "challenges") — ambos via Postgres Changes.
            AutoConnectRealtime = true,
            SessionHandler      = new MauiSessionHandler(),
        };

        Client = new Client(SupabaseConfig.Url, SupabaseConfig.AnonKey, options);
        await Client.InitializeAsync();
        IsReady = true;
    }
}
