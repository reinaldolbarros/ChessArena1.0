using System.Text.Json;
using ChessMAUI.Models;

namespace ChessMAUI.Services;

/// <summary>Persiste histórico dos últimos torneios em JSON no AppDataDirectory.</summary>
public class TournamentHistoryService
{
    private const int MaxRecords = 20;
    private readonly string _filePath;
    private List<TournamentRecord> _cache = [];

    public TournamentHistoryService()
    {
        _filePath = Path.Combine(FileSystem.AppDataDirectory, "tournament_history.json");
        Load();
    }

    public IReadOnlyList<TournamentRecord> Records => _cache;

    public void Add(TournamentRecord record)
    {
        _cache.Insert(0, record);
        if (_cache.Count > MaxRecords)
            _cache = _cache.Take(MaxRecords).ToList();
        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            _cache = JsonSerializer.Deserialize<List<TournamentRecord>>(json) ?? [];
        }
        catch { _cache = []; }
    }

    private void Save()
    {
        try { File.WriteAllText(_filePath, JsonSerializer.Serialize(_cache)); }
        catch { }
    }
}
