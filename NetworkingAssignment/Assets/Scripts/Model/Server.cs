using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static Model.SeaBattleBehavior;

namespace Model
{
    [DefaultExecutionOrder(-200)]
    internal class Server : MonoBehaviour
    {
        SeaBattleBehavior _battleBehavior;
        Login _login;
        public Login Login => _login;
        List<PlayerData> _connectedPlayersList;
        List<SessionData> _sessionDatas;
        Dictionary<string, SessionData> _userSessionKey = new Dictionary<string, SessionData>();
        Dictionary<string, PlayerData> _userPlayerKey = new Dictionary<string, PlayerData>(); 

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            _battleBehavior = new SeaBattleBehavior();
            string path = Path.Combine(Application.persistentDataPath, "players.json");
            _login = new Login(path);
        }

        // Update is called once per frame
        void Update()
        {

        }

        #region methodsToCall
        public string PlaceShip(string username, int[] location)
        {
            var player = _userPlayerKey[username];
            var session = _userSessionKey[username];
            var outcome = _battleBehavior.PlaceShip(session, player, location);

            string message = "N/A";

            switch (outcome)
            {
                case PlaceShipResult.Success:
                    {
                        // TODO: success logic
                        message = "SUCESS";
                        break;
                    }

                case PlaceShipResult.OutOfBounds:
                    {
                        // TODO: out of bounds logic
                        break;
                    }

                case PlaceShipResult.CellOccupied:
                    {
                        // TODO: cell occupied logic
                        break;
                    }

                case PlaceShipResult.ShipNearby:
                    {
                        // TODO: ship nearby logic
                        break;
                    }

                case PlaceShipResult.ShipLimitReached:
                    {
                        // TODO: ship limit reached logic
                        break;
                    }

                default:
                    {
                        // TODO: unexpected result logic
                        break;
                    }
            }

            return message;
        }

        public string PlaceMine(string username, int[] location)
        {
            var player = _userPlayerKey[username];
            var session = _userSessionKey[username];
            var outcome = _battleBehavior.PlaceMine(session, player, location);

            string message = "N/A";

            switch (outcome)
            {
                case PlaceMineResult.Success:
                    {
                        // TODO: success logic
                        message = "SUCESS";
                        break;
                    }

                case PlaceMineResult.OutOfBounds:
                    {
                        // TODO: out of bounds logic
                        break;
                    }

                case PlaceMineResult.CellOccupied:
                    {
                        // TODO: cell occupied logic
                        break;
                    }

                case PlaceMineResult.MineLimitReached:
                    {
                        // TODO: mine limit reached logic
                        break;
                    }

                default:
                    {
                        // TODO: unexpected result logic
                        break;
                    }
            }

            return message;
        }

        public string Bomb(string username, int[] location)
        {
            var player = _userPlayerKey[username];
            var session = _userSessionKey[username];
            var outcome = _battleBehavior.Bomb(session, player, location);

            string message = "N/A";

            switch (outcome)
            {
                case BombingResult.Sucess:
                    {
                        // TODO: success logic
                        message = "SUCESS";
                        break;
                    }

                case BombingResult.OutOfBounds:
                    {
                        // TODO: out of bounds logic
                        break;
                    }

                case BombingResult.AlreadyBombed:
                    {
                        // TODO: already bombed logic
                        break;
                    }

                case BombingResult.Empty:
                    {
                        // TODO: empty cell logic
                        break;
                    }

                case BombingResult.Mine:
                    {
                        // TODO: mine hit logic
                        break;
                    }

                case BombingResult.Victory:
                    {
                        // TODO: victory logic, broadcast to everybody, that the caller won
                        message = "YOU WON";
                        break;
                    }

                default:
                    {
                        // TODO: unexpected result logic
                        break;
                    }
            }

            return message;
        }

        public string MarkReady(string username)
        {
            var player = _userPlayerKey[username];
            var session = _userSessionKey[username];
            var outcome = _battleBehavior.MarkReady(session, player);

            string message = "N/A";

            switch (outcome)
            {
                case MarkingResult.Success:
                    {
                        // TODO: success logic
                        break;
                    }

                case MarkingResult.BattleStarted:
                    {
                        // TODO: battle started logic
                        break;
                    }

                case MarkingResult.ShipsNotPlaced:
                    {
                        // TODO: ships not placed logic
                        break;
                    }

                case MarkingResult.MinesNotPlaced:
                    {
                        // TODO: mines not placed logic
                        break;
                    }

                default:
                    {
                        // TODO: unexpected result logic
                        break;
                    }
            }

            return message;
        }
        #endregion
    }
}