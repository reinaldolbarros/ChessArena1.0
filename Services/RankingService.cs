using ChessMAUI.Models;
using Supabase.Postgrest;

namespace ChessMAUI.Services;

/// <summary>
/// Ranking global — combina os perfis reais sincronizados no Supabase com um preenchimento
/// fictício (nomes simulando jogadores reais). O preenchimento existe só pra a lista não
/// parecer vazia no início do app: como tudo é ordenado por rating e cortado no TopLimit,
/// jogadores reais com rating mais alto empurram os fictícios pra fora da lista sozinhos,
/// à medida que a base de usuários reais cresce — sem precisar de nenhuma lógica especial.
/// Visitantes/anônimos têm conta real (anônima) no Supabase — ver AuthService.LoginAnonymousAsync
/// — e por isso entram no ranking global normalmente, como qualquer outro jogador.
/// </summary>
public class RankingService
{
    private const int TopLimit = 50;

    // Cache curto do ranking global — evita ir ao servidor de novo a cada vez que a tela
    // inicial reaparece (troca de aba etc.), que era a causa da lentidão percebida ali.
    private List<RankingEntry>? _cachedGlobal;
    private DateTime            _cachedGlobalAt = DateTime.MinValue;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(45);

    public List<RankingEntry>? PeekGlobalCache() => _cachedGlobal;
    public bool IsGlobalCacheFresh => _cachedGlobal != null && DateTime.UtcNow - _cachedGlobalAt < CacheTtl;

    /// <summary>Descarta o cache — chamar sempre que o usuário logado muda (visitante ↔ conta
    /// real, ou troca de conta), senão a Lobby continua mostrando por até 45s a entrada
    /// destacada ("você") de QUEM estava logado antes da troca, já que o cache não sabia que a
    /// identidade mudou.</summary>
    public void InvalidateCache()
    {
        _cachedGlobal   = null;
        _cachedGlobalAt = DateTime.MinValue;
    }

    // Preenchimento fictício — nomes plausíveis de jogadores reais, com rating distribuído
    // numa curva realista. Nunca aparecem se já existir gente real o suficiente com rating
    // mais alto pra preencher a lista sozinha.
    // Valores irregulares (não múltiplos redondos) — simulam pontuação real, que só sobe
    // ou desce em unidades a cada partida, nunca em saltos "redondos" tipo 50 em 50.
    private static readonly (string Avatar, string Name, int Elo)[] FillerSeeds =
    [
        ("♛","Carlos Silva",    1865), ("♜","Ana Lima",         1817),
        ("♝","Pedro Santos",    1774), ("♞","Julia Rocha",      1731),
        ("🎯","Rafael Costa",    1698), ("🔥","Fernanda Alves",   1655),
        ("💎","Bruno Martins",   1622), ("👑","Camila Nunes",     1589),
        ("🦁","Diego Souza",     1546), ("🐉","Larissa Melo",     1513),
        ("⚡","Lucas Pereira",    1470), ("🌟","Beatriz Souza",    1437),
        ("🎭","Gabriel Rocha",   1394), ("🛡️","Mariana Lopes",    1361),
        ("♟","Thiago Almeida",  1318), ("♛","Isabela Ramos",    1285),
        ("♜","Felipe Cardoso",  1242), ("♝","Amanda Teixeira",  1209),
        ("♞","Vinícius Barros", 1166), ("🎯","Letícia Farias",   1123),
    ];

    private static List<RankingEntry> BuildFiller(bool weekly) => FillerSeeds.Select(f => new RankingEntry
    {
        Id         = "",
        Avatar     = f.Avatar,
        Name       = f.Name,
        Points     = f.Elo,
        WeekPoints = f.Elo / 15, // atividade semanal modesta, proporcional ao rating
        IsHuman    = false,
    }).ToList();

    public async Task<List<RankingEntry>> GetGlobalAsync(ProfileService profile)
    {
        var result = await FetchTopAsync(profile, weekly: false);
        _cachedGlobal   = result;
        _cachedGlobalAt = DateTime.UtcNow;
        return result;
    }

