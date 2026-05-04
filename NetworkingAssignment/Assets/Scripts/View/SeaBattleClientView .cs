using Controller;
using Model;
using NUnit.Framework;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
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
        [SerializeField] public UnityEvent JoiningBattleEvent;
        [Tooltip("When server confirms enqueuing for battle")]
        [SerializeField] public UnityEvent EnqueueingForBattle;


        [Tooltip("When server says that the battle starts")]
        public UnityEvent BattleStarted;
        [Tooltip("When server confirms pressing ready in the battle")]
        public UnityEvent BattleReady;

        public GridManager UserGrid;
        public GridManager EnemyGrid;

        [SerializeField] public List<GameObject> ShipPresets = new List<GameObject>();
        [SerializeField] public GameObject XPrefab;
        [SerializeField] public GameObject BombPrefab;
        Dictionary<Tile, GameObject> _destroyedTiles;
        Dictionary<Tile, GameObject> _bombedTiles;

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
            if (_controller != null)
            {
                _controller.NetworkClient.OnMarkReady += MarkReadyEvent;
                _controller.NetworkClient.OnEnqueue += EnqueueEvent;
                _controller.NetworkClient.OnBattleStarted += StartBattle;
                _controller.NetworkClient.OnBombing += BombResult;
                _controller.NetworkClient.OnVictory += VictoryResult;
            }
        }
        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.NetworkClient.OnMarkReady -= MarkReadyEvent;
                _controller.NetworkClient.OnEnqueue -= EnqueueEvent;
                _controller.NetworkClient.OnBattleStarted -= StartBattle;
                _controller.NetworkClient.OnBombing -= BombResult;
                _controller.NetworkClient.OnVictory -= VictoryResult;
            }
        }


        void MarkReadyEvent(int result)
        {
            switch (result)
            {
                case 0:
                    {
                        BattleReady?.Invoke();
                        _resultText.text = $"Waiting for the other party...";
                        break;
                    }
                case 1:
                    {
                        BattleStarted?.Invoke();
                        _resultText.text = $"Starting battle...";
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
                        EnqueueingForBattle?.Invoke();
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
        { // TODO: read the pacakge, create the board DONE, activate the preset, put the enemy stuff on UI
            JoiningBattleEvent?.Invoke();
            UserGrid.StartBattle(package.BoardSize, package.BoardSize);
            Instantiate(ShipPresets[package.ShipPreset - 1], this.transform); // spawn the preset at the view location
            EnemyUsernameText.text = $"Oponent: {package.EnemyUsername}";
            EnemyVictoriesText.text = $"Victories: {package.EnemyVictories}";
            EnemyGrid._width = package.BoardSize;
            EnemyGrid._height = package.BoardSize;
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
            var grid = package.IsForEnemy ? EnemyGrid : UserGrid;

            if (!grid.TryGetTile(package.location, out var tile))
                return;

            switch (package.result)
            {
                case 0:
                case 6:
                    {
                        // Use the apropriate grid, and spawn X there (add blowing VFX?)
                        tile.CurrentState = Tile.State.Destroyed;
                        var newPrf = Instantiate(XPrefab, tile.transform);
                        _destroyedTiles.Add(tile, newPrf);
                        break;
                    }

                case 3:
                    {
                        // Use the apropriate grid and spawn bomb
                        tile.CurrentState = Tile.State.Bombed;
                        var newPrf = Instantiate(BombPrefab, tile.transform);
                        _bombedTiles.Add(tile, newPrf);
                        break;
                    }
            }
        }

        void VictoryResult(bool isWinner)
        {// TODO: if true, we won, if not -> enemy

        }



        private void ShowResult(string message)
        {
            if (_resultText != null)
                _resultText.text = message;

            Debug.Log(message);
        }
    }
}