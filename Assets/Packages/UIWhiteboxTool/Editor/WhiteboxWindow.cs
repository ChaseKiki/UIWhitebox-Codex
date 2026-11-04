using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace UIWhiteboxTool
{
    public sealed class WhiteboxWindow : EditorWindow
    {
        [SerializeField] WhiteboxDocument document;
        [SerializeField] string instructions = "";
        [SerializeField] int selected = -1;
        Vector2 detailScroll, logScroll;
        string message = "", diff = "";
        bool advanced, showLog, showDiff;
        [SerializeField] TreeViewState hierarchyState;
        NodeHierarchy hierarchy;
        string hierarchySignature = "", discovered = "";
        void OnEnable()
        {
            WhiteboxCodex.Imported += OnImported;
            EditorApplication.update += Tick;
            discovered = WhiteboxCodex.Find() ?? "";
        }
        void OnDisable() { WhiteboxCodex.Imported -= OnImported; EditorApplication.update -= Tick; }
        bool wasBusy; void Tick() { if (WhiteboxCodex.Busy || wasBusy) Repaint(); wasBusy = WhiteboxCodex.Busy; }
        void OnImported(WhiteboxDocument doc)
        {
            document = doc; selected = doc.nodes.Count > 0 ? 0 : -1; hierarchySignature = "";
            message = "识别结果已自动导入，请进行第3步结构复核。"; Repaint();
        }
        string ToolRoot => WhiteboxPaths.Root;
        [MenuItem("Tools/UI白盒搭建/打开工作台", false, 0)] public static void Open() => GetWindow<WhiteboxWindow>("UI白盒搭建");
        void OnGUI()
        {
            minSize = new Vector2(1000, 700);
            EditorGUILayout.LabelField("UI 白盒搭建 · 1.0.0 基础版", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("1 导入图片  →  2 Codex分析  →  3 结构复核  →  4 生成Prefab   →   5 测试Canvas", EditorStyles.miniLabel);
            if (WhiteboxCodex.Busy)
            {
                Rect progress = EditorGUILayout.GetControlRect(false, 22);
                EditorGUI.ProgressBar(progress, WhiteboxCodex.Progress, WhiteboxCodex.Activity);
                EditorGUILayout.HelpBox("进度条表示执行阶段，模型不提供精确百分比。等待期间耗时持续更新；长时间无新输出可查看日志或取消重试。", MessageType.Info);
                if (GUILayout.Button("取消识别")) WhiteboxCodex.Cancel(); Repaint();
            }
            else if (!string.IsNullOrEmpty(WhiteboxCodex.Status)) EditorGUILayout.LabelField(WhiteboxCodex.Status);

            using (new EditorGUI.DisabledScope(WhiteboxCodex.Busy))
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    document = (WhiteboxDocument)EditorGUILayout.ObjectField("工作文档", document, typeof(WhiteboxDocument), false);
                    if (GUILayout.Button("新建文档", GUILayout.Width(85))) Act(Create);
                    if (GUILayout.Button("1 导入图片", GUILayout.Width(100))) Act(ImportImage);
                }
                if (!document) { EditorGUILayout.HelpBox("新建工作文档，导入PNG/JPG原型图或概念图。工具不依赖PsdTool。", MessageType.Info); return; }
                Undo.RecordObject(document, "Edit UI whitebox document"); EditorGUI.BeginChangeCheck();
                using (new EditorGUILayout.HorizontalScope())
                {
                    document.source = (Texture2D)EditorGUILayout.ObjectField("参考图", document.source, typeof(Texture2D), false);
                    document.panelName = EditorGUILayout.TextField("面板英文名", document.panelName);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    document.resolution = EditorGUILayout.Vector2IntField("参考分辨率", document.resolution);
                    
                    document.useSourceArt = GUILayout.Toggle(document.useSourceArt, new GUIContent("使用原图切图", "将UI区域矩形裁切为视觉资源；不恢复透明边缘、遮挡或独立状态。"), GUILayout.Width(120));
                }
                instructions = EditorGUILayout.TextField("补充界面说明", instructions);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("2 Codex识别（自动导入）", GUILayout.Height(28))) Act(() => { if (document.nodes.Count > 0 && !EditorUtility.DisplayDialog("重新识别", "完成后将自动替换当前节点结构，可Undo撤销。是否继续？", "开始识别", "返回")) return; message = ""; WhiteboxCodex.Export(document, instructions); WhiteboxCodex.Run(document); });
                    if (GUILayout.Button("3 检查结构 / 差异", GUILayout.Height(28))) Act(() => { WhiteboxRules.Check(document); diff = WhiteboxGenerator.Difference(document); showDiff = true; });
                    if (GUILayout.Button("4 生成白盒Prefab", GUILayout.Height(28))) Act(() => { WhiteboxRules.Check(document); diff = WhiteboxGenerator.Difference(document); showDiff = true; if (EditorUtility.DisplayDialog("生成白盒新版本", diff, "生成新版本", "返回")) message = "已生成：" + WhiteboxGenerator.Generate(document); });
                    if (GUILayout.Button("5 加入测试Canvas", GUILayout.Height(28))) Act(() => WhiteboxGenerator.Preview(document));
                }
                if (showDiff) EditorGUILayout.HelpBox(diff, MessageType.Info);
                using (new EditorGUILayout.HorizontalScope()) { DrawSource(); DrawNodes(); DrawDetail(); }
                if (EditorGUI.EndChangeCheck()) WhiteboxRules.Save(document);
                advanced = EditorGUILayout.Foldout(advanced, "高级 / Codex检测与诊断", true);
                if (advanced)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        WhiteboxCodex.Executable = EditorGUILayout.TextField("手动路径（可留空）", WhiteboxCodex.Executable);
                        if (GUILayout.Button("自动查找", GUILayout.Width(80))) { discovered = WhiteboxCodex.Find() ?? ""; message = discovered.Length > 0 ? "已找到Codex：" + discovered : "未检测到Codex CLI，请先安装；安装在自定义目录时可手动指定。"; }
                        if (GUILayout.Button("选择exe", GUILayout.Width(80))) { string p = EditorUtility.OpenFilePanel("选择官方codex.exe", "", "exe"); if (p.Length > 0) WhiteboxCodex.Executable = p; }
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("打开任务目录")) Act(() => { if (Directory.Exists(document.taskFolder)) EditorUtility.RevealInFinder(document.taskFolder); });
                        
                    }
                    EditorGUILayout.HelpBox("默认自动查找Codex，无需选择exe。仅自动查找失败时手动指定。首次使用请安装官方Codex CLI并完成codex login。检测路径：" + (discovered.Length == 0 ? "尚未找到" : discovered), MessageType.Info);
                }
            }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
            showLog = EditorGUILayout.Foldout(showLog, "识别日志", true);
            if (showLog) { logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.Height(95)); EditorGUILayout.SelectableLabel(WhiteboxCodex.Log, GUILayout.MinHeight(80)); EditorGUILayout.EndScrollView(); }
        }
        void DrawSource()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(position.width * .43f)))
            {
                EditorGUILayout.LabelField("原图与节点框 · 点击框选中", EditorStyles.boldLabel);
                Rect area = GUILayoutUtility.GetRect(150, 380, GUILayout.ExpandHeight(true));
                if (!document.source) return;
                float aspect = (float)document.source.width / document.source.height;
                Rect image = area; if (image.width / image.height > aspect) { image.width = image.height * aspect; image.x += (area.width - image.width) / 2; } else { image.height = image.width / aspect; image.y += (area.height - image.height) / 2; }
                GUI.DrawTexture(image, document.source, ScaleMode.StretchToFill);
                if (document.resolution.x <= 0 || document.resolution.y <= 0) return;
                for (int i = 0; i < document.nodes.Count; i++)
                {
                    var n = document.nodes[i]; Rect r = new Rect(image.x + n.x / document.resolution.x * image.width, image.y + n.y / document.resolution.y * image.height, n.width / document.resolution.x * image.width, n.height / document.resolution.y * image.height);
                    Color color = n.excluded ? Color.red : i == selected ? Color.yellow : new Color(.2f, 1, .7f, .7f);
                    if (Event.current.type == EventType.Repaint) Handles.DrawSolidRectangleWithOutline(r, Color.clear, color);
                }
                if (Event.current.type == EventType.MouseDown && image.Contains(Event.current.mousePosition))
                {
                    Vector2 point = Event.current.mousePosition;
                    var hit = document.nodes.Select((n, i) => new { n, i, r = new Rect(image.x + n.x / document.resolution.x * image.width, image.y + n.y / document.resolution.y * image.height, n.width / document.resolution.x * image.width, n.height / document.resolution.y * image.height) }).Where(o => o.r.Contains(point)).OrderBy(o => o.r.width * o.r.height).FirstOrDefault();
                    if (hit != null) { selected = hit.i; Repaint(); Event.current.Use(); }
                }
            }
        }
        void DrawNodes()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(260)))
            {
                EditorGUILayout.LabelField("3 结构复核 · Hierarchy", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("新增")) { string id = "Node" + Guid.NewGuid().ToString("N").Substring(0, 8); document.nodes.Add(new WhiteboxNode { id = id, name = id + "Container" }); selected = document.nodes.Count - 1; GUI.changed = true; }
                    if (GUILayout.Button("排除/恢复") && selected >= 0 && selected < document.nodes.Count) { document.nodes[selected].excluded = !document.nodes[selected].excluded; GUI.changed = true; }
                    if (GUILayout.Button("展开")) hierarchy?.ExpandAll();
                    if (GUILayout.Button("折叠")) hierarchy?.CollapseAll();
                }
                if (hierarchyState == null) hierarchyState = new TreeViewState();
                if (hierarchy == null) hierarchy = new NodeHierarchy(hierarchyState, this);
                string signature = document.GetInstanceID() + ":" + string.Join("|", document.nodes.Select(n => n.id + ":" + n.parentId + ":" + n.name + ":" + n.type + ":" + n.excluded + ":" + n.confidence));
                if (signature != hierarchySignature) { bool first = hierarchySignature.Length == 0; hierarchySignature = signature; hierarchy.Reload(); if (first) hierarchy.ExpandAll(); }
                if (selected >= 0 && !hierarchy.GetSelection().Contains(selected + 1)) hierarchy.SetSelection(new[] { selected + 1 }, TreeViewSelectionOptions.RevealAndFrame);
                Rect area = GUILayoutUtility.GetRect(240, 380, GUILayout.ExpandHeight(true)); hierarchy.OnGUI(area);
            }
        }
        sealed class NodeHierarchy : TreeView
        {
            readonly WhiteboxWindow window;
            public NodeHierarchy(TreeViewState state, WhiteboxWindow window) : base(state) { this.window = window; showBorder = true; showAlternatingRowBackgrounds = true; rowHeight = 22; Reload(); }
            protected override TreeViewItem BuildRoot()
            {
                var root = new TreeViewItem(0, -1, "Root") { children = new System.Collections.Generic.List<TreeViewItem>() };
                if (!window.document) return root;
                var nodes = window.document.nodes;
                var items = nodes.Select((n, i) => new TreeViewItem(i + 1, 0, n.name + "  [" + n.type + "]" + (n.excluded ? " ×" : n.confidence < .75f ? " ?" : "")) { icon = EditorGUIUtility.IconContent("RectTransform Icon").image as Texture2D }).ToArray();
                for (int i = 0; i < nodes.Count; i++)
                {
                    int p = nodes.FindIndex(n => n.id == nodes[i].parentId);
                    if (p < 0 || p == i || window.Descendant(nodes[p], nodes[i])) root.AddChild(items[i]); else items[p].AddChild(items[i]);
                }
                SetupDepthsFromParentsAndChildren(root); return root;
            }
            protected override void SelectionChanged(System.Collections.Generic.IList<int> ids) { if (ids.Count > 0) { window.selected = ids[0] - 1; window.Repaint(); } }
            protected override bool CanMultiSelect(TreeViewItem item) => false;
        }
        void DrawDetail()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("节点详情", EditorStyles.boldLabel);
                detailScroll = EditorGUILayout.BeginScrollView(detailScroll, GUILayout.MinHeight(360));
                if (selected >= 0 && selected < document.nodes.Count)
                {
                    var n = document.nodes[selected]; EditorGUILayout.SelectableLabel("ID: " + n.id, GUILayout.Height(18));
                    n.name = EditorGUILayout.TextField("英文名称", n.name);
                    int type = Array.IndexOf(WhiteboxRules.Types, n.type); int next = EditorGUILayout.Popup("节点类型", Mathf.Max(0, type), WhiteboxRules.Types);
                    if (next != type) { n.type = WhiteboxRules.Types[next]; if (WhiteboxRules.Control(n.type) && !n.name.StartsWith("m_")) n.name = "m_" + n.name; }
                    var parents = document.nodes.Where(p => p != n && new[] { "Container", "Button", "ToggleGroup", "ScrollView" }.Contains(p.type) && !Descendant(p, n)).ToArray();
                    string[] labels = new[] { "页面根节点" }.Concat(parents.Select(p => p.name + " [" + p.id + "]")).ToArray();
                    int oldParent = Array.FindIndex(parents, p => p.id == n.parentId) + 1;
                    int newParent = EditorGUILayout.Popup("父节点", oldParent, labels); if (newParent != oldParent) n.parentId = newParent == 0 ? "" : parents[newParent - 1].id;
                    n.x = EditorGUILayout.FloatField("页面X", n.x); n.y = EditorGUILayout.FloatField("页面Y", n.y);
                    n.width = EditorGUILayout.FloatField("宽", n.width); n.height = EditorGUILayout.FloatField("高", n.height);
                    EditorGUILayout.LabelField("文字 / 用途 / 下拉选项（|分隔）"); n.text = EditorGUILayout.TextArea(n.text, GUILayout.MinHeight(45));
                    n.excluded = EditorGUILayout.Toggle("排除此节点及子树", n.excluded);
                    if (new[] { "ScrollView", "Slider", "Scrollbar" }.Contains(n.type)) n.horizontal = EditorGUILayout.Toggle("水平方向", n.horizontal);
                    n.itemTemplate = EditorGUILayout.Toggle("导出为列表Item模板", n.itemTemplate);
                    EditorGUILayout.LabelField("识别置信度", n.confidence.ToString("P0"));
                    EditorGUILayout.LabelField("复核备注"); n.note = EditorGUILayout.TextArea(n.note, GUILayout.MinHeight(50));
                }
                EditorGUILayout.EndScrollView();
            }
        }
        bool Descendant(WhiteboxNode candidate, WhiteboxNode ancestor)
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            while (candidate != null && !string.IsNullOrEmpty(candidate.parentId) && seen.Add(candidate.id)) { if (candidate.parentId == ancestor.id) return true; candidate = document.nodes.Find(n => n.id == candidate.parentId); }
            return false;
        }
        void Create()
        {
            string folder = ToolRoot + "/Editor/Data/Documents"; Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            string p = AssetDatabase.GenerateUniqueAssetPath(folder + "/WhiteboxDocument.asset");
            document = CreateInstance<WhiteboxDocument>(); AssetDatabase.CreateAsset(document, p); AssetDatabase.SaveAssets(); selected = -1; hierarchySignature = "";
            message = "已创建工作文档。第1步：导入界面图。";
        }
        void ImportImage()
        {
            if (!document) Create(); if (!document) return;
            string p = EditorUtility.OpenFilePanelWithFilters("选择界面图", "", new[] { "Image", "png,jpg,jpeg" }); if (p.Length == 0) return;
            string folder = ToolRoot + "/Editor/Data/Sources"; Directory.CreateDirectory(folder);
            string target = AssetDatabase.GenerateUniqueAssetPath(folder + "/Source" + Path.GetExtension(p).ToLowerInvariant()); File.Copy(p, target); AssetDatabase.ImportAsset(target);
            var importer = (TextureImporter)AssetImporter.GetAtPath(target); importer.maxTextureSize = 16384; importer.npotScale = TextureImporterNPOTScale.None; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            Undo.RecordObject(document, "Import whitebox source"); document.source = AssetDatabase.LoadAssetAtPath<Texture2D>(target); document.resolution = new Vector2Int(document.source.width, document.source.height); WhiteboxRules.Save(document);
        }
        void Act(Action action) { try { action(); } catch (Exception e) { message = e.Message; UnityEngine.Debug.LogException(e); } }
    }
}
