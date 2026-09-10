using ChessMAUI.Models;

namespace ChessMAUI.Services;

/// <summary>
/// Escolhe, entre os candidatos de <see cref="StockfishService.GetTopMovesAsync"/> (todos
/// calculados em força máxima), qual combina com o estilo do bot — nunca inventa uma jogada
/// nova, só prioriza entre lances que o próprio motor já validou como bons.
///
/// Arquivo novo e isolado — remover essa classe e reverter as poucas chamadas em
/// GameViewModel.cs/GamePage.xaml.cs/CareerService.cs restaura o comportamento antigo
/// (sempre o lance nº 1 do Stockfish, igual antes desta funcionalidade existir).
/// </summary>
public static class PersonalityMoveSelector
{
    // Margem de avaliação (centipawns) que cada estilo aceita perder em troca de um lance mais
    // "com a cara dele" — nunca escolhe algo pior que isso, então nunca joga mal de propósito.
    private const int SolidToleranceCp      = 25;
    private const int AggressiveToleranceCp = 150;

    public static string Choose(
        List<(string Move, int ScoreCp, bool IsMate)> candidates,
        BotPersonality                                personality,
        Func<string, bool>                             isCapture,
        Func<string, int>                              countThreats,
        bool                                           botPlaysWhite,
        Random                                          rng)
    {
        var best = candidates[0];

        // Mate forçado (a favor ou contra): nunca troca por outro lance, de nenhum estilo.
        if (best.IsMate || personality == BotPersonality.Balanced)
            return best.Move;

        int tolerance = personality switch
        {
            BotPersonality.Solid      => SolidToleranceCp,
            BotPersonality.Aggressive => AggressiveToleranceCp,
            _                         => 0
        };

        var withinTolerance = candidates
            .Where(c => !c.IsMate && best.ScoreCp - c.ScoreCp <= tolerance)
            .ToList();

        var captures = withinTolerance.Where(c => isCapture(c.Move)).ToList();
        if (captures.Count > 0)
        {
            // Entre as capturas dentro da margem, o Agressivo prefere a que deixa mais peças
            // brancas ameaçadas depois — não só captura, mas captura "com mais intenção".
            if (personality == BotPersonality.Aggressive && captures.Count > 1)
                return captures.OrderByDescending(c => countThreats(c.Move)).First().Move;

            return captures[rng.Next(captures.Count)].Move;
        }

        // Agressivo sem nenhuma captura à mão: em vez de cair pro lance mais "morno" (o
        // objetivamente melhor), prefere o lance que deixa mais peças brancas ameaçadas —
        // e só usa o avanço no território adversário como desempate entre lances empatados
        // em número de ameaças (inclusive quando nenhum cria ameaça nova).
        if (personality == BotPersonality.Aggressive && withinTolerance.Count > 1)
        {
            return withinTolerance
                .OrderByDescending(c => countThreats(c.Move))
                .ThenBy(c => botPlaysWhite ? -DestinationRank(c.Move) : DestinationRank(c.Move))
                .First().Move;
        }

        return best.Move;
    }

    // Fileira de destino (1-8) direto do UCI ("e7e5" → 5) — sem precisar do tabuleiro.
    private static int DestinationRank(string uci) => uci.Length >= 4 ? uci[3] - '0' : 0;
}
