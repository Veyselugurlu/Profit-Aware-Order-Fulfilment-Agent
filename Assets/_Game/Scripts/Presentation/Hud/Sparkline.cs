using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fulfilment.Presentation
{
    /// <summary>Small line chart drawn with Painter2D: several series on a shared 0..1 vertical scale.</summary>
    public sealed class Sparkline : VisualElement
    {
        private readonly List<(List<float> values, Color colour)> series = new List<(List<float>, Color)>();
        private int capacity = 1;

        public Sparkline()
        {
            AddToClassList("sparkline");
            generateVisualContent += Draw;
        }

        /// <summary>Number of samples that fill the full width (e.g. hours in the episode).</summary>
        public int Capacity
        {
            get => capacity;
            set { capacity = Mathf.Max(2, value); MarkDirtyRepaint(); }
        }

        public int AddSeries(Color colour)
        {
            series.Add((new List<float>(), colour));
            return series.Count - 1;
        }

        public void Add(int index, float value01)
        {
            series[index].values.Add(Mathf.Clamp01(value01));
            MarkDirtyRepaint();
        }

        public void ClearData()
        {
            foreach (var s in series) s.values.Clear();
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext ctx)
        {
            Rect r = contentRect;
            if (r.width < 2f || r.height < 2f) return;
            Painter2D p = ctx.painter2D;

            // Baseline grid at 0 %, 50 % and 100 %.
            p.strokeColor = new Color(1f, 1f, 1f, 0.08f);
            p.lineWidth = 1f;
            foreach (float y in new[] { 0f, 0.5f, 1f })
            {
                p.BeginPath();
                p.MoveTo(new Vector2(r.xMin, r.yMax - y * r.height));
                p.LineTo(new Vector2(r.xMax, r.yMax - y * r.height));
                p.Stroke();
            }

            foreach ((List<float> values, Color colour) in series)
            {
                if (values.Count < 2) continue;
                p.strokeColor = colour;
                p.lineWidth = 2f;
                p.BeginPath();
                for (int i = 0; i < values.Count; i++)
                {
                    float x = r.xMin + r.width * i / (capacity - 1);
                    var point = new Vector2(Mathf.Min(x, r.xMax), r.yMax - values[i] * r.height);
                    if (i == 0) p.MoveTo(point);
                    else p.LineTo(point);
                }
                p.Stroke();
            }
        }
    }
}