    public Task<List<RankingEntry>> GetWeeklyAsync(ProfileService profile) => FetchTopAsync(profile, weekly: true);

    private async Task<List<RankingEntry>> FetchTopAsync(ProfileService profile, bool weekly)
    {
        var real = new List<RankingEntry>();
        var svc  = SupabaseService.Instance;

        if (svc.IsReady)
        {
            try
            {
                var query   = svc.Client.From<SupabaseProfile>();
                var ordered = weekly
                    ? query.Order(p => (object)p.WeekElo, Constants.Ordering.Descending)
                    : query.Order(p => (object)p.Elo,     Constants.Ordering.Descending);
                var top = await ordered.Limit(TopLimit).Get();

                string myId = AppState.Current.Auth.UserId;
                foreach (var row in top.Models)
                    real.Add(ToEntry(row, myId));
            }
            catch { /* sem internet ou erro de rede — segue só com o preenchimento, sem erro */ }
        }

        var all = real.Concat(BuildFiller(weekly)).ToList();

        // Se o usuário (autenticado ou visitante) ainda não está representado entre os
        // reais, adiciona a própria entrada ANTES de ordenar — assim, se o rating dele
        // for alto o bastante, ele aparece destacado no lugar certo da lista, e não
        // jogado artificialmente pro final.
        if (!all.Any(e => e.IsHuman))
            all.Add(await GetMyEntryAsync(profile, weekly));

        var merged = all
            .OrderByDescending(e => weekly ? e.WeekPoints : e.Points)
            .Take(TopLimit)
            .ToList();

        for (int i = 0; i < merged.Count; i++) merged[i].Position = i + 1;

        // Se mesmo assim o rating for baixo demais pra caber no TopLimit, garante que o
        // usuário ainda apareça (a RankingPage sempre espera achar um IsHuman na lista).
        if (!merged.Any(e => e.IsHuman))
            merged.Add(await GetMyEntryAsync(profile, weekly));

        return merged;
    }

    /// <summary>
    /// Posição real do usuário atual (quantos jogadores — reais ou do preenchimento fictício
    /// — têm rating maior, +1). Usada tanto pra montar a linha "sua posição" no Ranking quanto
    /// pro cabeçalho do Lobby, sem precisar buscar o top inteiro.
    /// </summary>
    public async Task<RankingEntry> GetMyEntryAsync(ProfileService profile, bool weekly = false)
    {
        string myId    = AppState.Current.Auth.UserId;
        int    myValue = weekly ? profile.WeekPoints : profile.Points;

        // A contagem é uma leitura pública (não depende de o usuário atual ter conta), então
        // até visitante/anônimo recebe uma posição real — comparando o Elo local dele com quem
        // já está sincronizado, mais o preenchimento fictício.
        int aheadReal = 0;
        var svc = SupabaseService.Instance;
        if (svc.IsReady)
        {
            try
            {
                string column = weekly ? "week_elo" : "elo";
                aheadReal = await svc.Client.From<SupabaseProfile>()
                    .Filter(column, Constants.Operator.GreaterThan, myValue)
                    .Count(Constants.CountType.Exact);
            }
            catch { aheadReal = 0; }
        }

        int aheadFiller = BuildFiller(weekly).Count(f => (weekly ? f.WeekPoints : f.Points) > myValue);

        return new RankingEntry
        {
            Id         = myId,
            Position   = aheadReal + aheadFiller + 1,
            Avatar     = profile.Avatar,
            Name       = profile.Name,
            Points     = profile.Points,
            WeekPoints = profile.WeekPoints,
            IsHuman    = true,
        };
    }

    private static RankingEntry ToEntry(SupabaseProfile r, string myId) => new()
    {
        Id         = r.Id,
        Avatar     = string.IsNullOrEmpty(r.Avatar) ? "♟" : r.Avatar,
        Name       = r.Name,
        Points     = r.Elo,
        WeekPoints = r.WeekElo,
        IsHuman    = !string.IsNullOrEmpty(myId) && r.Id == myId,
    };
}
