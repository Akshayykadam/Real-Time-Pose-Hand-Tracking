# Fruit Ninja Hand Tracking Game

A Fruit Ninja style game where you slice fruits using hand tracking via MediaPipe pose landmarks.

**Features:**
- **Hand Tracking Slicing:** Use your hand as a blade.
- **Neon Blade Trail:** Smooth, glowing blue trail (Cyan → Deep Blue).
- **Bomb Mechanics:** Camera shake + screen flash + slow-motion game over.
- **Scoring System:** Points, Combos, and High Scores.
- **Dynamic UI:** Custom "Shojumaru" font, animated popups, and FPS counter.

---

## Quick Setup (5 Minutes)

### 1. Create Scene

1. Duplicate the existing `PoseLandmarkDetection` scene
2. Save it as `Assets/FruitNinja/Scenes/FruitNinja.unity`

### 2. Replace Hand Controller

On the `PoseDetector` GameObject:
1. Remove or disable `HandFireballController` component
2. Add `HandSliceController` component (from FruitNinja namespace)

### 3. Add Game Controller

1. Create an empty GameObject named `GameController`
2. Add `FruitNinjaGameController` component
3. Add `ScoreManager` component
4. Add `GameUI` component

### 4. Assign Assets (Polished UI)

On the **GameUI** component:
1. Find the **Custom Font** field.
2. Drag the `Shojumaru SDF` asset from `Assets/FruitNinja/Fonts/` into it.
   *(If missing, create it: Right-click `Shojumaru-Regular.ttf` -> Create -> TextMeshPro -> Font Asset)*

### 5. Configure (Optional)

The game works with defaults, but you can adjust:

| Setting | Default | Description |
|---------|---------|-------------|
| Max Lives | 3 | Lives before game over |
| Bomb Chance | 10% | Probability of bomb spawn |
| Launch Force | 11-15 | Upward velocity of fruits (Higher = higher jump) |
| Fruit Size | 0.8 | Scale of fruit circles |

### 6. Play!

- Run the scene
- Wave your hand in front of the camera
- Game starts automatically after hand detection ("Wave to start" text pulses)
- Swipe your hand to slice fruits!

---

## Game Rules

- **Slice fruits** = +10-15 points
- **Rapid slices** = Combo bonus (+5 per extra) + "Pop" animation
- **Miss a fruit** = **NO Penalty** (Keep going!)
- **Slice a bomb** = **Lose 1 life** + Camera Shake + Red Flash
- **0 lives** = Game Over (Slow motion effect)

## Fruit Types (Default)

| Fruit | Color | Points | Size |
|-------|-------|--------|------|
| Apple | Red | 10 | Normal |
| Orange | Orange | 10 | Normal |
| Watermelon | Green | 15 | Large |
| Banana | Yellow | 10 | Small |
| Grape | Purple | 10 | Small |
| Bomb | Black | -1 life | Normal |

---

## Script Reference

| Script | Purpose |
|--------|---------|
| `FruitNinjaGameController` | Game loop, spawning, creates fruits at runtime |
| `HandSliceController` | Hand tracking, smooth trail interpolation (`SmoothDamp`) |
| `Fruit` | Fruit physics, slicing logic, full-size sliced halves |
| `ScorePopup` | Floating score text with bounce animation |
| `GameUI` | Manages all UI, custom fonts, animations, FPS counter |

---

## Customization

### Add Custom Fruit Types

1. Right-click → Create → FruitNinja → Fruit Data
2. Set color, points, size multiplier
3. Assign to `FruitNinjaGameController.Fruit Data Assets` array

### Adjust Hand Sensitivity

On `HandSliceController`:
- `Min Slice Velocity`: Lower = easier slicing (default: 300)
- `Slice Radius`: Higher = larger slice area (default: 50)
- `Visibility Threshold`: Hand detection sensitivity (default: 0.5)

### Spawn Area

On `FruitNinjaGameController`:
- `Spawn Y`: Vertical spawn position (default: -5)
- `Spawn X Min/Max`: Horizontal range (default: -3 to 3)

---

## Troubleshooting

### "Destroying assets is not permitted"
- Fixed in `FruitNinjaGameController`. It now correctly distinguishes between runtime data and asset data.

### Font not showing
- Ensure `Shojumaru SDF` is assigned to `GameUI` -> `Custom Font`.

### Fruits jump too low
- Increase `Min/Max Launch Force` on `FruitNinjaGameController` (current recommended: 11-15).
