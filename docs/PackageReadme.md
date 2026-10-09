# HighPerfUI：Unity UGUI 虚拟节点框架

主交付：`com.highperfui.runtime-0.3.0-preview.3.tgz`。这是源码 UPM 包，不是只提供可执行程序的 Demo。

## 运行

Unity 2022.3 LTS > Window > Package Manager > + > Add package from tarball，选择 .tgz。

包的 Samples 页导入 **SLG Armory Business**，双击导入目录中的 `Scenes/Inventory.unity` 后点击 Play。也可通过 `Tools > HighPerfUI > Open Armory Sample` 打开场景。先体验仓库/穿戴/强化/奖励，再主动打开“性能对照”。

不需要这个业务界面时，不导入 Sample，只接 Runtime。Minimal Integration 是更小的接入参考。

## 交付内容

- .tgz：可安装包，含 Runtime、Editor、可选 Tests 和 Samples。
- Source/com.highperfui.runtime：同一版本源码，可 Add package from disk；不需要依赖整个原工程。
- Documentation：接入、架构、迁移、面试问答和验证边界。
- Evidence、ValidationResults.md：该交付真正跑过的测试和指纹，不预写性能收益。
- VerificationProject：已安装该 tarball、已导入 Sample 的无缓存验证工程，可用 Unity Hub 添加；不要在这个工程重复导入 Sample。
- Scripts/RunUnity.ps1：可配置编辑器路径，复跑 EditMode/PlayMode 或构建 Sample；调用方法见 ValidationResults.md。
- InterviewWalkthrough.md：从实际问题、关键不变量、选型代价到现场演示的高级客户端面试讲述。
- Examples/Windows/HighPerfUI.exe：可直接体验的可选 Windows 开发构建，不是框架接入依赖。

## 面试重点

虚拟节点在这里解决的是完整 Item 模板冗余、重复绑定及池化后的跨帧一致性。循环列表解决数量，按需片段解决模板复杂度，字段增量解决无效写入，生命周期令牌解决迟到回调。四者不能混为一个“虚拟节点提升百分比”。

本版本是 Windows Unity 2022.3 的预发布实现，不能直接声称已商业认证、移动端验证或“九分”。接入是显式低侵入，非透明代理任意 Unity API；动态高度、真实项目资源适配和目标机性能仍需验证。
