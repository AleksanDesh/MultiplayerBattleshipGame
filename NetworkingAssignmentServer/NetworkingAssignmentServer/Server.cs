using Model;
using NetworkConnections;
using OSCTools;
using System.Net;
using System.Net.Sockets;
using static Model.SeaBattleBehavior;

namespace Network
{
    internal class Server
    {
        public Server() => Awake();
        // ----- General server code:
        TcpListener? _listener;
        List<TcpNetworkConnection> _allConnections = new List<TcpNetworkConnection>();
        List<TcpNetworkConnection> _unloggedConnections = new List<TcpNetworkConnection>();
        OSCDispatcher _dispatcher = new OSCDispatcher();

        SeaBattleBehavior _battleBehavior = new SeaBattleBehavior();
        Login _login = new Login("Data\\players.json");
        //public Login Login => _login;
        HashSet<PlayerData> _connectedPlayersMap = new HashSet<PlayerData>();
        List<PlayerData> _connectedPlayersList = new List<PlayerData>();
        List<SessionData> _sessionDatas = new List<SessionData>();
        Dictionary<string, SessionData> _userSessionKey = new Dictionary<string, SessionData>();
        Dictionary<string, PlayerData> _userPlayerKey = new Dictionary<string, PlayerData>();
        Dictionary<TcpNetworkConnection, PlayerData> _tcpNetPlayerKey = new Dictionary<TcpNetworkConnection, PlayerData>();
        Dictionary<IPEndPoint, TcpNetworkConnection> _ipEndToTcpNetKey = new Dictionary<IPEndPoint, TcpNetworkConnection>();



        // Queing for players, now enques all newly connected users
        Queue<PlayerData> _playerQueue = new Queue<PlayerData>();
        
        public void Awake()
        {
            //string path = Path.Combine(Application.persistentDataPath, "players.json");
            OSCLog.logging = true;

            // TODO: make the port auto adjustable
            int port = 5376;
            OSCLog.WriteLine("Starting server at " + port);
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();

            _dispatcher.ShowIncomingMessages = true;
            Initialize();
        }

