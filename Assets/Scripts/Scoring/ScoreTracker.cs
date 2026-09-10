using System;
using UnityEngine;

namespace YesChef.Scoring
{
    /// <summary>
    /// Shared run score and persisted high score. UI should subscribe to the events
    /// instead of polling current values.
    /// </summary>
    public static class ScoreTracker
    {
        public const string HighScorePlayerPrefsKey = "YesChef_HighScore";

        public static event Action<int> ScoreChanged;
        public static event Action<int> HighScoreChanged;

        public static int CurrentScore { get; private set; }
        public static int HighScore { get; private set; } = PlayerPrefs.GetInt(HighScorePlayerPrefsKey, 0);

        public static void AddOrderScore(int orderScore)
        {
            CurrentScore += orderScore;
            ScoreChanged?.Invoke(CurrentScore);

            if (CurrentScore <= HighScore)
            {
                return;
            }

            HighScore = CurrentScore;
            PlayerPrefs.SetInt(HighScorePlayerPrefsKey, HighScore);
            PlayerPrefs.Save();
            HighScoreChanged?.Invoke(HighScore);
        }

        public static void ResetRun()
        {
            CurrentScore = 0;
            ScoreChanged?.Invoke(CurrentScore);
        }
    }
}
