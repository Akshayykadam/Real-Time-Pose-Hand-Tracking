using UnityEngine;

namespace FruitNinja
{
    /// <summary>
    /// Simple script for fruit halves after slicing.
    /// Just handles auto-destruction and fading.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class FruitHalf : MonoBehaviour
    {
        [SerializeField] private float _fadeStartTime = 1.5f;
        [SerializeField] private float _fadeDuration = 1f;
        
        private SpriteRenderer _spriteRenderer;
        private float _spawnTime;
        
        private void Start()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _spawnTime = Time.time;
            
            // Add gravity
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.gravityScale = 1.5f;
            }
        }
        
        private void Update()
        {
            float elapsed = Time.time - _spawnTime;
            
            // Start fading after delay
            if (elapsed > _fadeStartTime && _spriteRenderer != null)
            {
                float fadeProgress = (elapsed - _fadeStartTime) / _fadeDuration;
                Color c = _spriteRenderer.color;
                c.a = Mathf.Lerp(1f, 0f, fadeProgress);
                _spriteRenderer.color = c;
            }
        }
    }
}
