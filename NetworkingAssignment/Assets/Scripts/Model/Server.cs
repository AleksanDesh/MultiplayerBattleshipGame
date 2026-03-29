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
        HashSet<PlayerData> _connectedPlayersMap = new HashSet<PlayerData>();
        List<PlayerData> _connectedPlayersList = new List<PlayerData>();
        List<SessionData> _sessionDatas = new List<SessionData>();
        Dictionary<string, SessionData> _userSessionKey = new Dictionary<string, SessionData>();
        Dictionary<string, PlayerData> _userPlayerKey = new Dictionary<string, PlayerData>();

        // Queing for players, now enques all newly connected users
        Queue<PlayerData> _playerQueue = new Queue<PlayerData>();
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            _battleBehavior = new SeaBattleBehavior();
            string path = Path.Combine(Application.persistentDataPath, "players.json");
            _login = new Login(path);
        }

        // Update is called once per frame
        void FixedUpdate()
        {
            if (_playerQueue.Count > 1)
            {
                bool isSessionCreated = TryCreateSession(_playerQueue.Dequeue(), _playerQueue.Dequeue());
                if (isSessionCreated) Debug.Log($"Server: Session created succesefully");
            }
        }
        #region UserLoging
        public bool ConnectUser(string name, string password)
        {
            var user = _login.LoginOrCreate(name, password);
            if (user != null)
            {
                if (!_userPlayerKey.ContainsKey(name))
                    _userPlayerKey.Add(name, user);
                else
                {
                    Debug.LogWarning($"Trying to connect already connected user {name}");
                    return false;
                }
                _connectedPlayersMap.Add(user);
                _connectedPlayersList.Add(user);
                // Enqueue immediately
                _playerQueue.Enqueue(user);
                Debug.Log($"Server: {name} connected succesefully, added to queue");
                return true;
            }
            else
                return false;
        }

        bool TryCreateSession(PlayerData player1, PlayerData player2)
        {
            // Run some checks... (which we won't do for now)
            SessionData newSession = new SessionData(player1, player2, 2, 1);
            if (newSession == null)
                return false;

            if (!_userSessionKey.ContainsKey(player1.Username) && !_userSessionKey.ContainsKey(player2.Username))
            {
                _userSessionKey.Add(player1.Username, newSession);
                _userSessionKey.Add(player2.Username, newSession);
            }
            else
            {
                if (_userSessionKey.ContainsKey(player1.Username))
                Debug.LogWarning($"Server: {player1.Username} already is in session, HOW are you creating another one?");
                if (_userSessionKey.ContainsKey(player2.Username))
                Debug.LogWarning($"Server: {player2.Username} already is in session, HOW are you creating another one?");
                return false;
            }
            _sessionDatas.Add(newSession);
            return true;
        }
    
        #endregion
        // TODO: Add checks if user is able to call a method by checking if he is in session, etc.
        #region MethodsToCall
        public string PlaceShip(string username, int[] location)
        {
            var player = _userPlayerKey[username];
            var session = _userSessionKey[username];
            var outcome = _battleBehavior.PlaceShip(session, player, location);

            string message = $"Server: {username} " + outcome.ToString();

            switch (outcome)
            {
                case PlaceShipResult.Success:
                    {
                        // TODO: success logic
                        //message = "SUCESS";
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

            string message = $"Server: {username} " + outcome.ToString();

            switch (outcome)
            {
                case PlaceMineResult.Success:
                    {
                        // TODO: success logic
                        //message = "SUCESS";
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

            string message = $"Server: {username} " + outcome.ToString();

            switch (outcome)
            {
                case BombingResult.Sucess:
                    {
                        // TODO: success logic
                        //message = "SUCESS";
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

            string message = $"Server: {username} " + outcome.ToString();

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