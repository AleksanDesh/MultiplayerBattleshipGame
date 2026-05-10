using System;
using static Model.SessionData;

namespace Model
{
    internal class SeaBattleBehavior
    {
        public PlaceShipResult PlaceShip(SessionData session, PlayerData player, Ship ship)
        {
            if (session.Phase != SessionData.GamePhase.Preparation)
                throw new InvalidOperationException("SeaBattleBehavior: Cannot place ships after battle has started.");

            if (ship == null)
                throw new ArgumentNullException(nameof(ship));

            if (ship.length <= 0)
                throw new ArgumentOutOfRangeException(nameof(ship.length), "Ship length must be positive.");

            var board = session.GetOwnBoard(player);
            var participant = session.GetParticipant(player);

            // If the ship already exists, this is a move/relocation of the same ship ID.
            bool isReplacement = participant.Ships.TryGetValue(ship.Id, out var previousShip);

            // Per-length quota check.
            // Example: if the scenario allows only one length-3 ship, placing a second one fails.
            int allowedCountForThisLength = session.Scenario.AllowedCount(ship.length);
            if (allowedCountForThisLength == 0)
                return PlaceShipResult.ShipLimitReached;

            uint currentCountForThisLength = participant.GetPlacedShipsByLength(ship.length);

            // If the same ship ID is being moved but keeps the same length, the old one should
            // not count against the quota, because it is effectively being replaced.
            if (isReplacement && previousShip?.length == ship.length)
                currentCountForThisLength--;

            if (currentCountForThisLength >= allowedCountForThisLength)
                return PlaceShipResult.ShipLimitReached;

            // Store the old occupied cells before any board changes.
            // This lets us treat the old ship's own cells as temporarily "free" during validation.
            HashSet<(int, int)> previousCells = null;
            if (isReplacement)
            {
                previousCells = GetShipCells(
                    (int)previousShip.position.X,
                    (int)previousShip.position.Y,
                    previousShip.length,
                    previousShip.rotated
                ).ToHashSet();
            }

            int x = (int)ship.position.X;
            int y = (int)ship.position.Y;
            int length = ship.length;
            bool rotated = ship.rotated;

            var shipCells = GetShipCells(x, y, length, rotated);

            // First pass: bounds + direct cell occupancy.
            // The "previousCells" exception prevents the current ship from blocking itself
            // while it is being moved to a new position.
            foreach (var cellPos in shipCells)
            {
                int cx = cellPos.Item1;
                int cy = cellPos.Item2;

                if (cx < 0 || cy < 0 || cx >= board.Length || cy >= board.Length)
                    return PlaceShipResult.OutOfBounds;

                if (board[cx][cy]._state != Cell.CellState.Empty &&
                    !(isReplacement && previousCells.Contains((cx, cy))))
                {
                    return PlaceShipResult.CellOccupied;
                }
            }

            // Second pass: adjacency rule.
            // Ships may not touch, even diagonally.
            // Again, previousCells are ignored because the current ship is allowed to occupy
            // the space where its old cells were before the move.
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

                        if (isReplacement && previousCells.Contains((nx, ny)))
                            continue;

                        if (board[nx][ny]._state == Cell.CellState.Ship)
                            return PlaceShipResult.ShipNearby;
                    }
                }
            }

            // Only after all validation passes do we mutate the board.
            if (isReplacement)
            {
                // Clear the old ship cells from the board before writing the new position.
                foreach (var cellPos in previousCells)
                {
                    if (board[cellPos.Item1][cellPos.Item2]._state == Cell.CellState.Ship)
                        board[cellPos.Item1][cellPos.Item2]._state = Cell.CellState.Empty;
                }

                participant.DecrementPlacedShipsByLength(previousShip.length);
            }
            else
            {
                participant.IncrementPlacedShips();
            }

            participant.IncrementPlacedShipsByLength(ship.length);

            foreach (var cellPos in shipCells)
            {
                board[cellPos.Item1][cellPos.Item2]._state = Cell.CellState.Ship;
            }

            participant.Ships[ship.Id] = ship;
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

        public PlaceMineResult PlaceMine(SessionData session, PlayerData player, Mine mine)
        {
            if (session.Phase != SessionData.GamePhase.Preparation)
                throw new InvalidOperationException("SeaBattleBehavior: Cannot place mines after battle has started.");

            if (mine == null)
                throw new ArgumentNullException(nameof(mine));

            var board = session.GetOwnBoard(player);
            var participant = session.GetParticipant(player);

            int x = (int)mine.position.X;
            int y = (int)mine.position.Y;

            bool isReplacement = participant.Mines.TryGetValue(mine.Id, out var previousMine);

            int previousX = -1;
            int previousY = -1;
            if (isReplacement)
            {
                previousX = (int)previousMine.position.X;
                previousY = (int)previousMine.position.Y;
            }

            if (x < 0 || y < 0 || x >= board.Length || y >= board.Length)
                return PlaceMineResult.OutOfBounds;

            var cell = board[x][y];

            // Allow placing onto the old cell when the same mine is being moved.
            bool isSameOldCell = isReplacement && previousX == x && previousY == y;

            if (cell._state != Cell.CellState.Empty && !isSameOldCell)
                return PlaceMineResult.CellOccupied;

            // Only enforce the mine limit for brand-new mines.
            if (!isReplacement && participant.PlacedMines >= session.MaxMines)
                return PlaceMineResult.MineLimitReached;

            // Mutate only after validation succeeds.
            if (isReplacement)
            {
                var previousCell = board[previousX][previousY];
                if (previousCell._state == Cell.CellState.Mine)
                    previousCell._state = Cell.CellState.Empty;
            }
            else
            {
                participant.IncrementPlacedMines();
            }

            cell._state = Cell.CellState.Mine;
            participant.Mines[mine.Id] = mine;

            return PlaceMineResult.Success;
        }

        public BombingResult Bomb(SessionData session, PlayerData player, int[] location, out List<BombTrace> extraHits)
        {
            extraHits = new List<BombTrace>();

            if (session.Phase != GamePhase.Battle)
                throw new InvalidOperationException("SeaBattleBehavior: Bombing is only allowed during battle.");
            if (location == null || location.Length < 2)
                throw new ArgumentException("SeaBattleBehavior: Invalid location.", nameof(location));

            var board = session.GetEnemyBoard(player);
            var participant = session.GetParticipant(player);

            if (!session.TryGetEnemyParticipant(player, out var enemyParticipant))
                throw new InvalidOperationException("SeaBattleBehavior: you are not part of this session");

            if (session._participantTurn != participant.Side)
                throw new InvalidOperationException("SeaBattleBehavior: It is not your turn, how did you send this? Are you cheating?");

            int x = location[0];
            int y = location[1];

            if (x < 0 || y < 0 || x >= board.Length || y >= board.Length)
                return BombingResult.OutOfBounds;

            var cell = board[x][y];

            // Default turn change: bomb passes the turn.
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
                        // Keep the turn if somehow bombed a bombed cell
                        session._participantTurn = participant.Side;
                        return BombingResult.AlreadyBombed;
                    }

                case Cell.CellState.Ship:
                    {
                        cell._state = Cell.CellState.Bombed;
                        enemyParticipant.IncrementLostShipCells();

                        if (IfLost(session, enemyParticipant))
                        {
                            player.UpdateTopScore(player.TopScore + 1);
                            session.FinishGame();
                            return BombingResult.Victory;
                        }

                        // Normal hit: attacker keeps turn.
                        session._participantTurn = participant.Side;
                        return BombingResult.Sucess;
                    }

                case Cell.CellState.Mine:
                    {
                        cell._state = Cell.CellState.Bombed;
                        // Don't add this as a hit in extra hits.
                        //extraHits.Add(new BombTrace(x, y, BombingResult.Mine));
                        enemyParticipant.IncrementLostMines();

                        ExplodeMine(session, player, enemyParticipant, x, y, 2, extraHits);
                        // Normal hit: attacker keeps turn.
                        session._participantTurn = participant.Side;

                        if (IfLost(session, enemyParticipant))
                        {
                            player.UpdateTopScore(player.TopScore + 1);
                            session.FinishGame();
                            return BombingResult.Victory;
                        }

                        return BombingResult.Mine;
                    }

                default:
                    throw new NotImplementedException("SeaBattleBehavior: This is not expected nor implemented");
            }
        }
        internal readonly record struct BombTrace(int X, int Y, BombingResult Result);
        private static void ExplodeMine(
            SessionData session,
            PlayerData attacker,
            SessionParticipant enemyParticipant,
            int centerX,
            int centerY,
            int radius,
            List<BombTrace> extraHits)
        {
            var board = session.GetEnemyBoard(attacker);

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int x = centerX + dx;
                    int y = centerY + dy;

                    if (x < 0 || y < 0 || x >= board.Length || y >= board.Length)
                        continue;

                    // The center mine itself is already recorded by the caller.
                    if (x == centerX && y == centerY)
                        continue;

                    ApplyExplosionCell(session, attacker, enemyParticipant, x, y, radius, extraHits);
                }
            }
        }
        private static void ApplyExplosionCell(SessionData session,
            PlayerData attacker,
            SessionParticipant enemyParticipant,
            int x,
            int y,
            int radius,
            List<BombTrace> extraHits)
        {
            var board = session.GetEnemyBoard(attacker);
            var cell = board[x][y];

            switch (cell._state)
            {
                case Cell.CellState.Empty:
                    cell._state = Cell.CellState.Bombed;
                    extraHits.Add(new BombTrace(x, y, BombingResult.Empty));
                    break;

                case Cell.CellState.Bombed:
                    break;

                case Cell.CellState.Ship:
                    cell._state = Cell.CellState.Bombed;
                    enemyParticipant.IncrementLostShipCells();
                    extraHits.Add(new BombTrace(x, y, BombingResult.Sucess));
                    break;

                case Cell.CellState.Mine:
                    cell._state = Cell.CellState.Bombed;
                    enemyParticipant.IncrementLostMines();
                    extraHits.Add(new BombTrace(x, y, BombingResult.Mine));
                    ExplodeMine(session, attacker, enemyParticipant, x, y, radius, extraHits);
                    break;

                default:
                    throw new NotImplementedException("SeaBattleBehavior: This is not expected nor implemented");
            }
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

            if (participant.IsReady)
                return MarkingResult.AlreadyMarked;

            if (participant.PlacedShips != session.MaxShips)
                return MarkingResult.ShipsNotPlaced;

            if (participant.PlacedMines != session.MaxMines)
                return MarkingResult.MinesNotPlaced;

            if (session.MarkReady(participant))
                return MarkingResult.BattleStarted;

            return MarkingResult.Success;
        }

        /// <summary>
        /// Returns true when the participant has lost all ship cells.
        /// </summary>
        /// <param name="session"></param>
        /// <param name="player"></param>
        /// <returns></returns>
        bool IfLost(SessionData session, SessionParticipant player)
        {
            return player.LostShipCells >= session.TotalShipCells;
        }

        internal enum PlaceShipResult
        {
            Success = 0,
            OutOfBounds = 1,
            CellOccupied = 2,
            ShipNearby = 3,
            ShipLimitReached = 4
        }
        internal enum PlaceMineResult
        {
            Success = 0,
            OutOfBounds = 1,
            CellOccupied = 2,
            MineLimitReached = 3
        }

        internal enum BombingResult
        {
            Sucess = 0,
            OutOfBounds = 1,
            AlreadyBombed = 2,
            Empty = 3,
            Mine = 4,
            Victory = 6
        }

        internal enum MarkingResult
        {
            Success = 0,
            BattleStarted = 1,
            ShipsNotPlaced = 2,
            MinesNotPlaced = 3,
            AlreadyMarked = 4
        }
    }
}
