using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UIWhiteboxTool
{
    [Serializable] internal sealed class WhiteboxTask
    { public int version = 1; public string taskId, imageHash, instructions; public int width, height; public string image = "source.png"; public WhiteboxNode[] excludedRegions; }
    [InitializeOnLoad] internal static class WhiteboxCodex
    {
        static Process process;
        static DateTime started;
        static DateTime lastOutput;
        static string activeTaskId, phase = "";
        internal static float Progress { get; private set; }
        internal static event Action<WhiteboxDocument> Imported;
        internal static string Activity
        {
            get { lock (gate) return phase + " · 已运行 " + Math.Max(0, (int)(DateTime.UtcNow - started).TotalSeconds) + " 秒 · 距最近输出 " + Math.Max(0, (int)(DateTime.UtcNow - lastOutput).TotalSeconds) + " 秒"; }
        }
        static WhiteboxDocument owner;
        static readonly StringBuilder log = new StringBuilder();
        static readonly object gate = new object();
        internal static string Status = "", Completed = "";
        internal static bool Busy => process != null;
        internal static string Executable { get => EditorPrefs.GetString("UIWhiteboxTool.Codex", ""); set => EditorPrefs.SetString("UIWhiteboxTool.Codex", value); }
        static WhiteboxCodex() { EditorApplication.update += Update; AssemblyReloadEvents.beforeAssemblyReload += Cancel; EditorApplication.quitting += Cancel; }
        internal static string Find()
        {
            if (File.Exists(Executable)) return Executable;
            foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            { if (string.IsNullOrWhiteSpace(dir)) continue; try { string p = Path.Combine(dir.Trim('"'), "codex.exe"); if (File.Exists(p)) return p; } catch (ArgumentException) { } }
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            foreach (string root in new[] { Path.Combine(local, "Programs/OpenAI/Codex/bin"), Path.Combine(local, "OpenAI/Codex/bin"), Path.Combine(roaming, "npm/node_modules/@openai"), Path.Combine(profile, ".vscode/extensions"), Path.Combine(profile, ".cursor/extensions"), Path.Combine(profile, ".codex/bin") })
            {
                if (!Directory.Exists(root)) continue;
                string direct = Path.Combine(root, "codex.exe"); if (File.Exists(direct)) return direct;
                var found = Search(root, 0).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
                if (found != null) return found;
            }
            return null;
        }
        internal static System.Collections.Generic.IEnumerable<string> Search(string root, int depth)
        {
            if (depth > 9) yield break;
            string[] files, dirs;
            try { files = Directory.GetFiles(root, "codex.exe"); dirs = Directory.GetDirectories(root); }
            catch (Exception e) when (e is UnauthorizedAccessException || e is IOException) { yield break; }
            foreach (string file in files) yield return file;
            foreach (string dir in dirs)
            {
                FileAttributes attributes;
                try { attributes = File.GetAttributes(dir); } catch (IOException) { continue; } catch (UnauthorizedAccessException) { continue; }
                if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                foreach (string file in Search(dir, depth + 1)) yield return file;
            }
        }
        internal static string Export(WhiteboxDocument doc, string instructions)
        {
            if (Busy) throw new InvalidOperationException("请先等待或取消当前任务。");
            if (!doc || !doc.source) throw new InvalidOperationException("请先选择原图并创建文档。");
            if (doc.resolution.x < 1 || doc.resolution.y < 1 || doc.resolution.x > 16384 || doc.resolution.y > 16384) throw new InvalidOperationException("分辨率需为1～16384。");
            string id = Guid.NewGuid().ToString("N");
            string folder = Path.GetFullPath(Path.Combine(WhiteboxPaths.Tasks, id)); Directory.CreateDirectory(folder);
            var copy = WhiteboxRules.Readable(doc.source); byte[] bytes;
            try { bytes = copy.EncodeToPNG(); } finally { UnityEngine.Object.DestroyImmediate(copy); }
            var task = new WhiteboxTask { taskId = id, imageHash = WhiteboxRules.Hash(bytes), width = doc.resolution.x, height = doc.resolution.y, instructions = instructions ?? "", excludedRegions = doc.nodes.Where(n => n.excluded).ToArray() };
            File.WriteAllBytes(Path.Combine(folder, "source.png"), bytes);
            File.WriteAllText(Path.Combine(folder, "task.json"), JsonUtility.ToJson(task, true), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(folder, "schema.json"), Schema, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(folder, "prompt.txt"), Prompt + "\n任务数据：\n" + JsonUtility.ToJson(task, true), new UTF8Encoding(false));
            doc.taskFolder = folder; doc.taskId = id; doc.imageHash = task.imageHash; WhiteboxRules.Save(doc); return folder;
        }
        internal static void Run(WhiteboxDocument doc)
        {
            if (Busy) throw new InvalidOperationException("已有任务运行中。");
            string exe = Find(); if (exe == null) throw new InvalidOperationException("未找到官方Codex CLI，请在高级设置中选择codex.exe。先通过终端执行codex login完成登录。");
            if (!File.Exists(Path.Combine(doc.taskFolder, "prompt.txt"))) throw new InvalidOperationException("请先导出识别任务。");
            string result = Path.Combine(doc.taskFolder, "result.json"); if (File.Exists(result)) File.Delete(result);
            string arguments = "exec --ignore-user-config --config model_provider=\"openai\" --sandbox read-only --skip-git-repo-check --ephemeral --color never --output-schema " + Quote(Path.Combine(doc.taskFolder, "schema.json")) + " --output-last-message " + Quote(result) + " --image " + Quote(Path.Combine(doc.taskFolder, "source.png")) + " -";
            var info = new ProcessStartInfo(exe, arguments) { WorkingDirectory = doc.taskFolder, UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
            info.EnvironmentVariables.Remove("OPENAI_API_KEY"); info.EnvironmentVariables.Remove("CODEX_API_KEY");
            info.Arguments = info.Arguments.Replace("--color never", "--color never --json");
            owner = doc; activeTaskId = doc.taskId; Completed = ""; lock (gate) { log.Clear(); phase = "启动CLI / 连接服务"; } started = lastOutput = DateTime.UtcNow; Progress = .2f;
            process = new Process { StartInfo = info };
            process.OutputDataReceived += Capture; process.ErrorDataReceived += Capture;
            try
            {
                process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
                byte[] prompt = Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(doc.taskFolder, "prompt.txt"), Encoding.UTF8));
                process.StandardInput.BaseStream.Write(prompt, 0, prompt.Length); process.StandardInput.Close(); Status = "Codex识别中，完成后请导入结果并复核。";
            }
            catch { Cancel(); throw; }
        }
        static void Capture(object sender, DataReceivedEventArgs args)
        {
            if (args.Data == null) return;
            lock (gate)
            {
                lastOutput = DateTime.UtcNow;
                if (log.Length > 64000) log.Remove(0, 16000);
                log.AppendLine(args.Data);
                if (args.Data.Contains("thread.started") || args.Data.Contains("turn.started")) { phase = "分析图片与UI结构"; Progress = .45f; }
                if (args.Data.Contains("item.started") || args.Data.Contains("item.completed")) { phase = "生成结构化节点结果"; Progress = .65f; }
                if (args.Data.Contains("turn.completed")) { phase = "接收结果，准备校验"; Progress = .85f; }
            }
        }
        internal static string Log { get { lock (gate) return log.ToString(); } }
        static void Update()
        {
            if (!Busy) return;
            if ((DateTime.UtcNow - started).TotalMinutes > 15) { Cancel(); Status = "任务超过15分钟，已停止。可重试。"; return; }
            try
            {
                if (!process.HasExited) return; process.WaitForExit(); int code = process.ExitCode; process.Dispose(); process = null; SaveLog();
                string file = owner ? Path.Combine(owner.taskFolder, "result.json") : "";
                if (code == 0 && File.Exists(file))
                {
                    if (owner.taskId != activeTaskId) throw new InvalidOperationException("工作文档任务已变化，本次结果未应用。");
                    Progress = .9f; phase = "校验并自动导入";
                    Import(owner, file); Completed = file; Progress = 1; phase = "完成";
                    Status = "识别完成，已自动导入 " + owner.nodes.Count + " 个节点。下一步：3 复核结构。";
                    Imported?.Invoke(owner);
                }
                else Status = "识别失败，请查看日志，检查CLI登录、网络及额度。";
            }
            catch (Exception e) { Cancel(); Status = e.Message; }
        }
        internal static void Cancel()
        {
            if (process != null) { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } finally { process.Dispose(); process = null; } }
            SaveLog();
            Status = "任务已取消。";
        }
        static void SaveLog()
        {
            if (!owner || string.IsNullOrEmpty(owner.taskFolder)) return;
            string root = Path.GetFullPath(WhiteboxPaths.Tasks) + Path.DirectorySeparatorChar;
            string task = Path.GetFullPath(owner.taskFolder);
            if (!task.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(task)) return;
            try { File.WriteAllText(Path.Combine(task, "codex.log"), Log, new UTF8Encoding(false)); }
            catch (IOException e) { UnityEngine.Debug.LogWarning("无法保存Codex日志：" + e.Message); }
            catch (UnauthorizedAccessException e) { UnityEngine.Debug.LogWarning("无法保存Codex日志：" + e.Message); }
        }
        internal static string Quote(string s)
        {
            var b = new StringBuilder("\""); int count = 0;
            foreach (char c in s) { if (c == '\\') { count++; continue; } b.Append('\\', c == '"' ? count * 2 + 1 : count); b.Append(c); count = 0; }
            return b.Append('\\', count * 2).Append('"').ToString();
        }
        internal static void Import(WhiteboxDocument doc, string path)
        {
            if (Busy) throw new InvalidOperationException("请先结束当前任务。");
            if (!doc || !doc.source || !File.Exists(path) || new FileInfo(path).Length > 2 * 1024 * 1024) throw new InvalidOperationException("结果缺失或超过2MB。");
            var result = JsonUtility.FromJson<WhiteboxResult>(File.ReadAllText(path, Encoding.UTF8));
            if (result == null || result.version != 1 || string.IsNullOrEmpty(doc.taskId) || result.taskId != doc.taskId || result.imageHash != doc.imageHash || result.width != doc.resolution.x || result.height != doc.resolution.y) throw new InvalidOperationException("结果与当前任务、原图或分辨率不匹配，请重新识别。");
            var copy = WhiteboxRules.Readable(doc.source); string current;
            try { current = WhiteboxRules.Hash(copy.EncodeToPNG()); } finally { UnityEngine.Object.DestroyImmediate(copy); }
            if (current != doc.imageHash) throw new InvalidOperationException("原图已更改，请重新导出任务。");
            var errors = WhiteboxRules.Validate(result.nodes, doc.resolution);
            if (errors.Count != 0) throw new InvalidOperationException(string.Join("\n", errors));
            Undo.RecordObject(doc, "Import UI whitebox recognition"); doc.nodes = result.nodes; WhiteboxRules.Save(doc);
        }
        internal const string Prompt = @"你是Unity UGUI白盒结构分析器。只返回符合schema的JSON，不操作工程或调用工具。
