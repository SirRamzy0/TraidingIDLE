using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

#if Authorization_yg
using YG;
#endif

namespace TraidingIDLE.UI
{
    public sealed class YandexPlayerAvatarUI : MonoBehaviour
    {
        private static readonly Dictionary<string, Texture2D> TextureCache = new();

        [Header("Refs")]
        [SerializeField] private Image maskImage;
        [SerializeField] private Image avatarImage;

        [Header("Mask")]
        [SerializeField] private bool addMaskToThisObject = true;
        [SerializeField] private bool showMaskGraphic = true;
        [SerializeField, Min(0f)] private float avatarInset = 6f;
        [SerializeField] private bool useSmoothCircleClip = true;
        [SerializeField, Range(0.001f, 0.08f)] private float circleEdgeSoftness = 0.02f;
        [SerializeField] private Shader smoothCircleShader;
        [SerializeField] private string smoothCircleShaderName = "TraidingIDLE/UI/SmoothCircleAvatar";
        [SerializeField] private bool preferLargeYandexAvatar = true;

        [Header("Loading")]
        [SerializeField] private bool hideAvatarWhenUnavailable = true;
        [SerializeField] private bool logLoadErrors;

        private Coroutine _loadRoutine;
        private string _currentUrl;
        private Vector2Int _currentTextureSize;
        private Vector2 _lastMaskSize;
        private Material _smoothCircleMaterial;

        private static readonly int CircleSoftnessId = Shader.PropertyToID("_CircleSoftness");

        private void Awake()
        {
            EnsureReferences();
            EnsureMask();
            SetAvatarVisible(false);
        }

        private void OnEnable()
        {
#if Authorization_yg
            YG2.onGetSDKData += Refresh;
#endif
            Refresh();
        }

        private void OnDisable()
        {
#if Authorization_yg
            YG2.onGetSDKData -= Refresh;
#endif

            if (_loadRoutine != null)
            {
                StopCoroutine(_loadRoutine);
                _loadRoutine = null;
            }
        }

        private void LateUpdate()
        {
            if (avatarImage == null || maskImage == null || _currentTextureSize.x <= 0 || _currentTextureSize.y <= 0)
                return;

            UpdateSmoothCircleMaterial();

            var maskRect = maskImage.rectTransform.rect.size;
            if ((maskRect - _lastMaskSize).sqrMagnitude <= 0.01f)
                return;

            FitAvatarToMask();
        }

        private void OnDestroy()
        {
            if (_smoothCircleMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(_smoothCircleMaterial);
            else
                DestroyImmediate(_smoothCircleMaterial);

            _smoothCircleMaterial = null;
        }

        private void Refresh()
        {
            EnsureReferences();
            EnsureMask();

            var url = GetPlayerPhotoUrl();
            if (string.IsNullOrWhiteSpace(url))
            {
                _currentUrl = null;
                SetAvatarVisible(!hideAvatarWhenUnavailable && avatarImage != null && avatarImage.sprite != null);
                return;
            }

            if (url == _currentUrl && avatarImage != null && avatarImage.sprite != null)
            {
                SetAvatarVisible(true);
                return;
            }

            _currentUrl = url;

            if (TextureCache.TryGetValue(url, out var cached) && cached != null)
            {
                ApplyTexture(cached);
                return;
            }

            if (_loadRoutine != null)
                StopCoroutine(_loadRoutine);

            _loadRoutine = StartCoroutine(LoadAvatar(url));
        }

        private string GetPlayerPhotoUrl()
        {
#if Authorization_yg
            if (!YG2.player.auth)
                return null;

            var photo = YG2.player.photo;
            if (string.IsNullOrWhiteSpace(photo) || photo == "null" || photo == "no data")
                return null;

            return preferLargeYandexAvatar ? ToLargeYandexAvatarUrl(photo) : photo;
#else
            return null;
#endif
        }

        private static string ToLargeYandexAvatarUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            return url
                .Replace("islands-34", "islands-200")
                .Replace("islands-50", "islands-200")
                .Replace("islands-68", "islands-200")
                .Replace("islands-75", "islands-200")
                .Replace("islands-100", "islands-200");
        }

