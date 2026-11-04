using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UIWhiteboxTool
{
    internal static class WhiteboxGenerator
    {
        internal static string Difference(WhiteboxDocument doc)
        {
            var previous = AssetDatabase.LoadAssetAtPath<GameObject>(doc.previousPrefab);
            int count = doc.nodes.Count(n => !n.excluded);
            if (!previous) return "首次生成：" + count + "个识别节点（另含标准控件内部节点）。";
            var lines = new List<string> { "旧版本：" + doc.previousPrefab, "当前识别节点：" + count };
            string snapshot = Path.Combine(Path.GetDirectoryName(doc.previousPrefab), "Structure.json");
            if (File.Exists(snapshot))
            {
                var old = JsonUtility.FromJson<WhiteboxResult>(File.ReadAllText(snapshot)).nodes.ToDictionary(n => n.id);
                var current = doc.nodes.ToDictionary(n => n.id);
                foreach (var n in doc.nodes)
                {
                    if (!old.TryGetValue(n.id, out var before)) { lines.Add("新增：" + n.name); continue; }
                    if (JsonUtility.ToJson(before) != JsonUtility.ToJson(n)) lines.Add("变更：" + before.name + " → " + n.name + "（名称/结构/布局/内容/复核信息）");
                }
                foreach (var n in old.Values.Where(n => !current.ContainsKey(n.id))) lines.Add("移除：" + n.name);
            }
            lines.Add("差异按节点ID比较；重新识别可能产生新ID。首版只另存版本，不覆盖旧Prefab、脚本及事件。");
            return string.Join("\n", lines.Take(45)) + (lines.Count > 45 ? "\n其余差异已省略。" : "");
        }
        internal static string Generate(WhiteboxDocument doc)
        {
            WhiteboxRules.Check(doc);
            string baseFolder = WhiteboxPaths.Output + "/" + doc.panelName;
            Directory.CreateDirectory(baseFolder);
            string folder = AssetDatabase.GenerateUniqueAssetPath(baseFolder + "/Version_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(folder); Directory.CreateDirectory(folder + "/Sprites"); Directory.CreateDirectory(folder + "/Items"); AssetDatabase.Refresh();
            Texture2D source = null; GameObject root = null;
            try
            {
                if (doc.useSourceArt) source = WhiteboxRules.Readable(doc.source);
                root = New(doc.panelName, null); SetRect(root.GetComponent<RectTransform>(), new Rect(0, 0, doc.resolution.x, doc.resolution.y));
                var map = new Dictionary<string, GameObject>();
                var nodes = doc.nodes.ToDictionary(n => n.id);
                bool Included(WhiteboxNode n)
                { if (n.excluded) return false; return string.IsNullOrEmpty(n.parentId) || Included(nodes[n.parentId]); }
                GameObject Build(WhiteboxNode n)
                {
                    if (map.TryGetValue(n.id, out var exists)) return exists;
                    Transform parent = root.transform; Rect parentBounds = new Rect(0, 0, doc.resolution.x, doc.resolution.y);
                    if (!string.IsNullOrEmpty(n.parentId))
                    {
                        var parentNode = nodes[n.parentId]; var pg = Build(parentNode); parent = pg.transform;
                        if (parentNode.type == "ScrollView") parent = pg.GetComponent<ScrollRect>().content;
                        parentBounds = new Rect(parentNode.x, parentNode.y, parentNode.width, parentNode.height);
                    }
                    var go = CreateControl(n, parent, doc); map.Add(n.id, go);
                    SetRect(go.GetComponent<RectTransform>(), new Rect(n.x - parentBounds.x, n.y - parentBounds.y, n.width, n.height));
                    if (source && n.type == "Image")
                    {
                        var image = go.GetComponent<Image>();
                        if (image)
                        {
                            Sprite sprite = Crop(source, n, doc.resolution, folder + "/Sprites/" + n.id + ".png");
                            if (sprite) { image.sprite = sprite; image.type = UnityEngine.UI.Image.Type.Simple; image.color = Color.white; }
                            else if (!string.IsNullOrEmpty(n.text)) Label(go.transform, n.text, TMP_Settings.defaultFontAsset);
                        }
                    }
                    return go;
                }
                foreach (var n in doc.nodes.Where(Included)) Build(n);
                foreach (var n in doc.nodes.Where(n => Included(n) && n.type == "Toggle"))
                {
                    var group = map[n.id].GetComponentInParent<ToggleGroup>();
                    map[n.id].GetComponent<Toggle>().group = group;
                }
                foreach (var n in doc.nodes.Where(n => Included(n) && n.type == "ToggleGroup"))
                {
                    var toggles = map[n.id].GetComponentsInChildren<Toggle>(true).Where(t => t.group == map[n.id].GetComponent<ToggleGroup>()).ToArray();
                    for (int i = 0; i < toggles.Length; i++) toggles[i].SetIsOnWithoutNotify(i == 0);
                }
                Normalize(root.transform);
                foreach (var n in doc.nodes.Where(n => Included(n) && n.type == "ScrollView"))
                {
                    var scroll = map[n.id].GetComponent<ScrollRect>();
                    float w = n.width, h = n.height;
                    foreach (var child in doc.nodes.Where(c => c.parentId == n.id && Included(c)))
                    { w = Mathf.Max(w, child.x - n.x + child.width); h = Mathf.Max(h, child.y - n.y + child.height); }
                    scroll.content.sizeDelta = new Vector2(w, h);
                    scroll.horizontal = n.horizontal; scroll.vertical = !n.horizontal;
                    foreach (var item in doc.nodes.Where(c => c.parentId == n.id && c.itemTemplate && Included(c)))
                    {
                        var copy = UnityEngine.Object.Instantiate(map[item.id]);
                        try { copy.name = map[item.id].name; copy.GetComponent<RectTransform>().anchoredPosition = Vector2.zero; PrefabUtility.SaveAsPrefabAsset(copy, folder + "/Items/" + item.name + ".prefab"); }
                        finally { UnityEngine.Object.DestroyImmediate(copy); }
                    }
                }
                Normalize(root.transform);
                string path = folder + "/" + doc.panelName + ".prefab";
                if (!PrefabUtility.SaveAsPrefabAsset(root, path)) throw new InvalidOperationException("Prefab保存失败。");
                File.WriteAllText(folder + "/Structure.json", JsonUtility.ToJson(new WhiteboxResult { width = doc.resolution.x, height = doc.resolution.y, nodes = doc.nodes, taskId = doc.taskId, imageHash = doc.imageHash }, true));
                AssetDatabase.Refresh(); doc.previousPrefab = path; WhiteboxRules.Save(doc); Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(path); return path;
            }
            finally { if (source) UnityEngine.Object.DestroyImmediate(source); if (root) UnityEngine.Object.DestroyImmediate(root); }
        }
        static GameObject CreateControl(WhiteboxNode n, Transform parent, WhiteboxDocument doc)
        {
            GameObject go;
            switch (n.type)
            {
                case "InputField": go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources()); break;
                case "Dropdown": go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources()); break;
                default: go = New(n.name, null); break;
            }
            go.name = n.name; go.transform.SetParent(parent, false);
            var font = TMP_Settings.defaultFontAsset;
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) { if (font) t.font = font; t.raycastTarget = false; }
            bool HasLabel() => doc.nodes.Any(c => c.parentId == n.id && c.type == "Text" && !c.excluded);
            switch (n.type)
            {
                case "Container": break;
                case "ToggleGroup": go.AddComponent<ToggleGroup>().allowSwitchOff = false; break;
                case "Text": Text(go, n.text, font, Mathf.Clamp(n.height * .55f, 16, 48)); break;
                case "Image":
                    Image(go, new Color(.4f, .46f, .52f, 1)).raycastTarget = false;
                    if (!doc.useSourceArt && !string.IsNullOrEmpty(n.text)) Label(go.transform, n.text, font); break;
                case "Button":
                    var bg = Image(go, new Color(.25f, .42f, .57f));
                    var button = go.AddComponent<Button>(); button.targetGraphic = bg; button.transition = Selectable.Transition.ColorTint;
                    if (!HasLabel()) Label(go.transform, string.IsNullOrEmpty(n.text) ? n.name : n.text, font); break;
                case "Toggle":
                    var toggle = go.AddComponent<Toggle>(); var toggleBg = Image(go, new Color(.26f, .31f, .37f));
                    var check = New("SelectedStateImage", go.transform); var mark = Image(check, new Color(.2f, .75f, .65f));
                    SetRect(check.GetComponent<RectTransform>(), new Rect(5, 5, Mathf.Min(26, n.height - 10), Mathf.Min(26, n.height - 10)));
                    toggle.targetGraphic = toggleBg; toggle.graphic = mark;
                    var label = Label(go.transform, string.IsNullOrEmpty(n.text) ? n.name : n.text, font);
                    label.rectTransform.offsetMin = new Vector2(36, 3); toggle.isOn = false; break;
                case "Slider": case "Scrollbar": case "Progress":
                    Image(go, new Color(.22f, .25f, .29f));
                    var fill = New("FillImage", go.transform); var fillImage = Image(fill, new Color(.2f, .7f, .6f)); Stretch(fill.GetComponent<RectTransform>(), 3);
                    if (n.type == "Progress")
                    { fillImage.sprite = WhiteSprite(); fillImage.type = UnityEngine.UI.Image.Type.Filled; fillImage.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; fillImage.fillOrigin = 0; fillImage.fillAmount = .5f; break; }
                    if (n.type == "Scrollbar")
                    {
                        var bar = go.AddComponent<Scrollbar>(); bar.handleRect = fill.GetComponent<RectTransform>(); bar.targetGraphic = fillImage; bar.direction = n.horizontal ? Scrollbar.Direction.LeftToRight : Scrollbar.Direction.BottomToTop; bar.size = .3f; break;
                    }
                    var handleArea = New("HandleArea", go.transform); Stretch(handleArea.GetComponent<RectTransform>(), 10);
                    var handle = New("HandleImage", handleArea.transform); var handleImage = Image(handle, new Color(.9f, .9f, .9f));
                    handle.GetComponent<RectTransform>().sizeDelta = new Vector2(20, 20);
                    var slider = go.AddComponent<Slider>(); slider.fillRect = fill.GetComponent<RectTransform>(); slider.handleRect = handle.GetComponent<RectTransform>(); slider.targetGraphic = handleImage;
                    slider.direction = n.horizontal ? Slider.Direction.LeftToRight : Slider.Direction.BottomToTop; slider.value = .5f; break;
                case "ScrollView":
                    Image(go, new Color(.16f, .19f, .23f));
                    var view = New("Viewport", go.transform); Stretch(view.GetComponent<RectTransform>()); view.AddComponent<RectMask2D>();
                    var content = New("Content", view.transform); SetRect(content.GetComponent<RectTransform>(), new Rect(0, 0, n.width, n.height));
                    var scroll = go.AddComponent<ScrollRect>(); scroll.viewport = view.GetComponent<RectTransform>(); scroll.content = content.GetComponent<RectTransform>(); scroll.movementType = ScrollRect.MovementType.Clamped; break;
                case "InputField":
                    var input = go.GetComponent<TMP_InputField>();
                    if (input.placeholder is TMP_Text placeholder) placeholder.text = string.IsNullOrEmpty(n.text) ? "请输入" : n.text;
                    input.text = ""; break;
                case "Dropdown":
                    var drop = go.GetComponent<TMP_Dropdown>(); drop.ClearOptions();
                    drop.AddOptions((string.IsNullOrEmpty(n.text) ? "选项一|选项二|选项三" : n.text).Split('|').ToList()); drop.RefreshShownValue(); break;
            }
            foreach (var selectable in go.GetComponentsInChildren<Selectable>(true)) selectable.navigation = new Navigation { mode = Navigation.Mode.None };
            return go;
        }
        static Sprite WhiteSprite() => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        static GameObject New(string name, Transform parent) { var go = new GameObject(name, typeof(RectTransform)); if (parent) go.transform.SetParent(parent, false); return go; }
        static Image Image(GameObject go, Color color) { var image = go.GetComponent<Image>() ?? go.AddComponent<Image>(); image.color = color; image.raycastTarget = true; return image; }
        static TMP_Text Text(GameObject go, string value, TMP_FontAsset font, float size = 22)
        { var text = go.AddComponent<TextMeshProUGUI>(); if (font) text.font = font; text.text = value ?? ""; text.fontSize = size; text.color = Color.white; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; text.enableWordWrapping = true; return text; }
        static TMP_Text Label(Transform parent, string value, TMP_FontAsset font) { var go = New("LabelText", parent); Stretch(go.GetComponent<RectTransform>(), 3); return Text(go, value, font); }
        static void Stretch(RectTransform rect, float inset = 0) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * inset; rect.offsetMax = Vector2.one * -inset; }
        static void SetRect(RectTransform rect, Rect bounds) { rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(bounds.x, -bounds.y); rect.sizeDelta = bounds.size; }
        static Sprite Crop(Texture2D source, WhiteboxNode node, Vector2Int resolution, string path)
        {
            float sx = (float)source.width / resolution.x, sy = (float)source.height / resolution.y;
            // Cropping beyond the frame would stretch a partial patch. Retain the placeholder instead.
            if (node.x < 0 || node.y < 0 || node.x + node.width > resolution.x || node.y + node.height > resolution.y) return null;
            int x = Mathf.Clamp(Mathf.FloorToInt(node.x * sx), 0, source.width - 1), y = Mathf.Clamp(Mathf.FloorToInt(source.height - (node.y + node.height) * sy), 0, source.height - 1);
            int w = Mathf.Clamp(Mathf.CeilToInt(node.width * sx), 1, source.width - x), h = Mathf.Clamp(Mathf.CeilToInt(node.height * sy), 1, source.height - y);
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            try { texture.SetPixels(source.GetPixels(x, y, w, h)); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path); var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        static void Normalize(Transform parent)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Transform t in parent)
            {
                string name = WhiteboxRules.Token(t.name); if (string.IsNullOrEmpty(name) || !char.IsLetter(name[0])) name = "Part" + name;
                string seed = name; int i = 2; while (!used.Add(name)) name = seed + "_" + i++;
                t.name = name; Normalize(t);
            }
        }
        internal static void Preview(WhiteboxDocument doc)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(doc.previousPrefab); if (!prefab) throw new InvalidOperationException("请先生成Prefab。");
            var root = New(doc.panelName + "_WhiteboxPreview", null); Undo.RegisterCreatedObjectUndo(root, "Create whitebox preview");
            var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = doc.resolution;
            root.AddComponent<GraphicRaycaster>();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform); Stretch(instance.GetComponent<RectTransform>());
            if (!UnityEngine.Object.FindObjectOfType<EventSystem>())
            { var events = new GameObject("WhiteboxEventSystem", typeof(EventSystem), typeof(StandaloneInputModule)); Undo.RegisterCreatedObjectUndo(events, "Create preview EventSystem"); }
            Selection.activeGameObject = root;
        }
    }
}
