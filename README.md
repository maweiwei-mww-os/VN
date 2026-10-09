# VN / HighPerfUI

Unity UGUI 虚拟节点高性能 UI 框架。当前源码版本：**0.3.0-preview.3**。

框架针对复杂 UI 的按需子树、精准刷新和跨帧生命周期一致性；可独立接入 Unity 项目。军备整备中心是可选业务 Sample，不是 Runtime 的编译依赖。

## 安装框架

使用 Unity 2022.3 LTS，在 Package Manager 中选择 **Add package from git URL**：

```text
https://github.com/maweiwei-mww-os/VN.git?path=/Packages/com.highperfui.runtime#main
```

也可以克隆本仓库，通过 **Add package from disk** 选择 `Packages/com.highperfui.runtime/package.json`。复现本次开发环境可使用 Unity 2022.3.60f1c1；其他版本尚未逐一验证。

包的 Samples 页包含 Minimal Integration、Reward List Demo 和 SLG Armory Business。导入军备 Sample，打开其 `Scenes/Inventory.unity`，点击 Play。

## 直接运行业务工程

1. 克隆整个仓库，在 Unity Hub 中添加 `ReferenceProject`。
2. 等待依赖解析，打开 `Assets/Scenes/Inventory.unity`。
3. 点击 Play，体验筛选/搜索、穿戴、强化、锁定、混合奖励领取和本地进度保存。

参考工程使用相对路径引用仓库中的框架，必须保留目录结构。正常入口是业务界面；“性能对照”是可选诊断入口。源码仓库不提交 Windows 二进制，需自行构建后再使用 `RunDemo.cmd`。`OpenUnity.cmd` 与 `Tools/Unity.ps1` 的默认编辑器位置来自开发机，其他机器推荐直接用 Unity Hub 打开。

## 目录

| 目录 | 内容 |
|---|---|
| `Packages/com.highperfui.runtime/Runtime` | 调度、增量展示、片段、池化、资源与集合 |
| `Packages/com.highperfui.runtime/Editor` | 编辑器诊断工具 |
| `Packages/com.highperfui.runtime/Tests` | 可选框架回归测试 |
| `Packages/com.highperfui.runtime/Samples~` | 三个可选接入/业务示例 |
| `Packages/com.highperfui.runtime/Documentation~` | 当前版本架构、接入、迁移、面试与验证说明 |
| `ReferenceProject` | 可运行军备业务、存档、测试与对比工具 |
| `ValidationProject/Assets` | 导入脚本所需的安装验证辅助源码，不是单独可打开的 Unity 工程 |
| `Tools` | 导出、包契约检查、测试、构建与采样脚本 |
| `docs` | 设计过程、面试材料与已记录的验证结果 |

## 核心机制

- 固定高度循环列表与有界池：限制实例数量；稳定 ID 与 index 分离，结构更新和内容更新分别处理。
- 类型化目标状态与 dirty mask：只同步相关字段，保留应用过程中的重入变更。
- 显式可选片段与依赖管理：按需实化、失败门禁、同步 Require 兜底及隐藏片段清退。
- 四优先级加权协作调度：工作合并、取消、异常隔离、软预算和超预算统计。
- Scope、绑定代际、请求序号与租约：隔离回收/重绑后的迟到回调，释放过期资源句柄。
- 提交/布局/必要片段就绪后的交互门禁，避免未就绪状态接收交互。

这是显式低侵入适配，不是透明代理任意 Unity API；不是完整 React Fiber、硬抢占或全局原子提交框架。

## 验证与性能边界

已记录的该版本实际 tarball 独立安装结果：Runtime-only EditMode 58/58、PlayMode 3/3；导入全部 Sample 后 EditMode 98/98、PlayMode 9/9；Windows Development / Mono 构建成功。它们是此前实际执行的记录，不能相加算作互不重叠测试，也不表示本次 Git 推送重新运行了全部 Unity 测试。

1000条业务记录的初步独立进程实测中，相对循环列表基线 B，C 的节点中位数从1324降到726，绑定次数从6760降到2150；但 C 首屏更慢、分配字节更多。A 的3次尝试有超时和缺结果，**完整 A/B/C 验收未通过**。额外一次验证还出现编辑器退出阶段停滞，未计为正常退出通过。

详见 [验证记录](docs/ValidationResults.md)。原始 XML、日志、CSV、截图和可执行程序保存在本地完整交付包，不包含在这个源码仓库中。2ms 是协作式软预算，不是整帧或 Instantiate 的硬上限。本版本为预发布，尚未完成手机真机、IL2CPP、长期压力及真实项目资源系统验证。

## 面试材料

- [技术讲述与演示路径](docs/InterviewWalkthrough.md)
- [当前实现架构](Packages/com.highperfui.runtime/Documentation~/Architecture.md)
- [接入与迁移](Packages/com.highperfui.runtime/Documentation~/Migration.md)
- [验证协议](Packages/com.highperfui.runtime/Documentation~/Validation.md)

重点讨论业务问题拆解、身份不变量、错误路径、选型代价和真实指标，而不是以抽象层数或技术名词代替收益。
