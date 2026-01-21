using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FruitNinja
{
    public enum GameState
    {
        Ready,      // Waiting for player/hand detection
        Playing,    // Active gameplay
        GameOver    // Game ended
    }
    
    /// <summary>
    /// Main game controller for Fruit Ninja.
    /// Creates fruits at runtime using default Unity sprites - no prefabs needed.
    /// </summary>
    public class FruitNinjaGameController : MonoBehaviour
    {
        public static FruitNinjaGameController Instance { get; private set; }
        
        [Header("References")]
        [SerializeField] private HandSliceController _handSliceController;
        [SerializeField] private GameUI _gameUI;
        [SerializeField] private ScoreManager _scoreManager;
        
        [Header("Fruit Types")]
        [SerializeField] private FruitData[] _fruitDataAssets;
        
        [Header("Bomb")]
        [SerializeField] private FruitData _bombDataAsset;
        
        [Header("Spawn Settings")]
        [SerializeField] private float _initialSpawnInterval = 2f;
        [SerializeField] private float _minSpawnInterval = 0.5f;
        [SerializeField] private float _spawnIntervalDecreaseRate = 0.02f;
        [SerializeField] private float _bombChance = 0.1f;
        
        [Header("Launch Settings")]
        [SerializeField] private float _minLaunchForce = 11f;
        [SerializeField] private float _maxLaunchForce = 15f;
        [SerializeField] private float _launchAngleMin = 60f;
        [SerializeField] private float _launchAngleMax = 120f;
        
        [Header("Game Settings")]
        [SerializeField] private int _maxLives = 3;
        [SerializeField] private float _handDetectionDelay = 2f;
        
        [Header("Spawn Zone")]
        [SerializeField] private float _spawnY = -5f;
        [SerializeField] private float _spawnXMin = -3f;
        [SerializeField] private float _spawnXMax = 3f;
        
        [Header("Fruit Appearance")]
        [SerializeField] private float _fruitSize = 0.8f;
        
        private GameState _currentState = GameState.Ready;
        private int _currentLives;
        private float _currentSpawnInterval;
        private Coroutine _spawnCoroutine;
        private float _handDetectedTime;
        private bool _handWasDetected;
        
        // Default fruit data (used if no ScriptableObjects assigned)
        private List<FruitData> _defaultFruitData;
        private FruitData _bombData;
        
        public GameState CurrentState => _currentState;
        public int CurrentLives => _currentLives;
        
        public event Action<GameState> OnGameStateChanged;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            CreateDefaultFruitData();
        }
        
        private void CreateDefaultFruitData()
        {
            _defaultFruitData = new List<FruitData>();
            
            // Apple - Red
            var apple = ScriptableObject.CreateInstance<FruitData>();
            apple.fruitName = "Apple";
            apple.fruitColor = new Color(0.9f, 0.2f, 0.2f); // Red
            apple.juiceColor = new Color(1f, 0.9f, 0.8f);
            apple.pointValue = 10;
            apple.sizeMultiplier = 1f;
            _defaultFruitData.Add(apple);
            
            // Orange
            var orange = ScriptableObject.CreateInstance<FruitData>();
            orange.fruitName = "Orange";
            orange.fruitColor = new Color(1f, 0.6f, 0.1f); // Orange
            orange.juiceColor = new Color(1f, 0.8f, 0.4f);
            orange.pointValue = 10;
            orange.sizeMultiplier = 1f;
            _defaultFruitData.Add(orange);
            
            // Watermelon - Green
            var watermelon = ScriptableObject.CreateInstance<FruitData>();
            watermelon.fruitName = "Watermelon";
            watermelon.fruitColor = new Color(0.2f, 0.7f, 0.3f); // Green
            watermelon.juiceColor = new Color(1f, 0.3f, 0.3f);
            watermelon.pointValue = 15;
            watermelon.sizeMultiplier = 1.3f;
            watermelon.speedMultiplier = 0.9f;
            _defaultFruitData.Add(watermelon);
            
            // Banana - Yellow
            var banana = ScriptableObject.CreateInstance<FruitData>();
            banana.fruitName = "Banana";
            banana.fruitColor = new Color(1f, 0.9f, 0.2f); // Yellow
            banana.juiceColor = new Color(1f, 1f, 0.8f);
            banana.pointValue = 10;
            banana.sizeMultiplier = 0.9f;
            banana.speedMultiplier = 1.1f;
            _defaultFruitData.Add(banana);
            
            // Grape - Purple
            var grape = ScriptableObject.CreateInstance<FruitData>();
            grape.fruitName = "Grape";
            grape.fruitColor = new Color(0.5f, 0.2f, 0.7f); // Purple
            grape.juiceColor = new Color(0.7f, 0.4f, 0.9f);
            grape.pointValue = 10;
            grape.sizeMultiplier = 0.7f;
            grape.speedMultiplier = 1.2f;
            _defaultFruitData.Add(grape);
            
            // Bomb - Black
            _bombData = ScriptableObject.CreateInstance<FruitData>();
            _bombData.fruitName = "Bomb";
            _bombData.fruitColor = new Color(0.1f, 0.1f, 0.1f); // Black
            _bombData.juiceColor = Color.gray;
            _bombData.isBomb = true;
            _bombData.bombPenalty = 1;
            _bombData.sizeMultiplier = 0.9f;
        }
        
        private void Start()
        {
            // Find components if not assigned
            if (_handSliceController == null)
                _handSliceController = FindObjectOfType<HandSliceController>();
            if (_gameUI == null)
                _gameUI = FindObjectOfType<GameUI>();
            if (_scoreManager == null)
                _scoreManager = FindObjectOfType<ScoreManager>();
            
            // Use assigned fruit data if available
            if (_fruitDataAssets != null && _fruitDataAssets.Length > 0)
            {
                _defaultFruitData.Clear();
                _defaultFruitData.AddRange(_fruitDataAssets);
            }
            
            // Use assigned bomb data if available
            if (_bombDataAsset != null)
            {
                _bombData = _bombDataAsset;
            }
            
            if (_gameUI != null)
            {
                _gameUI.OnRestartClicked += RestartGame;
            }
            
            SetGameState(GameState.Ready);
        }
        
        private void OnDestroy()
        {
            if (_gameUI != null)
            {
                _gameUI.OnRestartClicked -= RestartGame;
            }
            
            // Clean up runtime ScriptableObjects ONLY if we didn't use assets
            // If _fruitDataAssets is empty, we created default data at runtime, so we should clean it up.
            // If _fruitDataAssets is NOT empty, we are using project assets, so DO NOT destroy them.
            if (_fruitDataAssets == null || _fruitDataAssets.Length == 0)
            {
                foreach (var data in _defaultFruitData)
                {
                    if (data != null) Destroy(data);
                }
            }

            // Same for bomb data
            if (_bombDataAsset == null && _bombData != null)
            {
                Destroy(_bombData);
            }
        }
        
        private void Update()
        {
            switch (_currentState)
            {
                case GameState.Ready:
                    UpdateReadyState();
                    break;
                case GameState.Playing:
                    UpdatePlayingState();
                    break;
            }
        }
        
        private void UpdateReadyState()
        {
            if (_handSliceController != null && _handSliceController.IsAnyHandVisible)
            {
                if (!_handWasDetected)
                {
                    _handWasDetected = true;
                    _handDetectedTime = Time.time;
                }
                
                if (Time.time - _handDetectedTime > _handDetectionDelay)
                {
                    StartGame();
                }
            }
            else
            {
                _handWasDetected = false;
            }
        }
        
        private void UpdatePlayingState()
        {
            // Additional playing state logic can go here
        }
        
        private void SetGameState(GameState newState)
        {
            _currentState = newState;
            OnGameStateChanged?.Invoke(_currentState);
            
            switch (_currentState)
            {
                case GameState.Ready:
                    if (_gameUI != null)
                    {
                        _gameUI.ShowStartScreen();
                        _gameUI.HideGameOverScreen();
                    }
                    break;
                    
                case GameState.Playing:
                    if (_gameUI != null)
                    {
                        _gameUI.HideStartScreen();
                        _gameUI.HideGameOverScreen();
                    }
                    break;
                    
                case GameState.GameOver:
                    StopSpawning();
                    if (_gameUI != null && _scoreManager != null)
                    {
                        _gameUI.ShowGameOverScreen(
                            _scoreManager.CurrentScore, 
                            _scoreManager.HighScore
                        );
                    }
                    break;
            }
        }
        
        private void StartGame()
        {
            _currentLives = _maxLives;
            _currentSpawnInterval = _initialSpawnInterval;
            
            if (_scoreManager != null)
                _scoreManager.ResetScore();
            
            if (_gameUI != null)
            {
                _gameUI.InitializeLives(_maxLives);
                _gameUI.UpdateLives(_currentLives);
            }
            
            SetGameState(GameState.Playing);
            StartSpawning();
        }
        
        public void RestartGame()
        {
            // Clean up existing fruits
            foreach (var fruit in FindObjectsOfType<Fruit>())
            {
                Destroy(fruit.gameObject);
            }
            
            _handWasDetected = false;
            SetGameState(GameState.Ready);
        }
        
        private void StartSpawning()
        {
            if (_spawnCoroutine != null)
                StopCoroutine(_spawnCoroutine);
            
            _spawnCoroutine = StartCoroutine(SpawnRoutine());
        }
        
        private void StopSpawning()
        {
            if (_spawnCoroutine != null)
            {
                StopCoroutine(_spawnCoroutine);
                _spawnCoroutine = null;
            }
        }
        
        private IEnumerator SpawnRoutine()
        {
            yield return new WaitForSeconds(1f);
            
            while (_currentState == GameState.Playing)
            {
                SpawnFruit();
                
                // Occasionally spawn multiple fruits
                if (Random.value < 0.3f)
                {
                    yield return new WaitForSeconds(0.2f);
                    SpawnFruit();
                }
                
                yield return new WaitForSeconds(_currentSpawnInterval);
                
                _currentSpawnInterval = Mathf.Max(
                    _minSpawnInterval, 
                    _currentSpawnInterval - _spawnIntervalDecreaseRate
                );
            }
        }
        
        private void SpawnFruit()
        {
            float spawnX = Random.Range(_spawnXMin, _spawnXMax);
            Vector3 spawnPos = new Vector3(spawnX, _spawnY, 0);
            
            // Choose fruit data
            FruitData data;
            if (Random.value < _bombChance)
            {
                data = _bombData;
            }
            else
            {
                data = _defaultFruitData[Random.Range(0, _defaultFruitData.Count)];
            }
            
            // Create fruit GameObject at runtime
            GameObject fruitObj = new GameObject($"Fruit_{data.fruitName}");
            fruitObj.transform.position = spawnPos;
            fruitObj.transform.localScale = Vector3.one * _fruitSize;
            fruitObj.layer = LayerMask.NameToLayer("Default"); // Use Fruit layer if created
            
            // Add required components
            SpriteRenderer sr = fruitObj.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10;
            
            CircleCollider2D collider = fruitObj.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;
            
            Rigidbody2D rb = fruitObj.AddComponent<Rigidbody2D>();
            rb.gravityScale = 1f;
            
            Fruit fruit = fruitObj.AddComponent<Fruit>();
            fruit.Initialize(data);
            
            // Calculate launch velocity
            float angle = Random.Range(_launchAngleMin, _launchAngleMax) * Mathf.Deg2Rad;
            float force = Random.Range(_minLaunchForce, _maxLaunchForce);
            
            // Bias angle towards center
            float centerBias = -spawnX * 0.1f;
            angle += centerBias;
            
            Vector2 velocity = new Vector2(
                Mathf.Cos(angle) * force,
                Mathf.Sin(angle) * force
            );
            
            fruit.Launch(velocity);
        }
        
        public void OnFruitMissed()
        {
            if (_currentState != GameState.Playing) return;
            
            // User requested: Don't lose lives on missed fruit, only on bomb hit
            // But we still break combo
            if (_scoreManager != null)
            {
                _scoreManager.BreakCombo();
            }
        }
        
        public void OnBombHit(int penalty)
        {
            if (_currentState != GameState.Playing) return;
            
            _currentLives -= penalty;
            
            // Camera shake and flash effect
            StartCoroutine(BombEffect());
            
            if (_gameUI != null)
            {
                _gameUI.UpdateLives(_currentLives);
            }
            
            if (_currentLives <= 0)
            {
                StartCoroutine(GameOverEffect());
            }
        }
        
        private IEnumerator BombEffect()
        {
            Camera cam = Camera.main;
            if (cam == null) yield break;
            
            Vector3 originalPos = cam.transform.position;
            float shakeDuration = 0.3f;
            float shakeMagnitude = 0.2f;
            float elapsed = 0f;
            
            // Screen flash (using UI)
            if (_gameUI != null)
            {
                _gameUI.FlashScreen(Color.red, 0.2f);
            }
            
            // Camera shake
            while (elapsed < shakeDuration)
            {
                float x = Random.Range(-1f, 1f) * shakeMagnitude;
                float y = Random.Range(-1f, 1f) * shakeMagnitude;
                
                cam.transform.position = originalPos + new Vector3(x, y, 0);
                
                elapsed += Time.deltaTime;
                shakeMagnitude *= 0.9f; // Decay
                yield return null;
            }
            
            cam.transform.position = originalPos;
        }
        
        private IEnumerator GameOverEffect()
        {
            // Slow motion effect before game over
            Time.timeScale = 0.3f;
            
            if (_gameUI != null)
            {
                _gameUI.FlashScreen(Color.red, 0.5f);
            }
            
            yield return new WaitForSecondsRealtime(0.5f);
            
            Time.timeScale = 1f;
            SetGameState(GameState.GameOver);
        }
    }
}
