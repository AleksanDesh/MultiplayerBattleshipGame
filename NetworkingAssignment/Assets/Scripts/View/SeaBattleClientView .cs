using Controller;
using Model;
using NUnit.Framework;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Network.Client;

namespace View
{
    /// <summary>
    /// Temporal semi-controller.
    /// </summary>
   
    internal sealed class SeaBattleClientView : MonoBehaviour
    {
        [SerializeField] private SeaBattleClientController _controller;



        [Header("Output")]
        [SerializeField] private TMP_Text _resultText;
        [SerializeField] private TMP_Text EnemyUsernameText;
        [SerializeField] private TMP_Text EnemyVictoriesText;

        [Tooltip("When server says that the battle starts (building phase)")]
        [SerializeField] public UnityEvent OnJoiningBattleEvent;
        [Tooltip("When server confirms enqueuing for battle")]
        [SerializeField] public UnityEvent OnEnqueueingForBattle;


        [Tooltip("When server says that the battle starts")]
        public UnityEvent OnBattleStarted;
        [Tooltip("When server confirms pressing ready in the battle")]
        public UnityEvent OnBattleReady;

        public UnityEvent OnVictoryEvent;
        public UnityEvent OnLoseEvent;

        public UnityEvent OnLoginEvent;
        public UnityEvent OnRegisterEvent;

        public GridManager UserGrid;
        public GridManager EnemyGrid;

        [SerializeField] public List<GameObject> ShipPresets = new List<GameObject>();
        [SerializeField] public Transform ShipSpawnLocation;
        [SerializeField] public GameObject XPrefab;
        [SerializeField] public GameObject BombPrefab;
        Dictionary<Tile, GameObject> _destroyedTiles = new Dictionary<Tile, GameObject>();
        Dictionary<Tile, GameObject> _bombedTiles = new Dictionary<Tile, GameObject>();


        bool firstTurn = false;
        private void Awake()
        {
            if (_controller == null)
                _controller = FindFirstObjectByType<SeaBattleClientController>();

            if (_controller == null)
                Debug.LogError("SeaBattleClientView: Controller not found.");
        }

        // Maybe add in the future
        public void OnUsernameChanged()
        {
            if (_controller == null)
                return;

        }

        private void OnEnable()
        {
            var client = _controller?.NetworkClient;
            if (client == null)
                return;

            client.OnMarkReady += MarkReadyEvent;
            client.OnEnqueue += EnqueueEvent;
            client.OnBattleStarted += StartBattle;
            client.OnBombing += BombResult;
            client.OnVictory += VictoryResult;
            client.OnLogin += Login;
            client.OnRegister += Register;
            client.OnTimeout += TimeoutViewChange;
        }

        private void OnDisable()
        {
            var client = _controller?.NetworkClient;
            if (client == null)
                return;

            client.OnMarkReady -= MarkReadyEvent;
            client.OnEnqueue -= EnqueueEvent;
            client.OnBattleStarted -= StartBattle;
            client.OnBombing -= BombResult;
            client.OnVictory -= VictoryResult;
            client.OnLogin -= Login;
            client.OnRegister -= Register;
            client.OnTimeout -= TimeoutViewChange;
        }

        /// -1 = something went REALLY WRONG
        /// 0 = sucess
        /// 1 = user already connected
        /// 2 = wrong username
        /// 3 = wrong password
        void Login(int result)
        {
            switch (result)
            {
                case 0:
                    {
                        _resultText.text = "Logged in sucessefully!";
                        break;
                    }
                case 1:
                    {
                        _resultText.text = "User already connected";
                        break;
                    }
                case 2:
                    {
                        _resultText.text = "Wrong username";
                        break;
                    }
                case 3:
                    {
                        _resultText.text = "Wrong password";
                        break;
                    }
                case -1:
                    {
                        _resultText.text = "Somwthing went wrong";
                        break;
                    }
                default:
                    {
                        _resultText.text = "Unknown error";
                        break;
                    }

            }
        }

