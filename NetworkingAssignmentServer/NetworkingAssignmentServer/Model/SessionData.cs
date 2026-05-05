using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Model
{
    internal class SessionData
    {
        internal enum GamePhase
        {
            Preparation,
            Battle,
            Finished
        }

        static int _nextId;
        private readonly object _sync = new object();

        public int SessionID { get; }
        public GamePhase Phase { get; private set; } = GamePhase.Preparation;
        public Cell[][] FirstMap { get; }
        public Cell[][] SecondMap { get; }

        public ShipScenario Scenario { get; }

        public int MaxShips => Scenario.TotalShips;
        public int TotalShipCells => Scenario.TotalShipCells;

        public readonly int MaxMines;

        SessionParticipant[] _participants;
        public PlayerSide _participantTurn { get; set; }

        public SessionData(PlayerData player1, PlayerData player2, int shipScenario = 6, int maxMines = 4, int mapSize = 8)
        {
            if (player1 == null) throw new ArgumentNullException(nameof(player1));
            if (player2 == null) throw new ArgumentNullException(nameof(player2));
            if (mapSize <= 0) throw new ArgumentOutOfRangeException(nameof(mapSize));

            SessionID = Interlocked.Increment(ref _nextId);

            Scenario = ShipScenario.FromId(shipScenario);

            FirstMap = CreateMap(mapSize);
            SecondMap = CreateMap(mapSize);

            _participants = new[] {
            new SessionParticipant(player1, PlayerSide.First),
            new SessionParticipant(player2, PlayerSide.Second)
        };

            MaxMines = maxMines;
            _participantTurn = PlayerSide.First;
        }

        public string GetScenarioDescription() => Scenario.Describe();
        public void FinishGame()
        {
            lock (_sync)
            {
                if (Phase == GamePhase.Finished)
                    return;

                Phase = GamePhase.Finished;
            }
        }

        public PlayerSide GetSide(PlayerData player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));

            foreach (var participant in _participants)
            {
                if (participant.Player.Id == player.Id)
                    return participant.Side;
            }

            throw new InvalidOperationException("Player is not part of this session");
        }

        public Cell[][] GetOwnBoard(PlayerData player)
        {
            return GetSide(player) == PlayerSide.First ? FirstMap : SecondMap;
        }

        public Cell[][] GetEnemyBoard(PlayerData player)
        {
            return GetSide(player) == PlayerSide.First ? SecondMap : FirstMap;
        }

        public SessionParticipant GetParticipant(PlayerData player)
        {
            foreach (var participant in _participants)
            {
                if (participant.Player.Id == player.Id)
                    return participant;
            }
            throw new InvalidOperationException("Player is not part of this session");
        }

        public bool TryGetEnemyParticipant(PlayerData player, out SessionParticipant? participant)
        {
            PlayerSide otherSide = GetSide(player) == PlayerSide.First
                ? PlayerSide.Second
                : PlayerSide.First;
            foreach (var otherParticipant in _participants)
            {
                if (otherParticipant.Side == otherSide)
                {
                    participant = otherParticipant;
                    return true;
                }
            }
            participant = null;
            return false;
        }
        /// <summary>
        /// If returns true => battle has started
        /// </summary>
        /// <param name="participant"></param>
        /// <returns></returns>
        public bool MarkReady(SessionParticipant participant)
        {
            lock (_sync)
            {
                participant.IsReady = true;
            }

            return TryStartBattle();
        }

        public bool TryStartBattle()
        {
            lock (_sync)
            {
                if (Phase != GamePhase.Preparation)
                    return false;

                if (_participants.Any(p => !p.IsReady))
                    return false;
                Phase = GamePhase.Battle;
                return true;
            }
        }

        private static Cell[][] CreateMap(int mapSize)
        {
            var map = new Cell[mapSize][];
            for (int i = 0; i < mapSize; i++)
            {
                map[i] = new Cell[mapSize];
                for (int j = 0; j < mapSize; j++)
                    map[i][j] = new Cell();
            }
            return map;
        }
    }

    internal enum PlayerSide
    {
        First,
        Second
    }
    internal sealed class SessionParticipant
    {
        public PlayerData Player { get; }
        public PlayerSide Side { get; }

        private uint _lostShipCells = 0;
        private uint _lostMines = 0;
        private uint _placedShips = 0;
        private uint _placedMines = 0;

        private readonly Dictionary<int, uint> _placedShipsByLength = new Dictionary<int, uint>();

        public uint LostShipCells => _lostShipCells;
        public uint LousedMines => _lostMines;
        public uint PlacedShips => _placedShips;
        public uint PlacedMines => _placedMines;

        public bool IsReady { get; set; }

        private Dictionary<int, Ship> _ships = new Dictionary<int, Ship>();
        public Dictionary<int, Ship> Ships => _ships;

        public SessionParticipant(PlayerData player, PlayerSide side)
        {
            Player = player ?? throw new ArgumentNullException(nameof(player));
            Side = side;
        }

        public uint GetPlacedShipsByLength(int length)
            => _placedShipsByLength.TryGetValue(length, out var count) ? count : 0;

        public void IncrementPlacedShips()
        {
            _placedShips++;
        }

        public void IncrementPlacedShipsByLength(int length)
        {
            _placedShipsByLength[length] = GetPlacedShipsByLength(length) + 1;
        }

        public void DecrementPlacedShipsByLength(int length)
        {
            if (!_placedShipsByLength.TryGetValue(length, out var count) || count == 0)
                return;

            if (count == 1)
                _placedShipsByLength.Remove(length);
            else
                _placedShipsByLength[length] = count - 1;
        }

        public void IncrementPlacedMines(uint count = 1)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), "Cannot place fewer than 1 mine.");

            _placedMines += count;
        }

        public void IncrementLostShipCells(uint count = 1)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), "Cannot lose fewer than 1 ship cell.");

            _lostShipCells += count;
        }

        public void IncrementLostMines(uint count = 1)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), "Cannot place fewer than 1 mine.");

            _lostMines += count;
        }
    }
}
internal sealed class ShipScenario
{
    private readonly Dictionary<int, int> _limits;

    private ShipScenario(Dictionary<int, int> limits)
    {
        _limits = limits;
    }

    public static ShipScenario FromId(int id) => id switch
    {
        1 => new ShipScenario(new() { [1] = 1 }),
        2 => new ShipScenario(new() { [1] = 1, [2] = 1 }),
        3 => new ShipScenario(new() { [1] = 1, [2] = 1, [3] = 1 }),
        4 => new ShipScenario(new() { [1] = 2, [2] = 1, [3] = 1 }),
        5 => new ShipScenario(new() { [1] = 2, [2] = 2, [3] = 1 }),
        6 => new ShipScenario(new() { [1] = 2, [2] = 2, [3] = 2 }),
        _ => throw new ArgumentOutOfRangeException(nameof(id), "Unknown ship scenario.")
    };

    public int AllowedCount(int length)
        => _limits.TryGetValue(length, out var count) ? count : 0;

    public int TotalShips => _limits.Values.Sum();

    public int TotalShipCells => _limits.Sum(x => x.Key * x.Value);

    public string Describe()
        => string.Join(", ", _limits.OrderBy(x => x.Key)
            .Select(x => $"{x.Value} ship 1x{x.Key}"));
}
