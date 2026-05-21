using UnityEngine;
using UnityEngine.UI;

namespace TraidingIDLE.UI
{
    [DisallowMultipleComponent]
    public sealed class SmoothCircleImageUI : MonoBehaviour
    {
        [SerializeField] private Graphic targetGraphic;
        [SerializeField, Range(0.001f, 0.08f)] private float circleEdgeSoftness = 0.02f;
        [SerializeField] private Shader smoothCircleShader;
        [SerializeField] private string smoothCircleShaderName = "TraidingIDLE/UI/SmoothCircleAvatar";

        private Material _material;
        private Texture _lastTexture;
        private static readonly int CircleSoftnessId = Shader.PropertyToID("_CircleSoftness");

        private void Awake()
        {
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void LateUpdate()
        {
            if (targetGraphic == null)
                targetGraphic = GetComponent<Graphic>();

            var currentTexture = GetCurrentTexture();
            if (_material == null || currentTexture == _lastTexture)
                return;

            Apply();
        }

        private void OnDestroy()
        {
            if (_material == null)
                return;

            if (Application.isPlaying)
                Destroy(_material);
            else
                DestroyImmediate(_material);

            _material = null;
            _lastTexture = null;
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
                return;

            Apply();
        }

        private void Apply()
        {
            if (targetGraphic == null)
                targetGraphic = GetComponent<Graphic>();

            if (targetGraphic == null || !EnsureMaterial())
                return;

            targetGraphic.material = _material;
            _material.SetFloat(CircleSoftnessId, circleEdgeSoftness);
            ApplyTextureSettings();
        }

        private bool EnsureMaterial()
        {
            if (_material != null)
                return true;

            var shader = smoothCircleShader != null
                ? smoothCircleShader
                : Shader.Find(smoothCircleShaderName);
            if (shader == null)
                return false;

            _material = new Material(shader)
            {
                name = "Smooth Circle Image (Runtime)",
                hideFlags = HideFlags.DontSave
            };

            return true;
        }

        private void ApplyTextureSettings()
        {
            var texture = GetCurrentTexture();
            _lastTexture = texture;
            if (texture == null)
                return;

            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
        }

        private Texture GetCurrentTexture()
        {
            if (targetGraphic is Image image && image.sprite != null)
                return image.sprite.texture;

            if (targetGraphic is RawImage rawImage)
                return rawImage.texture;

            return null;
        }
    }
}
