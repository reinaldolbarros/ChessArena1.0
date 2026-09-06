using ChessMAUI.Models;

namespace ChessMAUI.Services;

/// <summary>
/// Gerencia múltiplas salas abertas simultaneamente (estilo PokerStars lobby).
/// Cada sala preenche bots independentemente para simular movimento real.
/// </summary>
public class RoomLobbyService
{
    private readonly List<TournamentRoom> _rooms       = [];
    private readonly List<TournamentRoom> _playerRooms = [];   // criados por jogador
    private readonly Random _rng = Random.Shared;

    // Inclui salas públicas criadas por jogador
    public IReadOnlyList<TournamentRoom> Rooms =>
        ((IEnumerable<TournamentRoom>)_rooms)
        .Concat(_playerRooms.Where(r => r.Status == RoomStatus.Open))
        .ToList();

    public event Action? RoomsUpdated;

    public RoomLobbyService()
    {
        GenerateRooms();
        _ = SimulateLobbyActivityAsync();
    }

    private void GenerateRooms()
    {
        _rooms.Clear();

        // ── Clássico (mata-mata) ───────────────────────────────────
        Add(new TournamentRoom { Size = 8,  BuyIn = 10,  TimeMinutes = 5,  Type = TournamentType.Standard });
        Add(new TournamentRoom { Size = 8,  BuyIn = 25,  TimeMinutes = 5,  Type = TournamentType.Standard });
        Add(new TournamentRoom { Size = 16, BuyIn = 10,  TimeMinutes = 10, Type = TournamentType.Standard });
        Add(new TournamentRoom { Size = 16, BuyIn = 50,  TimeMinutes = 10, Type = TournamentType.Standard });
        Add(new TournamentRoom { Size = 32, BuyIn = 25,  TimeMinutes = 15, Type = TournamentType.Standard });
        Add(new TournamentRoom { Size = 64, BuyIn = 50,  TimeMinutes = 20, Type = TournamentType.Standard });

        // ── Duelo (1v1) ────────────────────────────────────────────
        Add(new TournamentRoom { Size = 2, BuyIn = 10,  TimeMinutes = 5,  Type = TournamentType.HeadsUp });
        Add(new TournamentRoom { Size = 2, BuyIn = 25,  TimeMinutes = 5,  Type = TournamentType.HeadsUp });
        Add(new TournamentRoom { Size = 2, BuyIn = 50,  TimeMinutes = 10, Type = TournamentType.HeadsUp });
        Add(new TournamentRoom { Size = 2, BuyIn = 100, TimeMinutes = 10, Type = TournamentType.HeadsUp });

        // ── Bounty ─────────────────────────────────────────────────
        Add(new TournamentRoom { Size = 8,  BuyIn = 20, TimeMinutes = 5,  Type = TournamentType.Bounty, BountyPerPlayer = 5  });
        Add(new TournamentRoom { Size = 16, BuyIn = 50, TimeMinutes = 10, Type = TournamentType.Bounty, BountyPerPlayer = 10 });
        Add(new TournamentRoom { Size = 8,  BuyIn = 50, TimeMinutes = 10, Type = TournamentType.Bounty, BountyPerPlayer = 15 });

        // ── Bilhete Dourado (Satélite) ─────────────────────────────
        Add(new TournamentRoom { Size = 8,  BuyIn = 5,  TimeMinutes = 5,  Type = TournamentType.Satellite, SatelliteTarget = 50  });
        Add(new TournamentRoom { Size = 8,  BuyIn = 10, TimeMinutes = 5,  Type = TournamentType.Satellite, SatelliteTarget = 100 });
        Add(new TournamentRoom { Size = 16, BuyIn = 25, TimeMinutes = 10, Type = TournamentType.Satellite, SatelliteTarget = 500 });

        // ── Turbo (3–4 min) ────────────────────────────────────────
        Add(new TournamentRoom { Size = 8,  BuyIn = 10, TimeMinutes = 4, Type = TournamentType.Turbo });
        Add(new TournamentRoom { Size = 8,  BuyIn = 25, TimeMinutes = 3, Type = TournamentType.Turbo });
        Add(new TournamentRoom { Size = 16, BuyIn = 25, TimeMinutes = 4, Type = TournamentType.Turbo });
        Add(new TournamentRoom { Size = 16, BuyIn = 50, TimeMinutes = 3, Type = TournamentType.Turbo });
        Add(new TournamentRoom { Size = 32, BuyIn = 50, TimeMinutes = 4, Type = TournamentType.Turbo });

        // ── Hyper-Turbo (1–2 min) ─────────────────────────────────
        Add(new TournamentRoom { Size = 8,  BuyIn = 10, TimeMinutes = 1, Type = TournamentType.HyperTurbo });
        Add(new TournamentRoom { Size = 8,  BuyIn = 10, TimeMinutes = 2, Type = TournamentType.HyperTurbo });
        Add(new TournamentRoom { Size = 8,  BuyIn = 25, TimeMinutes = 1, Type = TournamentType.HyperTurbo });
        Add(new TournamentRoom { Size = 16, BuyIn = 25, TimeMinutes = 2, Type = TournamentType.HyperTurbo });
        Add(new TournamentRoom { Size = 16, BuyIn = 50, TimeMinutes = 1, Type = TournamentType.HyperTurbo });

        // ── Satélites para torneios de alto valor ──────────────────
        Add(new TournamentRoom { Size = 8,  BuyIn = 10,  TimeMinutes = 5,  Type = TournamentType.Satellite, SatelliteTarget = 50   });
        Add(new TournamentRoom { Size = 8,  BuyIn = 50,  TimeMinutes = 5,  Type = TournamentType.Satellite, SatelliteTarget = 500  });
        Add(new TournamentRoom { Size = 16, BuyIn = 50,  TimeMinutes = 10, Type = TournamentType.Satellite, SatelliteTarget = 500  });
        Add(new TournamentRoom { Size = 8,  BuyIn = 100, TimeMinutes = 10, Type = TournamentType.Satellite, SatelliteTarget = 1000 });
        Add(new TournamentRoom { Size = 16, BuyIn = 100, TimeMinutes = 10, Type = TournamentType.Satellite, SatelliteTarget = 1000 });
        Add(new TournamentRoom { Size = 16, BuyIn = 250, TimeMinutes = 10, Type = TournamentType.Satellite, SatelliteTarget = 2500 });

        // ── Grand Prix ($500 buy-in) ────────────────────────────────
        Add(new TournamentRoom { Size = 8,  BuyIn = 500,  TimeMinutes = 10, Type = TournamentType.Standard });
        Add(new TournamentRoom { Size = 16, BuyIn = 500,  TimeMinutes = 10, Type = TournamentType.Standard });
        Add(new TournamentRoom { Size = 8,  BuyIn = 500,  TimeMinutes = 10, Type = TournamentType.HeadsUp  });
        Add(new TournamentRoom { Size = 16, BuyIn = 500,  TimeMinutes = 10, Type = TournamentType.Bounty, BountyPerPlayer = 100 });

        // ── Master Series ($1000 buy-in) ───────────────────────────
        Add(new TournamentRoom { Size = 8,  BuyIn = 1000, TimeMinutes = 15, Type = TournamentType.Standard });
        Add(new TournamentRoom { Size = 16, BuyIn = 1000, TimeMinutes = 15, Type = TournamentType.Standard });
        Add(new TournamentRoom { Size = 8,  BuyIn = 1000, TimeMinutes = 15, Type = TournamentType.HeadsUp  });

        // ── Elite Cup ($2500 buy-in) ───────────────────────────────
        Add(new TournamentRoom { Size = 8,  BuyIn = 2500, TimeMinutes = 20, Type = TournamentType.Standard });
        Add(new TournamentRoom { Size = 16, BuyIn = 2500, TimeMinutes = 20, Type = TournamentType.Standard });
    }

