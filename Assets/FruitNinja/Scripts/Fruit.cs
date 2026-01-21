using UnityEngine;

namespace FruitNinja
{
    /// <summary>
    /// Fruit behavior - handles physics, slicing, and visual effects.
    /// Supports both sprite-based and procedural circle visuals.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class Fruit : MonoBehaviour
    {
        [Header("Fruit Data")]
        [SerializeField] private FruitData _fruitData;
        
        [Header("Slice Settings")]
        [SerializeField] private float _minSliceVelocity = 2f;
        
        private SpriteRenderer _spriteRenderer;
        private Rigidbody2D _rigidbody;
        private Collider2D _collider;
        private bool _isSliced = false;
        private bool _hasExitedScreen = false;
        private float _rotationDirection;
        private Sprite _proceduralSprite; // Cache for cleanup
        
        public FruitData Data => _fruitData;
        public bool IsSliced => _isSliced;
        
        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            
            _rotationDirection = Random.value > 0.5f ? 1f : -1f;
        }
        
        private void Start()
        {
            ApplyVisuals();
        }
        
        private void ApplyVisuals()
        {
            if (_fruitData != null && _fruitData.HasSprites)
            {
                // Use assigned sprite
                _spriteRenderer.sprite = _fruitData.wholeSprite;
                _spriteRenderer.color = Color.white; // No tint for sprites
            }
            else
            {
                // Create procedural circle if no sprite
                if (_spriteRenderer.sprite == null)
                {
                    _proceduralSprite = CreateCircleSprite();
                    _spriteRenderer.sprite = _proceduralSprite;
                }
                
                if (_fruitData != null)
                {
                    _spriteRenderer.color = _fruitData.fruitColor;
                }
            }
            
            if (_fruitData != null)
            {
                transform.localScale *= _fruitData.sizeMultiplier;
            }
        }
        
        private Sprite CreateCircleSprite()
        {
            int size = 64;
            Texture2D texture = new Texture2D(size, size);
            Color[] colors = new Color[size * size];
            
            float radius = size / 2f;
            Vector2 center = new Vector2(radius, radius);
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist < radius - 1)
                    {
                        colors[y * size + x] = Color.white;
                    }
                    else if (dist < radius)
                    {
                        float alpha = radius - dist;
                        colors[y * size + x] = new Color(1, 1, 1, alpha);
                    }
                    else
                    {
                        colors[y * size + x] = Color.clear;
                    }
                }
            }
            
            texture.SetPixels(colors);
            texture.Apply();
            
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
        
        private void Update()
        {
            if (!_isSliced && !_hasExitedScreen)
            {
                Vector3 viewportPos = Camera.main.WorldToViewportPoint(transform.position);
                if (viewportPos.y < -0.1f)
                {
                    _hasExitedScreen = true;
                    OnMissed();
                }
            }
            
            if (transform.position.y < -10f)
            {
                Destroy(gameObject);
            }
            
            // Rotate while flying
            float rotationSpeed = _rigidbody.linearVelocity.magnitude * 3f;
            transform.Rotate(0, 0, rotationSpeed * Time.deltaTime * _rotationDirection);
        }
        
        private void OnDestroy()
        {
            // Clean up procedural texture
            if (_proceduralSprite != null && _proceduralSprite.texture != null)
            {
                Destroy(_proceduralSprite.texture);
                Destroy(_proceduralSprite);
            }
        }
        
        public void Launch(Vector2 velocity)
        {
            if (_rigidbody != null)
            {
                if (_fruitData != null)
                {
                    velocity *= _fruitData.speedMultiplier;
                }
                _rigidbody.linearVelocity = velocity;
                _rigidbody.angularVelocity = Random.Range(-180f, 180f);
            }
        }
        
        public bool TrySlice(Vector2 sliceVelocity, Vector2 sliceDirection)
        {
            if (_isSliced) return false;
            if (sliceVelocity.magnitude < _minSliceVelocity) return false;
            
            _isSliced = true;
            
            if (_fruitData != null && _fruitData.isBomb)
            {
                OnBombSliced();
                return true;
            }
            
            OnSliced(sliceDirection);
            return true;
        }
        
        private void OnSliced(Vector2 sliceDirection)
        {
            if (ScoreManager.Instance != null && _fruitData != null)
            {
                ScoreManager.Instance.AddScore(_fruitData.pointValue, transform.position);
            }
            
            SpawnHalves(sliceDirection);
            
            _collider.enabled = false;
            _spriteRenderer.enabled = false;
            
            Destroy(gameObject, 2f);
        }
        
        private void OnBombSliced()
        {
            if (FruitNinjaGameController.Instance != null && _fruitData != null)
            {
                FruitNinjaGameController.Instance.OnBombHit(_fruitData.bombPenalty);
            }
            
            StartCoroutine(BombFlashEffect());
        }
        
        private System.Collections.IEnumerator BombFlashEffect()
        {
            for (int i = 0; i < 3; i++)
            {
                _spriteRenderer.color = Color.white;
                yield return new WaitForSeconds(0.05f);
                _spriteRenderer.color = Color.black;
                yield return new WaitForSeconds(0.05f);
            }
            Destroy(gameObject);
        }
        
        private void OnMissed()
        {
            if (!_isSliced && _fruitData != null && !_fruitData.isBomb)
            {
                if (FruitNinjaGameController.Instance != null)
                {
                    FruitNinjaGameController.Instance.OnFruitMissed();
                }
                
                if (ScoreManager.Instance != null)
                {
                    ScoreManager.Instance.BreakCombo();
                }
            }
        }
        
        private void SpawnHalves(Vector2 sliceDirection)
        {
            Vector2 perpendicular = new Vector2(-sliceDirection.y, sliceDirection.x).normalized;
            
            for (int i = 0; i < 2; i++)
            {
                GameObject half = new GameObject($"FruitHalf_{i}");
                half.transform.position = transform.position;
                half.transform.localScale = transform.localScale; // Use full scale now that we have proper sprites
                
                SpriteRenderer sr = half.AddComponent<SpriteRenderer>();
                sr.sortingOrder = _spriteRenderer.sortingOrder;
                
                // Use sliced sprites if available, otherwise use whole sprite or procedural
                if (_fruitData != null && _fruitData.HasSlicedSprites)
                {
                    sr.sprite = (i == 0) ? _fruitData.slicedHalf1 : _fruitData.slicedHalf2;
                    sr.color = Color.white;
                }
                else if (_fruitData != null && _fruitData.HasSprites)
                {
                    sr.sprite = _fruitData.wholeSprite;
                    sr.color = Color.white;
                    half.transform.localScale = transform.localScale * 0.5f;
                }
                else
                {
                    sr.sprite = _spriteRenderer.sprite;
                    sr.color = _fruitData != null ? _fruitData.fruitColor : _spriteRenderer.color;
                }
                
                Rigidbody2D rb = half.AddComponent<Rigidbody2D>();
                rb.gravityScale = 1.5f;
                Vector2 direction = (i == 0) ? perpendicular : -perpendicular;
                rb.linearVelocity = _rigidbody.linearVelocity + direction * 3f;
                rb.angularVelocity = Random.Range(-360f, 360f);
                
                half.AddComponent<FruitHalf>();
                
                Destroy(half, 3f);
            }
        }
        
        public void Initialize(FruitData data)
        {
            _fruitData = data;
            ApplyVisuals();
        }
    }
}
