using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YesChef.Scoring;
using YesChef.Stations;
using YesChef.Core;
using YesChef.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private TextMeshProUGUI startScreenHighScoreText;
    [SerializeField] private TextMeshProUGUI currentScoreText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private Button restartButton;
    [SerializeField] private TextMeshProUGUI endScreenHighScoreText;
    [SerializeField] private TextMeshProUGUI endScreenCurrentScoreText;
    [SerializeField] private Image tableProgressImage;
    [SerializeField] private Image[] _slots;
    [SerializeField] private GameObject startScreen;
    [SerializeField] private GameObject gameScreen;
    [SerializeField] private GameObject endScreen;
    [SerializeField] private GameObject pauseScreen;
    [SerializeField] private GameObject newHighScoreIndicator;

    [Header("Score Popup")]
    [SerializeField] private RectTransform scorePopupContainer;
    [SerializeField] private Camera scorePopupWorldCamera;
    [SerializeField] private TMP_FontAsset scorePopupFont;
    [SerializeField, Min(0.01f)] private float scorePopupDuration = 1.25f;
    [SerializeField, Min(0f)] private float scorePopupRiseDistance = 80f;
    [SerializeField] private Color scorePopupColor = new Color(1f, 0.82f, 0.1f, 1f);

    private Canvas _canvas;
    private RectTransform _resolvedScorePopupContainer;

    void Awake()
    {
        _canvas = GetComponent<Canvas>();
        _resolvedScorePopupContainer = scorePopupContainer != null
            ? scorePopupContainer
            : gameScreen != null ? gameScreen.transform as RectTransform : transform as RectTransform;

        startButton.onClick.AddListener(StartRound);
        restartButton.onClick.AddListener(StartRound);
        resumeButton.onClick.AddListener(ResumeGame);
        pauseButton.onClick.AddListener(PauseGame);
        quitButton.onClick.AddListener(QuitGame);

    }

    private void QuitGame()
    {
        Application.Quit();
    }


    private void ResumeGame()
    {
        pauseScreen.SetActive(false);
        startScreen.SetActive(false);
        endScreen.SetActive(false);
        gameScreen.SetActive(true);
        GameManager.Instance.ResumeGame();
    }

    private void PauseGame()
    {
        pauseScreen.SetActive(true);
        startScreen.SetActive(false);
        gameScreen.SetActive(false);
        endScreen.SetActive(false);
        GameManager.Instance.PauseGame();
    }

    void OnEnable()
    {
        ScoreTracker.ScoreChanged += UpdateCurrentScore;
        ScoreTracker.HighScoreChanged += UpdateHighScore;
        ScoreTracker.NewHighScoreAchieved += UpdateHighScoreIndicator;
        TableStation.ProgressChanged += UpdateTableProgress;
        StoveStation.SlotProgressChanged += UpdateStoveProgress;
        CustomerWindowStation.OrderScoreAdded += ShowScorePopup;
        GameManager.OnRoundEnded += EndGame;
        GameManager.OnTimerTick += UpdateTimer;

        UpdateHighScore(ScoreTracker.HighScore);
    }



    void OnDisable()
    {
        ScoreTracker.ScoreChanged -= UpdateCurrentScore;
        ScoreTracker.HighScoreChanged -= UpdateHighScore;
        TableStation.ProgressChanged -= UpdateTableProgress;
        StoveStation.SlotProgressChanged -= UpdateStoveProgress;
        CustomerWindowStation.OrderScoreAdded -= ShowScorePopup;
        ScoreTracker.NewHighScoreAchieved -= UpdateHighScoreIndicator;
        GameManager.OnRoundEnded -= EndGame;
        GameManager.OnTimerTick -= UpdateTimer;
    }

    private void UpdateHighScoreIndicator(int obj)
    {
        newHighScoreIndicator.SetActive(true);
    }

    private void UpdateTimer(float obj)
    {
        timerText.text = $"Time: {Mathf.CeilToInt(obj)}s";
    }

    private void UpdateStoveProgress(int slotIndex, float progress)
    {
        if (_slots == null || slotIndex >= _slots.Length || _slots[slotIndex] == null)
        {
            return;
        }

        if (_slots[slotIndex] != null)
        {
            _slots[slotIndex].fillAmount = progress;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    private void EndGame()
    {
        startScreen.SetActive(false);
        gameScreen.SetActive(false);
        endScreen.SetActive(true);
        pauseScreen.SetActive(false);

        UpdateCurrentScore(ScoreTracker.CurrentScore);
        UpdateHighScore(ScoreTracker.HighScore);
    }

    private void StartRound()
    {
        startScreen.SetActive(false);
        gameScreen.SetActive(true);
        endScreen.SetActive(false);
        pauseScreen.SetActive(false);

        GameManager.Instance.StartRound();
        ScoreTracker.ResetRun();
        newHighScoreIndicator.SetActive(false);
    }

    private void UpdateCurrentScore(int score)
    {
        currentScoreText.text = $"Current Score: {score}";
        endScreenCurrentScoreText.text = $"Current Score: {score}";
    }

    private void UpdateHighScore(int highScore)
    {
        startScreenHighScoreText.text = $"High Score: {highScore}";
        endScreenHighScoreText.text = $"High Score: {highScore}";
    }

    private void ShowScorePopup(Vector3 worldPosition, int score)
    {
        if (_canvas == null || _resolvedScorePopupContainer == null)
        {
            Debug.LogWarning("[UIManager] Cannot show a score popup because the canvas or popup container is missing.", this);
            return;
        }

        Camera worldCamera = scorePopupWorldCamera != null ? scorePopupWorldCamera : Camera.main;
        if (worldCamera == null)
        {
            Debug.LogWarning("[UIManager] Cannot show a score popup because no world camera is assigned or tagged MainCamera.", this);
            return;
        }

        Vector3 screenPosition = worldCamera.WorldToScreenPoint(worldPosition);
        if (screenPosition.z < 0f)
        {
            return;
        }

        Camera canvasCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _resolvedScorePopupContainer,
                screenPosition,
                canvasCamera,
                out Vector2 localPosition))
        {
            return;
        }

        ScorePopup popup = ScorePopup.Create(_resolvedScorePopupContainer);
        popup.Initialize(
            score,
            localPosition,
            scorePopupFont,
            scorePopupColor,
            scorePopupDuration,
            scorePopupRiseDistance);
    }

    private void UpdateTableProgress(float progress)
    {
        tableProgressImage.fillAmount = progress;
    }
}
