using System;
using static Model.SessionData;

namespace Model
{
    internal class SeaBattleBehavior
    {
        public PlaceShipResult PlaceShip(SessionData session, PlayerData player, Ship ship)
        {// TODO: check if this ship already exists
            if (session.Phase != SessionData.GamePhase.Preparation)
                throw new InvalidOperationException("SeaBattleBehavior: Cannot place ships after battle has started.");

            if (ship == null)
                throw new ArgumentNullException(nameof(ship));

            var board = session.GetOwnBoard(player);
            var participant = session.GetParticipant(player);

            // If the ship was placed earlier, allow it to be placed, else return
            if (participant.PlacedShips >= session.MaxShips && !participant.Ships.ContainsKey(ship.Id))
                return PlaceShipResult.ShipLimitReached;

            int x = (int)ship.position.X;
            int y = (int)ship.position.Y;
            int length = ship.length;
            bool rotated = ship.rotated;

            // Clean cell data where the ship was located before it it existed on the board
            if (participant.Ships.ContainsKey(ship.Id))
            {
                Ship previous = participant.Ships[ship.Id];
                var previousCells = GetShipCells((int)previous.position.X, (int)previous.position.Y, previous.length, previous.rotated);
                foreach (var cellPos in previousCells)
                {
                    int cx = cellPos.Item1;
                    int cy = cellPos.Item2;

                    if (cx < 0 || cy < 0 || cx >= board.Length || cy >= board.Length)
                        throw new ArgumentOutOfRangeException("SeaBattleBehavior: " +
                            "Ship was located outside of bounds. This should not be possible");

                    if (board[cx][cy]._state == Cell.CellState.Ship)
                        board[cx][cy]._state = Cell.CellState.Empty;
                }
            }

            var shipCells = GetShipCells(x, y, length, rotated);

            // Check if the cells are availible
            foreach (var cellPos in shipCells)
            {
                int cx = cellPos.Item1;
                int cy = cellPos.Item2;

                if (cx < 0 || cy < 0 || cx >= board.Length || cy >= board.Length)
                    return PlaceShipResult.OutOfBounds;

                if (board[cx][cy]._state != Cell.CellState.Empty)
                    return PlaceShipResult.CellOccupied;
            }

            // Check if the surrounding cells are occupied
            foreach (var cellPos in shipCells)
            {
                int cx = cellPos.Item1;
                int cy = cellPos.Item2;

                for (int i = -1; i < 2; i++)
                {
                    for (int j = -1; j < 2; j++)
                    {
                        int nx = cx + i;
                        int ny = cy + j;

                        if (nx < 0 || ny < 0 || nx >= board.Length || ny >= board.Length)
                            continue;

                        if (!shipCells.Contains((nx, ny)) && board[nx][ny]._state == Cell.CellState.Ship)
                            return PlaceShipResult.ShipNearby;
                    }
                }
            }
            foreach (var cellPos in shipCells)
            {
                board[cellPos.Item1][cellPos.Item2]._state = Cell.CellState.Ship;
            }
            // If the ship did exist, remove it and don't increment placed ships
            if (participant.Ships.ContainsKey(ship.Id))
                participant.Ships.Remove(ship.Id);
            else // new ship placed => increment
                participant.IncrementPlacedShips();

            participant.TryAddShip(ship);
            return PlaceShipResult.Success;
        }
        /// <summary>
        /// Helper to get cells on which the ship is placed
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="length"></param>
        /// <param name="rotated"></param>
        /// <returns></returns>
        private static List<(int, int)> GetShipCells(int x, int y, int length, bool rotated)
        {
            var cells = new List<(int, int)>(length);

            int startOffset = -(length / 2);

            for (int i = 0; i < length; i++)
            {
                int offset = startOffset + i;
                int cx = rotated ? x : x + offset;
                int cy = rotated ? y + offset : y;

                cells.Add((cx, cy));
            }

            return cells;
        }

