using ChessMAUI.Models;

namespace ChessMAUI.Services;

public class TitleService
{
    private const string KeyLeagueWins    = "title_league_wins";
    private const string KeyParticipations = "title_participations";
    private const string KeyMonthlyChamps  = "title_monthly_champs";
    private const string KeyTop3Months    = "title_top3_months";

    public int LeagueWins
    {
        get => Preferences.Default.Get(KeyLeagueWins, 0);
        private set => Preferences.Default.Set(KeyLeagueWins, value);
    }

    public int Participations
    {
        get => Preferences.Default.Get(KeyParticipations, 0);
        private set => Preferences.Default.Set(KeyParticipations, value);
    }

    public int MonthlyChampionships
    {
        get => Preferences.Default.Get(KeyMonthlyChamps, 0);
        private set => Preferences.Default.Set(KeyMonthlyChamps, value);
    }

    public int Top3Months
    {
        get => Preferences.Default.Get(KeyTop3Months, 0);
        private set => Preferences.Default.Set(KeyTop3Months, value);
    }

    public PlayerTitle CurrentTitle
    {
        get
        {
            if (MonthlyChampionships >= 3) return PlayerTitle.Lenda;
            if (LeagueWins >= 10 || MonthlyChampionships >= 1) return PlayerTitle.GraoMestre;
            if (LeagueWins >= 3 || Top3Months >= 2) return PlayerTitle.Mestre;
            if (LeagueWins >= 1) return PlayerTitle.Cavaleiro;
            if (Participations >= 3) return PlayerTitle.Competidor;
            return PlayerTitle.Recruta;
        }
    }

    public string TitleLabel => CurrentTitle switch
    {
        PlayerTitle.Lenda      => "Lenda",
        PlayerTitle.GraoMestre => "Grão-Mestre",
        PlayerTitle.Mestre     => "Mestre",
        PlayerTitle.Cavaleiro  => "Cavaleiro",
        PlayerTitle.Competidor => "Competidor",
        _                      => "Recruta"
    };

    public string TitleIcon => CurrentTitle switch
    {
        PlayerTitle.Lenda      => "♚",
        PlayerTitle.GraoMestre => "♛",
        PlayerTitle.Mestre     => "♜",
        PlayerTitle.Cavaleiro  => "♞",
        PlayerTitle.Competidor => "♝",
        _                      => "♟"
    };

    public string NextRequirement => CurrentTitle switch
    {
        PlayerTitle.Recruta    => "Participe de 3 torneios da Liga",
        PlayerTitle.Competidor => "Vença 1 torneio da Liga",
        PlayerTitle.Cavaleiro  => "Vença 3 torneios ou Top 3 por 2 meses",
        PlayerTitle.Mestre     => "Vença 10 torneios ou seja campeão mensal",
        PlayerTitle.GraoMestre => "Seja campeão mensal 3 vezes",
        _                      => "Título máximo atingido!"
    };

    // Chamado ao entrar no torneio (sempre)
    public void RecordLeagueParticipation() => Participations++;

    // Chamado ao vencer — NÃO incrementa participação (já foi chamado ao entrar)
    public void RecordLeagueWin() => LeagueWins++;

    public void RecordMonthlyTop3()    => Top3Months++;
    public void RecordMonthlyChampion() { MonthlyChampionships++; Top3Months++; }
}
