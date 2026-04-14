using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace Sol.Outline
{
    /// URP ScriptableRendererFeature that renders per-object silhouette outlines.
    /// Add this to the URP Renderer Data asset (e.g. PC_Renderer).
    public class SolOutlineRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class OutlineSettings
        {
            [Tooltip("Maximum outline width in pixels (caps per-object width)")]
            public float maxOutlineWidth = 20.0f;
        }

        public OutlineSettings settings = new OutlineSettings();

        private Material _maskMaterial;
        private Material _fullscreenMaterial;
        private OutlineMaskPass _maskPass;
        private OutlineCompositePass _compositePass;

        // -- Static registration list --
        private static readonly List<OutlineComponent> s_activeOutlines = new List<OutlineComponent>();
        internal static float s_maxOutlineWidth = 20f;

        public static void Register(OutlineComponent c)
        {
            if (!s_activeOutlines.Contains(c))
                s_activeOutlines.Add(c);
        }

        public static void Unregister(OutlineComponent c)
        {
            s_activeOutlines.Remove(c);
        }

        public static IReadOnlyList<OutlineComponent> ActiveOutlines => s_activeOutlines;

        // -- Feature lifecycle --
        public override void Create()
        {
            _maskMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Sol/OutlineMask"));
            _fullscreenMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Sol/OutlineFullscreen"));

            _maskPass = new OutlineMaskPass(_maskMaterial);
            _maskPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

            _compositePass = new OutlineCompositePass(_fullscreenMaterial);
            _compositePass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_maskMaterial == null || _fullscreenMaterial == null) return;
            PruneInactiveOutlines();
            if (!HasRenderableOutlines()) return;

            _compositePass.maxOutlineWidth = settings.maxOutlineWidth;
            s_maxOutlineWidth = settings.maxOutlineWidth;

            renderer.EnqueuePass(_maskPass);
            renderer.EnqueuePass(_compositePass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_maskMaterial);
            CoreUtils.Destroy(_fullscreenMaterial);
        }

        private static void PruneInactiveOutlines()
        {
            for (int i = s_activeOutlines.Count - 1; i >= 0; i--)
            {
                OutlineComponent outline = s_activeOutlines[i];
                if (outline == null || !outline.isActiveAndEnabled || !outline.IsOutlineActive)
                    s_activeOutlines.RemoveAt(i);
            }
        }

        private static bool HasRenderableOutlines()
        {
            for (int i = 0; i < s_activeOutlines.Count; i++)
            {
                OutlineComponent outline = s_activeOutlines[i];
                if (outline == null || !outline.isActiveAndEnabled || !outline.IsOutlineActive)
                    continue;

                Renderer[] renderers = outline.GetRenderers();
                if (renderers == null || renderers.Length == 0)
                    continue;

                return true;
            }

            return false;
        }

        // ----------------------------------------------------------------------
        // Pass 1: Render outlined objects into a mask RT using Unsafe pass
        //         (needed for cmd.DrawRenderer on specific objects)
        // ----------------------------------------------------------------------
        private class OutlineMaskPass : ScriptableRenderPass
        {
            private readonly Material _material;
            private static readonly int s_outlineColorId = Shader.PropertyToID("_OutlineColor");

            public OutlineMaskPass(Material material)
            {
                _material = material;
                profilingSampler = new ProfilingSampler("Sol_OutlineMask");
            }

            private class MaskPassData
            {
                internal Material material;
                internal TextureHandle maskTexture;
                internal TextureHandle depthTexture;
                internal List<(Renderer[] renderers, Color color, bool priority, float encodedAlpha)> drawCalls;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                SolOutlineMaskData maskData = frameData.GetOrCreate<SolOutlineMaskData>();
                maskData.maskTexture = TextureHandle.nullHandle;

                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();

                // Create the mask render texture
                var desc = new TextureDesc(cameraData.cameraTargetDescriptor.width,
                                           cameraData.cameraTargetDescriptor.height);
                desc.colorFormat = GraphicsFormat.R8G8B8A8_UNorm;
                desc.depthBufferBits = DepthBits.None;
                desc.name = "_SolOutlineMask";
                desc.clearBuffer = true;
                desc.clearColor = Color.clear;
                TextureHandle maskTex = renderGraph.CreateTexture(desc);

                float maxWidth = SolOutlineRendererFeature.s_maxOutlineWidth;

                // Collect draw calls, sorted: always-visible first, hover last.
                // Within each group: non-priority before priority.
                // Hover mask pixels overwrite always-visible ones so the
                // fullscreen shader detects group transitions correctly.
                var drawCalls = new List<(Renderer[] renderers, Color color, bool priority, float encodedAlpha)>();
                for (int i = 0; i < s_activeOutlines.Count; i++)
                {
                    var outline = s_activeOutlines[i];
                    if (outline == null || !outline.IsOutlineActive) continue;
                    var renderers = outline.GetRenderers();
                    if (renderers == null || renderers.Length == 0) continue;
                    // Encode width + group flag in alpha:
                    //   always-visible ? alpha in (0, 0.49]
                    //   hover/transient ? alpha in (0.5, 1.0]
                    float norm = Mathf.Clamp(outline.outlineWidth / maxWidth, 0.02f, 1f);
                    float encodedAlpha = outline.alwaysVisible
                        ? norm * 0.49f
                        : 0.51f + norm * 0.49f;
                    drawCalls.Add((renderers, outline.outlineColor, outline.priority, encodedAlpha));
                }

                if (drawCalls.Count == 0) return;

                // Sort: always-visible (alpha <= 0.5) before hover (alpha > 0.5),
                // then non-priority before priority within each group
                drawCalls.Sort((a, b) =>
                {
                    bool aHover = a.encodedAlpha > 0.5f;
                    bool bHover = b.encodedAlpha > 0.5f;
                    if (aHover != bHover) return aHover ? 1 : -1;
                    if (a.priority != b.priority) return a.priority ? 1 : -1;
                    return 0;
                });

                using (var builder = renderGraph.AddUnsafePass<MaskPassData>("Sol_OutlineMask", out var passData))
                {
                    passData.material = _material;
                    passData.maskTexture = maskTex;
                    passData.depthTexture = resourceData.activeDepthTexture;
                    passData.drawCalls = drawCalls;

                    builder.UseTexture(maskTex, AccessFlags.Write);
                    builder.UseTexture(resourceData.activeDepthTexture, AccessFlags.Read);

                    builder.SetRenderFunc((MaskPassData data, UnsafeGraphContext ctx) =>
                    {
                        var cmd = ctx.cmd;

                        // Set render target: mask color + camera depth for Z-testing
                        cmd.SetRenderTarget(data.maskTexture, data.depthTexture);
                        cmd.ClearRenderTarget(false, true, Color.clear);

                        // Draw order (single pass, pre-sorted):
                        //   always-visible first ? hover last (hover overwrites)
                        //   within each group: non-priority before priority
                        // Priority entries use shader pass 1 (ZTest Always),
                        // non-priority use pass 0 (ZTest LEqual).
                        foreach (var (renderers, color, priority, widthNorm) in data.drawCalls)
                        {
                            int shaderPass = priority ? 1 : 0;
                            cmd.SetGlobalColor(s_outlineColorId, new Color(color.r, color.g, color.b, widthNorm));
                            foreach (var rend in renderers)
                            {
                                if (rend == null) continue;
                                for (int sub = 0; sub < rend.sharedMaterials.Length; sub++)
                                    cmd.DrawRenderer(rend, data.material, sub, shaderPass);
                            }
                        }
                    });
                }

                // Store the mask texture handle for the composite pass to pick up
                maskData.maskTexture = maskTex;
            }
        }

        // ----------------------------------------------------------------------
        // Pass 2: Full-screen blit that composites outline onto scene
        // ----------------------------------------------------------------------
        private class OutlineCompositePass : ScriptableRenderPass
        {
            private readonly Material _material;
            internal float maxOutlineWidth = 20.0f;

            private static readonly int s_blitTexId = Shader.PropertyToID("_BlitTexture");
            private static readonly int s_maskTexId = Shader.PropertyToID("_MaskTex");
            private static readonly int s_maxWidthId = Shader.PropertyToID("_MaxOutlineWidth");
            private static readonly int s_blitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
            private static readonly int s_maskTexelSizeId = Shader.PropertyToID("_MaskTex_TexelSize");

            public OutlineCompositePass(Material material)
            {
                _material = material;
                profilingSampler = new ProfilingSampler("Sol_OutlineComposite");
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var maskData = frameData.GetOrCreate<SolOutlineMaskData>();
                if (!maskData.maskTexture.IsValid()) return;

                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();

                int texWidth  = cameraData.cameraTargetDescriptor.width;
                int texHeight = cameraData.cameraTargetDescriptor.height;

                // Copy current screen
                TextureHandle source = resourceData.activeColorTexture;
                var desc = renderGraph.GetTextureDesc(resourceData.cameraColor);
                desc.name = "_SolCopiedColor";
                desc.clearBuffer = false;
                TextureHandle copiedColor = renderGraph.CreateTexture(desc);
                renderGraph.AddBlitPass(source, copiedColor, Vector2.one, Vector2.zero, passName: "Sol_CopyColor");

                // Use unsafe pass to set globals and blit
                using (var builder = renderGraph.AddUnsafePass<CompositePassData>("Sol_OutlineComposite", out var passData))
                {
                    passData.material = _material;
                    passData.copiedColor = copiedColor;
                    passData.maskTexture = maskData.maskTexture;
                    passData.destination = resourceData.activeColorTexture;
                    passData.maxOutlineWidth = maxOutlineWidth;
                    passData.texelSize = new Vector4(1f / texWidth, 1f / texHeight, texWidth, texHeight);

                    builder.UseTexture(copiedColor, AccessFlags.Read);
                    builder.UseTexture(maskData.maskTexture, AccessFlags.Read);
                    builder.UseTexture(resourceData.activeColorTexture, AccessFlags.Write);

                    builder.SetRenderFunc((CompositePassData data, UnsafeGraphContext ctx) =>
                    {
                        var cmd = ctx.cmd;
                        var mpb = new MaterialPropertyBlock();
                        mpb.SetTexture(s_blitTexId, data.copiedColor);
                        mpb.SetTexture(s_maskTexId, data.maskTexture);
                        mpb.SetFloat(s_maxWidthId, data.maxOutlineWidth);
                        mpb.SetVector(s_blitScaleBiasId, new Vector4(1, 1, 0, 0));
                        mpb.SetVector(s_maskTexelSizeId, data.texelSize);

                        cmd.SetRenderTarget(data.destination);
                        cmd.DrawProcedural(Matrix4x4.identity, data.material, 0,
                            MeshTopology.Triangles, 3, 1, mpb);
                    });
                }
            }

            private class CompositePassData
            {
                internal Material material;
                internal TextureHandle copiedColor;
                internal TextureHandle maskTexture;
                internal TextureHandle destination;
                internal float maxOutlineWidth;
                internal Vector4 texelSize;
            }
        }

        // -- Shared data between passes ----------------------------------------
        private class SolOutlineMaskData : ContextItem
        {
            public TextureHandle maskTexture;

            public override void Reset()
            {
                maskTexture = TextureHandle.nullHandle;
            }
        }
    }
}
