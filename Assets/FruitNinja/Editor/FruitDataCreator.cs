using UnityEngine;
using UnityEditor;
using System.IO;

namespace FruitNinja.Editor
{
    /// <summary>
    /// Editor tool to auto-create FruitData assets from sprites in the Fruit folder.
    /// </summary>
    public class FruitDataCreator : EditorWindow
    {
        [MenuItem("FruitNinja/Create Fruit Data Assets")]
        public static void CreateFruitDataAssets()
        {
            string fruitDataPath = "Assets/FruitNinja/FruitData";
            
            // Create FruitData folder if it doesn't exist
            if (!AssetDatabase.IsValidFolder(fruitDataPath))
            {
                AssetDatabase.CreateFolder("Assets/FruitNinja", "FruitData");
            }
            
            // Define fruit configurations
            var fruits = new[]
            {
                new FruitConfig("Banana", "Assets/FruitNinja/Fruit/Normal Fruit/Banana.png", 
                    "Assets/FruitNinja/Fruit/Sliced Fruit/Sliced Banana/Banana_Part 1.png",
                    "Assets/FruitNinja/Fruit/Sliced Fruit/Sliced Banana/Banana_Part 2.png",
                    new Color(1f, 0.9f, 0.2f), 10, 0.9f),
                    
                new FruitConfig("Grapes", "Assets/FruitNinja/Fruit/Normal Fruit/Grapes.png",
                    "Assets/FruitNinja/Fruit/Sliced Fruit/Sliced Grapes/Grapes_Part 1.png",
                    "Assets/FruitNinja/Fruit/Sliced Fruit/Sliced Grapes/Grapes_Part 2.png",
                    new Color(0.5f, 0.2f, 0.7f), 10, 0.8f),
                    
                new FruitConfig("Orange", "Assets/FruitNinja/Fruit/Normal Fruit/Orange.png",
                    "Assets/FruitNinja/Fruit/Sliced Fruit/Sliced Orange/Orange_Part 1.png",
                    "Assets/FruitNinja/Fruit/Sliced Fruit/Sliced Orange/Orange_Part 2.png",
                    new Color(1f, 0.6f, 0.1f), 10, 1f)
            };
            
            int created = 0;
            
            foreach (var fruit in fruits)
            {
                string assetPath = $"{fruitDataPath}/{fruit.name}Data.asset";
                
                // Check if already exists
                if (AssetDatabase.LoadAssetAtPath<FruitData>(assetPath) != null)
                {
                    Debug.Log($"FruitData for {fruit.name} already exists, skipping...");
                    continue;
                }
                
                // Create new FruitData
                FruitData data = ScriptableObject.CreateInstance<FruitData>();
                data.fruitName = fruit.name;
                data.fruitColor = fruit.color;
                data.juiceColor = fruit.color;
                data.pointValue = fruit.points;
                data.sizeMultiplier = fruit.size;
                data.speedMultiplier = 1f;
                data.isBomb = false;
                
                // Load sprites
                data.wholeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(fruit.wholePath);
                data.slicedHalf1 = AssetDatabase.LoadAssetAtPath<Sprite>(fruit.half1Path);
                data.slicedHalf2 = AssetDatabase.LoadAssetAtPath<Sprite>(fruit.half2Path);
                
                if (data.wholeSprite == null)
                {
                    Debug.LogWarning($"Could not find sprite at: {fruit.wholePath}");
                }
                
                // Save asset
                AssetDatabase.CreateAsset(data, assetPath);
                created++;
                Debug.Log($"Created FruitData: {assetPath}");
            }
            
            // Create Bomb data
            string bombPath = $"{fruitDataPath}/BombData.asset";
            if (AssetDatabase.LoadAssetAtPath<FruitData>(bombPath) == null)
            {
                FruitData bombData = ScriptableObject.CreateInstance<FruitData>();
                bombData.fruitName = "Bomb";
                bombData.fruitColor = new Color(0.1f, 0.1f, 0.1f);
                bombData.juiceColor = Color.gray;
                bombData.pointValue = 0;
                bombData.sizeMultiplier = 0.9f;
                bombData.speedMultiplier = 1f;
                bombData.isBomb = true;
                bombData.bombPenalty = 1;
                
                AssetDatabase.CreateAsset(bombData, bombPath);
                created++;
                Debug.Log($"Created BombData: {bombPath}");
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"✅ Created {created} FruitData assets in {fruitDataPath}");
            EditorUtility.DisplayDialog("Fruit Data Creator", 
                $"Created {created} FruitData assets!\n\nPath: {fruitDataPath}\n\nNow drag these into the FruitNinjaGameController's 'Fruit Data Assets' array.", 
                "OK");
        }
        
        private struct FruitConfig
        {
            public string name;
            public string wholePath;
            public string half1Path;
            public string half2Path;
            public Color color;
            public int points;
            public float size;
            
            public FruitConfig(string name, string wholePath, string half1Path, string half2Path, Color color, int points, float size)
            {
                this.name = name;
                this.wholePath = wholePath;
                this.half1Path = half1Path;
                this.half2Path = half2Path;
                this.color = color;
                this.points = points;
                this.size = size;
            }
        }
    }
}
