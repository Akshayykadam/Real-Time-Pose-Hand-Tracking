using System;
using UnityEngine;

namespace FruitNinja
{
    /// <summary>
    /// Manages game score, combos, and high scores
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }
        
        [Header("Combo Settings")]
        [SerializeField] private float _comboTimeWindow = 0.5f;
        [SerializeField] private int _comboBonus = 5;
        
        private int _currentScore;
        private int _highScore;
        private int _comboCount;
        private float _lastSliceTime;
        
        public int CurrentScore => _currentScore;
        public int HighScore => _highScore;
        public int ComboCount => _comboCount;
        
        public event Action<int> OnScoreChanged;
        public event Action<int> OnComboChanged;
        public event Action<int> OnHighScoreChanged;
        public event Action<int, Vector3> OnScorePopup; // score, world position
        
        private const string HIGH_SCORE_KEY = "FruitNinja_HighScore";
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            _highScore = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
        }
        
        public void ResetScore()
        {
            _currentScore = 0;
            _comboCount = 0;
            _lastSliceTime = 0f;
            OnScoreChanged?.Invoke(_currentScore);
            OnComboChanged?.Invoke(_comboCount);
        }
        
        public void AddScore(int basePoints, Vector3 worldPosition)
        {
            float timeSinceLastSlice = Time.time - _lastSliceTime;
            
            // Check if within combo window
            if (timeSinceLastSlice <= _comboTimeWindow && _lastSliceTime > 0)
            {
                _comboCount++;
            }
            else
            {
                _comboCount = 1;
            }
            
            _lastSliceTime = Time.time;
            
            // Calculate total points with combo bonus
            int comboBonus = (_comboCount > 1) ? (_comboCount - 1) * _comboBonus : 0;
            int totalPoints = basePoints + comboBonus;
            
            _currentScore += totalPoints;
            
            // Update high score if needed
            if (_currentScore > _highScore)
            {
                _highScore = _currentScore;
                PlayerPrefs.SetInt(HIGH_SCORE_KEY, _highScore);
                PlayerPrefs.Save();
                OnHighScoreChanged?.Invoke(_highScore);
            }
            
            OnScoreChanged?.Invoke(_currentScore);
            OnComboChanged?.Invoke(_comboCount);
            OnScorePopup?.Invoke(totalPoints, worldPosition);
        }
        
        public void BreakCombo()
        {
            if (_comboCount > 1)
            {
                _comboCount = 0;
                OnComboChanged?.Invoke(_comboCount);
            }
        }
    }
}
