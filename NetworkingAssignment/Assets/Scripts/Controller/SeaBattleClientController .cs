using Model;
using Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Controller
{
    [DefaultExecutionOrder(-200)]
    internal sealed class SeaBattleClientController : MonoBehaviour
    {
        //[SerializeField] private Server _server;

        [SerializeField] private TMP_InputField IpInput;
        [SerializeField] private TMP_InputField _username;
        [SerializeField] private TMP_InputField _password;

        public UnityEvent OnJoiningEvent;
        public UnityEvent OnRegisteringEvent;
        public UnityEvent OnEnqueuedEvent;
        
        public string Username => _username.text;

        Client _networkClient; //TODO: use the Instance >client.Instance
        public Client NetworkClient => _networkClient;
        GridPlacement _gridPlacement;

        private void Awake()
        {
            //if (_server == null)
            //    _server = FindFirstObjectByType<Server>();
            //
            //if (_server == null)
            //    Debug.LogError("SeaBattleClientController: Server not found.");
            if (_networkClient == null)
                _networkClient = FindFirstObjectByType<Client>();
            if (_networkClient == null)
                Debug.LogError("SeaBattleClientController: _networkClient not found.");

            _gridPlacement = FindFirstObjectByType<GridPlacement>();

        }

        private void Update()
        {
            _gridPlacement?.UpdateGrids();
        }

        #region ButtonMethods
        public void ConnectBtn()
        {
            var text = IpInput.text.Trim();

            if (!IPAddress.TryParse(text, out var ip))
            {
                GlobalMessageUI.Instance.Show($"Invalid IP address: '{text}'");
                return;
            }
            //GlobalMessageUI.Instance.Show("Client: Connecting with client to server " + ip);
            bool connected = _networkClient.Connect(ip);
            GlobalMessageUI.Instance.Show(connected ? "Connect succeeded" : "Connect failed");
        }

        bool IsJoiningRunning = false;
        public void BtnJoin()
        {
            if (IsJoiningRunning) return;
            Login();
        }

        bool IsRegisteringRunning = false;
        public void BtnRegister()
        {
            if (IsRegisteringRunning) return;
            Register();
        }

        bool IsPlaceShipRunning = false;

        bool IsPlaceMineRunning = false;

        bool IsBombRunning = false;

        bool IsMarkReadyRunning = false;
        public void BtnMarkReady()
        {
            if (IsMarkReadyRunning) return;
            MarkReady();
        }
        bool IsEnqueueRunning = false;
        public void BtnEnqueue(int queueId)
        {
            if (IsEnqueueRunning) return;
            Enqueue(queueId);
        }
        #endregion

        #region CalledButtonMethods

        private async void Login()
        {
            IsJoiningRunning = true;
            try
            {
                int connected = await _networkClient.Login(_username.text, _password.text);

                Debug.Log($"SeaBattleClientController: Loging to the server was {connected == 0} with username {_username.text} and password {_password.text}");

                if (connected == 0)
                    OnJoiningEvent?.Invoke();
            }
            catch (TimeoutException ex)
            {
                Debug.LogWarning($"SeaBattleClientController: Login timeout. {ex.Message}");
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"SeaBattleClientController: Login connection error. {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                IsJoiningRunning = false;
            }
        }
        private async void Register()
        {
            IsRegisteringRunning = true;
            try
            {
                int connected = await _networkClient.Register(_username.text, _password.text);

                Debug.Log($"SeaBattleClientController: Registering to the server was {connected == 0} with username {_username.text} and password {_password.text}");

                if (connected == 0)
                    OnRegisteringEvent?.Invoke();
            }
            catch (TimeoutException ex)
            {
                Debug.LogWarning($"SeaBattleClientController: Register timeout. {ex.Message}");
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"SeaBattleClientController: Register connection error. {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                IsRegisteringRunning = false;
            }
        }
        public async Task<bool> PlaceShip(Ship ship, int x, int y)
        {
            if (IsPlaceShipRunning) return false;
            IsPlaceShipRunning = true;

            try
            {
                int result = await _networkClient.PlaceShip(x, y, ship);
                return result == 0;
            }
            catch (TimeoutException ex)
            {
                Debug.LogWarning($"SeaBattleClientController: PlaceShip timeout. {ex.Message}");
                return false;
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"SeaBattleClientController: PlaceShip connection error. {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
            finally
            {
                IsPlaceShipRunning = false;
            }
        }
        public async Task<bool> PlaceMine(Mine mine, int x, int y)
        {
            IsPlaceMineRunning = true;
            try
            {
                int result = await _networkClient.PlaceMine(x, y, mine);
                Debug.Log($"SeaBattleClientController: PlaceMine result = {result}");
                return result == 0;
            }
            catch (TimeoutException ex)
            {
                Debug.LogWarning($"SeaBattleClientController: PlaceMine timeout. {ex.Message}");
                return false;
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"SeaBattleClientController: PlaceMine connection error. {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
            finally
            {
                IsPlaceMineRunning = false;
            }
        }
        public async Task<bool> Bomb(int x, int y)
        {
            IsBombRunning = true;
            try
            {
                int result = await _networkClient.Bomb(x, y);
                bool sucess = result == 0 || result == 6 || result == 3 || result == 4;
                return sucess;
            }
            catch (TimeoutException ex)
            {
                Debug.LogWarning($"SeaBattleClientController: Bomb timeout. {ex.Message}");
                return false;
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"SeaBattleClientController: Bomb connection error. {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
            finally
            {
                IsBombRunning = false;
            }
        }
        private async void MarkReady()
        {
            IsMarkReadyRunning = true;
            try
            {
                int result = await _networkClient.MarkReady();
                Debug.Log($"SeaBattleClientController: MarkReady result = {result}");
            }
            catch (TimeoutException ex)
            {
                Debug.LogWarning($"SeaBattleClientController: MarkReady timeout. {ex.Message}");
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"SeaBattleClientController: MarkReady connection error. {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                IsMarkReadyRunning = false;
            }
        }
        public async void Enqueue(int queueId)
        {
            IsEnqueueRunning = true;
            try
            {
                int result = await _networkClient.Enqueue(queueId);
                if (result == 0)
                    OnEnqueuedEvent?.Invoke();
            }
            catch (TimeoutException ex)
            {
                Debug.LogWarning($"SeaBattleClientController: Enqueue timeout. {ex.Message}");
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"SeaBattleClientController: Enqueue connection error. {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                IsEnqueueRunning = false;
            }
        }
        #endregion


        #region Helpers

        #endregion

    }
}