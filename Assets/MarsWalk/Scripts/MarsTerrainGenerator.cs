using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gera um terreno marciano procedural (crateras, relevo, um "delta" elevado e
/// encostas rochosas). Use-o enquanto não importa um DTM real (ver README).
/// </summary>
public static class MarsTerrainGenerator
{
    struct Crater { public float x, z, r, depth; }

    /// <param name="flattenSpots">x, z (mundo), raio — áreas planas para bases/estações</param>
    public static Terrain Create(int seed, float size, float maxHeight, List<Vector3> flattenSpots)
    {
        const int res = 513;
        var rng = new System.Random(seed);
        float ox = rng.Next(0, 10000), oz = rng.Next(0, 10000);
        float ox2 = rng.Next(0, 10000), oz2 = rng.Next(0, 10000);
        float half = size * 0.5f;
        float cell = size / (res - 1);

        float[,] h = new float[res, res]; // [z, x] em metros

        // 1) Relevo base: fBm + cristas
        for (int iz = 0; iz < res; iz++)
        {
            for (int ix = 0; ix < res; ix++)
            {
                float wx = ix * cell - half;
                float wz = iz * cell - half;

                float n = 0f, amp = 1f, freq = 1f / 650f;
                for (int o = 0; o < 6; o++)
                {
                    n += (Mathf.PerlinNoise(wx * freq + ox, wz * freq + oz) - 0.5f) * amp;
                    amp *= 0.5f; freq *= 2.05f;
                }
                float height = n * 70f;

                float ridge = 1f - Mathf.Abs(Mathf.PerlinNoise(wx / 950f + ox2, wz / 950f + oz2) * 2f - 1f);
                height += ridge * ridge * 40f;

                // "Delta" sedimentar elevado a nordeste (inspirado no delta de Jezero)
                float dx = (wx - 560f) / 420f, dz = (wz - 600f) / 330f;
                float dd = Mathf.Sqrt(dx * dx + dz * dz);
                height += 38f * SmoothStep(1.0f, 0.78f, dd);

                h[iz, ix] = height;
            }
        }

        // 2) Crateras
        var craters = new List<Crater>();
        for (int i = 0; i < 55; i++)
        {
            float t = (float)rng.NextDouble();
            float r = Mathf.Lerp(12f, 170f, t * t * t); // muitas pequenas, poucas grandes
            craters.Add(new Crater
            {
                x = (float)(rng.NextDouble() * size - half),
                z = (float)(rng.NextDouble() * size - half),
                r = r,
                depth = r * Mathf.Lerp(0.22f, 0.32f, (float)rng.NextDouble())
            });
        }
        foreach (var c in craters)
        {
            float reach = c.r * 2f;
            int x0 = Mathf.Max(0, (int)((c.x - reach + half) / cell));
            int x1 = Mathf.Min(res - 1, (int)((c.x + reach + half) / cell) + 1);
            int z0 = Mathf.Max(0, (int)((c.z - reach + half) / cell));
            int z1 = Mathf.Min(res - 1, (int)((c.z + reach + half) / cell) + 1);
            float rimH = c.r * 0.07f;
            for (int iz = z0; iz <= z1; iz++)
            {
                for (int ix = x0; ix <= x1; ix++)
                {
                    float wx = ix * cell - half, wz = iz * cell - half;
                    float d = Mathf.Sqrt((wx - c.x) * (wx - c.x) + (wz - c.z) * (wz - c.z)) / c.r;
                    float delta = 0f;
                    if (d < 1f) delta += c.depth * (d * d - 1f);
                    float k = (d - 1f) / 0.28f;
                    delta += rimH * Mathf.Exp(-k * k);
                    h[iz, ix] += delta;
                }
            }
        }

        // 3) Áreas planas para bases e estações
        if (flattenSpots != null)
        {
            foreach (var s in flattenSpots)
            {
                int cx = Mathf.Clamp(Mathf.RoundToInt((s.x + half) / cell), 0, res - 1);
                int cz = Mathf.Clamp(Mathf.RoundToInt((s.y + half) / cell), 0, res - 1);
                float target = h[cz, cx];
                float outer = s.z * 2.2f;
                int reach = Mathf.CeilToInt(outer / cell) + 1;
                for (int iz = Mathf.Max(0, cz - reach); iz <= Mathf.Min(res - 1, cz + reach); iz++)
                {
                    for (int ix = Mathf.Max(0, cx - reach); ix <= Mathf.Min(res - 1, cx + reach); ix++)
                    {
                        float wx = ix * cell - half, wz = iz * cell - half;
                        float d = Mathf.Sqrt((wx - s.x) * (wx - s.x) + (wz - s.y) * (wz - s.y));
                        float w = SmoothStep(outer, s.z, d);
                        h[iz, ix] = Mathf.Lerp(h[iz, ix], target, w);
                    }
                }
            }
        }

        // 4) Normaliza para 0..1
        float baseOffset = maxHeight * 0.4f;
        var norm = new float[res, res];
        for (int iz = 0; iz < res; iz++)
            for (int ix = 0; ix < res; ix++)
                norm[iz, ix] = Mathf.Clamp01((h[iz, ix] + baseOffset) / maxHeight);

        var td = new TerrainData();
        td.heightmapResolution = res;
        td.size = new Vector3(size, maxHeight, size);
        td.SetHeights(0, 0, norm);

        // 5) Texturas: regolito + rocha nas encostas
        var regolith = new TerrainLayer();
        regolith.diffuseTexture = MakeTexture(seed, new Color(0.66f, 0.38f, 0.22f), new Color(0.50f, 0.27f, 0.15f), 0.35f);
        regolith.tileSize = new Vector2(14f, 14f);
        var rock = new TerrainLayer();
        rock.diffuseTexture = MakeTexture(seed + 1, new Color(0.36f, 0.24f, 0.18f), new Color(0.20f, 0.13f, 0.10f), 0.6f);
        rock.tileSize = new Vector2(9f, 9f);
        td.terrainLayers = new TerrainLayer[] { regolith, rock };

        int ares = 256;
        td.alphamapResolution = ares;
        var alpha = new float[ares, ares, 2];
        for (int y = 0; y < ares; y++)
        {
            for (int x = 0; x < ares; x++)
            {
                float u = x / (ares - 1f), v = y / (ares - 1f);
                float steep = td.GetSteepness(u, v);
                float patch = Mathf.PerlinNoise(u * 40f + ox, v * 40f + oz) * 0.25f;
                float r = Mathf.Clamp01(SmoothStep(14f, 30f, steep) + patch - 0.1f);
                alpha[y, x, 0] = 1f - r;
                alpha[y, x, 1] = r;
            }
        }
        td.SetAlphamaps(0, 0, alpha);

        var go = Terrain.CreateTerrainGameObject(td);
        go.name = "Mars Terrain (procedural)";
        go.transform.position = new Vector3(-half, 0f, -half);
        var terrain = go.GetComponent<Terrain>();
        terrain.heightmapPixelError = 3f;
        terrain.basemapDistance = 4000f;
        return terrain;
    }

    public static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    // Textura repetível (sem emendas) gerada por ruído de valor periódico
    static Texture2D MakeTexture(int seed, Color a, Color b, float contrast)
    {
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n = 0f, amp = 0.5f;
                int period = 4;
                for (int o = 0; o < 6; o++)
                {
                    n += TileNoise(x * period / (float)size, y * period / (float)size, period, seed + o * 31) * amp;
                    amp *= 0.5f; period *= 2;
                }
                float grain = Hash(x, y, seed * 7) * 0.12f;
                float t = Mathf.Clamp01(0.5f + (n - 0.5f) * (1f + contrast * 2f) + grain - 0.06f);
                px[y * size + x] = Color.Lerp(b, a, t);
            }
        }
        tex.SetPixels(px);
        tex.Apply(true);
        return tex;
    }

    static float TileNoise(float x, float y, int period, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Hash(Mod(x0, period), Mod(y0, period), seed);
        float b = Hash(Mod(x0 + 1, period), Mod(y0, period), seed);
        float c = Hash(Mod(x0, period), Mod(y0 + 1, period), seed);
        float d = Hash(Mod(x0 + 1, period), Mod(y0 + 1, period), seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static int Mod(int a, int m) { return ((a % m) + m) % m; }

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144665);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }
}
