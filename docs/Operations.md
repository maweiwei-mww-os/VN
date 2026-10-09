# 运行、接入与验证

## 不装 Unity，直接体验

解压完整交付包，双击根目录 `RunDemo.cmd`。`Builds/Windows` 必须整体保留，不能只拿走 exe。

启动即进入军备库，不是按钮演示面板。点击装备查看比较，左侧是角色配装；右侧可穿戴、强化和锁定。分类、搜索和品质/战力排序用于改变集合。A/B/C 按钮切换同一业务的实现，三组对比会重置演示数据。

运行三组对比期间锁定人工操作，完成后显示报告。它是快速体验，并非正式独立进程统计。要复核正式结果，使用下面的脚本。

## 在 Unity 中看源码

1. 安装 Unity 2022.3 LTS；本次验证补丁为 2022.3.60f1c1。
2. Unity Hub 添加交付目录中的 `ReferenceProject`。保持它与 `Packages` 同级，避免本地包路径失效。
3. 打开 `Assets/Scenes/Inventory.unity`，点击 Play。
4. 装备业务在 `Assets/HighPerfUI.Reference`；公共框架位于 Project 窗口 Packages 中的 High Performance UI Runtime。

没有附带 Library/Temp，首次导入会花时间；这不是缺少源码。`OpenUnity.cmd` 优先使用本机已验证安装目录，其他电脑可经 Hub 打开，或修改启动脚本的编辑器路径。

## 接入自己的工程

Package Manager 的加号菜单选择 Add package from tarball，选交付的 `com.highperfui.runtime-0.2.0-preview.1.tgz`；也可 Add package from disk 指向 package.json。导入 Minimal Integration 示例，在空场景空物体上挂 `MinimalIntegrationDemo`，运行即见固定列表。

随后按包内 `Documentation~/Integration.md` 接入一个热点界面。无需引入装备 Model、替换路由或改用本项目资源系统。卸载前删除场景、Prefab 和业务脚本对 HighPerfUI 类型的引用，再在 Package Manager Remove；已有业务数据不由本包持久化。

## 复跑自动验证

PowerShell 进入交付根目录。另一台电脑先修改 `Tools/Unity.ps1` 的 `$editor` 为实际 Unity.exe 路径。

```powershell
./Tools/Unity.ps1 -Action EditMode -Label local
./Tools/Unity.ps1 -Action PlayMode -Label local
./Tools/Package.ps1 -PackageOnly
./Tools/Unity.ps1 -Action PlayMode -Label install -Project ValidationProject
./Tools/Unity.ps1 -Action Build -Label local
./Tools/TestValidation.ps1
./Tools/Compare.ps1 -Count 1000 -Repeats 10 -Frames 1200
```

测试 XML 和日志在 Artifacts。独立 Player 的每轮摘要、逐帧 CSV、截图和 attempts 状态在 Benchmarks 的唯一输出目录；脚本拒绝混入旧结果，校验失败会退出报错。

测量时不要操作窗口，不要同时跑编辑器、编译、压缩等重负载，不要最小化 Player。结果受硬件、温度、帧率上限、开发构建和系统后台任务影响。本次无法控制所有系统噪声，重复结果不是手机性能承诺。

## 看懂数字

- 1000 项是业务数据规模；A 建 1000 个 Item，B/C 只维护视口及缓冲所需的 Item。
- 2ms 是框架调度目标，既不是整帧耗时，也不是硬上限；一个原生调用可以超预算。
- 首屏可交互从装备集合打开开始，公共壳层和共享图标已经准备，不含 Unity 进程启动。
- Frame P95/P99 是帧间隔分位，不是主线程纯 CPU 时间；120 FPS 上限会产生约 8.33ms 平台。
- 节点数统计集合内含 inactive 的 Transform，包括缓冲池，不含整个场景与共享模板。
- GC 是采样窗口内分配累计量，不是 GC 次数；MonoBytes 是末端托管占用，不代表总进程内存。
- 首轮和帧统计属于不同口径。当前 CSV 是组合轨迹，不应称为纯滚动或纯创建分布。

## 出问题先看哪里

白屏/黑屏优先看对应 PNG 和 Player 日志；无导出先看进程退出码和 attempts。队列停滞看 Runtime Diagnostics 的等待年龄、Faults 和必需片段状态。错图先核对 BindingGeneration、请求序号和资源提供器回调线程。复杂第三方组件回池后残留状态，应补充 Scope 清理和具体重置契约。
