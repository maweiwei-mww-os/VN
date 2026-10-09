# 渐进接入指南

## 安装与依赖

首个验证环境为 Unity 2022.3.60f1c1 + UGUI，Windows。通过 Package Manager 的 Add package from disk 选择本包 package.json。包不依赖 ReferenceProject，不包含页面路由，不需要 Addressables。

在 Package Manager 导入 Minimal Integration 示例，在空场景的空对象上挂 MinimalIntegrationDemo 即可验证基础接入。导入 SLG Armory Business 可直接打开其 Scenes/Inventory.unity，体验完整本地军备业务切片；业务 Sample 不是 Runtime 依赖。

## 一个预算域，一个 Runtime

场景放置 UiRuntime，并将其引用传给接入组件。默认 Update 驱动处理当前帧快照。FrameBudgetMs 是目标预算，不是可中断原生调用的硬上限。第一次 Instantiate、AddComponent 或文本原生工作仍可能单步超时。

调度器支持四级加权轮转、结构化 WorkKey、取消和异常诊断。执行期间产生的新工作以及续步进入下一帧。异常工作不会自动重试；业务自行确定是否可以重试。

## 状态接入

展示单元继承 IncrementalUiView，保存自己的类型化目标状态。setter 修改目标值并 MarkDirty；ApplyDirty 只写实际变化的字段。不要在 setter 中直接遍历和同步全部 UGUI 对象。

实例使用 OnRent -> BeginBind(stableId) -> 设置目标状态 -> 状态提交和布局就绪 -> OnReturn 的生命周期。MarkLayoutReady 是原生布局适配层的职责；不能在新建对象后立即伪装成布局就绪。内置固定集合使用 Canvas 回调确认布局阶段。

独立界面可以调用 ConfigureInteraction 指定 CanvasGroup。关键状态变化调用 InvalidateState；未就绪不能接受交互。数据模型的权威修改仍留在项目业务层。

一次性行为使用 UiCommandQueue。Enqueue(binding, action, readyPredicate) 可以等待业务自行判断的就绪条件；当前没有内置 TargetRevision/CommittedRevision 屏障，不能把它描述为自动等待指定提交版本。普通属性合并不能代替命令顺序。网络和支付操作不能作为可自动重试的展示命令。就绪条件或命令异常会终结该条命令并继续处理后续命令，不自动重放副作用。

## 固定列表和网格

实现 IVirtualizedItemSource 并配置 FixedVirtualizedScrollList。columns=1 为纵向列表，columns>1 为固定网格。正常接入保持 ScheduleWork=true 和 Virtualize=true；关闭它们仅用于显式兼容或参考工程对照。

同 ID 的内容改变调用 RefreshItem / RefreshItems；未知变更范围才调用 RefreshVisible。排序或筛选调用 RefreshStructure，按稳定 ID 保留实例；完全切换数据源才调用 SetSource。结构变化并不自动刷新同 ID 的新字段；两种变化同时发生时，两种通知都要发。Unbind 时 view.DataId 是旧业务绑定的身份，不要通过已经变动的数据数组和旧 index 反查旧对象。

内置集合的池缺失创建经过 Runtime。直接调用 UiViewPool.Rent 是同步底层 API；业务如需批量租借，应在自己的小步工作中调用，不能在 Update 中无预算循环。

## 可选片段

将通用 Item 的可选结构拆为独立模板，通过 UiFragmentDescriptor 配置 Id、Prefab、挂接点、依赖和 RequiredForInteraction。TemplateCatalog.Validate 检查重复 ID、缺失引用、未知依赖和循环依赖。

LazyFragmentHost.Configure 接收 Runtime、所属 PooledUiView 和描述列表。SetVisible(true) 申请准备，SetVisible(false) 不创建。SetVisibleState 一次设置全部分支需求。GetIfCreated 和 IsCreated 只查询。Require 是必要访问的同步兜底；RequireVisibleState 是小范围批量同步兜底，避免先排队又逐个取消请求；这条路径不受调度预算约束，不适合在大循环中使用。宿主复用时捕获新 Binding；旧请求不能在重新激活后恢复执行。

不要把动画或布局强耦合结构拆成很多微小片段。先用单步最大耗时和 C/B 对比判断拆分是否值得。

## 资源与清理

IUiSpriteProvider 接口适配现有资源系统。回调必须在 Unity 主线程执行，返回 Sprite 和释放消费者租约的 Action。Cancel 取消的是消费者需求，不假设底层共享加载一定停止。同步完成和取消后仍返回的结果均需支持。

AsyncSpriteSlot 同时校验请求序号和绑定代际，丢弃旧结果时释放租约。示例加载器仅模拟延迟，不代表网络、Addressables 或 AssetBundle 性能。

Scope.Track 登记事件解绑、动画停止、定时器停止或句柄释放动作。清理幂等且隔离异常。未登记的外部监听和第三方组件状态不会被框架自动发现，接入方必须提供重置契约。

UiViewPool 支持单池数量上限，并可注入共享 UiPoolBudget 实现多个池的总闲置数量限制。默认集合当前使用单池限制；项目级共享预算应显式注入扩展，不要声称已自动限制全场景所有缓存。字节预算、模板热更新迁移和自动内存压力策略仍属于后续能力。

CaptureLease 返回 UiLeaseToken，检查的是租借身份；CaptureBinding 检查的是本次内容绑定。池会拒绝重复归还和错误池归还，但低层 Return(view) 没有 token 参数，不替调用者校验租约代际。不要让过期异步回调直接归还新租约，应校验令牌并由拥有者统一归还。HOT/WARM/PROTO 三级池并未在此版本实现。

## 诊断与验证

编辑器 Window -> HighPerfUI -> Runtime Diagnostics 显示预算、队列、等待年龄、故障与池统计。Tools -> HighPerfUI -> Validate Fragment Descriptors 检查当前加载的片段配置。

项目 manifest 中加入 testables: ["com.highperfui.runtime"] 可启用包测试。先验证关闭、重绑、失效回调、异常和交互状态，再测性能。Runtime 的 Stopwatch 数值不包含引擎后续 Canvas 和渲染工作。
