using System;
using UnityEngine;

namespace CatGame.SaveSystem
{
    public static class SavePreviewCapture
    {
        public static byte[] CaptureMainCameraPng(
            int width,
            int height,
            bool verboseLogs = false)
        {
            width = Mathf.Max(64, width);
            height = Mathf.Max(36, height);

            Camera sourceCamera = Camera.main;

            if (sourceCamera == null)
                sourceCamera = UnityEngine.Object.FindObjectOfType<Camera>();

            if (sourceCamera == null)
            {
                if (verboseLogs)
                    Debug.LogWarning("[SavePreviewCapture] Camera not found. Preview was skipped.");

                return null;
            }

            RenderTexture previousTarget = sourceCamera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            float previousAspect = sourceCamera.aspect;

            RenderTexture renderTexture = null;
            Texture2D texture = null;

            try
            {
                renderTexture = RenderTexture.GetTemporary(
                    width,
                    height,
                    24,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB
                );

                renderTexture.name = "SavePreviewCaptureRT";
                renderTexture.filterMode = FilterMode.Bilinear;
                renderTexture.wrapMode = TextureWrapMode.Clamp;

                sourceCamera.targetTexture = renderTexture;
                sourceCamera.aspect = width / (float)height;

                sourceCamera.Render();

                RenderTexture.active = renderTexture;

                texture = new Texture2D(
                    width,
                    height,
                    TextureFormat.RGB24,
                    false,
                    false
                );

                texture.name = "SavePreviewCaptureTexture";
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                texture.Apply(false, false);

                byte[] pngBytes = texture.EncodeToPNG();

                if (verboseLogs)
                {
                    Debug.Log(
                        "[SavePreviewCapture] Preview captured: " +
                        width + "x" + height + ", bytes=" +
                        (pngBytes != null ? pngBytes.Length : 0)
                    );
                }

                return pngBytes;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[SavePreviewCapture] Could not capture save preview. " +
                    exception
                );

                return null;
            }
            finally
            {
                sourceCamera.targetTexture = previousTarget;
                sourceCamera.aspect = previousAspect;
                RenderTexture.active = previousActive;

                if (texture != null)
                    UnityEngine.Object.Destroy(texture);

                if (renderTexture != null)
                    RenderTexture.ReleaseTemporary(renderTexture);
            }
        }
    }
}
