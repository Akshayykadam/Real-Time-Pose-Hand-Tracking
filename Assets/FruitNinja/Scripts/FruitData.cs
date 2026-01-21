using UnityEngine;

namespace FruitNinja
{
    /// <summary>
    /// ScriptableObject defining fruit properties.
    /// Supports both sprite-based and color-based fruits.
    /// </summary>
    [CreateAssetMenu(fileName = "NewFruitData", menuName = "FruitNinja/Fruit Data")]
    public class FruitData : ScriptableObject
    {
        [Header("Visual - Sprites (Optional)")]
        [Tooltip("Main fruit sprite. If not set, uses colored circle.")]
        public Sprite wholeSprite;
        
        [Tooltip("First half after slicing (left/top half)")]
        public Sprite slicedHalf1;
        
        [Tooltip("Second half after slicing (right/bottom half)")]
        public Sprite slicedHalf2;
        
        [Header("Visual - Colors")]
        [Tooltip("Used if no sprite assigned, or for tinting/effects")]
        public Color fruitColor = Color.white;
        public Color juiceColor = Color.red;
        
        [Header("Info")]
        public string fruitName = "Fruit";
        
        [Header("Gameplay")]
        public int pointValue = 10;
        
        [Range(0.5f, 2f)]
        public float sizeMultiplier = 1f;
        
        [Range(0.5f, 1.5f)]
        public float speedMultiplier = 1f;
        
        [Header("Special")]
        public bool isBomb = false;
        public int bombPenalty = 1;
        
        /// <summary>
        /// Returns true if this fruit has sprite assets assigned
        /// </summary>
        public bool HasSprites => wholeSprite != null;
        
        /// <summary>
        /// Returns true if sliced half sprites are available
        /// </summary>
        public bool HasSlicedSprites => slicedHalf1 != null && slicedHalf2 != null;
    }
}