        private IEnumerator LoadAvatar(string url)
        {
            using var request = UnityWebRequestTexture.GetTexture(url);
            yield return request.SendWebRequest();

            _loadRoutine = null;

            if (request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.ProtocolError
                || request.result == UnityWebRequest.Result.DataProcessingError)
            {
                if (logLoadErrors)
                    Debug.LogWarning($"Player avatar load failed: {request.error}", this);

                SetAvatarVisible(!hideAvatarWhenUnavailable && avatarImage != null && avatarImage.sprite != null);
                yield break;
            }

            var texture = DownloadHandlerTexture.GetContent(request);
            if (texture == null)
            {
                SetAvatarVisible(!hideAvatarWhenUnavailable && avatarImage != null && avatarImage.sprite != null);
                yield break;
            }

            TextureCache[url] = texture;
            ApplyTexture(texture);
        }

        private void ApplyTexture(Texture2D texture)
        {
            EnsureReferences();
            if (avatarImage == null || texture == null)
                return;

            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            var rect = new Rect(0f, 0f, texture.width, texture.height);
            var sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f));

            avatarImage.sprite = sprite;
            avatarImage.color = Color.white;
            avatarImage.raycastTarget = false;
            avatarImage.type = Image.Type.Simple;
            avatarImage.preserveAspect = true;
            ApplyAvatarMaterial();

            _currentTextureSize = new Vector2Int(texture.width, texture.height);
            FitAvatarToMask();
            SetAvatarVisible(true);
        }

        private void FitAvatarToMask()
        {
            if (maskImage == null || avatarImage == null || _currentTextureSize.x <= 0 || _currentTextureSize.y <= 0)
                return;

            var targetSize = maskImage.rectTransform.rect.size;
            targetSize.x = Mathf.Max(1f, targetSize.x - avatarInset * 2f);
            targetSize.y = Mathf.Max(1f, targetSize.y - avatarInset * 2f);

            var targetAspect = targetSize.x / targetSize.y;
            var textureAspect = _currentTextureSize.x / (float)_currentTextureSize.y;

            if (textureAspect > targetAspect)
                targetSize.x = targetSize.y * textureAspect;
            else
                targetSize.y = targetSize.x / textureAspect;

            var rect = avatarImage.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = targetSize;

            _lastMaskSize = maskImage.rectTransform.rect.size;
        }

        private void EnsureReferences()
        {
            if (maskImage == null)
                maskImage = GetComponent<Image>();

            if (avatarImage == null)
                avatarImage = FindOrCreateAvatarImage();
        }

        private Image FindOrCreateAvatarImage()
        {
            var child = transform.Find("Yandex Player Avatar");
            if (child != null && child.TryGetComponent<Image>(out var existing))
                return existing;

            var go = new GameObject("Yandex Player Avatar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            go.layer = gameObject.layer;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.maskable = true;
            image.enabled = false;
            ApplyAvatarMaterial(image);

            return image;
        }

        private void EnsureMask()
        {
            if (maskImage != null && useSmoothCircleClip && EnsureSmoothCircleMaterial())
            {
                var existingMask = maskImage.GetComponent<Mask>();
                if (existingMask != null)
                    existingMask.enabled = false;

                return;
            }

            if (!addMaskToThisObject || maskImage == null)
                return;

            var mask = maskImage.GetComponent<Mask>();
            if (mask == null)
                mask = maskImage.gameObject.AddComponent<Mask>();

            mask.enabled = true;
            mask.showMaskGraphic = showMaskGraphic;
        }

        private void ApplyAvatarMaterial()
        {
            ApplyAvatarMaterial(avatarImage);
        }

        private void ApplyAvatarMaterial(Image image)
        {
            if (image == null)
                return;

            image.material = useSmoothCircleClip && EnsureSmoothCircleMaterial()
                ? _smoothCircleMaterial
                : null;
        }

        private bool EnsureSmoothCircleMaterial()
        {
            if (_smoothCircleMaterial != null)
                return true;

            var shader = smoothCircleShader != null
                ? smoothCircleShader
                : Shader.Find(smoothCircleShaderName);
            if (shader == null)
                return false;

            _smoothCircleMaterial = new Material(shader)
            {
                name = "Player Avatar Smooth Circle (Runtime)",
                hideFlags = HideFlags.DontSave
            };

            UpdateSmoothCircleMaterial();
            return true;
        }

        private void UpdateSmoothCircleMaterial()
        {
            if (_smoothCircleMaterial == null)
                return;

            _smoothCircleMaterial.SetFloat(CircleSoftnessId, circleEdgeSoftness);
        }

        private void SetAvatarVisible(bool visible)
        {
            if (avatarImage != null)
                avatarImage.enabled = visible;
        }
    }
}
