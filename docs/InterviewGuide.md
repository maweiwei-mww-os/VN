# 资深客户端面试讲解与证据索引

## 项目定位

本项目是围绕复杂 UGUI 创建、刷新和复用问题实现的独立框架与验证工程。它采用商业项目工程约束，但未宣称已经在商业项目或手游真机上线。原 SLG 项目中观察到的问题、新实现的机制、本机实测结果分别叙述。

## 三分钟演示

1. 打开装备库，完成筛选、选中、穿戴与强化，先证明业务完整可操作。
2. 解释 A、B、C：A 全量实例；B 循环网格加池；C 在 B 上追加可选结构懒创建、增量提交和调度。
3. 打开真实报告，先看业务一致性和渲染有效性，再看首屏交互、帧尾耗时、节点与内存。
4. 主动指出 C 相对 B 的代价。若首屏更慢或 GC 更高，解释原因，不能只展示 C/A 最好看的百分比。

## 十分钟架构讲解

- 问题：完整通用 Prefab 包含很多未用分支；池化只减少重复创建，不减少每个实例的结构。
- 简单方案：先用常规循环列表和对象池，把逻辑数据量与真实实例量解耦。
- 剩余成本：Item 内部可选结构、重复状态提交、集中创建、异步回写和回收残留。
- 针对设计：片段按需创建；状态最终值合并；全局小步调度；Binding/Scope 保证归属；Pool 限制驻留。
- 正确性：失效必须立即发生，原生销毁可以随后执行；布局/关键状态未就绪不能交互。
- 验证：独立进程、相同数据脚本、同种素材，保留负收益和失败轮；原始 CSV 可以重算。
- 边界：单次 Instantiate 无法抢占；简单页面不必迁移；图标提供器是模拟实现，不伪装真实网络测试。

## 源码追问入口

| 追问 | 源码与测试 |
| --- | --- |
| 为什么有帧预算还会卡？ | Runtime/Core/FrameBudgetScheduler.cs 的单步时间与预算检查；报告的最大单步与帧时间 |
| 回调中再更新会丢失吗？ | RuntimeRegressionTests.WorkRequeuedDuringCompletionIsNotLost；IncrementalUiView 的提交后 Dirty 检查 |
| ID 没变，数量为什么需要刷新？ | CollectionRegressionTests.SameIdContentRefreshRebindsVisibleViews |
| 旧异步结果如何处理？ | AsyncSpriteSlot 的序号/Binding 双重检查；LifecycleRegressionTests.StaleAssetCallbackDoesNotClearNewRequest |
| 回收再激活为什么不会恢复旧子树？ | FragmentLifecycleTests.RebindDoesNotResurrectOldFragmentDemand |
| Scope 是否只是一个 bool？ | UiScope.Track/Invalidate；清理异常隔离与幂等测试 |
| 池是否会无限涨？ | UiViewPool 的租借归属与上限；GlobalPoolCapacityIsBoundedAndReleased |
| 为什么不是做完整 Virtual DOM？ | 类型化目标状态和 Dirty 字段满足当前接入需求，无需全树扫描及通用 Diff 成本 |
| 是否真的能独立接入？ | ValidationProject 只有包和 MinimalIntegration，不引用装备业务程序集 |

## 可讲的失败与修复案例

- 旧调度器在工作执行异常后，队列和去重集合不一致。先构造失败测试，再保证移除与异常隔离。
- 同 StableId 内容变化被列表的身份判断吞掉。区分绑定身份与内容刷新。
- 旧资源回调在检查有效性之前清空请求句柄，导致新的请求无法取消。先识别回调归属再修改当前句柄。
- 片段队列虽然检查代际，但后台重试仍可能把旧需求重新绑定给新对象。需求本身也必须在代际变化时清空。
- 隐藏 Windows 窗口得到黑屏与看似正常的低帧耗时。加入正常渲染和截图像素检查，保留并作废早期结果。

## 表述要求

不说“2ms 保证不卡”，而说“在可切分工作边界控制预算，记录无法切分的超支”。不说“减少真实节点就等于帧率提高”，而说明 CPU、布局、渲染、缓存和目标帧率限制各自的影响。

不预写收益数字。正式简历表述从验证后的报告提取，写清测试机器、规模和对照组。清楚说明个人参与的设计、验证与工具辅助过程。
