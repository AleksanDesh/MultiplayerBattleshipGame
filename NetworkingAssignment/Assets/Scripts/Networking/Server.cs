using Model;
using NetworkConnections;
using OSCTools;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using static Model.SeaBattleBehavior;

namespace Network
{
    [DefaultExecutionOrder(-200)]
    internal class Server : MonoBehaviour
    {
        // ----- General server code:
        TcpListener listener;
        List<TcpNetworkConnection> connections;
        OSCDispatcher dispatcher;

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

        void Awake()
        {
            _battleBehavior = new SeaBattleBehavior();
            string path = Path.Combine(Application.persistentDataPath, "players.json");
            _login = new Login(path);



            int port = 5376;
            Debug.Log("Starting server at " + port);
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            connections = new List<TcpNetworkConnection>();

            // Initialize the dispatcher and callbacks for incoming OSC messages:
            dispatcher = new OSCDispatcher();
            dispatcher.ShowIncomingMessages = true;
            Initialize();
        }

        void Initialize()
        {
            // TODO: Subscribe to the apropriate methods
        }

        void AcceptNewConnection()
        {
            if (listener.Pending())
            {
                TcpClient client = listener.AcceptTcpClient();
                TcpNetworkConnection connection = new TcpNetworkConnection(client);
                connections.Add(connection);
                Debug.Log("Server: Adding new connection from " + connection.Remote);
            }
        }

        //void ClientJoined(TcpNetworkConnection newClient)
        //{
        //    if (playerIDs.Count < 2)
        //    {
        //        // We had fewer than 2 players, so this new client will be a player.
        //        playerIDs[newClient] = playerIDs.Count + 1;
        //        Debug.Log($"Registering new player: {newClient.Remote} = player {playerIDs[newClient]}");
        //        if (playerIDs.Count == 2)
        //        { // start game
        //            Debug.Log("Server: starting game");
        //            foreach (var pid in playerIDs.Keys)
        //            {
        //                SendPrivateInformationCommand(playerIDs[pid], pid);
        //            }
        //        }
        //    }
        //    else
        //    {
        //        Debug.Log("Sorry - already have two players");
        //        // Note: this client is still allowed to join as spectator, but not as player!
        //        // TODO: Send a message to this client
        //    }
        //}
        //void AcceptNewConnections()
        //{
        //    if (listener.Pending())
        //    {
        //        TcpClient client = listener.AcceptTcpClient();
        //        TcpNetworkConnection connection = new TcpNetworkConnection(client);
        //        connections.Add(connection);
        //        Debug.Log("Server: Adding new connection from " + connection.Remote);
        //        ClientJoined(connection);
        //    }
        //}
        //void ClientJoined(TcpNetworkConnection newClient)
        //{
        //    if (playerIDs.Count < 2)
        //    {
        //        // We had fewer than 2 players, so this new client will be a player.
        //        playerIDs[newClient] = playerIDs.Count + 1;
        //        Debug.Log($"Registering new player: {newClient.Remote} = player {playerIDs[newClient]}");
        //        if (playerIDs.Count == 2)
        //        { // start game
        //            Debug.Log("Server: starting game");
        //            foreach (var pid in playerIDs.Keys)
        //            {
        //                SendPrivateInformationCommand(playerIDs[pid], pid);
        //            }
        //        }
        //    }
        //    else
        //    {
        //        Debug.Log("Sorry - already have two players");
        //        // Note: this client is still allowed to join as spectator, but not as player!
        //        // TODO: Send a message to this client
        //    }
        //}
        //
        //void UpdateConnections()
        //{
        //    foreach (TcpNetworkConnection conn in connections)
        //    {
        //        // The connection will call HandlePacket when a packet is available:
        //        while (conn.Available() > 0)
        //        {
        //            HandlePacket(conn.GetPacket(), conn.Remote);
        //        }
        //    }
        //}
        //
        //void CleanConnections()
        //{
        //
        //}
        void Update()
        {
            AcceptNewConnection();
        }


        void FixedUpdate()
        {
            if (_playerQueue.Count > 1)
            {
                bool isSessionCreated = TryCreateSession();
                if (isSessionCreated) Debug.Log($"Server: Session created succesefully");
            }
        }


        #region UserLoging
        public bool ConnectUser(string name, string password)
        { // TODO: maybe add instant data sending with the leaderboards?
            var user = _login.LoginOrCreate(name, password);
            if (user != null)
            {// TODO: add a check if the user is already in a session, if he is, put him in there
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

        bool TryCreateSession()
        {
            PlayerData player1 = _playerQueue.Dequeue();
            PlayerData player2 = _playerQueue.Dequeue();
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

                // TODO: reinqueue both?
                return false;
            }
            _sessionDatas.Add(newSession);

            // TODO: send info to both players about who they play against, which involves their victories as well
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
                        _login.SaveAccount(player);
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
                        // tell the client to wait for the other player
                        message = $"Server: sucessefuly pressed Ready. Waiting for the other party...";
                        break;
                    }

                case MarkingResult.BattleStarted:
                    {
                        // TODO: battle started logic
                        // send info to both, that the game has started
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