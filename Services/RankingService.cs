using ChessMAUI.Models;
using Supabase.Postgrest;

namespace ChessMAUI.Services;

/// <summary>
/// Ranking global — combina os perfis reais sincronizados no Supabase com um preenchimento
/// fictício (nomes simulando jogadores reais). O preenchimento existe só pra a lista não
/// parecer vazia no início do app: como tudo é ordenado por rating e cortado no TopLimit,
/// jogadores reais com rating mais alto empurram os fictícios pra fora da lista sozinhos,
/// à medida que a base de usuários reais cresce — sem precisar de nenhuma lógica especial.
/// Visitantes/anônimos não têm conta sincronizada (ver AuthService.IsAnonymous), então não
/// entram no ranking global; eles veem sua pontuação local, mas sem uma posição real
/// ("—" em vez de um número inventado).
/// </summary>
public class RankingService
{
    private const int TopLimit = 50;

    // Preenchimento fictício — nomes plausíveis de jogadores reais, com rating distribuído
    // numa curva realista. Nunca aparecem se já existir gente real o suficiente com rating
    // mais alto pra preencher a lista sozinha.
    private static readonly (string Avatar, string Name, int Elo)[] FillerSeeds =
    [
        ("♛","Carlos Silva",    2200), ("♜","Ana Lima",         2050),
        ("♝","Pedro Santos",    1950), ("♞","Julia Rocha",      1850),
        ("🎯","Rafael Costa",    1780), ("🔥","Fernanda Alves",   1720),
        ("💎","Bruno Martins",   1650), ("👑","Camila Nunes",     1600),
        ("🦁","Diego Souza",     1550), ("🐉","Larissa Melo",     1500),
        ("⚡","Lucas Pereira",    1450), ("🌟","Beatriz Souza",    1400),
        ("🎭","Gabriel Rocha",   1350), ("🛡️","Mariana Lopes",    1300),
        ("♟","Thiago Almeida",  1250), ("♛","Isabela Ramos",    1200),
        ("♜","Felipe Cardoso",  1150), ("♝","Amanda Teixeira",  1100),
        ("♞","Vinícius Barros", 1050), ("🎯","Letícia Farias",   1000),
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

    public Task<List<RankingEntry>> GetGlobalAsync(ProfileService profile) => FetchTopAsync(profile, weekly: false);
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
