# PaneSet

修改产品行为时，先读 docs/PRODUCT.md；实现 Windows 窗口操作时，同时读 docs/WINDOWS_PLAN.md。需要判断竞品或系统接口事实时，查 docs/REFERENCES.md。

运行检查：`verify.cmd -Check --case <case>`（等价 tools/run.ps1 -Check）。用户实例正在运行时 Release 输出被占用，改 `dotnet build -c Debug`（.tools/dotnet/dotnet.exe，先设 DOTNET_ROOT）；注入类检查（quick-*、overview、appearance 等）在有前台抢占的交互环境会环境性失败，判定时须与既有基线对照。

## Agent skills

### Issue tracker

任务和规格使用本地 Markdown，位于 .scratch/<feature>/。读写任务前查看 docs/agents/issue-tracker.md。

### Triage labels

使用技能默认的五个分诊标签。处理外部问题时查看 docs/agents/triage-labels.md。

### Domain docs

采用单一上下文。探索领域概念或决策时查看 docs/agents/domain.md。
