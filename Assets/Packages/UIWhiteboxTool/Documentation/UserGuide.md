# UI白盒搭建：基础版使用说明

版本：1.0.0 Basic。基线：Windows、Unity 2022.3、uGUI 1.0.0、TMP 3.0.7。

## 安装

通过Assets → Import Package → Custom Package导入UIWhiteboxTool-1.0.0-Basic.unitypackage。安装根目录固定为Assets/Packages/UIWhiteboxTool，此处Packages是Assets内普通文件夹，不是UPM包。

工程需已安装uGUI和TextMeshPro及其Essential Resources。工具使用组件默认字体，不要求白盒阶段手动配置。默认字体缺字时由工程自行处理。

使用AI需要安装官方Codex CLI，完成codex login，并拥有可用网络和额度。工具会自动搜索常见安装目录；高级设置的手动exe路径仅用于自动搜索失败的情况。

## 五步流程

1. 打开Tools → UI白盒搭建 → 打开工作台，点击「1 导入图片」。新文档自动创建，无需选择保存位置。制作其他界面时可点「新建文档」。
2. 填写可选补充说明，点击「2 Codex识别（自动导入）」。顶部显示阶段进度、已运行秒数、距最近输出时间，支持取消。模型没有精确完成百分比；长时间无新输出时可查看日志。成功后自动校验并导入，可Undo撤销。
3. 在「3 结构复核」中点击原图框或Hierarchy树选择节点，调整名称、类型、父节点、布局和文字。节点树可折叠、展开及键盘选择；排除节点会排除整棵子树。
4. 点击「4 生成白盒Prefab」，检查差异后另存新版本。旧Prefab、程序脚本和事件不会被覆盖，尚不支持稳定ID增量合并。
5. 点击「5 加入测试Canvas」，在当前场景检查布局和原生控件交互，再由程序接入业务功能。此动作可Undo，不自动保存场景。

## 控件与视觉

支持Container、Image、Text、Button、Toggle、ToggleGroup、ScrollView、Scrollbar、Slider、Progress、InputField、Dropdown。Toggle、Slider等终端控件内部结构由生成器建立；Button可带Text/Image子节点。

ToggleGroup用于页签互斥。ScrollView直属Container可指定一个Item模板；工具导出独立Item Prefab，其余可见条目保留为示例。下拉选项用|分隔。

默认简化白盒采用颜色、状态和用途文字区分控件。启用「使用原图切图」时，Image节点矩形会导出PNG/Sprite；控件本身仍用原生组件和TMP文字。不自动抠图或补全被遮挡内容，超出原图的区域保留占位标签。

节点坐标为参考页面左上角绝对坐标。英文命名，控件使用m_前缀；同级不能重名。首版优先保持参考布局，不深入做多分辨率适配。

## 文件管理

所有以下路径均位于Assets/Packages/UIWhiteboxTool内部：

| 相对目录 | 内容 |
| --- | --- |
| Editor/Data/Documents | 自动创建的工作文档 |
| Editor/Data/Sources | 导入图片副本 |
| Editor/Data/Tasks | source.png、task.json、schema.json、prompt.txt、result.json及codex.log |
| Editor/Data/QA | 隔离批处理测试资产与报告 |
| Generated/面板名/Version_时间戳 | Prefab、Sprites、Items和Structure.json |

任务ID、图片哈希和分辨率不匹配，或结果层级不合法时不会应用识别结果。重新识别会替换当前工作文档节点，开始前会提示，可Undo。

Editor/Data、Generated为个人项目数据，不属于源码发布内容。源码.gitignore默认排除。Unity引擎缓存和官方CLI的账号配置仍由对应软件管理。旧版外部目录不自动删除；需要移动已有资源时请通过Unity Project窗口完成，以保留GUID引用。

## 验收与许可

已通过编译与19项确定性检查，真实图片识别及Unity运行交互仍待完整验收。请逐项检查按钮热区、Toggle选中、滚动裁剪、输入、下拉、Slider及列表模板引用。

本工具不编写业务逻辑或绑定业务事件。按MIT许可证开源，按现状提供；详见同目录LICENSE、DISCLAIMER.md和THIRD_PARTY_NOTICES.md。
