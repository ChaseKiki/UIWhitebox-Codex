# UIWhitebox-Codex

[简体中文](README.md) | [English](README.en.md)

An independent Unity editor tool that turns UI wireframes or concept screenshots into reviewable uGUI whitebox prefabs using Codex. Version **1.0.0 Basic** is the initial baseline: compilation and deterministic validation passed; real CLI recognition and Unity interaction still require runtime acceptance.

## Installation

Download [UIWhiteboxTool-1.0.0-Basic.unitypackage](Releases/1.0.0/UIWhiteboxTool-1.0.0-Basic.unitypackage) and import via Assets > Import Package > Custom Package. Alternatively copy Assets/Packages/UIWhiteboxTool and its .meta files into an existing Unity project. This is a legacy asset package, not a UPM package or a complete Unity project.

Baseline dependencies: Windows, Unity 2022.3, uGUI 1.0.0 and TMP 3.0.7 with essential resources. Install and authenticate the official Codex CLI separately; a browser login alone is insufficient. The tool automatically searches common CLI locations, with a manual override for custom installations.

Open Tools > UI白盒搭建 > 打开工作台. Follow the numbered actions: import image, recognize (automatic result import), review hierarchy, generate prefab, create preview Canvas. The editor UI is currently Chinese.

## Features and scope

Review screenshot overlays and a collapsible Hierarchy tree; edit node names, types, bounds, text and ownership. Generate native uGUI/TMP controls, list-item prefabs and optional rectangular crops for Image nodes. Recognition displays execution phases, elapsed time and last-output age; it does not claim a precise model completion percentage.

All Unity-side tool files, documents, source copies, tasks, logs, QA artifacts and generated assets remain under **Assets/Packages/UIWhiteboxTool**. User-specific Editor/Data and Generated folders are ignored by git. Unity engine caches and official CLI account/global files remain managed by their respective products.

Business logic, business event bindings, transparent-image reconstruction, complex responsive layouts and safe incremental merging are outside this baseline. Each generation saves a separate prefab version. Text uses the component/project default TMP font; missing glyphs are a project concern.

## License and data

MIT licensed; preserve the copyright and complete license notice when distributing. See [LICENSE](LICENSE), [DISCLAIMER.md](DISCLAIMER.md) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). No affiliation with Unity or OpenAI is claimed.

AI processing sends the image and task metadata through the authenticated official Codex CLI to its service. Use only assets you have permission to process. No user screenshots, fonts, credentials, CLI binary or model allowance are included. AI results and cropped resources require human review.

## Optional support

If this tool saves you repetitive work, you can support its continued maintenance on [Ko-fi](https://ko-fi.com/chasekiki) or through WeChat.

**Support is entirely voluntary. All features and MIT license rights remain available whether or not you contribute. A tip does not purchase features, priority handling or guaranteed response times.** This is a personally maintained project; bug reports, documentation improvements and sharing your experience are equally appreciated.

<details>
<summary>WeChat appreciation code (click to expand)</summary>

Scan using WeChat. Click the image to open the original.

<a href="https://raw.githubusercontent.com/ChaseKiki/PSD2UGUI-Codex/main/docs/support/wechat-support.jpg"><img src="https://raw.githubusercontent.com/ChaseKiki/PSD2UGUI-Codex/main/docs/support/wechat-support.jpg" alt="ChaseKiki WeChat appreciation code" width="320"></a>

</details>

The repository's Sponsor button also links to [Ko-fi](https://ko-fi.com/chasekiki) and the [support page](docs/support/README.md).
