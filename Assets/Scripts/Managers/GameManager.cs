using System;
using System.Collections;
using UnityEngine;

namespace YesChef.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        [Header("Round Settings")]
        [SerializeField, Min(10f)] private float _roundDurationSeconds = 10f; // 3 minutes

        private float _timeRemaining;
        private bool _isRoundActive;

        /// <summary>
        /// Invoked every frame while round is active with remaining time in seconds.
        /// </summary>
        public static event Action<float> OnTimerTick;

        /// <summary>
        /// Invoked when the round timer reaches 0.
        /// </summary>
        public static event Action OnRoundEnded;
        public static event Action OnRoundStarted;

        public float TimeRemaining => _timeRemaining;
        public bool IsRoundActive => _isRoundActive;

        public enum GameState
        {
            Playing,
            Paused,
        }

        public static GameState CurrentGameState { get; private set; } = GameState.Paused;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            Application.targetFrameRate = 60;
            
        }

        private void Update()
        {
            if (!_isRoundActive) return;

            _timeRemaining -= Time.deltaTime;
            OnTimerTick?.Invoke(_timeRemaining);

            if (_timeRemaining <= 0f)
            {
                _timeRemaining = 0f;
                EndRound();
            }
        }

        public void StartRound()
        {
            _timeRemaining = _roundDurationSeconds;
            _isRoundActive = true;
            CurrentGameState = GameState.Playing;
            OnRoundStarted?.Invoke();
            OnTimerTick?.Invoke(_timeRemaining);
        }

        private void EndRound()
        {
            _isRoundActive = false;
            OnRoundEnded?.Invoke();
            CurrentGameState = GameState.Paused;
            Debug.Log("[GameManager] Round time expired!");
        }

        public void PauseGame()
        {
            CurrentGameState = GameState.Paused;
            Time.timeScale = 0f;
        }

        public void ResumeGame()
        {
            CurrentGameState = GameState.Playing;
            Time.timeScale = 1f;
        }
    }
}