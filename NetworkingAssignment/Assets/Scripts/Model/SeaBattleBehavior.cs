using System;
using UnityEngine;
using static Model.SessionData;

namespace Model
{
    internal class SeaBattleBehavior
    {
        public PlaceShipResult PlaceShip(SessionData session, PlayerData player, int[] location)
        {
            if (session.Phase != SessionData.GamePhase.Preparation)
                throw new InvalidOperationException("Cannot place ships after battle has started.");

            if (location == null || location.Length < 2)
                throw new ArgumentException("Invalid location.", nameof(location));

            var board = session.GetOwnBoard(player);
            var participant = session.GetParticipant(player);

            int x = location[0];
            int y = location[1];

            if (x < 0 || y < 0 || x >= board.Length || y >= board.Length)
                return PlaceShipResult.OutOfBounds;

            var cell = board[x][y];

            if (cell._state != Cell.CellState.Empty)
                return PlaceShipResult.CellOccupied;

            for (int i = -1; i < 2; i++)
                for (int j = -1; j < 2; j++)
                    if (board[i][y]._state == Cell.CellState.Ship)
                        return PlaceShipResult.ShipNearby;

            if (participant.PlacedShips >= session.MaxShips)
                return PlaceShipResult.ShipLimitReached;

            cell._state = Cell.CellState.Ship;
            participant.IncrementPlacedShips();

            return PlaceShipResult.Success;
        }

        public PlaceMineResult PlaceMine(SessionData session, PlayerData player, int[] location)
        {
            if (session.Phase != SessionData.GamePhase.Preparation)
                throw new InvalidOperationException("Cannot place mines after battle has started.");
            if (location == null || location.Length < 2)
                throw new ArgumentException("Invalid location.", nameof(location));

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
                throw new InvalidOperationException("Bombing is only allowed during battle.");
            if (location == null || location.Length < 2)
                throw new ArgumentException("Invalid location.", nameof(location));

            var board = session.GetEnemyBoard(player);
            var participant = session.GetParticipant(player);
            var enemyParticipant = session.GetEnemyParticipant(player);

            int x = location[0];
            int y = location[1];

            if (x < 0 || y < 0 || x >= board.Length || y >= board.Length)
                return BombingResult.OutOfBounds;

            var cell = board[x][y];

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
                            return BombingResult.Victory;
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
            return BombingResult.Sucess;
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
            Success,
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
