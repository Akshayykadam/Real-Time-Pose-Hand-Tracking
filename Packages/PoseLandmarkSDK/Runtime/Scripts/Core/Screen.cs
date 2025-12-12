// Copyright (c) 2021 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using UnityEngine;
using UnityEngine.UI;

namespace Mediapipe.Unity.PoseLandmarkSDK.Core
{
  public class Screen : MonoBehaviour
  {
    [SerializeField] private RawImage _screen;

    private ImageSource _imageSource;

    public Texture texture
    {
      get => _screen.texture;
      set => _screen.texture = value;
    }

    public UnityEngine.Rect uvRect
    {
      set => _screen.uvRect = value;
    }

    public void Initialize(ImageSource imageSource, RunningMode runningMode = RunningMode.Async)
    {
      _imageSource = imageSource;

      Resize(_imageSource.textureWidth, _imageSource.textureHeight);
      Rotate(_imageSource.rotation.Reverse());
      ResetUvRect(RunningMode.Async);
      texture = imageSource.GetCurrentTexture();
    }

    public void Resize(int width, int height)
    {
        var rectTransform = _screen.rectTransform;
        var parent = rectTransform.parent as RectTransform;

        if (parent != null)
        {
             // Determine Visual Dimensions based on Rotation
             float visualWidth = width;
             float visualHeight = height;

             if (_imageSource != null)
             {
                 var rotation = _imageSource.rotation;
                 if (rotation == RotationAngle.Rotation90 || rotation == RotationAngle.Rotation270)
                 {
                     visualWidth = height;
                     visualHeight = width;
                 }
             }

             float widthRatio = parent.rect.width / visualWidth;
             float heightRatio = parent.rect.height / visualHeight;

             // Aspect Draw Mode: FIT (Letterbox)
             // Use Min scale to ensure image fits inside screen.
             float scale = Mathf.Min(widthRatio, heightRatio);

             rectTransform.sizeDelta = new Vector2(width * scale, height * scale);
        }
        else
        {
            _screen.rectTransform.sizeDelta = new Vector2(width, height);
        }
        
        _screen.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _screen.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _screen.rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    public void Rotate(RotationAngle rotationAngle)
    {
      _screen.rectTransform.localEulerAngles = rotationAngle.GetEulerAngles();
    }

    public void ReadSync(Experimental.TextureFrame textureFrame)
    {
      if (!(texture is Texture2D))
      {
        texture = new Texture2D(_imageSource.textureWidth, _imageSource.textureHeight, TextureFormat.RGBA32, false);
        ResetUvRect(RunningMode.Sync);
      }
      textureFrame.CopyTexture(texture);
    }

    private void ResetUvRect(RunningMode runningMode)
    {
      var rect = new UnityEngine.Rect(0, 0, 1, 1);

      if (_imageSource.isVerticallyFlipped && runningMode == RunningMode.Async)
      {
        // In Async mode, we don't need to flip the screen vertically since the image will be copied on CPU.
        rect = FlipVertically(rect);
      }

      if (_imageSource.isFrontFacing)
      {
        // Flip the image (not the screen) horizontally.
        // It should be taken into account that the image will be rotated later.
        var rotation = _imageSource.rotation;

        if (rotation == RotationAngle.Rotation0 || rotation == RotationAngle.Rotation180)
        {
          rect = FlipHorizontally(rect);
        }
        else
        {
          rect = FlipVertically(rect);
        }
      }

      uvRect = rect;
    }

    private UnityEngine.Rect FlipHorizontally(UnityEngine.Rect rect)
    {
      return new UnityEngine.Rect(1 - rect.x, rect.y, -rect.width, rect.height);
    }

    private UnityEngine.Rect FlipVertically(UnityEngine.Rect rect)
    {
      return new UnityEngine.Rect(rect.x, 1 - rect.y, rect.width, -rect.height);
    }
  }
}
