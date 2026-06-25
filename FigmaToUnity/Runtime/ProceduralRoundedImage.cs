using System;
using UnityEngine;
using UnityEngine.UI;

namespace FigmaToUnity.Runtime
{
    [AddComponentMenu("UI/Procedural Rounded Image")]
    public sealed class ProceduralRoundedImage : Image
    {
        private const string ShaderName = "LongGames/UI/Procedural Rounded Image";

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int UseUiAlphaClipId = Shader.PropertyToID("_UseUIAlphaClip");
        private static Material? s_sharedMaterial;

        [SerializeField] private Vector4 m_CornerRadii;
        [SerializeField] private float m_StrokeWidth;
        [SerializeField] private Color m_StrokeColor = Color.black;
        [SerializeField] private float m_FalloffDistance = 0.5f;

        public Vector4 CornerRadii
        {
            get => m_CornerRadii;
            set
            {
                Vector4 clamped = Vector4.Max(value, Vector4.zero);
                if (m_CornerRadii == clamped)
                {
                    return;
                }

                m_CornerRadii = clamped;
                SetVerticesDirty();
            }
        }

        public float StrokeWidth
        {
            get => m_StrokeWidth;
            set
            {
                float clamped = Mathf.Max(0f, value);
                if (Mathf.Approximately(m_StrokeWidth, clamped))
                {
                    return;
                }

                m_StrokeWidth = clamped;
                SetVerticesDirty();
            }
        }

        public Color StrokeColor
        {
            get => m_StrokeColor;
            set
            {
                if (m_StrokeColor == value)
                {
                    return;
                }

                m_StrokeColor = value;
                SetVerticesDirty();
            }
        }

        public float FalloffDistance
        {
            get => m_FalloffDistance;
            set
            {
                float clamped = Mathf.Max(0.01f, value);
                if (Mathf.Approximately(m_FalloffDistance, clamped))
                {
                    return;
                }

                m_FalloffDistance = clamped;
                SetVerticesDirty();
            }
        }

        public override Material defaultMaterial => SharedMaterial;

        public override Material GetModifiedMaterial(Material baseMaterial)
        {
            Material material = base.GetModifiedMaterial(baseMaterial);
            ConfigureAlphaClip(material);
            return material;
        }

        public override Texture mainTexture
        {
            get
            {
                Sprite activeSprite = overrideSprite != null ? overrideSprite : sprite;
                if (activeSprite != null && activeSprite.texture != null)
                {
                    return activeSprite.texture;
                }

                Material activeMaterial = material;
                if (activeMaterial != null && activeMaterial.HasProperty(MainTexId) && activeMaterial.mainTexture != null)
                {
                    return activeMaterial.mainTexture;
                }

                return s_WhiteTexture;
            }
        }

        private static Material SharedMaterial
        {
            get
            {
                if (s_sharedMaterial != null)
                {
                    return s_sharedMaterial;
                }

                Shader shader = Shader.Find(ShaderName);
                if (shader == null)
                {
                    throw new InvalidOperationException($"Shader '{ShaderName}' was not found.");
                }

                s_sharedMaterial = new Material(shader)
                {
                    name = "Procedural Rounded Image [Shared]"
                };
                ConfigureAlphaClip(s_sharedMaterial);
                return s_sharedMaterial;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureCanvasShaderChannels();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            EnsureCanvasShaderChannels();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            m_CornerRadii = ClampCornerRadii(m_CornerRadii, GetPixelAdjustedRect().size);
        }

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            base.OnPopulateMesh(toFill);

            Rect rect = GetPixelAdjustedRect();
            Vector2 size = rect.size;
            Vector4 cornerRadii = ClampCornerRadii(m_CornerRadii, size);
            float strokeWidth = Mathf.Clamp(m_StrokeWidth, 0f, Mathf.Min(size.x, size.y) * 0.5f);
            float falloff = Mathf.Max(0.01f, m_FalloffDistance);

            UIVertex vertex = default;
            for (int i = 0; i < toFill.currentVertCount; i++)
            {
                toFill.PopulateUIVertex(ref vertex, i);

                float normalizedX = size.x > 0.001f ? Mathf.Clamp01((vertex.position.x - rect.xMin) / size.x) : 0f;
                float normalizedY = size.y > 0.001f ? Mathf.Clamp01((vertex.position.y - rect.yMin) / size.y) : 0f;

                vertex.uv1 = new Vector4(normalizedX, normalizedY, size.x, size.y);
                vertex.uv2 = cornerRadii;
                vertex.normal = new Vector3(strokeWidth, falloff, 0f);
                vertex.tangent = QualitySettings.activeColorSpace == ColorSpace.Linear
                    ? (Vector4)m_StrokeColor.linear
                    : (Vector4)m_StrokeColor;

                toFill.SetUIVertex(vertex, i);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            m_CornerRadii = ClampCornerRadii(m_CornerRadii, GetPixelAdjustedRect().size);
            m_StrokeWidth = Mathf.Max(0f, m_StrokeWidth);
            m_FalloffDistance = Mathf.Max(0.01f, m_FalloffDistance);
            EnsureCanvasShaderChannels();
        }
#endif

        private void EnsureCanvasShaderChannels()
        {
            if (canvas == null)
            {
                return;
            }

            AdditionalCanvasShaderChannels channels = canvas.additionalShaderChannels;
            channels |= AdditionalCanvasShaderChannels.TexCoord1;
            channels |= AdditionalCanvasShaderChannels.TexCoord2;
            channels |= AdditionalCanvasShaderChannels.Normal;
            channels |= AdditionalCanvasShaderChannels.Tangent;
            canvas.additionalShaderChannels = channels;
        }

        private static Vector4 ClampCornerRadii(Vector4 cornerRadii, Vector2 size)
        {
            cornerRadii = Vector4.Max(cornerRadii, Vector4.zero);
            float maxRadius = Mathf.Max(0f, Mathf.Min(size.x, size.y));
            cornerRadii = Vector4.Min(cornerRadii, Vector4.one * maxRadius);

            float scaleFactor = Mathf.Min(
                Mathf.Min(
                    Mathf.Min(
                        Mathf.Min(
                            SafeRatio(size.x, cornerRadii.x + cornerRadii.y),
                            SafeRatio(size.x, cornerRadii.z + cornerRadii.w)),
                        SafeRatio(size.y, cornerRadii.x + cornerRadii.w)),
                    SafeRatio(size.y, cornerRadii.z + cornerRadii.y)),
                1f);

            return cornerRadii * scaleFactor;
        }

        private static float SafeRatio(float size, float radiiSum)
        {
            if (radiiSum <= 0.0001f)
            {
                return 1f;
            }

            return size / radiiSum;
        }

        private static void ConfigureAlphaClip(Material material)
        {
            if (material == null || !material.HasProperty(UseUiAlphaClipId))
            {
                return;
            }

            material.SetFloat(UseUiAlphaClipId, 1f);
            material.EnableKeyword("UNITY_UI_ALPHACLIP");
        }
    }
}
