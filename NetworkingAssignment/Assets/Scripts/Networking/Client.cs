using NetworkConnections;
using OSCTools;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI.Table;

namespace Network
{
    public class Client : MonoBehaviour
    { // TODO: Add error displaying dictionary
        public IPAddress ServerIP = IPAddress.Loopback;
        TcpNetworkConnection _connection;
        OSCDispatcher _dispatcher;

        // Answer if the login was sucesseful
        private TaskCompletionSource<bool> loginTcs;

        // ----- TicTacToe client things:

        // Views subscribe here, on any client:
        //public delegate void CellChangeEvent(int row, int col, int value);
        //public event CellChangeEvent OnCellChange;

        //public delegate void ActivePlayerChangeEvent(int activePlayer);
        //public event ActivePlayerChangeEvent OnActivePlayerChange;

        //public event System.Action<int> OnPlayerInfoReceived;

        //public delegate void GameOverEvent(int winner);
        //public event GameOverEvent OnGameOver;

        //void Start()
        //{
        //    TcpClient client = new TcpClient();
        //    client.Connect(new IPEndPoint(ServerIP, 50006));
        //    connection = new TcpNetworkConnection(client);
        //    // TODO: error handling

        //    Debug.Log("Starting client, connecting to " + ServerIP);

        //    // Initialize the dispatcher and callbacks for incoming OSC messages:
        //    dispatcher = new OSCDispatcher();
        //    dispatcher.ShowIncomingMessages = true;
        //    Initialize();
        //}
        void Awake()
        {
            Connect();
        }
        public bool Connect(int port = 5376)
        {// TODO: Add username loging. Also add Register.
            try
            {
                TcpClient client = new TcpClient();
                client.Connect(new IPEndPoint(ServerIP, port));
                _connection = new TcpNetworkConnection(client);
                Debug.Log("Client: Connecting with client to server " + ServerIP);

                _dispatcher = new OSCDispatcher();
                _dispatcher.ShowIncomingMessages = true;
                Initialize();
                return true;
            }
            catch (Exception exp)
            {
                Debug.LogException(exp);
                return false;
            }
        }

        public Task<bool> Login(string username, string password)
        {
            loginTcs = new TaskCompletionSource<bool>();
            //OSCMessageOut message = new OSCMessageOut("/MakeMove").AddInt(row).AddInt(col);
            OSCMessageOut message = new OSCMessageOut("/Login").AddString(username).AddString(password);
            _connection.Send(message.GetBytes());
            return loginTcs.Task;
        }

        public void Register(string username, string password)
        {

        }

        void Initialize()
        {
            // Subscribe to methods.
            _dispatcher.AddListener("/TryJoin", TryEnter, OSCUtil.INT);
        }

        /// <summary>
        /// Called from NetworkConnection callback (connection.Update), when a packet arrives:
        /// </summary>
        void HandlePacket(byte[] packet, IPEndPoint remote)
        {
            OSCMessageIn mess = new OSCMessageIn(packet);
            Debug.Log("Message arrives on client: " + mess);
            _dispatcher.HandlePacket(packet, remote);
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            // Check for incoming packets, and deal with them:
            while (_connection.Available() > 0)
            {
                HandlePacket(_connection.GetPacket(), _connection.Remote);
            }
            // TODO: disconnect handling
        }


        #region receivedMessages
        /// <summary>
        /// -1 = something went REALLY WRONG
        /// 0 = user already connected
        /// 1 = sucess
        /// 2 = wrong username
        /// 3 = wrong password
        /// </summary>
        /// <param name="message"></param>
        /// <param name="remote"></param>
        void TryEnter(OSCMessageIn message, IPEndPoint remote)
        {
            int result = message.ReadInt();
            bool success = result == 1;
            loginTcs?.TrySetResult(success);
            loginTcs = null;
            // For now no display of errors.
            // TODO: Add error message pop-up whenever something goes wrong
            switch (result)
            {
                case 0:
                    {
                        break;
                    }
                case 1:
                    {
                        // Ignore
                        break;
                    }
                case 2:
                    {

                        break;
                    }
                case 3:
                    {
                        break;
                    }
                default:
                    {
                        break;
                    }

            }
        }

        #endregion
    }
}