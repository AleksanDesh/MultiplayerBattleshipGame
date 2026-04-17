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
        private readonly object _sync = new object(); // in case both players click ready at the same time

        public int SessionID { get; }
        public GamePhase Phase { get; private set; } = GamePhase.Preparation;
        public Cell[][] FirstMap { get; }
        public Cell[][] SecondMap { get; }

        public readonly int MaxShips;
        public readonly int MaxMines;

        SessionParticipant[] _participants;
        public PlayerSide _participantTurn { get; set; }

        public SessionData(PlayerData player1, PlayerData player2, int maxShips = 8, int maxMines = 4, int mapSize = 8)
        {
            if (player1 == null) throw new ArgumentNullException(nameof(player1));
            if (player2 == null) throw new ArgumentNullException(nameof(player2));
            if (mapSize <= 0) throw new ArgumentOutOfRangeException(nameof(mapSize));

            SessionID = Interlocked.Increment(ref _nextId);

            FirstMap = CreateMap(mapSize);
            SecondMap = CreateMap(mapSize);

            _participants = new[] {
                new SessionParticipant(player1, PlayerSide.First),
                new SessionParticipant(player2, PlayerSide.Second)
            };

            MaxMines = maxMines;
            MaxShips = maxShips;
            _participantTurn = PlayerSide.First;
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

        public SessionParticipant GetEnemyParticipant(PlayerData player)
        {
            PlayerSide otherSide = GetSide(player) == PlayerSide.First
                ? PlayerSide.Second
                : PlayerSide.First;
            foreach (var otherParticipant in _participants)
            {
                if (otherParticipant.Side == otherSide)
                    return otherParticipant;
            }
                
            throw new InvalidOperationException("Player is not part of this session");
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
        private uint _lostShips = 0;
        private uint _lostMines = 0;
        private uint _placedShips = 0;
        private uint _placedMines = 0;
        public uint LostShips => _lostShips;
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

        public bool TryAddShip(Ship ship)
        {
            if (_ships.ContainsKey(ship.Id))
                return false;
            _ships.Add(ship.Id, ship);
            return true;
        }

        public void IncrementPlacedShips(uint count = 1)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), "Cannot place fewer than 1 ship.");

            _placedShips += count;
        }

        public void IncrementPlacedMines(uint count = 1)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), "Cannot place fewer than 1 mine.");

            _placedMines += count;
        }

        public void IncrementLostShips(uint count = 1)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), "Cannot place fewer than 1 ship.");

            _lostShips += count;
        }

        public void IncrementLostMines(uint count = 1)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), "Cannot place fewer than 1 mine.");

            _lostMines += count;
        }
    }
}