        /// result values:
        ///  0  = registration succeeded.
        ///  1  = username is empty or whitespace.
        ///  2  = password is empty or whitespace.
        ///  3  = username already exists.
        /// -1  = something unexpected happened.
        void Register(int result)
        {
            switch (result)
            {
                case 0:
                    {
                        _resultText.text = "Registration succeeded. Please login with the same credentials";
                        break;
                    }
                case 1:
                    {
                        _resultText.text = "Username is empty or whitespace";
                        break;
                    }
                case 2:
                    {
                        _resultText.text = "Password is empty or whitespace";
                        break;
                    }
                case 3:
                    {
                        _resultText.text = "Username already exists";
                        break;
                    }
                case -1:
                    {
                        _resultText.text = "Somwthing went wrong";
                        break;
                    }
                default:
                    {
                        _resultText.text = "Unknown error";
                        break;
                    }

            }
        }

        void MarkReadyEvent(int result)
        {
            switch (result)
            {
                case 0:
                    {
                        OnBattleReady?.Invoke();
                        _resultText.text = $"Waiting for the other party...";
                        break;
                    }
                case 1:
                    {
                        OnBattleReady?.Invoke();
                        OnBattleStarted?.Invoke();
                        if (firstTurn)
                            _resultText.text = $"Your turn, bomb";
                        else
                            _resultText.text = $"Enemy turn, wait";
                        break;
                    }
                case 2:
                    {
                        _resultText.text = $"Place all ships before pressing Ready";
                        // shipas not placed
                        break;
                    }
                case 3:
                    {
                        _resultText.text = $"Place all mines before pressing Ready";
                        // mines not placed
                        break;
                    }
                case 4:
                    {
                        _resultText.text = $"Already pressed. How did you press it again?";
                        break;
                    }
                default:
                    {
                        // unexpected
                        break;
                    }
            }
        }

        void EnqueueEvent(int result)
        {
            switch (result)
            {
                case 0:
                    {
                        OnEnqueueingForBattle?.Invoke();
                        _resultText.text = $"Enqueued sucessefully!";
                        break;
                    }
                case 1:
                    {
                        _resultText.text = $"Enqueueing failed. Player not found";
                        break;
                    }
                default:
                    {
                        // unexpected
                        _resultText.text = $"Enqueueing failed. Unknown";
                        break;
                    }
            }
        }

        void StartBattle(BattleStartPckg package)
        {
            OnJoiningBattleEvent?.Invoke();
            Debug.Log($"Current scene {SceneManager.GetActiveScene().name}. Must be in session scene, aka 1");
            UserGrid.StartBattle(package.BoardSize, package.BoardSize);
            var ships = Instantiate(ShipPresets[package.ShipPreset - 1], UserGrid.transform); // spawn the preset at the view location
            ships.transform.position = ShipSpawnLocation.position;
            EnemyUsernameText.text = $"Oponent: {package.EnemyUsername}";
            EnemyVictoriesText.text = $"Victories: {package.EnemyVictories}";
            EnemyGrid._width = package.BoardSize;
            EnemyGrid._height = package.BoardSize;
            firstTurn = package.Turn;
        }

        /// 0 = sucess
        /// 1 = out of bounds
        /// 2 = already bombed
        /// 3 = empty 
        /// 4 = mine 
        /// 5 = not in a session
        /// 6 = victory
        void BombResult(Bombpckg package)
        {
            if (package == null)
            {
                Debug.LogWarning("BombResult called with null package.");
                return;
            }

            UpdateTurnVisually(package);

            ApplyBombHit(package.location, package.result, package.IsForEnemy);

            if (package.ExtraHits != null)
            {
                foreach (var hit in package.ExtraHits)
                {
                    ApplyBombHit(new Vector2Int(hit.X, hit.Y), hit.Result, package.IsForEnemy);
                }
            }
        }