        void AcceptNewConnection()
        {
            if (_listener != null && _listener.Pending())
            {
                TcpClient client = _listener.AcceptTcpClient();
                TcpNetworkConnection connection = new TcpNetworkConnection(client);
                _allConnections.Add(connection);
                _unloggedConnections.Add(connection);
                OSCLog.WriteLine("Server: Adding new connection from " + connection.Remote);
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
            OSCLog.WriteLine("Server: Message arrived to server: " + mess);

            _dispatcher.HandlePacket(packet, remote);
        }

        //void CleanConnections()
        //{
        //
        //}
        public void Update()
        {
            AcceptNewConnection();
            UpdateConnections();
        }


        public void FixedUpdate()
        {
            if (_playerQueue.Count > 1)
            {
                bool isSessionCreated = TryCreateSession();
                if (isSessionCreated) OSCLog.WriteLine($"Server: Session created succesefully");
            }
        }

        #region ConnectionRequests
        void Initialize()
        {
            // TODO: Make ALL OF THEM CHECK IF THE INPUT IS VALID FOR THEM
            _dispatcher.AddListener("/Login", ConnectionLoginRequest, OSCUtil.STRING, OSCUtil.STRING);
            _dispatcher.AddListener("/Register", ConnectionRegisterRequest, OSCUtil.STRING, OSCUtil.STRING);
            _dispatcher.AddListener("/PlaceShip", ConnectionPlaceShipRequest, OSCUtil.INT, OSCUtil.INT);
            _dispatcher.AddListener("/PlaceMine", ConnectionPlaceMineRequest, OSCUtil.INT, OSCUtil.INT);
            _dispatcher.AddListener("/Bomb", ConnectionBombRequest, OSCUtil.INT, OSCUtil.INT);
            _dispatcher.AddListener("/MarkReady", ConnectionMarkReadyRequest);
            _dispatcher.AddListener("/Enqueue", ConnectionEnqueueRequest);
        }
        void ConnectionLoginRequest(OSCMessageIn message, IPEndPoint remote)
        {
            if (message.ReadString() is not string username || message.ReadString() is not string password)
                return;

            OSCLog.WriteLine($"Server: Received Login attempt with username {username} and password {password}");
            bool sucess = TryUserLogin(username, password, out var result, out PlayerData? playerData);
            foreach (var conn in _unloggedConnections)
            {
                if (conn.Remote.Equals(remote))
                {
                    if (sucess && playerData != null)
                    {
                        _tcpNetPlayerKey.Add(conn, playerData);
                        _ipEndToTcpNetKey.Add(remote, conn);
                        _unloggedConnections.Remove(conn);
                    }
                    OSCMessageOut reply = new OSCMessageOut("/TryJoin").AddInt(result);
                    conn.Send(reply.GetBytes());
                    break;
                }
            }
        }

        void ConnectionRegisterRequest(OSCMessageIn message, IPEndPoint remote)
        {
            if (message.ReadString() is not string username || message.ReadString() is not string password)
                return;

            TcpNetworkConnection connection = _ipEndToTcpNetKey[remote];
            OSCLog.WriteLine($"Server: Received Register attempt with username {username} and password {password}");
            bool sucess = TryUserRegister(username, password, out var result, out PlayerData? playerData);
            OSCMessageOut reply = new OSCMessageOut("/Register").AddInt(result);
            connection.Send(reply.GetBytes());
        }

        void ConnectionPlaceShipRequest(OSCMessageIn message, IPEndPoint remote)
        {
            if (message.ReadInt() is not int x || message.ReadInt() is not int y)
                return;

            int[] coordinates = new int[2] { x, y };
            TcpNetworkConnection connection = _ipEndToTcpNetKey[remote];
            var player = _tcpNetPlayerKey[connection];
            string debug = PlaceShip(player.Username, coordinates, out int result);
            OSCLog.WriteLine(debug);
            OSCMessageOut reply = new OSCMessageOut("/PlaceShip").AddInt(result);
            connection.Send(reply.GetBytes());

        }

        void ConnectionPlaceMineRequest(OSCMessageIn message, IPEndPoint remote)
        {
            if (message.ReadInt() is not int x || message.ReadInt() is not int y)
                return;

            int[] coordinates = new int[2] { x, y };
            TcpNetworkConnection connection = _ipEndToTcpNetKey[remote];
            var player = _tcpNetPlayerKey[connection];
            string debug = PlaceMine(player.Username, coordinates, out int result);
            OSCLog.WriteLine(debug);
            OSCMessageOut reply = new OSCMessageOut("/PlaceMine").AddInt(result);
            connection.Send(reply.GetBytes());

        }

        void ConnectionBombRequest(OSCMessageIn message, IPEndPoint remote)
        {
            if (message.ReadInt() is not int x || message.ReadInt() is not int y)
                return;
            int[] coordinates = new int[2] { x, y };
            TcpNetworkConnection connection = _ipEndToTcpNetKey[remote];
            var player = _tcpNetPlayerKey[connection];
            string debug = Bomb(player.Username, coordinates, out int result);
            OSCLog.WriteLine(debug);
            OSCMessageOut reply = new OSCMessageOut("/Bomb").AddInt(result);
            connection.Send(reply.GetBytes());
        }

        void ConnectionMarkReadyRequest(OSCMessageIn message, IPEndPoint remote)
        {
            TcpNetworkConnection connection = _ipEndToTcpNetKey[remote];
            var player = _tcpNetPlayerKey[connection];
            string debug = MarkReady(player.Username, out int result);
            OSCLog.WriteLine(debug);
            OSCMessageOut reply = new OSCMessageOut("/MarkReady").AddInt(result);
            connection.Send(reply.GetBytes());
        }

        void ConnectionEnqueueRequest(OSCMessageIn message, IPEndPoint remote)
        {
            int result = -1;
            TcpNetworkConnection connection = _ipEndToTcpNetKey[remote];
            if (_tcpNetPlayerKey.TryGetValue(connection, out var player))
            {
                result = 0;
                _playerQueue.Enqueue(player);
                OSCLog.WriteLine($"Server: {player.Username} enqueued sucessefully." +
                    $" Queue consists of {_playerQueue.Count} players");
            }
            else
                result = 1;
            OSCMessageOut reply = new OSCMessageOut("/Enqueue").AddInt(result);
            connection.Send(reply.GetBytes());
        }
        #endregion

        #region UserLoging
        /// <summary>
        /// Attempt to connect the user
        /// TODO: Make it so the answer will be a number, that coresponds to a certain error. Dictionary of errors,
        /// so i can send a number, which will represent what went wrong
        /// </summary>
        /// <param name="result"> The output of what happend. 
        /// -1 = something went REALLY WRONG
        /// 0 = sucess
        /// 1 = user already connected
        /// 2 = wrong username
        /// 3 = wrong password
        /// </param>
        /// <returns></returns>
        bool TryUserLogin(string username, string password, out int result, out PlayerData? playerData)
        {
            result = -1;
            bool sucess = _login.LoginUser(username, password, out PlayerData? user);
            playerData = user;
            if (sucess && user != null)
            {
                if (!_userPlayerKey.ContainsKey(username))
                    _userPlayerKey.Add(username, user);
                else
                {
                    OSCLog.WriteLine($"Server: !!!! Trying to connect already connected user {username}");
                    result = 1;
                    return false;
                }
                _connectedPlayersMap.Add(user);
                _connectedPlayersList.Add(user);
                // Enqueue immediately
                //_playerQueue.Enqueue(user);
                OSCLog.WriteLine($"Server: {username} connected succesefully, added to queue");
                result = 0;
                return true;
            }
            else
            {
                if (user == null)
                    result = 2;
                else
                    result = 3;
                return false;
            }
        }

        bool TryUserRegister(string username, string password, out int result, out PlayerData? playerData)
        {
            result= -1;
            bool sucess = _login.RegisterUser(username, password, out playerData);

            if (sucess)
            {
                result = 0;
                return true;
            }

            return false;
        }
        public bool ConnectUser(string name, string password)
        { // TODO: maybe add instant data sending with the leaderboards?
            bool success = _login.LoginOrCreate(name, password, out PlayerData? user);
            if (user != null)
            {// TODO: add a check if the user is already in a session, if he is, put him in there
                if (!_userPlayerKey.ContainsKey(name))
                    _userPlayerKey.Add(name, user);
                else
                {
                    OSCLog.WriteLine($"Server: !!!! Trying to connect already connected user {name}");
                    return false;
                }
                _connectedPlayersMap.Add(user);
                _connectedPlayersList.Add(user);
                // Enqueue immediately
                _playerQueue.Enqueue(user);
                OSCLog.WriteLine($"Server: {name} connected succesefully, added to queue");
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
                    OSCLog.WriteLine($"Server: !!!! {player1.Username} already is in session, HOW are you creating another one?");
                if (_userSessionKey.ContainsKey(player2.Username))
                    OSCLog.WriteLine($"Server: !!!! {player2.Username} already is in session, HOW are you creating another one?");

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
        /// <summary>
        /// 
        /// </summary>
        /// <param name="username"></param>
        /// <param name="location"></param>
        /// <param name="result">
        /// -1 = unexpected
        /// 0 = everything is correct 
        /// 5 = player not in session
        /// </param>
        /// <returns></returns>
        public string PlaceShip(string username, int[] location, out int result)
        {
            if (!_userSessionKey.ContainsKey(username))
            {
                result = 5;
                return $"Server: {username} is not in a session";
            }
            var player = _userPlayerKey[username];
            var session = _userSessionKey[username];
            var outcome = _battleBehavior.PlaceShip(session, player, location);

            string message = $"Server: {username} " + outcome.ToString();

            result = (int)outcome;

            switch (outcome)
            {
                case PlaceShipResult.Success:
                    {
                        // TODO: success logic
                        result = 0;
                        //message = "SUCESS";
                        break;
                    }

                case PlaceShipResult.OutOfBounds:
                    {
                        result = 1;
                        // TODO: out of bounds logic
                        break;
                    }

                case PlaceShipResult.CellOccupied:
                    {
                        result = 2;
                        // TODO: cell occupied logic
                        break;
                    }

                case PlaceShipResult.ShipNearby:
                    {
                        result = 3;
                        // TODO: ship nearby logic
                        break;
                    }

                case PlaceShipResult.ShipLimitReached:
                    {
                        result = 4;
                        // TODO: ship limit reached logic
                        break;
                    }

                default:
                    {
                        result = -1;
                        // TODO: unexpected result logic
                        break;
                    }
            }

            return message;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="username"></param>
        /// <param name="location"></param>
        /// <param name="result">
        /// 0 = sucess
        /// 1 = out of bounds</param>
        /// <returns></returns>
        public string PlaceMine(string username, int[] location, out int result)
        {
            if (!_userSessionKey.ContainsKey(username))
            {
                result = 5;
                return $"Server: {username} is not in a session";
            }
            var player = _userPlayerKey[username];
            var session = _userSessionKey[username];
            var outcome = _battleBehavior.PlaceMine(session, player, location);

            string message = $"Server: {username} " + outcome.ToString();

            switch (outcome)
            {
                case PlaceMineResult.Success:
                    {
                        // TODO: success logic
                        result = 0;
                        //message = "SUCESS";
                        break;
                    }

                case PlaceMineResult.OutOfBounds:
                    {
                        result = 1;
                        // TODO: out of bounds logic
                        break;
                    }

                case PlaceMineResult.CellOccupied:
                    {
                        result = 2;
                        // TODO: cell occupied logic
                        break;
                    }

                case PlaceMineResult.MineLimitReached:
                    {
                        result = 3;
                        // TODO: mine limit reached logic
                        break;
                    }

                default:
                    {
                        result = -1;
                        // TODO: unexpected result logic
                        break;
                    }
            }

            return message;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="username"></param>
        /// <param name="location"></param>
        /// <param name="result">
        /// 0 = sucess
        /// 1 = out of bounds
        /// 2 = already bombed
        /// 3 = empty 
        /// 4 = mine 
        /// 5 = not in a session
        /// 6 = victory </param>
        /// <returns></returns>
        public string Bomb(string username, int[] location, out int result)
        {
            if (!_userSessionKey.ContainsKey(username))
            {
                result = 5;
                return $"Server: {username} is not in a session";
            }
            var player = _userPlayerKey[username];
            var session = _userSessionKey[username];
            var outcome = _battleBehavior.Bomb(session, player, location);

            string message = $"Server: {username} " + outcome.ToString();

            switch (outcome)
            {
                case BombingResult.Sucess:
                    {
                        // TODO: success logic
                        result = 0;
                        //message = "SUCESS";
                        break;
                    }

                case BombingResult.OutOfBounds:
                    {
                        result = 1;
                        // TODO: out of bounds logic
                        break;
                    }

                case BombingResult.AlreadyBombed:
                    {
                        result = 2;
                        // TODO: already bombed logic
                        break;
                    }

                case BombingResult.Empty:
                    {
                        result = 3;
                        // TODO: empty cell logic
                        break;
                    }

                case BombingResult.Mine:
                    {
                        result = 4;
                        // TODO: mine hit logic
                        break;
                    }

                case BombingResult.Victory:
                    {
                        result = 6;
                        // TODO: victory logic, broadcast to everybody, that the caller won
                        _login.SaveAccount(player);
                        message = "YOU WON";
                        break;
                    }

                default:
                    {
                        result = -1;
                        // TODO: unexpected result logic
                        break;
                    }
            }

            return message;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="username"></param>
        /// <param name="result">
        /// 0 = sucess
        /// 1 = battle started
        /// 2 = ship not placed
        /// 3 = mines not placed</param>
        /// <returns></returns>
        public string MarkReady(string username, out int result)
        {
            var player = _userPlayerKey[username];
            var session = _userSessionKey[username];
            var outcome = _battleBehavior.MarkReady(session, player);

            string message = $"Server: {username} " + outcome.ToString();

            switch (outcome)
            {
                case MarkingResult.Success:
                    {
                        result = 0;
                        // TODO: success logic
                        // tell the client to wait for the other player
                        message = $"Server: sucessefuly pressed Ready. Waiting for the other party...";
                        break;
                    }

                case MarkingResult.BattleStarted:
                    {
                        result = 1;
                        // TODO: battle started logic
                        // send info to both, that the game has started
                        // broadcast who's turn it is
                        break;
                    }

                case MarkingResult.ShipsNotPlaced:
                    {
                        result = 2;
                        // TODO: ships not placed logic
                        break;
                    }

                case MarkingResult.MinesNotPlaced:
                    {
                        result = 3;
                        // TODO: mines not placed logic
                        break;
                    }

                default:
                    {
                        result = -1;
                        // TODO: unexpected result logic
                        break;
                    }
            }

            return message;
        }
        #endregion
    }
}