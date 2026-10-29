using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fulfilment.Presentation
{
    public enum IconShape
    {
        Disc,
        Ring,
        Square,
        Warehouse,
        Pin,
        Parcel,
        Cross
    }

    /// <summary>
    /// Flat white icons drawn once into small textures and tinted by SpriteRenderer.color, so the scene has
    /// one consistent icon style without external art. Shapes are evaluated as signed-distance style masks
    /// with a one-pixel soft edge (anti-aliasing).
    /// </summary>
    public static class SpriteFactory
    {
        private const int Size = 64;
        private static readonly Dictionary<IconShape, Sprite> Cache = new Dictionary<IconShape, Sprite>();

        public static Sprite Get(IconShape shape)
        {
            if (Cache.TryGetValue(shape, out Sprite sprite) && sprite != null)
                return sprite;
            sprite = Create(shape);
            Cache[shape] = sprite;
            return sprite;
        }

        /// <summary>A texture of the road grid, one pixel per cell, shown as the map ground.</summary>
        public static Sprite Ground(Fulfilment.Simulation.RoadNetwork roads)
        {
            var tex = new Texture2D(roads.Width, roads.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Ground"
            };
            var pixels = new Color32[roads.Width * roads.Height];
            for (int y = 0; y < roads.Height; y++)
                for (int x = 0; x < roads.Width; x++)
                {
                    Color c;
                    switch (roads[x, y])
                    {
                        case Fulfilment.Simulation.CellType.Road:
                        case Fulfilment.Simulation.CellType.Site: c = Palette.Road; break;
                        case Fulfilment.Simulation.CellType.River: c = Palette.River; break;
                        case Fulfilment.Simulation.CellType.Bridge: c = Palette.Bridge; break;
                        default: c = Palette.Block; break;
                    }
                    pixels[y * roads.Width + x] = c;
                }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, roads.Width, roads.Height), Vector2.zero, 1f);
        }

        private static Sprite Create(IconShape shape)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = shape.ToString()
            };
            Func<float, float, float> mask = Mask(shape);
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    // Pixel centre in [-1, 1]
                    float u = (x + 0.5f) / Size * 2f - 1f;
                    float v = (y + 0.5f) / Size * 2f - 1f;
                    float a = Mathf.Clamp01(mask(u, v));
                    pixels[y * Size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }

        // Coverage from a signed distance (negative inside), soft over one pixel.
        private static float Fill(float signedDistance) => 0.5f - signedDistance * Size * 0.5f;

        private static float Box(float u, float v, float halfW, float halfH, float radius)
        {
            float qx = Mathf.Abs(u) - halfW + radius, qy = Mathf.Abs(v) - halfH + radius;
            float outside = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
        }

        private static Func<float, float, float> Mask(IconShape shape)
        {
            switch (shape)
            {
                case IconShape.Disc:
                    return (u, v) => Fill(Mathf.Sqrt(u * u + v * v) - 0.92f);
                case IconShape.Ring:
                    return (u, v) => Fill(Mathf.Abs(Mathf.Sqrt(u * u + v * v) - 0.8f) - 0.12f);
                case IconShape.Square:
                    return (u, v) => Fill(Box(u, v, 0.9f, 0.9f, 0.2f));
                case IconShape.Parcel:
                    // Rounded box with a half-transparent tape stripe down the middle.
                    return (u, v) => Fill(Box(u, v, 0.85f, 0.7f, 0.12f)) * (Mathf.Abs(u) < 0.12f ? 0.55f : 1f);
                case IconShape.Warehouse:
                    return (u, v) =>
                    {
                        float body = Box(u, v + 0.25f, 0.85f, 0.6f, 0.08f);
                        float roof = Mathf.Max(v - 0.85f + Mathf.Abs(u) * 0.55f, -(v - 0.3f)); // triangle above body
                        roof = Mathf.Max(roof, Mathf.Abs(u) - 0.92f);
                        float door = Box(u, v + 0.55f, 0.22f, 0.3f, 0.04f);
                        float shapeSd = Mathf.Min(body, roof);
                        return Mathf.Min(Fill(shapeSd), Fill(-door));
                    };
                case IconShape.Pin:
                    return (u, v) =>
                    {
                        float head = Mathf.Sqrt(u * u + (v - 0.25f) * (v - 0.25f)) - 0.62f;
                        float tip = Mathf.Max(Mathf.Abs(u) * 1.4f + (v + 0.95f) * -0.8f, -(v + 0.95f)); // cone down to the tip
                        tip = Mathf.Max(tip, v - 0.2f);
                        float hole = Mathf.Sqrt(u * u + (v - 0.25f) * (v - 0.25f)) - 0.25f;
                        return Mathf.Min(Fill(Mathf.Min(head, tip)), Fill(-hole));
                    };
                case IconShape.Cross:
                    return (u, v) =>
                    {
                        float a = Mathf.Abs(u - v) / 1.4142f, b = Mathf.Abs(u + v) / 1.4142f;
                        float arms = Mathf.Min(Mathf.Max(a - 0.16f, Mathf.Abs(u + v) / 1.4142f - 0.95f),
                                               Mathf.Max(b - 0.16f, Mathf.Abs(u - v) / 1.4142f - 0.95f));
                        return Fill(arms);
                    };
                default:
                    return (u, v) => 1f;
            }
        }
    }
}