        private void ApplyBombHit(Vector2Int location, int result, bool isForEnemy)
        {
            var grid = isForEnemy ? EnemyGrid : UserGrid;
            var offset = isForEnemy ? new Vector3(0, 0, 0) : new Vector3(0, 1, 0);

            if (!grid.TryGetTile(location, out var tile))
            {
                Debug.LogWarning($"BombResult: tile not found at {location.x},{location.y}");
                return;
            }

            switch (result)
            {
                case 0:
                case 6:
                    {
                        tile.CurrentState = Tile.State.Destroyed;

                        if (!_destroyedTiles.ContainsKey(tile))
                        {
                            var newPrf = Instantiate(XPrefab, tile.transform);
                            newPrf.transform.position += offset;
                            _destroyedTiles.Add(tile, newPrf);
                        }
                        else
                        {
                            Debug.LogWarning($"Tile was present in destroyed dictionary with tile {tile.name}");
                        }

                        break;
                    }

                case 3:
                    {
                        tile.CurrentState = Tile.State.Bombed;

                        if (!_bombedTiles.ContainsKey(tile))
                        {
                            var newPrf = Instantiate(BombPrefab, tile.transform);
                            _bombedTiles.Add(tile, newPrf);
                        }
                        else
                        {
                            Debug.LogWarning($"Tile was present in bombed dictionary with tile {tile.name}");
                        }

                        break;
                    }
                case 4:
                    {// TODO: This is the mine. Make explosion VFX?
                        break;
                    }

                default:
                    {
                        Debug.LogWarning($"BombResult: unexpected bomb result {result} at {location.x},{location.y}");
                        break;
                    }
            }
        }

        //void BombResult(Bombpckg package)
        //{
        //    var grid = package.IsForEnemy ? EnemyGrid : UserGrid;
        //    var offset = package.IsForEnemy ? new Vector3(0, 0, 0) : new Vector3(0, 1, 0);

        //    if (!grid.TryGetTile(package.location, out var tile))
        //        return;

        //    UpdateTurnVisually(package);

        //    switch (package.result)
        //    {
        //        case 0:
        //        case 6:
        //            {
        //                // Use the apropriate grid, and spawn X there (add blowing VFX?)
        //                tile.CurrentState = Tile.State.Destroyed;
        //                var newPrf = Instantiate(XPrefab, tile.transform);
        //                newPrf.transform.position += offset;
        //                if (!_destroyedTiles.ContainsKey(tile))
        //                    _destroyedTiles.Add(tile, newPrf);
        //                else
        //                {
        //                    Debug.LogWarning($"Tile was present in destroyed dictionary with tile {tile.name}");    
        //                }
        //                break;
        //            }

        //        case 3:
        //            {
        //                // Use the apropriate grid and spawn bomb
        //                tile.CurrentState = Tile.State.Bombed;
        //                var newPrf = Instantiate(BombPrefab, tile.transform);
        //                _bombedTiles.Add(tile, newPrf);
        //                break;
        //            }
        //    }
        //}

        void UpdateTurnVisually(Bombpckg pckg)
        {// if sucessefully bombed enemy tile or mine, make one more turn
            if ((pckg.result == 0 || pckg.result == 4) && pckg.IsForEnemy)
                _resultText.text = $"Your turn, bomb";
            // if was not my turn and it failed (thus none of the above at the same time) => my turn (ignore result 6)
            else if ((pckg.result != 0 && pckg.result != 4) && !pckg.IsForEnemy)
                _resultText.text = $"Your turn, bomb";
            else
                _resultText.text = $"Enemy turn, wait";

        }

        void VictoryResult(bool isWinner)
        {// TODO: if true, we won, if not -> enemy
            var globalMessager = GlobalMessageUI.Instance;


            if (isWinner)
            {
                OnVictoryEvent?.Invoke();
                globalMessager.Show("You WON");
            }
            else
            {
                OnLoseEvent?.Invoke();
                globalMessager.Show("You LOOOOOST");
            }

        }

        void TimeoutViewChange(TimeoutInfo info)
        {
            var globalMessager = GlobalMessageUI.Instance;
            globalMessager.Show("Timed out with message: " + info.Message);
            SceneManager.LoadScene(0); // Load loging screen
        }

        private void ShowResult(string message)
        {
            if (_resultText != null)
                _resultText.text = message;

            Debug.Log(message);
        }
    }
}