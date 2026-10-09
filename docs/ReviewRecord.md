# 独立静态审查与修复

日期：2026-10-08。审查范围为调度、生命周期、片段、资源、集合和测量可信度；只读审查，不由审查者修改实现或运行测试。

| 发现 | 修复与验证 |
| --- | --- |
| 旧续步可能覆盖相同键的新任务 | 续步检查键是否已有新工作；OldContinuationCannotOverwriteNewKeyedRevision |
| 旧 Scope 注册可能释放新生命周期相同委托 | Registration 身份移除、清理前摘除；OldRegistrationCannotDisposeNewLifetimeRegistration |
| ApplyDirty 异常丢失脏字段 | 恢复快照但不自动重试；FailedDirtyFieldsAreRetainedForExplicitRetry |
| 批量 Unbind 异常留下后续存活视图 | 每项清理后汇总抛错；OneUnbindFailureDoesNotLeaveOtherScopesAlive |
| 无效范围任务已被调度丢弃，但请求索引残留 | 新请求核对 Scheduler 归属；DroppedRequestCanBeRequestedAfterCountRecovers |
| 命令 Ready 异常阻塞后续命令 | 失败命令出队并安排后续；ThrowingCommandReadinessDoesNotStrandFollowingCommands |
| 过载误丢弃必需片段或其低优先级依赖 | 沿显式需求传播关键性并提升调度级别；RequiredFragmentsAndDependenciesSurviveOverload |
| Cancel/Release 异常中断清理或重复释放 | 先摘除持有引用，再分别隔离异常；CancellationErrorStillReleasesLeaseOnlyOnce |
| 测量期间仍可手工操作 | 根 CanvasGroup 射线锁、清空焦点；PlayMode 断言被锁定按钮不可命中 |
| 用整屏非黑和自身 ID 自证正确 | 检查期望索引覆盖、唯一身份、真实图标/徽标；单独网格 ROI 像素检查与实际射线点击 |
| 批量脚本忽略退出失败/混入旧数据 | 唯一空目录、attempts、模式数/配置/语义/渲染校验；工具测试注入超时、错语义、空网格 |

新增回归最初失败已保留，集中修复后 EditMode 35/35、PlayMode 4/4、独立安装 4/4 通过。各测试套件包含重叠包测试，不能相加宣称 43 个不同测试。

同一审查者只读复查上述修复后，没有发现该范围内残留 P1/P2 或明显新增回归。该结论不是无缺陷保证；未做外部生产压力认证。完整原始测试证据随交付 Evidence 目录提供。
