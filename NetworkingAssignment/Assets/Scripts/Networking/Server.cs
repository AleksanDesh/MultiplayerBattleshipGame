using Model;
using NetworkConnections;
using OSCTools;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using static Model.SeaBattleBehavior;
using static UnityEngine.UIElements.UxmlAttributeDescription;

namespace Network
{
    [DefaultExecutionOrder(-200)]
    internal class Server : MonoBehaviour
    {
        // ----- General server code:
        TcpListener _listener;
        List<TcpNetworkConnection> _allConnections;
        List<TcpNetworkConnection> _unloggedConnections;
        OSCDispatcher _dispatcher;

        SeaBattleBehavior _battleBehavior;
        Login _login;
        public Login Login => _login;
        HashSet<PlayerData> _connectedPlayersMap = new HashSet<PlayerData>();
        List<PlayerData> _connectedPlayersList = new List<PlayerData>();
        List<SessionData> _sessionDatas = new List<SessionData>();
        Dictionary<string, SessionData> _userSessionKey = new Dictionary<string, SessionData>();
        Dictionary<string, PlayerData> _userPlayerKey = new Dictionary<string, PlayerData>();
        Dictionary<TcpNetworkConnection, PlayerData> _tcpNetPlayerKey = new Dictionary<TcpNetworkConnection, PlayerData>();
        


        // Queing for players, now enques all newly connected users
        Queue<PlayerData> _playerQueue = new Queue<PlayerData>();

        void Awake()
        {
            _battleBehavior = new SeaBattleBehavior();
            string path = Path.Combine(Application.persistentDataPath, "players.json");
            _login = new Login(path);


            // TODO: make the port auto adjustable
            int port = 5376;
            Debug.Log("Starting server at " + port);
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();

            _allConnections = new List<TcpNetworkConnection>();
            _unloggedConnections = new List<TcpNetworkConnection>();

            // Initialize the dispatcher and callbacks for incoming OSC messages:
            _dispatcher = new OSCDispatcher();
            _dispatcher.ShowIncomingMessages = true;
            Initialize();
        }

        void Initialize()
        {
            // TODO: Subscribe to the apropriate methods
            _dispatcher.AddListener("/Login", UserLogin, OSCUtil.STRING, OSCUtil.STRING);
        }

        void AcceptNewConnection()
        {
            if (_listener.Pending())
            {
                TcpClient client = _listener.AcceptTcpClient();
                TcpNetworkConnection connection = new TcpNetworkConnection(client);
                _allConnections.Add(connection);
                _unloggedConnections.Add(connection);
                Debug.Log("Server: Adding new connection from " + connection.Remote);
            }
        }
        void UpdateConnections()
        {
            foreach (TcpNetworkConnection conn in _allConnections)
                while (conn.Available() > 0)
                    HandlePacket(conn.GetPacket(), conn.Remote);
        }
        void HandlePacket(byte[] packet, IPEndPoint remote)
        {
            OSCMessageIn mess = new OSCMessageIn(packet);
            Debug.Log("Server: Message arrived to server: " + mess);

            _dispatcher.HandlePacket(packet, remote);
        }

        //void CleanConnections()
        //{
        //
        //}
        void Update()
        {
            AcceptNewConnection();
            UpdateConnections();
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
        void UserLogin(OSCMessageIn message, IPEndPoint remote)
        {
            string username = message.ReadString();
            string password = message.ReadString();
            Debug.Log($"Server: Received Login attempt with username {username} and password {password}");
            bool sucess = TryUserLogin(username, password, out var answer, out var playerData);
            OSCMessageOut reply = new OSCMessageOut("/TryJoin").AddInt(answer);
            foreach (var conn in _unloggedConnections)
            {
                if (conn.Remote.Equals(remote))
                {
                    if (sucess)
                        _tcpNetPlayerKey.Add(conn, playerData);
                    conn.Send(reply.GetBytes());
                }
            }
        }

        /// <summary>
        /// Attempt to connect the user
        /// TODO: Make it so the answer will be a number, that coresponds to a certain error. Dictionary of errors,
        /// so i can send a number, which will represent what went wrong
        /// </summary>
        /// <param name="answer"> The output of what happend. 
        /// -1 = something went REALLY WRONG
        /// 0 = user already connected
        /// 1 = sucess
        /// 2 = wrong username
        /// 3 = wrong password
        /// </param>
        /// <returns></returns>
        bool TryUserLogin(string username, string password, out int answer, out PlayerData playerData)
        {
            answer = -1;
            bool sucess = _login.LoginUser(username, password, out PlayerData user);
            playerData = user;
            if (sucess)
            {
                if (!_userPlayerKey.ContainsKey(name))
                    _userPlayerKey.Add(name, user);
                else
                {
                    Debug.LogWarning($"Trying to connect already connected user {name}");
                    answer = 0;
                    return false;
                }
                _connectedPlayersMap.Add(user);
                _connectedPlayersList.Add(user);
                // Enqueue immediately
                _playerQueue.Enqueue(user);
                Debug.Log($"Server: {name} connected succesefully, added to queue");
                answer = 1;
                return true;
            }
            else
            {
                if (user == null)
                    answer = 2;
                else
                    answer = 3;
                return false;
            }
        }

        void UserRegister()
        {

        }
        public bool ConnectUser(string name, string password)
        { // TODO: maybe add instant data sending with the leaderboards?
            bool success  = _login.LoginOrCreate(name, password, out PlayerData user);
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