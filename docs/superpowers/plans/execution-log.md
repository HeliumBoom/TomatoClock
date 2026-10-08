# Execution log — 2026-10-08-tomato-clock.md

- Ruling: 用户“执行该计划”作为直接执行授权，本会话实现，不重复请求流程确认。
- Ruling: 当前目录不是 Git 仓库，跳过 worktree、Git 提交及依赖 Git 的跟踪脚本；以此日志记录验证。
- Pre-flight: Task 2 消费 Task 1 的计时与设置接口；Task 3 使用窗体生命周期与已定义设置。无接口冲突。
- Task 1: complete — 10 个模型和设置测试先因缺失实现失败，再全部通过。
- Task 2: complete — 5 个窗体测试先因缺失实现失败，再全部通过；检查普通、设置、紧凑暂停的渲染图。
- Task 3: 单实例跨进程测试先失败再通过；目前 16/16，Release 构建 0 警告、0 错误。独立审查进行中。
- Verification boundary: 电源测试为窗口消息注入；未使用户电脑休眠、锁屏或更改显示器缩放。
- Final review: 独立只读审查发现 3 项 P2；无 P0/P1。
- Final: fixed 紧凑窗口悬停提示无消息入口 — WM_NCMOUSEHOVER → ToolTip.Popup 回归先失败后通过；增加非客户区鼠标跟踪。
- Final: fixed 恢复普通尺寸后窗口超出当前工作区 — 大屏历史尺寸回归先失败后通过；恢复时限制尺寸和位置。
- Final: fixed 生产与测试 DPI 初始化不一致 — 真实 ApplicationConfiguration 初始化回归先失败后通过；统一 PerMonitorV2，设置窗体声明 96 DPI 基准，主窗体显式布局。
- Final: 补充八方向命中及双击消息、设置重启、提前激活信号验证；完整测试 22/22 通过。
- Final: Ruling: 真实鼠标手势、休眠、锁屏、音频和多显示器 DPI 留作人工验收；不为自动验证改变用户系统状态。风险是这些设备相关行为尚无现场验收证据。
- Final: Ruling: 再次启动使用显示、Activate 与 SetForegroundWindow 请求已有窗口；Windows 前台焦点限制下仍可能只闪烁任务栏，不宣称绕过操作系统焦点策略。
- Final: Ruling: 不增加崩溃注入、跨完整性级别启动支持或规格排除的托盘、统计与长休息；本轮按普通用户桌面应用范围交付。
- Delivery: Release 解决方案构建 0 警告、0 错误；Release 测试 22/22；框架依赖版本发布到 dist。
- Published executable smoke: 实际启动 dist/TomatoClock.exe 并等待 UI 就绪成功；第二进程退出码 0，首进程继续存活；测试启动的进程已关闭。

## 托盘支持增量

- 用户追加要求不在任务栏显示、支持右下角托盘。新增持久化 TrayOnly 设置，普通默认不变；托盘模式下关闭或最小化收起，明确退出才结束。
- 新增 Form1.Tray.cs 管理 NotifyIcon、共享菜单、显示／隐藏与退出；窗口恢复复用单实例激活路径。
- 3 项功能测试先失败；实现后复现恢复最小化状态重入和菜单可用状态未同步，分别加入恢复保护与可见性刷新后通过。
- 完整测试 26/26：含隐藏窗口实际 UI 定时器推进、托盘持久化、恢复与退出、系统关闭不拦截。
- 独立只读审查未发现功能缺陷；补充其建议的真实 ShowInTaskbar true→false→true 句柄重建与设置窗口仍可交互检查，完整测试仍为 26/26。
- Release 构建 0 警告、0 错误。原 dist DLL 被正在运行的旧程序占用，覆盖发布失败；为保留当前计时，改为发布到 dist-tray。真实 Windows 通知区域点击和系统折叠图标位置未做桌面人工验收。

## 透明度与鼠标穿透增量

- 新增 OpacityPercent（10～100，默认 100）与 ClickThrough（默认 false），设置实时应用并持久化。设置窗体不继承效果。
- Form1.Appearance.cs 使用 CreateParams 保留分层／透明窗口风格，兼容句柄重建；透明度为 100% 时同样初始化分层 alpha。
- 托盘恢复入口条件扩大为 TrayOnly 或 ClickThrough，不改变原任务栏显示偏好。
- 4 项新增回归先失败后通过；补充真实独立进程作为下层窗口，Windows WindowFromPoint/GetAncestor 在开启穿透前／后／取消后依次命中自身／其他进程／自身。
- 全套测试 31/31 通过；跨进程检查为实际 Windows 命中测试，未注入用户鼠标输入。
- 独立只读审查无重大问题；补充穿透开启时不透明度 40%→100%→20% 的实际原生 alpha 和样式检查，通过。
- Release 构建 0 警告、0 错误；最终 31/31 通过；发布目录 dist-overlay，未中断旧版正在进行的计时。

## 托盘模式隐藏 Alt+Tab

- 用户要求托盘模式不进入 Alt+Tab 列表。主窗体 CreateParams 按 TrayOnly 添加 WS_EX_TOOLWINDOW、移除 WS_EX_APPWINDOW；设置窗体同步应用相同排除规则。
- 托盘开关显式刷新原生样式，覆盖 ShowInTaskbar 已为 false 的情况。关闭后由 WinForms 默认样式恢复任务栏／窗口切换；鼠标穿透和透明度样式保持独立。
- 2 项回归先失败后通过：开启／关闭、已打开设置窗口、边框模式切换、穿透透明度保持，以及持久化启动与托盘收起恢复。
- 全套测试 33/33 通过，Release 构建 0 警告、0 错误，发布到 dist-overlay。
- 验证边界：检查真实 HWND 扩展样式；未操作用户桌面 Alt+Tab 切换界面。
