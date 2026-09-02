Shader "Skybox/AsemicGlyphs"
{
    Properties
    {
        _GlyphAtlas ("Glyph Atlas (8x8 grid, black on white)", 2D) = "white" {}
        _InvertInk ("Invert Ink (white glyphs on black)", Float) = 0
        _AtlasCols ("Atlas Columns", Float) = 8
        _AtlasRows ("Atlas Rows", Float) = 8
        [Space]
        _Seed ("Seed", Float) = 1337
        _GridResolution ("Grid Resolution (per cube face)", Float) = 8
        _GlyphAngularSize ("Glyph Angular Size (degrees)", Float) = 2.5
        _GlyphSizeVariation ("Glyph Size Variation", Float) = 0.6
        _GlyphRotationAmount ("Glyph Rotation (radians)", Float) = 3.14159
        _DriftSpeed ("Drift Speed", Float) = 0.03
        _DriftAmount ("Drift Amount", Float) = 0.5
        _RotationDrift ("Rotation Drift", Float) = 0.05
        [Space]
        _TimeOfDay ("Time of Day (0=day, 1=afternoon, 2=night)", Float) = 0
        _AutoCycle ("Auto Cycle", Float) = 0
        _CycleSpeed ("Cycle Speed", Float) = 0.02
        [Space]
        _DayTop ("Day Top Color", Color) = (0.55, 0.68, 0.82, 1)
        _DayHorizon ("Day Horizon Color", Color) = (0.93, 0.90, 0.82, 1)
        _DayBottom ("Day Bottom Color", Color) = (0.96, 0.94, 0.88, 1)
        _DayGlyphColor ("Day Glyph Color", Color) = (0.10, 0.09, 0.08, 1)
        _DayGlyphOpacity ("Day Glyph Opacity", Float) = 0.35
        [Space]
        _AfternoonTop ("Afternoon Top Color", Color) = (0.42, 0.52, 0.72, 1)
        _AfternoonHorizon ("Afternoon Horizon Color", Color) = (0.98, 0.66, 0.38, 1)
        _AfternoonBottom ("Afternoon Bottom Color", Color) = (0.90, 0.62, 0.55, 1)
        _AfternoonGlyphColor ("Afternoon Glyph Color", Color) = (0.18, 0.10, 0.07, 1)
        _AfternoonGlyphOpacity ("Afternoon Glyph Opacity", Float) = 0.55
        [Space]
        _NightTop ("Night Top Color", Color) = (0.03, 0.04, 0.10, 1)
        _NightHorizon ("Night Horizon Color", Color) = (0.10, 0.09, 0.22, 1)
        _NightBottom ("Night Bottom Color", Color) = (0.05, 0.05, 0.12, 1)
        _NightGlyphColor ("Night Glyph Color", Color) = (0.80, 0.84, 1.00, 1)
        _NightGlyphOpacity ("Night Glyph Opacity", Float) = 0.8
        _GlowStrength ("Night Glow Strength", Float) = 0.25
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_GlyphAtlas);
            SAMPLER(sampler_GlyphAtlas);

            CBUFFER_START(UnityPerMaterial)
                float _InvertInk;
                float _AtlasCols;
                float _AtlasRows;
                float _Seed;
                float _GridResolution;
                float _GlyphAngularSize;
                float _GlyphSizeVariation;
                float _GlyphRotationAmount;
                float _DriftSpeed;
                float _DriftAmount;
                float _RotationDrift;
                float _TimeOfDay;
                float _AutoCycle;
                float _CycleSpeed;
                float4 _DayTop;
                float4 _DayHorizon;
                float4 _DayBottom;
                float4 _DayGlyphColor;
                float _DayGlyphOpacity;
                float4 _AfternoonTop;
                float4 _AfternoonHorizon;
                float4 _AfternoonBottom;
                float4 _AfternoonGlyphColor;
                float _AfternoonGlyphOpacity;
                float4 _NightTop;
                float4 _NightHorizon;
                float4 _NightBottom;
                float4 _NightGlyphColor;
                float _NightGlyphOpacity;
                float _GlowStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 ray : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.ray = TransformObjectToWorld(input.positionOS.xyz) - _WorldSpaceCameraPos;
                return o;
            }

            uint GlyphHash(uint x)
            {
                x = x * 747796405u + 2891336453u;
                x = ((x >> 16u) ^ x) * 0x45d9f3bu;
                x = ((x >> 16u) ^ x) * 0x45d9f3bu;
                x = (x >> 16u) ^ x;
                return x;
            }

            float GlyphRand(uint seed)
            {
                return GlyphHash(seed) * (1.0 / 4294967295.0);
            }

            float3 GlyphRotateAroundAxis(float3 v, float3 axis, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return v * c + cross(axis, v) * s + axis * dot(axis, v) * (1.0 - c);
            }

            float3 GlyphFaceUVToDir(int face, float2 p)
            {
                if (face == 0) return normalize(float3( 1.0,  p.y,  p.x));
                if (face == 1) return normalize(float3(-1.0,  p.y, -p.x));
                if (face == 2) return normalize(float3( p.x,  1.0,  p.y));
                if (face == 3) return normalize(float3( p.x, -1.0, -p.y));
                if (face == 4) return normalize(float3( p.x,  p.y,  1.0));
                return            normalize(float3(-p.x,  p.y, -1.0));
            }

            void GlyphDirToFace(float3 dir, out int face, out float2 p)
            {
                float3 ad = abs(dir);
                if (ad.x >= ad.y && ad.x >= ad.z)
                {
                    if (dir.x >= 0.0) { face = 0; p = float2(dir.z, dir.y) / ad.x; }
                    else              { face = 1; p = float2(-dir.z, dir.y) / ad.x; }
                }
                else if (ad.y >= ad.z)
                {
                    if (dir.y >= 0.0) { face = 2; p = float2(dir.x, dir.z) / ad.y; }
                    else              { face = 3; p = float2(dir.x, -dir.z) / ad.y; }
                }
                else
                {
                    if (dir.z >= 0.0) { face = 4; p = float2(dir.x, dir.y) / ad.z; }
                    else              { face = 5; p = float2(-dir.x, dir.y) / ad.z; }
                }
            }

            float SampleGlyph(int face, int ci, int cj, int res, float3 dir)
            {
                uint seed = GlyphHash(uint(face) * 1000003u + uint(ci) * 997u + uint(cj) * 31u + (uint)floor(max(_Seed, 0.0)) + 1u);

                float jx = GlyphRand(seed);
                float jy = GlyphRand(seed + 1u);
                float sizeVar = GlyphRand(seed + 2u);
                float rot = (GlyphRand(seed + 3u) - 0.5) * _GlyphRotationAmount;
                int cols = max(1, (int)_AtlasCols);
                int rows = max(1, (int)_AtlasRows);
                float gidx = GlyphRand(seed + 4u) * (float)(cols * rows - 1);
                float driftA = (GlyphRand(seed + 5u) - 0.5) * 2.0;
                float driftB = (GlyphRand(seed + 6u) - 0.5) * 2.0;
                float rotDrift = (GlyphRand(seed + 7u) - 0.5) * 2.0;

                float2 pGlyph = ((float2(ci, cj) + float2(jx, jy)) / (float)res) * 2.0 - 1.0;
                float3 gdir = GlyphFaceUVToDir(face, pGlyph);

                if (_DriftSpeed > 0.001)
                {
                    float3 driftAxis = normalize(cross(gdir, gdir + float3(0.37, 0.73, 0.51)));
                    gdir = GlyphRotateAroundAxis(gdir, driftAxis, _Time.y * _DriftSpeed * driftA * _DriftAmount);
                }

                float3 up = abs(gdir.y) > 0.9 ? float3(1.0, 0.0, 0.0) : float3(0.0, 1.0, 0.0);
                float3 t1 = normalize(cross(gdir, up));
                float3 t2 = cross(gdir, t1);
                float2 off = float2(dot(dir, t1), dot(dir, t2));

                float baseSize = radians(_GlyphAngularSize);
                float size = baseSize * lerp(1.0 - _GlyphSizeVariation * 0.5, 1.0 + _GlyphSizeVariation * 0.5, sizeVar);
                float radius = sin(size);
                float dist = length(off);
                if (dist > radius) return 0.0;

                float angle = rot + _Time.y * _RotationDrift * rotDrift;
                float ca = cos(angle);
                float sa = sin(angle);
                float2 local = float2(off.x * ca + off.y * sa, -off.x * sa + off.y * ca) / radius;

                int gcol = (int)gidx % cols;
                int grow = (int)gidx / cols;
                float2 atlasUV = (float2(gcol, grow) + 0.085 + 0.83 * (local * 0.5 + 0.5)) * float2(1.0 / (float)cols, 1.0 / (float)rows);

                float r = SAMPLE_TEXTURE2D(_GlyphAtlas, sampler_GlyphAtlas, atlasUV).r;
                float ink = _InvertInk > 0.5 ? r : (1.0 - r);
                float edge = 1.0 - smoothstep(0.8, 1.0, dist / radius);
                return ink * edge;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.ray);
                // cosine ping-pong: day -> afternoon -> night -> afternoon -> day, no hard snap
                float t = _AutoCycle > 0.5 ? (1.0 - cos(3.14159265358979323846 * frac(_Time.y * _CycleSpeed))) : _TimeOfDay;

                // smoothstep crossfades between states, normalized so colors never overshoot
                float dayW = 1.0 - smoothstep(0.0, 0.7, t);
                float aftW = saturate(1.0 - abs(t - 1.0));
                float ngtW = smoothstep(1.3, 2.0, t);
                float wSum = dayW + aftW + ngtW;
                dayW /= wSum;
                aftW /= wSum;
                ngtW /= wSum;

                float3 top = _DayTop.rgb * dayW + _AfternoonTop.rgb * aftW + _NightTop.rgb * ngtW;
                float3 horizon = _DayHorizon.rgb * dayW + _AfternoonHorizon.rgb * aftW + _NightHorizon.rgb * ngtW;
                float3 bottom = _DayBottom.rgb * dayW + _AfternoonBottom.rgb * aftW + _NightBottom.rgb * ngtW;
                float3 glyphColor = _DayGlyphColor.rgb * dayW + _AfternoonGlyphColor.rgb * aftW + _NightGlyphColor.rgb * ngtW;
                float glyphOpacity = _DayGlyphOpacity * dayW + _AfternoonGlyphOpacity * aftW + _NightGlyphOpacity * ngtW;
                float glow = _GlowStrength * ngtW;

                float h = dir.y;
                float3 col = lerp(bottom, horizon, smoothstep(-0.25, 0.05, h));
                col = lerp(col, top, smoothstep(0.05, 0.6, h));

                int face;
                float2 p;
                GlyphDirToFace(dir, face, p);
                float2 uv = p * 0.5 + 0.5;
                int res = max(2, (int)_GridResolution);

                float inkTotal = 0.0;
                for (int di = -1; di <= 1; di++)
                {
                    for (int dj = -1; dj <= 1; dj++)
                    {
                        int ci = ((int)(uv.x * (float)res) + di + res) % res;
                        int cj = ((int)(uv.y * (float)res) + dj + res) % res;
                        inkTotal += SampleGlyph(face, ci, cj, res, dir);
                    }
                }

                col = lerp(col, glyphColor, saturate(inkTotal) * glyphOpacity);
                col += glyphColor * saturate(inkTotal) * glow;

                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}