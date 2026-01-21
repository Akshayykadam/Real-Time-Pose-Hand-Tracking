using UnityEngine;
using TMPro;

namespace FruitNinja
{
    /// <summary>
    /// Score popup that floats up and fades out
    /// </summary>
    public class ScorePopup : MonoBehaviour
    {
        [SerializeField] private float _floatSpeed = 100f;
        [SerializeField] private float _fadeTime = 0.8f;
        
        private TextMeshProUGUI _text;
        private CanvasGroup _canvasGroup;
        private float _startTime;
        
        private void Awake()
        {
            _text = GetComponentInChildren<TextMeshProUGUI>();
            _canvasGroup = GetComponent<CanvasGroup>();
            
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
        
        private void Start()
        {
            _startTime = Time.time;
            
            // Apply custom font if available
            GameUI gameUI = FindObjectOfType<GameUI>();
            if (gameUI != null && gameUI.CustomFont != null && _text != null)
            {
                _text.font = gameUI.CustomFont;
            }
            
            // Pop effect
            transform.localScale = Vector3.zero;
        }
        
        private void Update()
        {
            // Float up
            transform.position += Vector3.up * _floatSpeed * Time.deltaTime;
            
            float elapsed = Time.time - _startTime;
            float t = elapsed / _fadeTime;
            
            // Pop in and Fade out
            if (t < 0.2f)
            {
                // Pop in
                float scale = t / 0.2f;
                transform.localScale = Vector3.one * (1f + Mathf.Sin(scale * Mathf.PI) * 0.2f); // Slight bounce
            }
            else
            {
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one, Time.deltaTime * 5f);
            }
            
            if (_canvasGroup != null)
            {
                // Fade out near end
                if (t > 0.5f)
                {
                    _canvasGroup.alpha = Mathf.Lerp(1f, 0f, (t - 0.5f) * 2f);
                }
            }
            
            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
        
        public void SetScore(int score)
        {
            if (_text != null)
            {
                _text.text = $"+{score}";
            }
        }
    }
}
