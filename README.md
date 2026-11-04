# UIWhitebox-Codex

[简体中文](README.md) | [English](README.en.md)

**Unity UI白盒搭建AI工具：从原型图或概念图出发，用Codex识别UI结构，经人工复核生成可供程序接入的UGUI Prefab。**

当前版本：**1.0.0 第一版基础版**。已通过Unity 2022.3程序集编译与确定性检查；真实图片识别与Unity交互尚待完整运行验收。

- [下载Unity安装包](Releases/1.0.0/UIWhiteboxTool-1.0.0-Basic.unitypackage)
- [使用说明](Assets/Packages/UIWhiteboxTool/Documentation/UserGuide.md)
- [更新记录](CHANGELOG.md)
- [MIT许可证](LICENSE) · [免责声明](DISCLAIMER.md)

## 适用场景

正式美术资产尚未到位时，先搭出控件、层级和布局，让程序能接入并测试自己的功能。输入是PNG/JPG原型图或概念成品图；场景背景、人物和建筑不生成，UI底板、遮罩和交互区域保留。

不提供业务逻辑、业务事件绑定、正式美术重建或复杂分辨率适配。控件语义、布局和交互需要人工复核。

## 基础版功能

- 自动查找本机Codex CLI；高级设置允许指定自定义路径。
- 显示识别阶段、耗时和距最近输出的时间，支持取消；成功后自动校验导入。
- 带步骤编号的工作台、原图节点框、Hierarchy式父子树与节点属性编辑。
- 支持容器、Image、TMP文字、Button、Toggle、ToggleGroup、ScrollView、Scrollbar、Slider、Progress、InputField、Dropdown。
- 简化白盒以颜色和用途文字区分；可选从Image节点矩形裁切原图。
- 列表Item模板、结构差异预览、Prefab另存版本、测试Canvas。
- 独立源码与Editor程序集，不依赖PsdTool。MIT开放源码，可自行修改和集成。

## 安装与操作

1. 在已有Unity工程中安装uGUI和TMP，导入TMP Essential Resources。基线为Windows、Unity 2022.3、uGUI 1.0.0、TMP 3.0.7。
2. 通过Assets → Import Package → Custom Package导入安装包；或复制本仓库Assets/Packages/UIWhiteboxTool及匹配.meta到工程。这里的Assets/Packages是普通资源目录，本仓库不是UPM包，也不是完整Unity示例工程。
3. 安装官方Codex CLI并完成codex login；需要网络、账号及可用额度。仅网页登录不等于CLI登录，工具不附带账号或额度。
4. 打开Tools → UI白盒搭建 → 打开工作台。
5. 依次点击「1 导入图片」「2 Codex识别（自动导入）」，然后在「3 结构复核」中检查并修正。
6. 点击「4 生成白盒Prefab」「5 加入测试Canvas」，由项目人员检查并接入功能。

文字使用组件/项目默认TMP字体，白盒阶段不要求手工选字体；默认字体缺字时由项目自行处理。

## 统一目录规则

所有工具源码与Unity内生成文件都在 **Assets/Packages/UIWhiteboxTool** 内管理：

| 目录 | 用途 |
| --- | --- |
| Editor | 编辑器源码 |
| Documentation | 使用说明、许可和免责声明 |
| Editor/Data/Documents | 自动创建的工作文档 |
| Editor/Data/Sources | 用户导入的参考图副本 |
| Editor/Data/Tasks | AI任务图片、协议、结果及日志 |
| Editor/Data/QA | 隔离工程测试产物 |
| Generated/面板名/Version_时间戳 | Prefab、Sprites、Item模板与结构快照 |

工具不在Assets根目录新建UIWhiteboxOutput或UIWhiteboxData，也不在Library下建立自己的任务目录。Unity与官方CLI自行管理的引擎缓存、账号和全局配置遵循各自软件规则。

Editor/Data和Generated为使用者数据，已被.gitignore排除；仓库只发布源码、文档和干净安装包。升级前请备份工作数据；旧版产生的Assets/UIWhiteboxOutput、Assets/UIWhiteboxData或Library任务不会被自动删除。如需归档已有资产，请在Unity Project窗口移动，以保留引用。

## 已知边界与验收

AI会误判或遗漏；进度条反映执行阶段，不是模型完成百分比。矩形切图不能恢复透明边缘、遮挡及独立图层。相同图片重新识别可能产生新节点ID，基础版采用另存版本，尚未实现安全增量合并。

生成成功不代表运行验收通过；真实CLI识别、Unity窗口及各控件点击/输入/滚动仍需验证。没有准确率、性能或节省工时百分比承诺。验证情况见[VALIDATION.txt](VALIDATION.txt)。

## 数据、许可与免责声明

启用AI会向所登录的官方Codex CLI及其服务提供图片、文字、几何和层级等任务数据，并非完全离线处理。请确认素材权利及服务使用授权。凭据由官方CLI管理，本仓库不提供或打包token、API Key和订阅。

本项目采用[MIT许可证](LICENSE)，允许使用、修改、分发和商用；分发时需保留版权与完整许可声明。软件按现状提供，无担保；见[免责声明](DISCLAIMER.md)及[第三方依赖说明](THIRD_PARTY_NOTICES.md)。本项目与Unity、OpenAI无官方隶属关系。

设计约定参考[PSD2UGUI-Codex](https://github.com/ChaseKiki/PSD2UGUI-Codex)，两者是独立工具。欢迎按[贡献说明](CONTRIBUTING.md)提交脱敏反馈。
