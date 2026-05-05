using Model;
using NetworkConnections;
using OSCTools;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using static Model.PlayerData;
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
        // Probably this is redundant (and therefore can cause bugs later?):
        Dictionary<string, PlayerData> _userPlayerKey = new Dictionary<string, PlayerData>();
        Dictionary<TcpNetworkConnection, PlayerData> _tcpNetPlayerKey = new Dictionary<TcpNetworkConnection, PlayerData>();
        Dictionary<string, TcpNetworkConnection> _userTcpKey = new Dictionary<string, TcpNetworkConnection>();
        Dictionary<IPEndPoint, TcpNetworkConnection> _ipEndToTcpNetKey = new Dictionary<IPEndPoint, TcpNetworkConnection>();



        // Queing for players, now enques all newly connected users
        Queue<PlayerData> _playerQueue = new Queue<PlayerData>();
        public static string GetLocalIPv4()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());

            var ip = host.AddressList
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork &&
                                     !IPAddress.IsLoopback(a));

            return ip?.ToString() ?? "No IPv4 found";
        }
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
            OSCLog.WriteLine(("Host IP: " + GetLocalIPv4()));
        }

        void AcceptNewConnection()
        {
            if (_listener != null && _listener.Pending())
            {
                TcpClient client = _listener.AcceptTcpClient();
                TcpNetworkConnection connection = new TcpNetworkConnection(client);
                IPEndPoint remoteEndPoint = (IPEndPoint)client.Client.RemoteEndPoint!;

                _allConnections.Add(connection);
                _unloggedConnections.Add(connection);

                // Keep the latest socket for this endpoint.
                _ipEndToTcpNetKey[remoteEndPoint] = connection;

                // TODO: store in _ipBlah dictionary (and use it)
                OSCLog.WriteLine("Server: Adding new connection from " + connection.Remote);
            }
        }
        void UpdateConnections()
        {
            // Snapshot so disconnects do not invalidate enumeration.
            foreach (TcpNetworkConnection conn in _allConnections.ToArray())
            {
                try
                {
                    if (conn.IsDisconnected)
                    {
                        DisconnectConnection(conn, "Remote closed the connection");
                        continue;
                    }

                    while (conn.Available() > 0)
                        HandlePacket(conn.GetPacket(), conn.Remote);
                }
                catch (System.Exception ex) when (
                    ex is SocketException ||
                    ex is IOException ||
                    ex is ObjectDisposedException)
                {
                    DisconnectConnection(conn, ex.Message);
                }
            }
        }
        #region Helpers

        private string HandleBattleException(string username, string action, Exception ex, out int result)
        {
            result = -1;

            if (ex is ArgumentNullException ||
                ex is ArgumentException ||
                ex is ArgumentOutOfRangeException ||
                ex is InvalidOperationException ||
                ex is NotImplementedException)
            {
                OSCLog.WriteLine($"Server: {action} failed for {username}: {ex.Message}");
                return ex.Message;
            }

            OSCLog.WriteLine($"Server: Unexpected {action} error for {username}: {ex}");
            return "Server error";
        }

        void HandlePacket(byte[] packet, IPEndPoint remote)
        {
            OSCMessageIn mess = new OSCMessageIn(packet);
            OSCLog.WriteLine("Server: Message arrived to server: " + mess);

            _dispatcher.HandlePacket(packet, remote);
        }
        bool TryGetConnection(IPEndPoint remote, out TcpNetworkConnection connection)
        {
            if (_ipEndToTcpNetKey.TryGetValue(remote, out connection))
                return true;

            OSCLog.WriteLine($"Server: No connection found for {remote}");
            return false;
        }

        bool TryGetPlayer(TcpNetworkConnection connection, out PlayerData player)
        {
            if (_tcpNetPlayerKey.TryGetValue(connection, out player))
                return true;

            OSCLog.WriteLine($"Server: Connection {connection.Remote} is not logged in");
            return false;
        }

        void RemoveQueuedPlayer(PlayerData player)
        {
            // Queue has no Remove, so rebuild it safely.
            if (_playerQueue.Count == 0)
                return;

            Queue<PlayerData> newQueue = new Queue<PlayerData>();

            while (_playerQueue.Count > 0)
            {
                PlayerData queuedPlayer = _playerQueue.Dequeue();

                if (queuedPlayer.Username != player.Username)
                    newQueue.Enqueue(queuedPlayer);
            }

            _playerQueue = newQueue;
        }

        void DisconnectConnection(TcpNetworkConnection connection, string reason)
        {
            // Cache the player first, because the dictionaries may be cleaned below.
            PlayerData? player = null;
            if (_tcpNetPlayerKey.TryGetValue(connection, out var foundPlayer))
                player = foundPlayer;

            _allConnections.Remove(connection);
            _unloggedConnections.Remove(connection);

            if (player != null)
            {
                _tcpNetPlayerKey.Remove(connection);
                _userTcpKey.Remove(player.Username);
                _connectedPlayersMap.Remove(player);
                _connectedPlayersList.RemoveAll(p => p.Username == player.Username);

                if (player.SessionState == PlayerSessionState.InQueue)
                {
                    // Queued users are removed completely.
                    RemoveQueuedPlayer(player);
                    player.SetSessionState(PlayerSessionState.InMenu); // fully disconnected
                }
                else if (player.SessionState != PlayerSessionState.InGame)
                {
                    player.SetSessionState(PlayerSessionState.InMenu); // disconnected outside battle, back to menu state
                }

                // Keep the session mapping only for in-battle reconnects.
                // This deliberately does not remove _userSessionKey.

                OSCLog.WriteLine($"Server: Disconnected {player.Username}. Reason: {reason}");
            }
            else
            {
                OSCLog.WriteLine($"Server: Disconnected unlogged connection {connection.Remote}. Reason: {reason}");
            }

            // Remove any stale endpoint mapping that still points to this connection.
            if (_ipEndToTcpNetKey.TryGetValue(connection.Remote, out var mappedConnection) &&
                ReferenceEquals(mappedConnection, connection))
            {
                _ipEndToTcpNetKey.Remove(connection.Remote);
            }

            // TODO: If TcpNetworkConnection exposes a close/dispose method, call it here.
        }

        void SendRejoinPayload(TcpNetworkConnection connection, PlayerData player, SessionData session)
        {
            // Intentionally empty for now.
            // TODO: Fill this with reconnect/rejoin packet payload later.
        }

        #endregion
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
            _dispatcher.AddListener("/PlaceShip", ConnectionPlaceShipRequest, OSCUtil.INT, OSCUtil.INT, OSCUtil.INT, OSCUtil.INT, OSCUtil.BOOL);
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

            if (!TryGetConnection(remote, out TcpNetworkConnection connection))
                return;
            // TODO: maybe make it store hte connection as well?
            bool sucess = TryUserLogin(username, password, connection, out var result, out PlayerData? playerData);

            OSCMessageOut reply = new OSCMessageOut("/TryJoin").AddInt(result);
            connection.Send(reply.GetBytes());
        }

        void ConnectionRegisterRequest(OSCMessageIn message, IPEndPoint remote)
        {
            if (message.ReadString() is not string username || message.ReadString() is not string password)
                return;

            if (!TryGetConnection(remote, out TcpNetworkConnection connection))
                return;

            OSCLog.WriteLine($"Server: Received Register attempt with username {username} and password {password}");
            bool sucess = TryUserRegister(username, password, out var result, out PlayerData? playerData);
            OSCMessageOut reply = new OSCMessageOut("/Register").AddInt(result);
            connection.Send(reply.GetBytes());
        }

        void ConnectionPlaceShipRequest(OSCMessageIn message, IPEndPoint remote)
        {
            if (message.ReadInt() is not int id
                || message.ReadInt() is not int x
                || message.ReadInt() is not int y
                || message.ReadInt() is not int len)
                return;

            bool rot = message.ReadBool();

            if (!TryGetConnection(remote, out TcpNetworkConnection connection) ||
                !TryGetPlayer(connection, out PlayerData player))
                return;

            Vector2 coordinates = new Vector2(x, y);
            Ship ship = new Ship(coordinates, len, rot, id);

            int result;
            string debug;

            try
            {
                debug = PlaceShip(player.Username, ship, out result);
            }
            catch (Exception ex) when (
                ex is ArgumentNullException ||
                ex is ArgumentException ||
                ex is ArgumentOutOfRangeException ||
                ex is InvalidOperationException ||
                ex is NotImplementedException)
            {
                debug = HandleBattleException(player.Username, "PlaceShip", ex, out result);
            }

            OSCLog.WriteLine(debug);
            OSCMessageOut reply = new OSCMessageOut("/PlaceShip").AddInt(result);
            connection.Send(reply.GetBytes());
        }

        void ConnectionPlaceMineRequest(OSCMessageIn message, IPEndPoint remote)
        {
            if (message.ReadInt() is not int x || message.ReadInt() is not int y)
                return;

            if (!TryGetConnection(remote, out TcpNetworkConnection connection) ||
                !TryGetPlayer(connection, out PlayerData player))
                return;

            int[] coordinates = new int[2] { x, y };

            int result;
            string debug;

            try
            {
                debug = PlaceMine(player.Username, coordinates, out result);
            }
            catch (Exception ex) when (
                ex is ArgumentNullException ||
                ex is ArgumentException ||
                ex is ArgumentOutOfRangeException ||
                ex is InvalidOperationException ||
                ex is NotImplementedException)
            {
                debug = HandleBattleException(player.Username, "PlaceMine", ex, out result);
            }

            OSCLog.WriteLine(debug);
            OSCMessageOut reply = new OSCMessageOut("/PlaceMine").AddInt(result);
            connection.Send(reply.GetBytes());
        }

        void ConnectionBombRequest(OSCMessageIn message, IPEndPoint remote)
        {
            if (message.ReadInt() is not int x || message.ReadInt() is not int y)
                return;

            if (!TryGetConnection(remote, out TcpNetworkConnection connection) ||
                !TryGetPlayer(connection, out PlayerData player))
                return;

            int[] coordinates = new int[2] { x, y };

            if (!_userSessionKey.TryGetValue(player.Username, out var session))
                return;

            if (!_userTcpKey.TryGetValue(player.Username, out var activeConnection) ||
                !ReferenceEquals(activeConnection, connection))
                return;

            if (!session.TryGetEnemyParticipant(player, out var participant))
            {
                OSCLog.WriteLine($"Server: No enemy participant found for {player.Username}");
                return;
            }

            var enemyPlayer = participant?.Player;
            if (enemyPlayer == null)
            {
                OSCLog.WriteLine($"Server: {participant} does not have PlayerData.");
                return;
            }

            int result;
            string debug;

            try
            {
                debug = Bomb(player.Username, coordinates, out result);
            }
            catch (Exception ex) when (
                ex is ArgumentNullException ||
                ex is ArgumentException ||
                ex is ArgumentOutOfRangeException ||
                ex is InvalidOperationException ||
                ex is NotImplementedException)
            {
                debug = HandleBattleException(player.Username, "Bomb", ex, out result);
            }

            OSCLog.WriteLine(debug);

            OSCMessageOut reply = new OSCMessageOut("/Bomb")
                .AddInt(result)
                .AddInt(coordinates[0])
                .AddInt(coordinates[1])
                .AddBool(true);

            connection.Send(reply.GetBytes());

            if (result == 0 || result == 3 || result == 4)
            {
                if (_userTcpKey.TryGetValue(enemyPlayer.Username, out var enemyConnection))
                {
                    OSCMessageOut enemyInform = new OSCMessageOut("/Bomb")
                        .AddInt(result)
                        .AddInt(coordinates[0])
                        .AddInt(coordinates[1])
                        .AddBool(false);

                    enemyConnection.Send(enemyInform.GetBytes());
                }
            }
            else if (result == 6)
            {
                OSCMessageOut enemyPlayerMessage = new OSCMessageOut("/Victory").AddBool(false);
                OSCMessageOut playerMessage = new OSCMessageOut("/Victory").AddBool(true);

                connection.Send(playerMessage.GetBytes());

                if (_userTcpKey.TryGetValue(enemyPlayer.Username, out var enemyConnection))
                    enemyConnection.Send(enemyPlayerMessage.GetBytes());
            }
        }

        void ConnectionMarkReadyRequest(OSCMessageIn message, IPEndPoint remote)
        {
            if (!TryGetConnection(remote, out TcpNetworkConnection connection) ||
                !TryGetPlayer(connection, out PlayerData player))
                return;

            int result;
            string debug;

            try
            {
                debug = MarkReady(player.Username, out result);
            }
            catch (Exception ex) when (
                ex is KeyNotFoundException ||
                ex is ArgumentNullException ||
                ex is ArgumentException ||
                ex is ArgumentOutOfRangeException ||
                ex is InvalidOperationException ||
                ex is NotImplementedException)
            {
                debug = HandleBattleException(player.Username, "MarkReady", ex, out result);
            }

            OSCLog.WriteLine(debug);
            OSCMessageOut reply = new OSCMessageOut("/MarkReady").AddInt(result);
            connection.Send(reply.GetBytes());

            if (result == 1)
            {
                if (_userSessionKey.TryGetValue(player.Username, out var session) &&
                    session.TryGetEnemyParticipant(player, out var enemyParticipant))
                {
                    var enemyPlayer = enemyParticipant.Player;
                    if (_userTcpKey.TryGetValue(enemyPlayer.Username, out var enemyConnection))
                        enemyConnection.Send(reply.GetBytes());
                }
            }
        }
        // We receive name, the oponent's victories coumt, ship preset (or game preset)
        // (2, 3, 4 etc. ships on the board allowed. Each number has a prefab of ships included),
        // mines to place
        // board size
        void ConnectionEnqueueRequest(OSCMessageIn message, IPEndPoint remote)
        {// TODO: check if already in a session or inapropriate state
         // OR (PB): TODO: Use Rooms for a clean separation :-)   (See BC3, slide 43)
            int result = -1;

            if (!TryGetConnection(remote, out TcpNetworkConnection connection))
                return;

            if (_tcpNetPlayerKey.TryGetValue(connection, out var player))
            {
                result = 0;
                player.SetSessionState(PlayerSessionState.InQueue);
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
        bool TryUserLogin(string username, string password, TcpNetworkConnection connection, out int result, out PlayerData? playerData)
        {
            result = -1;

            bool sucess = _login.LoginUser(username, password, out PlayerData? user);
            playerData = user;

            if (sucess && user != null)
            {
                if (_userTcpKey.TryGetValue(username, out var existingConnection) &&
                    !ReferenceEquals(existingConnection, connection))
                {
                    // Replace the previous socket for this user.
                    DisconnectConnection(existingConnection, "Replaced by a new login");
                }

                _userPlayerKey[username] = user;

                _connectedPlayersMap.Add(user);
                if (!_connectedPlayersList.Exists(p => p.Username == user.Username))
                    _connectedPlayersList.Add(user);

                _tcpNetPlayerKey[connection] = user;
                _userTcpKey[username] = connection;
                _unloggedConnections.Remove(connection);

                if (_userSessionKey.ContainsKey(username))
                    user.SetSessionState(PlayerSessionState.InGame);
                else
                    user.SetSessionState(PlayerSessionState.InMenu);

                OSCLog.WriteLine($"Server: {username} connected succesefully");
                result = 0;

                // Existing session means this is a reconnect/rejoin after disconnect or kick.
                if (user.SessionState == PlayerSessionState.InGame && _userSessionKey.TryGetValue(username, out var session))
                    SendRejoinPayload(connection, user, session);

                return true;
            }
            else
            {
                if (user == null)
                {
                    result = 2;
                    OSCLog.WriteLine($"Server: {username} user not found");
                }
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
        //public bool ConnectUser(string name, string password)
        //{ // TODO: maybe add instant data sending with the leaderboards?
        //    bool success = _login.LoginOrCreate(name, password, out PlayerData? user);
        //    if (user != null)
        //    {
        //        if (!_userPlayerKey.ContainsKey(name))
        //            _userPlayerKey.Add(name, user);
        //        else
        //        {
        //            OSCLog.WriteLine($"Server: !!!! Trying to connect already connected user {name}");
        //            return false;
        //        }
        //        _connectedPlayersMap.Add(user);
        //        _connectedPlayersList.Add(user);
        //        // Enqueue immediately
        //        _playerQueue.Enqueue(user);
        //        OSCLog.WriteLine($"Server: {name} connected succesefully, added to queue");
        //        return true;
        //    }
        //    else
        //        return false;
        //}

        bool TryCreateSession()
        {
            PlayerData player1 = _playerQueue.Dequeue(); // First player
            PlayerData player2 = _playerQueue.Dequeue(); // Second player

            // Check both if the connection is still valid. (If closed from unityEditor
            // the disconnect is never detected? Only when run is pressed again???)
            if (!_userTcpKey.TryGetValue(player1.Username, out var player1Connection) ||
                player1Connection == null ||
                player1Connection.Status != ConnectionStatus.Connected ||
                player1Connection.IsDisconnected)
            {
                OSCLog.WriteLine($"Server: {player1.Username} is no longer connected, skipping session creation.");
                player1.SetSessionState(PlayerSessionState.InMenu);
                return false;
            }

            if (!_userTcpKey.TryGetValue(player2.Username, out var player2Connection) ||
                player2Connection == null ||
                player2Connection.Status != ConnectionStatus.Connected ||
                player2Connection.IsDisconnected)
            {
                OSCLog.WriteLine($"Server: {player2.Username} is no longer connected, skipping session creation.");
                player2.SetSessionState(PlayerSessionState.InMenu);

                // Put player1 back, since they were valid.
                _playerQueue.Enqueue(player1);
                player1.SetSessionState(PlayerSessionState.InQueue);
                return false;
            }

            // Run some checks... (which we won't do for now)
            SessionData newSession = new SessionData(player1, player2, 3, 0);
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
            player1.SetSessionState(PlayerSessionState.InGame);
            player2.SetSessionState(PlayerSessionState.InGame);

            // This is for starting the battle. Broadcast
            OSCMessageOut player1Info =
                new OSCMessageOut("/StartBattle").AddString(player2.Username).AddInt(player2.TopScore)
                .AddInt(newSession.MaxShips).AddInt(newSession.MaxMines).AddInt(newSession.FirstMap.Length).AddBool(true); // bool if first turn
            OSCMessageOut player2Info =
                new OSCMessageOut("/StartBattle").AddString(player1.Username).AddInt(player1.TopScore)
                .AddInt(newSession.MaxShips).AddInt(newSession.MaxMines).AddInt(newSession.FirstMap.Length).AddBool(false);

            player1Connection.Send(player1Info.GetBytes());
            player2Connection.Send(player2Info.GetBytes());
            return true;
        }

        #endregion
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
        public string PlaceShip(string username, Ship ship, out int result)
        {
            if (!_userSessionKey.ContainsKey(username))
            {
                result = 5;
                return $"Server: {username} is not in a session";
            }
            var player = _userPlayerKey[username];
            var session = _userSessionKey[username];
            var outcome = _battleBehavior.PlaceShip(session, player, ship);

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
            result = -1;
            if (!_userSessionKey.ContainsKey(username))
            {
                result = 5;
                return $"Server: {username} is not in a session";
            }
            var player = _userPlayerKey[username];
            var session = _userSessionKey[username];
            BombingResult outcome;
            try
            {
                outcome = _battleBehavior.Bomb(session, player, location);
            }
            catch (InvalidOperationException error)
            {
                result = -1;
                return error.Message;
                //TODO: Disconnect this malicious client
                
            }

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
                        // TODO: victory logic, clear the room ,move the player, etc.
                        if (!session.TryGetEnemyParticipant(player, out var participant))
                            return message;
                        result = 6;
                        var enemyPlayer = participant.Player;
                        _login.SaveAccount(player);
                        _userSessionKey.Remove(player.Username);
                        _userSessionKey.Remove(enemyPlayer.Username);
                        _sessionDatas.Remove(session);
                        player.SetSessionState(PlayerSessionState.InMenu);
                        enemyPlayer.SetSessionState(PlayerSessionState.InMenu);
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
                case MarkingResult.AlreadyMarked:
                    {
                        result = 4;
                        // Already marked
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