    private void Add(TournamentRoom room)
    {
        room.Joined = _rng.Next((int)(room.Size * 0.1), Math.Max((int)(room.Size * 0.1) + 1, (int)(room.Size * 0.8)));
        _rooms.Add(room);
    }

    // Simula bots entrando e salas enchendo/esvaziando ao longo do tempo
    private async Task SimulateLobbyActivityAsync()
    {
        while (true)
        {
            await Task.Delay(_rng.Next(8000, 15000));

            bool changed = false;
            foreach (var room in _rooms.ToList())
            {
                if (room.Status != RoomStatus.Open) continue;

                // Chance de alguém entrar (70%) ou sair (20%)
                int roll = _rng.Next(100);
                if (roll < 70 && room.Joined < room.Size)
                {
                    room.Joined++;
                    changed = true;

                    if (room.Joined >= room.Size)
                    {
                        room.Status = RoomStatus.Starting;
                        _ = ResetRoomAsync(room);
                    }
                }
                else if (roll < 90 && room.Joined > 1)
                {
                    room.Joined = Math.Max(1, room.Joined - 1);
                    changed = true;
                }
            }

            if (changed)
                MainThread.BeginInvokeOnMainThread(() => RoomsUpdated?.Invoke());
        }
    }

    // Após sala iniciar, recria ela vazia depois de alguns segundos
    private async Task ResetRoomAsync(TournamentRoom room)
    {
        await Task.Delay(5000);
        room.Status = RoomStatus.InProgress;
        RoomsUpdated?.Invoke();

        await Task.Delay(3000);
        // Recria a sala com os mesmos parâmetros
        var idx  = _rooms.IndexOf(room);
        var nova = new TournamentRoom
        {
            Size = room.Size, BuyIn = room.BuyIn, TimeMinutes = room.TimeMinutes,
            Type = room.Type, BountyPerPlayer = room.BountyPerPlayer,
            SatelliteTarget = room.SatelliteTarget,
            Joined = _rng.Next(1, Math.Max(2, room.Size / 4))
        };
        if (idx >= 0) _rooms[idx] = nova;
        MainThread.BeginInvokeOnMainThread(() => RoomsUpdated?.Invoke());
    }