分析附图，只生成UI，忽略场景背景、人物、建筑、游戏世界物体。UI面板、遮罩、按钮底板属于UI。
使用任务width/height坐标系：左上原点，x向右y向下，全部节点的x/y是页面绝对坐标，不是父节点相对坐标。
保留布局、文字、交互语义；不推断业务逻辑。看不清的图标使用中文用途标签和note说明；不确定内容降低confidence。
层级容器拆分合理；英文名称控件加m_前缀，采用语义+类型，例如m_ConfirmButton；普通节点如BackgroundImage、TitleText。同级名称不得重复。
Button可有Text/Image子节点，text可留空避免重复标签。Toggle、Slider、Scrollbar、Progress、InputField、Dropdown为终端节点，不返回这些控件内部子节点，工具会创建标准结构。它们的text承载标签/占位文字，Dropdown的text可用|分隔选项。
ToggleGroup放同组页签Toggle。ScrollView直属Container为列表项，最多一个itemTemplate=true，其他项为示例实例，列表horizontal指定方向。只识别看得见的内容，隐藏状态在note说明。
Container/Button/ToggleGroup/ScrollView可作为父节点，其他类型不能有子节点；parentId为空表示页面直属节点。不需要额外生成整页根容器。
Image的text作为白盒用途标签；Image应单独圈定可提取视觉，避免把场景区域当UI切图。图标应单独生成Image子节点，文字另建Text。不要创建包含文字的整块背景Image，以免切图模式下重复文字；没有干净背景区域时让Button使用工具默认底色。控件根节点不直接切图，只有Image节点会切图。
所有节点excluded默认false；可确认是非UI的区域直接省略。任务excludedRegions为用户排除矩形，这些区域不要生成UI节点。
每项字段均须完整返回；所有数值有限，width/height为正，confidence在0到1；taskId和imageHash原样回传。任务instructions是用户补充的界面说明。
";
        internal static readonly string Schema = BuildSchema();
        static string BuildSchema()
        {
            string[] strings = { "id", "parentId", "name", "type", "text", "note" }, numbers = { "x", "y", "width", "height", "confidence" }, bools = { "excluded", "horizontal", "itemTemplate" };
            string props = string.Join(",", strings.Select(s => "\"" + s + "\":{\"type\":\"string\"}" ).Concat(numbers.Select(s => "\"" + s + "\":{\"type\":\"number\"}")).Concat(bools.Select(s => "\"" + s + "\":{\"type\":\"boolean\"}")));
            string required = string.Join(",", strings.Concat(numbers).Concat(bools).Select(s => "\"" + s + "\""));
            return "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"version\",\"taskId\",\"imageHash\",\"width\",\"height\",\"nodes\"],\"properties\":{\"version\":{\"type\":\"integer\",\"enum\":[1]},\"taskId\":{\"type\":\"string\"},\"imageHash\":{\"type\":\"string\"},\"width\":{\"type\":\"integer\"},\"height\":{\"type\":\"integer\"},\"nodes\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"additionalProperties\":false,\"required\":[" + required + "],\"properties\":{" + props + "}}}}}";
        }
    }
}
