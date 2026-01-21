using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FruitNinja
{
    /// <summary>
    /// Manages all game UI elements.
    /// Creates UI at runtime if not assigned - no prefabs needed.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        [Header("Score Display")]
        [SerializeField] private TextMeshProUGUI _scoreText;
        [SerializeField] private TextMeshProUGUI _highScoreText;
        [SerializeField] private TextMeshProUGUI _comboText;
        
        [Header("Lives Display")]
        [SerializeField] private Transform _livesContainer;
        [SerializeField] private GameObject _heartPrefab;
        
        [Header("Screens")]
        [SerializeField] private GameObject _startScreen;
        [SerializeField] private GameObject _gameOverScreen;
        [SerializeField] private TextMeshProUGUI _finalScoreText;
        [SerializeField] private TextMeshProUGUI _gameOverHighScoreText;
        [SerializeField] private Button _restartButton;
        
        [Header("Colors")]
        [SerializeField] private Color _heartColor = Color.red;
        [SerializeField] private Color _scoreColor = Color.white;
        [SerializeField] private Color _comboColor = Color.yellow;
        
        [Header("Animation")]
        [SerializeField] private float _comboFadeTime = 1f;
        
        private GameObject[] _hearts;
        private float _comboDisplayTime;
        private Canvas _canvas;
        private TextMeshProUGUI _livesText;
        private TextMeshProUGUI _fpsText;
        private float _fpsTimer;
        private int _frameCount;
        private Image _flashPanel;
        
        [Header("Customization")]
        [SerializeField] private TMP_FontAsset _customFont;
        
        public TMP_FontAsset CustomFont => _customFont;
        
        public event Action OnRestartClicked;
        
        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null)
            {
                _canvas = FindObjectOfType<Canvas>();
            }
        }
        
        private void Start()
        {
            // Create UI elements if not assigned
            CreateUIIfNeeded();
            
            if (_restartButton != null)
            {
                _restartButton.onClick.AddListener(() => OnRestartClicked?.Invoke());
            }
            
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged += UpdateScoreDisplay;
                ScoreManager.Instance.OnComboChanged += UpdateComboDisplay;
                ScoreManager.Instance.OnHighScoreChanged += UpdateHighScoreDisplay;
            }
        }
        
        private void CreateUIIfNeeded()
        {
            if (_canvas == null) return;
            
            RectTransform canvasRect = _canvas.GetComponent<RectTransform>();
            
            // Create Score Text
            if (_scoreText == null)
            {
                GameObject scoreObj = CreateTextObject("ScoreText", "0", 72, _scoreColor);
                RectTransform rt = scoreObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0, -80);
                _scoreText = scoreObj.GetComponent<TextMeshProUGUI>();
            }
            
            // Create High Score Text
            if (_highScoreText == null)
            {
                GameObject highScoreObj = CreateTextObject("HighScoreText", "Best: 0", 32, new Color(1, 1, 1, 0.7f));
                RectTransform rt = highScoreObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-120, -40);
                _highScoreText = highScoreObj.GetComponent<TextMeshProUGUI>();
                
                if (ScoreManager.Instance != null)
                {
                    _highScoreText.text = $"Best: {ScoreManager.Instance.HighScore}";
                }
            }
            
            // Create Combo Text
            if (_comboText == null)
            {
                GameObject comboObj = CreateTextObject("ComboText", "", 48, _comboColor);
                RectTransform rt = comboObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0, 100);
                _comboText = comboObj.GetComponent<TextMeshProUGUI>();
                _comboText.gameObject.SetActive(false);
            }
            
            // Create Lives Container
            if (_livesContainer == null)
            {
                GameObject livesObj = new GameObject("LivesContainer");
                livesObj.transform.SetParent(_canvas.transform, false);
                RectTransform rt = livesObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(30, -30);
                rt.sizeDelta = new Vector2(200, 50);
                rt.pivot = new Vector2(0, 1);
                
                HorizontalLayoutGroup hlg = livesObj.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 8;
                hlg.childAlignment = TextAnchor.MiddleLeft;
                hlg.childControlWidth = false;
                hlg.childControlHeight = false;
                
                _livesContainer = livesObj.transform;
            }

            // Create FPS Counter
            if (_fpsText == null)
            {
                GameObject fpsObj = CreateTextObject("FPSCounter", "FPS: 60", 24, Color.green);
                RectTransform rt = fpsObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(30, -80); // Below lives
                rt.pivot = new Vector2(0, 1);
                _fpsText = fpsObj.GetComponent<TextMeshProUGUI>();
                _fpsText.alignment = TextAlignmentOptions.Left;
            }
            
            // Create Start Screen
            if (_startScreen == null)
            {
                _startScreen = CreateScreenPanel("StartScreen");
                
                GameObject titleText = CreateTextObject("Title", "FRUIT NINJA", 72, Color.white);
                titleText.transform.SetParent(_startScreen.transform, false);
                RectTransform titleRt = titleText.GetComponent<RectTransform>();
                titleRt.anchoredPosition = new Vector2(0, 100);
                
                GameObject instructionText = CreateTextObject("Instructions", "Wave your hand to start!", 36, new Color(1, 1, 1, 0.8f));
                instructionText.transform.SetParent(_startScreen.transform, false);
                RectTransform instrRt = instructionText.GetComponent<RectTransform>();
                instrRt.anchoredPosition = new Vector2(0, -50);
            }
            
            // Create Game Over Screen
            if (_gameOverScreen == null)
            {
                _gameOverScreen = CreateScreenPanel("GameOverScreen");
                _gameOverScreen.SetActive(false);
                
                GameObject gameOverTitle = CreateTextObject("GameOverTitle", "GAME OVER", 72, Color.red);
                gameOverTitle.transform.SetParent(_gameOverScreen.transform, false);
                RectTransform goTitleRt = gameOverTitle.GetComponent<RectTransform>();
                goTitleRt.anchoredPosition = new Vector2(0, 160); // Moved up
                
                GameObject finalScoreObj = CreateTextObject("FinalScore", "Score: 0", 48, Color.white);
                finalScoreObj.transform.SetParent(_gameOverScreen.transform, false);
                RectTransform fsRt = finalScoreObj.GetComponent<RectTransform>();
                fsRt.anchoredPosition = new Vector2(0, 50); // Moved up
                _finalScoreText = finalScoreObj.GetComponent<TextMeshProUGUI>();
                
                GameObject goHighScoreObj = CreateTextObject("GameOverHighScore", "Best: 0", 36, new Color(1, 1, 0.5f, 1));
                goHighScoreObj.transform.SetParent(_gameOverScreen.transform, false);
                RectTransform gohsRt = goHighScoreObj.GetComponent<RectTransform>();
                gohsRt.anchoredPosition = new Vector2(0, -50); // Moved down
                _gameOverHighScoreText = goHighScoreObj.GetComponent<TextMeshProUGUI>();
                
                // Create Restart Button
                GameObject buttonObj = new GameObject("RestartButton");
                buttonObj.transform.SetParent(_gameOverScreen.transform, false);
                
                Image buttonImage = buttonObj.AddComponent<Image>();
                buttonImage.color = new Color(0.2f, 0.7f, 0.3f);
                
                RectTransform buttonRt = buttonObj.GetComponent<RectTransform>();
                buttonRt.anchoredPosition = new Vector2(0, -180); // Moved further down
                buttonRt.sizeDelta = new Vector2(200, 60);
                
                _restartButton = buttonObj.AddComponent<Button>();
                _restartButton.targetGraphic = buttonImage;
                _restartButton.onClick.AddListener(() => OnRestartClicked?.Invoke());
                
                GameObject buttonText = CreateTextObject("ButtonText", "RESTART", 32, Color.white);
                buttonText.transform.SetParent(buttonObj.transform, false);
            }
        }
        
        private GameObject CreateTextObject(string name, string text, float fontSize, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(_canvas.transform, false);
            
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400, 100);
            
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold; // Ensure fontStyle is set once
            if (_customFont != null) tmp.font = _customFont;
            
            return obj;
        }
        
        private GameObject CreateScreenPanel(string name)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(_canvas.transform, false);
            
            RectTransform rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            
            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.7f);
            
            return panel;
        }
        
        private void OnDestroy()
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged -= UpdateScoreDisplay;
                ScoreManager.Instance.OnComboChanged -= UpdateComboDisplay;
                ScoreManager.Instance.OnHighScoreChanged -= UpdateHighScoreDisplay;
            }
        }
        
        private void Update()
        {
            // Fade combo text
            if (_comboText != null && _comboText.gameObject.activeSelf)
            {
                _comboDisplayTime -= Time.deltaTime;
                if (_comboDisplayTime <= 0)
                {
                    _comboText.gameObject.SetActive(false);
                }
            }
            
            // Pulse start screen text
            if (_startScreen != null && _startScreen.activeSelf)
            {
                Transform t = _startScreen.transform.Find("Instructions");
                if (t != null)
                {
                    float scale = 1f + Mathf.Sin(Time.time * 5f) * 0.1f;
                    t.localScale = new Vector3(scale, scale, 1f);
                }
            }
            
            // Update FPS
            _fpsTimer += Time.deltaTime;
            _frameCount++;
            if (_fpsTimer >= 0.5f)
            {
                float fps = _frameCount / _fpsTimer;
                if (_fpsText != null) _fpsText.text = $"FPS: {Mathf.RoundToInt(fps)}";
                _fpsTimer = 0;
                _frameCount = 0;
            }
        }
        
        public void InitializeLives(int maxLives)
        {
            // Clear existing hearts
            if (_hearts != null)
            {
                foreach (var heart in _hearts)
                {
                    if (heart != null) Destroy(heart);
                }
            }
            
            _hearts = new GameObject[maxLives];
            
            if (_livesContainer != null)
            {
                for (int i = 0; i < maxLives; i++)
                {
                    GameObject heart;
                    if (_heartPrefab != null)
                    {
                        heart = Instantiate(_heartPrefab, _livesContainer);
                    }
                    else
                    {
                        // Create heart as colored square (no sprite needed)
                        heart = new GameObject($"Heart_{i}");
                        heart.transform.SetParent(_livesContainer, false);
                        
                        RectTransform rt = heart.AddComponent<RectTransform>();
                        rt.sizeDelta = new Vector2(40, 40);
                        
                        Image img = heart.AddComponent<Image>();
                        img.color = _heartColor;
                        // No sprite = solid colored rectangle, which works fine as a heart indicator
                    }
                    _hearts[i] = heart;
                }
            }
        }
        
        public void UpdateLives(int currentLives)
        {
            // Update lives text
            if (_livesText != null)
            {
                _livesText.text = $"x{currentLives}";
            }
            
            // Update heart icons (optional visual)
            if (_hearts == null) return;
            
            for (int i = 0; i < _hearts.Length; i++)
            {
                if (_hearts[i] != null)
                {
                    _hearts[i].SetActive(i < currentLives);
                }
            }
        }
        
        public void ShowStartScreen()
        {
            if (_startScreen != null) _startScreen.SetActive(true);
            if (_gameOverScreen != null) _gameOverScreen.SetActive(false);
        }
        
        public void HideStartScreen()
        {
            if (_startScreen != null) _startScreen.SetActive(false);
        }
        
        public void ShowGameOverScreen(int finalScore, int highScore)
        {
            if (_gameOverScreen != null)
            {
                _gameOverScreen.SetActive(true);
                
                if (_finalScoreText != null)
                    _finalScoreText.text = $"Score: {finalScore}";
                    
                if (_gameOverHighScoreText != null)
                    _gameOverHighScoreText.text = $"Best: {highScore}";
            }
        }
        
        public void HideGameOverScreen()
        {
            if (_gameOverScreen != null) _gameOverScreen.SetActive(false);
        }
        
        private void UpdateScoreDisplay(int score)
        {
            if (_scoreText != null)
            {
                _scoreText.text = score.ToString();
            }
        }
        
        private void UpdateHighScoreDisplay(int highScore)
        {
            if (_highScoreText != null)
            {
                _highScoreText.text = $"Best: {highScore}";
            }
        }
        
        private void UpdateComboDisplay(int combo)
        {
            if (_comboText != null)
            {
                if (combo > 1)
                {
                    _comboText.gameObject.SetActive(true);
                    _comboText.text = $"x{combo} COMBO!";
                    _comboDisplayTime = _comboFadeTime;
                }
            }
        }
        
        /// <summary>
        /// Flash the screen with a color (for bomb explosion effect)
        /// </summary>
        public void FlashScreen(Color color, float duration)
        {
            StartCoroutine(FlashScreenCoroutine(color, duration));
        }
        
        private IEnumerator FlashScreenCoroutine(Color color, float duration)
        {
            // Create flash panel if not exists
            if (_flashPanel == null && _canvas != null)
            {
                GameObject flashObj = new GameObject("FlashPanel");
                flashObj.transform.SetParent(_canvas.transform, false);
                
                RectTransform rt = flashObj.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                
                _flashPanel = flashObj.AddComponent<Image>();
                _flashPanel.raycastTarget = false;
                
                // Ensure it's on top
                flashObj.transform.SetAsLastSibling();
            }
            
            if (_flashPanel == null) yield break;
            
            _flashPanel.gameObject.SetActive(true);
            
            // Fade from full color to transparent
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float alpha = Mathf.Lerp(0.6f, 0f, elapsed / duration);
                _flashPanel.color = new Color(color.r, color.g, color.b, alpha);
                elapsed += Time.unscaledDeltaTime; // Use unscaled for slow-mo compatibility
                yield return null;
            }
            
            _flashPanel.color = Color.clear;
            _flashPanel.gameObject.SetActive(false);
        }
    }
}