    /// <summary>Registra o humano em uma sala. Retorna a sala atualizada.</summary>
    public TournamentRoom JoinRoom(TournamentRoom room)
    {
        room.Joined = Math.Min(room.Size, room.Joined + 1);
        if (room.Joined >= room.Size) room.Status = RoomStatus.Starting;
        return room;
    }

    public List<TournamentRoom> GetByBuyIn(decimal buyIn) =>
        _rooms.Where(r => r.BuyIn == buyIn && r.Status == RoomStatus.Open).ToList();

    public List<TournamentRoom> GetBySize(int size) =>
        _rooms.Where(r => r.Size == size && r.Status == RoomStatus.Open).ToList();

    // ── Torneios criados por jogador ─────────────────────────────────────────

    /// <summary>Registra um torneio criado pelo jogador (público ou privado).</summary>
    public void CreatePlayerRoom(TournamentRoom room)
    {
        // Remove sala anterior do mesmo criador com o mesmo código (re-criação)
        _playerRooms.RemoveAll(r => r.AccessCode == room.AccessCode && room.AccessCode != "");
        _playerRooms.Add(room);
        MainThread.BeginInvokeOnMainThread(() => RoomsUpdated?.Invoke());
    }

    /// <summary>Tenta localizar uma sala privada pelo código de acesso.</summary>
    public bool TryGetByCode(string code, out TournamentRoom? room)
    {
        room = _playerRooms.FirstOrDefault(r =>
            r.IsPrivate &&
            r.AccessCode.Equals(code.Trim().ToUpper(), StringComparison.Ordinal) &&
            r.Status == RoomStatus.Open);
        return room != null;
    }

    /// <summary>Marca a sala do jogador como encerrada (quando o torneio inicia).</summary>
    public void ClosePlayerRoom(string accessCode)
    {
        var room = _playerRooms.FirstOrDefault(r => r.AccessCode == accessCode);
        if (room != null) room.Status = RoomStatus.InProgress;
    }

    /// <summary>Gera um código único de 6 caracteres.</summary>
    public static string GenerateAccessCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return string.Concat(Enumerable.Range(0, 6).Select(_ => chars[Random.Shared.Next(chars.Length)]));
    }
}