        public PlaceMineResult PlaceMine(SessionData session, PlayerData player, int[] location)
        {
            if (session.Phase != SessionData.GamePhase.Preparation)
                throw new InvalidOperationException("SeaBattleBehavior: Cannot place mines after battle has started.");
            if (location == null || location.Length < 2)
                throw new ArgumentException("SeaBattleBehavior: Invalid location.", nameof(location));

            var board = session.GetOwnBoard(player);
            var participant = session.GetParticipant(player);

            int x = location[0];
            int y = location[1];

            if (x < 0 || y < 0 || x >= board.Length || y >= board.Length)
                return PlaceMineResult.OutOfBounds;

            var cell = board[x][y];

            if (cell._state != Cell.CellState.Empty)
                return PlaceMineResult.CellOccupied;

            if (participant.PlacedMines >= session.MaxMines)
                return PlaceMineResult.MineLimitReached;

            cell._state = Cell.CellState.Mine;
            participant.IncrementPlacedMines();

            return PlaceMineResult.Success;
        }

        public BombingResult Bomb(SessionData session, PlayerData player, int[] location)
        {
            if (session.Phase != GamePhase.Battle)
                throw new InvalidOperationException("SeaBattleBehavior: Bombing is only allowed during battle.");
            if (location == null || location.Length < 2)
                throw new ArgumentException("SeaBattleBehavior: Invalid location.", nameof(location));

            var board = session.GetEnemyBoard(player);
            var participant = session.GetParticipant(player);
            var enemyParticipant = session.GetEnemyParticipant(player);

            if (session._participantTurn != participant.Side)
            {
                throw new InvalidOperationException("SeaBattleBehavior: It is not your turn, how did you send this? Are you cheating?");
            }

            int x = location[0];
            int y = location[1];

            if (x < 0 || y < 0 || x >= board.Length || y >= board.Length)
                return BombingResult.OutOfBounds;

            var cell = board[x][y];

            session._participantTurn = enemyParticipant.Side;
            switch (cell._state)
            {
                case Cell.CellState.Empty:
                    {
                        cell._state = Cell.CellState.Bombed;
                        return BombingResult.Empty;
                    }
                case Cell.CellState.Bombed:
                    {
                        return BombingResult.AlreadyBombed;
                    }
                case Cell.CellState.Ship:
                    {
                        cell._state = Cell.CellState.Bombed;
                        enemyParticipant.IncrementLostShips();
                        if (IfLost(session, enemyParticipant))
                        {
                            player.UpdateTopScore(player.TopScore+1);
                            return BombingResult.Victory;
                        }
                        session._participantTurn = participant.Side;
                        return BombingResult.Sucess;
                    }
                case Cell.CellState.Mine:
                    {
                        cell._state = Cell.CellState.Bombed;
                        return BombingResult.Mine;
                    }
                default:
                    {
                        break;
                    }
            }
            throw new NotImplementedException("SeaBattleBehavior: This is not expected nor implemented");
        }

        /// <summary>
        /// If returns true => start the battle
        /// </summary>
        /// <param name="session"></param>
        /// <param name="player"></param>
        /// <returns></returns>
        public MarkingResult MarkReady(SessionData session, PlayerData player)
        {
            var participant = session.GetParticipant(player);
            if (participant.PlacedShips != session.MaxShips)
                return MarkingResult.ShipsNotPlaced;
            if (participant.PlacedMines != session.MaxMines)
                return MarkingResult.MinesNotPlaced;

            if (session.MarkReady(participant))
                return MarkingResult.BattleStarted;
            else
                return MarkingResult.Success;
        }

        /// <summary>
        /// Method to check whether this party has lost
        /// </summary>
        /// <param name="session"></param>
        /// <param name="player">The party, that should not have ships left</param>
        /// <returns></returns>
        bool IfLost(SessionData session, SessionParticipant player)
        {
            if (player.LostShips == session.MaxShips) return true;
            return false;
        }

        internal enum PlaceShipResult
        {
            Success = 1,
            OutOfBounds,
            CellOccupied,
            ShipNearby,
            ShipLimitReached
        }
        internal enum PlaceMineResult
        {
            Success,
            OutOfBounds,
            CellOccupied,
            MineLimitReached
        }

        internal enum BombingResult
        {
            Sucess,
            OutOfBounds,
            AlreadyBombed,
            Empty,
            Mine,
            Victory
        }

        internal enum MarkingResult
        {
            Success,
            BattleStarted,
            ShipsNotPlaced,
            MinesNotPlaced
        }
    }
}
