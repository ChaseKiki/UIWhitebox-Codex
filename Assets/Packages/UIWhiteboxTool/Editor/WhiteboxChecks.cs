using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UIWhiteboxTool
{
    // Batch entry point: -executeMethod UIWhiteboxTool.WhiteboxChecks.RunBatch
    // Operates only in a scratch project; creates smoke assets under Assets/Packages/UIWhiteboxTool/Editor/Data/QA.
    public static class WhiteboxChecks
    {
        static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("请在隔离测试工程的batchmode下运行。");
            try
            {
                Directory.CreateDirectory("Assets/Packages/UIWhiteboxTool/Editor/Data/QA"); AssetDatabase.Refresh();
                var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                texture.SetPixels(Enumerable.Range(0, 256 * 256).Select(i => new Color((i % 256) / 255f, (i / 256) / 255f, .3f, 1)).ToArray()); texture.Apply();
                File.WriteAllBytes("Assets/Packages/UIWhiteboxTool/Editor/Data/QA/source.png", texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture); AssetDatabase.Refresh();
                var doc = ScriptableObject.CreateInstance<WhiteboxDocument>(); doc.panelName = "WhiteboxSmoke"; doc.source = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Packages/UIWhiteboxTool/Editor/Data/QA/source.png"); doc.resolution = new Vector2Int(750, 1334);
                doc.font = AssetDatabase.FindAssets("t:TMP_FontAsset").Select(g => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(f => f && f.name.Contains("Liberation"));
                if (!doc.font) doc.font = TMP_Settings.defaultFontAsset;
                Require(doc.font, "TMP测试字体缺失"); AssetDatabase.CreateAsset(doc, "Assets/Packages/UIWhiteboxTool/Editor/Data/QA/SmokeDocument.asset");
                WhiteboxNode Node(string id, string type, float x, float y, string parent = "", string text = "Test")
                { var n = new WhiteboxNode { id = id, name = (WhiteboxRules.Control(type) ? "m_" : "") + id + type, type = type, x = x, y = y, width = 190, height = 55, parentId = parent, text = text, horizontal = true }; doc.nodes.Add(n); return n; }
                Node("Title", "Text", 20, 20); Node("Confirm", "Button", 20, 90);
                Node("Icon", "Image", 240, 90, "", "Mail"); Node("Tabs", "ToggleGroup", 20, 160);
                Node("TabA", "Toggle", 20, 160, "Tabs"); Node("TabB", "Toggle", 240, 160, "Tabs");
                Node("Value", "Slider", 20, 240); Node("Fill", "Progress", 240, 240); Node("Bar", "Scrollbar", 460, 240);
                Node("Input", "InputField", 20, 320); Node("Options", "Dropdown", 240, 320, "", "A|B|C");
                var list = Node("List", "ScrollView", 20, 430); list.width = 650; list.height = 300; list.horizontal = false;
                for (int i = 0; i < 5; i++) { var item = Node("Item" + i, "Container", 30, 440 + i * 90, "List"); item.itemTemplate = i == 0; Node("ItemLabel" + i, "Text", 35, 440 + i * 90, item.id); }
                Node("Excluded", "Image", 20, 800).excluded = true;
                Require(WhiteboxRules.Validate(doc.nodes, doc.resolution).Count == 0, "合法结构被拒绝");
                var invalid = new WhiteboxNode { id = "Cycle", name = "CycleContainer", parentId = "Cycle" }; doc.nodes.Add(invalid);
                Require(WhiteboxRules.Validate(doc.nodes, doc.resolution).Any(e => e.Contains("循环")), "未拒绝循环"); doc.nodes.Remove(invalid);
                string taskFolder = WhiteboxCodex.Export(doc, "Smoke test");
                var result = new WhiteboxResult { taskId = doc.taskId, imageHash = "stale", width = 750, height = 1334, nodes = doc.nodes };
                string resultFile = Path.Combine(taskFolder, "result.json"); File.WriteAllText(resultFile, JsonUtility.ToJson(result)); bool rejected = false;
                try { WhiteboxCodex.Import(doc, resultFile); } catch (InvalidOperationException) { rejected = true; } Require(rejected, "未拒绝过期结果");
                result.imageHash = doc.imageHash; File.WriteAllText(resultFile, JsonUtility.ToJson(result)); WhiteboxCodex.Import(doc, resultFile);
                string first = WhiteboxGenerator.Generate(doc); byte[] baseline = File.ReadAllBytes(first);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(first);
                Require(prefab.GetComponentsInChildren<Button>(true).Length >= 1, "Button缺失");
                var scroll = prefab.GetComponentInChildren<ScrollRect>(true); Require(scroll && scroll.content && scroll.viewport, "ScrollRect引用缺失");
                Require(scroll.content.rect.height > 300, "列表Content未扩展");
                Require(prefab.GetComponentInChildren<TMP_InputField>(true).textComponent, "InputField引用缺失");
                var drop = prefab.GetComponentInChildren<TMP_Dropdown>(true); Require(drop.template && drop.captionText && drop.itemText && drop.options.Count == 3, "Dropdown引用或选项缺失");
                var slider = prefab.GetComponentInChildren<Slider>(true); Require(slider.fillRect && slider.handleRect && slider.targetGraphic, "Slider引用缺失");
                var toggles = prefab.GetComponentsInChildren<Toggle>(true).Where(t => t.group).ToArray(); Require(toggles.Length == 2 && toggles.Count(t => t.isOn) == 1, "页签组默认状态错误");
                Require(!prefab.transform.Find("ExcludedImage"), "排除节点仍生成");
                Require(Directory.GetFiles(Path.GetDirectoryName(first) + "/Items", "*.prefab").Length == 1, "Item模板导出错误");
                Require(prefab.GetComponentsInChildren<Component>(true).All(c => c && !c.GetType().Namespace.StartsWith("PsdTool")), "存在丢失组件或PsdTool依赖");
                doc.useSourceArt = true; string second = WhiteboxGenerator.Generate(doc); Require(first != second && baseline.SequenceEqual(File.ReadAllBytes(first)), "另存破坏了旧Prefab");
                var art = AssetDatabase.LoadAssetAtPath<GameObject>(second).transform.Find("IconImage").GetComponent<Image>(); Require(art.sprite, "切图模式缺少Sprite");
                Color pixel = ReadPixel(art.sprite.texture); Require(pixel.g > .7f, "裁切Y轴方向错误");
                WhiteboxGenerator.Preview(doc); Require(UnityEngine.Object.FindObjectOfType<Canvas>() && UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>(), "测试Canvas或事件系统缺失");
                File.WriteAllText(WhiteboxPaths.QA + "/whitebox-checks.txt", "PASS: validation, cycle rejection, stale task rejection, JSON import, all control generation, ScrollRect bounds, TMP references, ToggleGroup state, exclusion, Item prefab, independent components, source-art crop direction, revision preservation, preview Canvas.");
                Debug.Log("UIWHITEBOX_CHECKS_PASS"); EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); File.WriteAllText(WhiteboxPaths.QA + "/whitebox-checks.txt", "FAIL: " + e); EditorApplication.Exit(1); }
        }
        static Color ReadPixel(Texture2D source)
        { var t = WhiteboxRules.Readable(source); try { return t.GetPixel(t.width / 2, t.height / 2); } finally { UnityEngine.Object.DestroyImmediate(t); } }
    }
}
