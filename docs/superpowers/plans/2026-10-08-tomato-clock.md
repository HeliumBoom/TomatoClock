# TomatoClock Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** 交付支持自由缩放、紧凑悬浮、自动工作休息循环的 Windows 番茄钟。

**Architecture:** 计时模型独立于 WinForms，设置存储独立于 UI。窗体负责显示与输入，系统电源消息负责暂停，单实例入口负责再次启动激活。

**Tech Stack:** C#、.NET 8 Windows Forms、System.Text.Json、Win32；无第三方依赖。

**Spec:** ../specs/2026-10-08-tomato-clock-design.md

## Global Constraints

- 时长 1～180 分钟，默认工作 25、休息 5，声音和置顶默认关闭。
- 普通客户区默认 400 × 420；紧凑最小 120 × 64；进入阈值 320 × 240，恢复阈值 360 × 280。
- 睡眠或休眠暂停，锁屏和最小化继续；恢复需要手动继续。
- 保持代码 CRLF。当前目录无 Git，不创建仓库或提交。
- 用户已明确要求执行，在本会话直接实现并验证。

## Review Focus

- 长时间 UI 延迟与阶段边界：测试一次跨越多轮、恰好到零。
- 设置文件字段类型损坏：逐字段回退，不丢失其他有效值。
- 无标题栏模式的缩放和双击冲突：测试尺寸切换与命中测试，实际手势另做验收。
- 电源恢复不能补扣休眠时间：模型暂停测试与窗口电源消息测试。
- 第二实例启动时首实例尚未显示：带有排队通知的命名事件激活，验证进程只存活一个。

## Task 1 计时与设置

Files: Core/PomodoroTimer.cs, Core/AppSettings.cs, Core/SettingsStore.cs, tests/TomatoClock.Tests/Program.cs, tests/TomatoClock.Tests/TomatoClock.Tests.csproj, TomatoClock.csproj.

Interfaces: PomodoroTimer(int workMinutes, int breakMinutes); Start(), Pause(), Reset(), Configure(int,int), Advance(TimeSpan) -> bool; Phase, State, Remaining, Duration, DisplayTime, Progress. SettingsStore(string path); Load() -> AppSettings; Save(AppSettings) -> string? error.

- [x] 写无第三方依赖的控制台测试，检查类型存在，再测试真实状态和持久化行为；运行确认缺失实现导致失败。
- [x] 实现工作休息切换、暂停、更新时长快照；验证 25 分钟工作经过 10 分钟后改为 15，本轮仍剩 15，下一轮才采用 15。
- [x] 实现逐字段 JSON 读取、缺省回退、临时文件替换和保存错误返回；测试工作字段损坏但声音 true 仍保留。
- [x] 运行 dotnet run --project tests/TomatoClock.Tests，要求全部通过。

## Task 2 窗体与交互

Files: Form1.cs, Form1.Designer.cs, UI/ClockFace.cs, UI/SettingsForm.cs, UI/WindowLayout.cs, NativeMethods.cs.

Consumes: Task 1 model and settings interfaces.

- [x] 添加真实窗体测试：初始 25:00、开始暂停、TopMost、缩小无边框、恢复、设置面板、最长时间渲染；运行确认旧空窗体失败。
- [x] 实现绘制自适应时钟、中文控件、阶段进度和操作按钮，设置用 owned 非模态窗体。
- [x] 实现紧凑布局、边缘缩放、拖动、双击恢复、右键和空格快捷键；按钮与控件不劫持文本输入。
- [x] 使用 Stopwatch 和 UI Timer 刷新；重置或修改设置前同步当前经过时间。
- [x] 运行完整测试；构建 Release。

## Task 3 生命周期与交付

Files: Program.cs, SingleInstance.cs, README.md, tests/TomatoClock.Tests/Program.cs.

- [x] 添加电源消息测试，确认运行中挂起后暂停且恢复不续计；单实例测试同时启动两进程。
- [x] 实现 WM_POWERBROADCAST 处理及恢复时清除时间基准；每用户会话 Mutex + 命名 EventWaitHandle，UI 线程激活已有实例。
- [x] 验证设置重启持久化、错误文件回退、模型与窗体测试全部通过。
- [x] 完成独立代码审查并修复重要发现，提供 Release 可执行程序和操作说明。
- [x] 记录已运行检查与仍需人工进行的休眠、锁屏、提示音、高 DPI 验收。
