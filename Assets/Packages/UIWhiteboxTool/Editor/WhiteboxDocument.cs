using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UIWhiteboxTool
{
    internal static class WhiteboxPaths
    {
        internal const string Root = "Assets/Packages/UIWhiteboxTool";
        internal const string Data = Root + "/Editor/Data";
        internal const string Output = Root + "/Generated";
        internal const string Tasks = Data + "/Tasks";
        internal const string QA = Data + "/QA";
    }
    [Serializable] public sealed class WhiteboxNode
    {
        public string id, parentId = "", name, type = "Container", text = "", note = "";
        public float x, y, width = 100, height = 60, confidence = 1;
        public bool excluded, horizontal, itemTemplate;
    }
    [Serializable] public sealed class WhiteboxResult
    {
        public int version = 1;
        public string taskId, imageHash;
        public int width, height;
        public List<WhiteboxNode> nodes = new List<WhiteboxNode>();
    }
    public sealed class WhiteboxDocument : ScriptableObject
    {
        public Texture2D source;
        public string panelName = "SamplePanel", taskFolder = "", taskId = "", imageHash = "";
        public Vector2Int resolution = new Vector2Int(750, 1334);
        public bool useSourceArt;
        [HideInInspector] public TMPro.TMP_FontAsset font; // Legacy serialized field; components now use their default font.
        public List<WhiteboxNode> nodes = new List<WhiteboxNode>();
        public string previousPrefab = "";
    }
    internal static class WhiteboxRules
    {
        internal static readonly string[] Types = { "Container", "Image", "Text", "Button", "Toggle", "ToggleGroup", "ScrollView", "Scrollbar", "Slider", "Progress", "InputField", "Dropdown" };
        internal static bool Control(string type) => !new[] { "Container", "Image", "Text", "Progress" }.Contains(type);
        internal static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        internal static string Token(string value) => Regex.Replace(value ?? "", "[^A-Za-z0-9_]", "");
        internal static List<string> Validate(List<WhiteboxNode> nodes, Vector2Int size)
        {
            var errors = new List<string>();
            if (size.x < 1 || size.y < 1 || size.x > 16384 || size.y > 16384) errors.Add("设计分辨率必须在1～16384之间。");
            if (nodes == null || nodes.Count == 0 || nodes.Count > 1000) { errors.Add("节点数量必须为1～1000。"); return errors; }
            var map = new Dictionary<string, WhiteboxNode>();
            foreach (var n in nodes)
            {
                if (n == null || string.IsNullOrEmpty(n.id) || !Regex.IsMatch(n.id, "^[A-Za-z][A-Za-z0-9_]{0,95}$") || map.ContainsKey(n.id)) { errors.Add("节点ID缺失、非法或重复。"); continue; }
                map.Add(n.id, n);
                if (!Types.Contains(n.type)) errors.Add(n.id + "：不支持的控件类型。");
                if (!Regex.IsMatch(n.name ?? "", "^(m_)?[A-Za-z][A-Za-z0-9_]{0,95}$")) errors.Add(n.id + "：名称需为英文标识符。");
                if (Control(n.type) && !(n.name ?? "").StartsWith("m_", StringComparison.Ordinal)) errors.Add(n.id + "：控件名称需要m_前缀。");
                if (new[] { n.x, n.y, n.width, n.height, n.confidence }.Any(v => float.IsNaN(v) || float.IsInfinity(v)) || n.width <= 0 || n.height <= 0 || n.width > 32768 || n.height > 32768 || Math.Abs(n.x) > 32768 || Math.Abs(n.y) > 32768 || n.confidence < 0 || n.confidence > 1) errors.Add(n.id + "：坐标、尺寸或置信度非法。");
                if ((n.text ?? "").Length > 4096 || (n.note ?? "").Length > 4096) errors.Add(n.id + "：文本过长。");
            }
            foreach (var n in map.Values)
            {
                var seen = new HashSet<string> { n.id }; var current = n;
                while (!string.IsNullOrEmpty(current.parentId))
                {
                    if (!map.TryGetValue(current.parentId, out current)) { errors.Add(n.id + "：父节点不存在。"); break; }
                    if (!seen.Add(current.id)) { errors.Add(n.id + "：层级存在循环。"); break; }
                    if (!new[] { "Container", "Button", "ToggleGroup", "ScrollView" }.Contains(current.type)) { errors.Add(n.id + "：父控件不接受自定义子节点。"); break; }
                }
                if (n.itemTemplate && (!map.TryGetValue(n.parentId ?? "", out var parent) || parent.type != "ScrollView" || n.type != "Container")) errors.Add(n.id + "：Item模板必须为ScrollView直属Container。");
            }
            foreach (var group in nodes.Where(n => n != null && !n.excluded).GroupBy(n => n.parentId ?? ""))
                if (group.GroupBy(n => n.name ?? "", StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) errors.Add("同级节点名称重复。");
            foreach (var group in nodes.Where(n => n != null && n.itemTemplate && !n.excluded).GroupBy(n => n.parentId))
                if (group.Count() > 1) errors.Add("每个列表最多指定一个Item模板。");
            return errors.Distinct().ToList();
        }
        internal static void Check(WhiteboxDocument doc)
        {
            if (!doc || !doc.source) throw new InvalidOperationException("请先选择原图。");
            if (!Regex.IsMatch(doc.panelName ?? "", "^[A-Za-z][A-Za-z0-9_]{0,63}$")) throw new InvalidOperationException("面板名需为英文标识符，最长64字符。");
            var errors = Validate(doc.nodes, doc.resolution);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            if (!doc.nodes.Any(n => !n.excluded)) throw new InvalidOperationException("没有可生成的节点。");
        }
        internal static Texture2D Readable(Texture2D texture)
        {
            var active = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try { Graphics.Blit(texture, rt); RenderTexture.active = rt; var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false); copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); copy.Apply(); return copy; }
            finally { RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); }
        }
        internal static void Save(WhiteboxDocument doc) { EditorUtility.SetDirty(doc); AssetDatabase.SaveAssetIfDirty(doc); }
    }
}
