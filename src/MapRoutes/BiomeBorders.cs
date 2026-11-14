using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimMapRoutes
{
    /// <summary>
    /// The borders between land biomes, traced once per map from the game's map textures into smooth lines in world
    /// space. <see cref="RouteMap"/> draws them with the same dots as routes, only where the map is explored.
    /// </summary>
    internal static class BiomeBorders
    {
        // Each line is smoothed with a moving average of this many grid corners on each side, this many times, and
        // then thinned to this tolerance in map pixels.
        private const int SmoothRadius = 2;
        private const int SmoothPasses = 2;
        private const float Tolerance = 0.4f;
        // Lines of this many map pixel edges or fewer are specks, not borders.
        private const int MinEdges = 4;
        // Swamp ground lies around the water level, so shallow water this deep still counts as land. Ocean is white on
        // the map texture and must be above the water level, so coasts stay without a line.
        private const float ShallowDepth = 3f;

        private static readonly FieldInfo HasGeneratedField = AccessTools.Field(typeof(Minimap), "m_hasGenerated");
        private static readonly FieldInfo HeightTextureField = AccessTools.Field(typeof(Minimap), "m_heightTexture");
        private static readonly FieldInfo ExploredField = AccessTools.Field(typeof(Minimap), "m_explored");
        private static readonly FieldInfo ExploredOthersField = AccessTools.Field(typeof(Minimap), "m_exploredOthers");

        private static Minimap _owner;
        private static bool _broken;
        private static BitArray _explored;
        private static BitArray _exploredOthers;
        private static int _size;
        private static float _pixelSize;

        internal static readonly List<List<Vector3>> Lines = new List<List<Vector3>>();

        /// <summary>Traces the borders for this map if needed. False while the map isn't built yet, or on failure.</summary>
        internal static bool Refresh(Minimap minimap)
        {
            if (_broken)
            {
                return false;
            }
            if (_owner == minimap)
            {
                return true;
            }
            if (!(HasGeneratedField?.GetValue(minimap) is bool generated) || !generated)
            {
                return false;
            }
            var heights = HeightTextureField?.GetValue(minimap) as Texture2D;
            _explored = ExploredField?.GetValue(minimap) as BitArray;
            _exploredOthers = ExploredOthersField?.GetValue(minimap) as BitArray;
            if (minimap.m_mapTexture == null || heights == null || _explored == null)
            {
                _broken = true;
                Plugin.Log.LogError("Biome borders disabled: the map textures were not found in this game version");
                return false;
            }
            _owner = minimap;
            _size = minimap.m_mapTexture.width;
            _pixelSize = minimap.m_pixelSize;
            Trace(minimap.m_mapTexture.GetPixels32(), heights);
            return true;
        }

        /// <summary>Whether the map pixel under a world position is explored, by the player or a shared map.</summary>
        internal static bool IsExplored(Vector3 world)
        {
            var half = _size / 2;
            var x = Mathf.FloorToInt(world.x / _pixelSize + half);
            var y = Mathf.FloorToInt(world.z / _pixelSize + half);
            if (x < 0 || y < 0 || x >= _size || y >= _size)
            {
                return false;
            }
            var index = y * _size + x;
            return _explored[index] || (_exploredOthers != null && _exploredOthers[index]);
        }

        // m_mapTexture holds one flat color per biome, so a border is an edge between two neighbouring pixels of
        // different color. Edges where either pixel is in deep water are left out, so coasts, rivers and the sea get no
        // line. The edges lie on the pixel grid; they are chained into lines from corner to corner, which are then
        // smoothed, since raw they are staircases.
        private static void Trace(Color32[] colors, Texture2D heights)
        {
            Lines.Clear();
            var size = _size;
            var water = ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
            var stride = size + 1;
            var links = new Dictionary<int, List<int>>();
            var edges = 0;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var i = y * size + x;
                    // The edge to the right pixel runs up the grid line x + 1; the one to the pixel above along y + 1.
                    if (x + 1 < size && Differs(colors, heights, water, i, x, y, i + 1, x + 1, y))
                    {
                        Link(links, y * stride + x + 1, (y + 1) * stride + x + 1);
                        edges++;
                    }
                    if (y + 1 < size && Differs(colors, heights, water, i, x, y, i + size, x, y + 1))
                    {
                        Link(links, (y + 1) * stride + x, (y + 1) * stride + x + 1);
                        edges++;
                    }
                }
            }

            var used = new HashSet<long>();
            var points = 0;
            // Start at line ends and junctions first, so open lines are walked whole; what's left are closed loops.
            foreach (var loops in new[] { false, true })
            {
                foreach (var start in links)
                {
                    if (!loops && start.Value.Count == 2)
                    {
                        continue;
                    }
                    foreach (var next in start.Value)
                    {
                        if (used.Contains(Key(start.Key, next)))
                        {
                            continue;
                        }
                        var corners = Walk(links, used, start.Key, next);
                        if (corners.Count <= MinEdges)
                        {
                            continue;
                        }
                        var line = ToWorld(Thin(Smooth(corners, stride)));
                        points += line.Count;
                        Lines.Add(line);
                    }
                }
            }
            Plugin.Log.LogInfo($"Biome borders: {edges} border edges on land, {Lines.Count} lines, {points} points");
        }

        private static bool Differs(Color32[] colors, Texture2D heights, float water, int a, int ax, int ay, int b, int bx, int by)
        {
            var ca = colors[a];
            var cb = colors[b];
            if (ca.r == cb.r && ca.g == cb.g && ca.b == cb.b)
            {
                return false;
            }
            return IsLand(ca, heights.GetPixel(ax, ay).r, water) && IsLand(cb, heights.GetPixel(bx, by).r, water);
        }

        // White is Ocean, but also Mountain and Deep North, which are high anyway.
        private static bool IsLand(Color32 color, float height, float water)
        {
            var white = color.r == 255 && color.g == 255 && color.b == 255;
            return height >= (white ? water : water - ShallowDepth);
        }

        private static void Link(Dictionary<int, List<int>> links, int a, int b)
        {
            Add(links, a, b);
            Add(links, b, a);
        }

        private static void Add(Dictionary<int, List<int>> links, int from, int to)
        {
            if (!links.TryGetValue(from, out var list))
            {
                list = new List<int>(2);
                links[from] = list;
            }
            list.Add(to);
        }

        private static long Key(int a, int b)
        {
            return a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        }

        // Follows unused edges from start through next until a line end, a junction or back at the start.
        private static List<int> Walk(Dictionary<int, List<int>> links, HashSet<long> used, int start, int next)
        {
            var corners = new List<int> { start };
            var previous = start;
            var current = next;
            used.Add(Key(previous, current));
            while (true)
            {
                corners.Add(current);
                var neighbours = links[current];
                if (neighbours.Count != 2 || current == start)
                {
                    return corners;
                }
                var following = neighbours[0] == previous ? neighbours[1] : neighbours[0];
                if (!used.Add(Key(current, following)))
                {
                    return corners;
                }
                previous = current;
                current = following;
            }
        }

        // A moving average over the corners. The ends stay put, so lines still meet at junctions; a closed loop
        // wraps around.
        private static List<Vector2> Smooth(List<int> corners, int stride)
        {
            var points = corners.ConvertAll(c => new Vector2(c % stride, c / stride));
            var closed = corners[0] == corners[corners.Count - 1];
            var count = closed ? points.Count - 1 : points.Count;
            for (var pass = 0; pass < SmoothPasses; pass++)
            {
                var smoothed = new List<Vector2>(points);
                for (var i = closed ? 0 : 1; i < (closed ? count : count - 1); i++)
                {
                    var sum = Vector2.zero;
                    var n = 0;
                    for (var k = -SmoothRadius; k <= SmoothRadius; k++)
                    {
                        var j = i + k;
                        if (closed)
                        {
                            j = (j % count + count) % count;
                        }
                        else if (j < 0 || j >= count)
                        {
                            continue;
                        }
                        sum += points[j];
                        n++;
                    }
                    smoothed[i] = sum / n;
                }
                if (closed)
                {
                    smoothed[count] = smoothed[0];
                }
                points = smoothed;
            }
            return points;
        }

        // Ramer-Douglas-Peucker in map pixels.
        private static List<Vector2> Thin(List<Vector2> points)
        {
            var keep = new bool[points.Count];
            keep[0] = true;
            keep[points.Count - 1] = true;
            var spans = new Stack<KeyValuePair<int, int>>();
            spans.Push(new KeyValuePair<int, int>(0, points.Count - 1));
            while (spans.Count > 0)
            {
                var span = spans.Pop();
                var farthest = -1;
                var farthestDistance = Tolerance;
                for (var i = span.Key + 1; i < span.Value; i++)
                {
                    var distance = RouteMap.DistanceToSegment(points[i], points[span.Key], points[span.Value]);
                    if (distance > farthestDistance)
                    {
                        farthestDistance = distance;
                        farthest = i;
                    }
                }
                if (farthest >= 0)
                {
                    keep[farthest] = true;
                    spans.Push(new KeyValuePair<int, int>(span.Key, farthest));
                    spans.Push(new KeyValuePair<int, int>(farthest, span.Value));
                }
            }
            var kept = new List<Vector2>();
            for (var i = 0; i < points.Count; i++)
            {
                if (keep[i])
                {
                    kept.Add(points[i]);
                }
            }
            return kept;
        }

        // A grid corner g is the edge of pixel g, so in world space it is (g - half) * pixel size: the inverse of
        // RouteMap's ToLocal, which maps world x to (x / pixel size + half) / texture size.
        private static List<Vector3> ToWorld(List<Vector2> points)
        {
            var half = _size / 2;
            return points.ConvertAll(p => new Vector3((p.x - half) * _pixelSize, 0f, (p.y - half) * _pixelSize));
        }
    }
}
