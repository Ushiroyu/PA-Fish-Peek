# PA Fish Peek 数据与天气审计

日期：2026-10-17  
活动版本：PA Fish Peek 2.8.16.57（主线程轮询版）

## 结论

本次更新前，预测逻辑只读 `PartyAnimalsAsset\MonoBehaviour`，而且运行时资源增强开关关闭；秋季资源目录中的鱼竿、两种新增鱼和天气位因此没有进入候选计算。活动源码已改为同时读取基础资源与 `FishingAutumnData\MonoBehaviour`，并在 F10 开启时预热鱼竿白名单、鱼种等级和当前天气。

当前活动目录静态统计为 70 个鱼种 JSON、17 个鱼竿 JSON、16 个唯一鱼竿名。`AE_006123_Rod.json` 在基础目录和秋季目录各有一份，秋季目录版本按后读规则覆盖基础版本。

## 更新后的数据交集

| 项目 | 当前数据 | 结论 |
|---|---:|---|
| `MeteorFish` | 秋季 4、地点 12、天气 64、等级 6 | 与 `AE_006182_Rod` 的 32 项白名单相交 |
| `Radiodont` | 秋季 4、地点 128、天气 64、等级 4 | 与 `AE_006123_Rod` 的 14 项白名单相交 |
| `AE_006123_Rod` | 秋季版本 14 项 | 14/14 可由当前 `path_id.txt` 解析，均有鱼种资源或静态回退 |
| `AE_006182_Rod` | 32 项 | 29 项有当前鱼种 JSON；`吉利鱼`、`河马`、`榴莲龟`只有静态等级/环境表，保留静态回退 |
| `AE_006183_Rod` | 内置旧档存在 | 当前资源目录没有同名 JSON，继续作为旧档回退，不当作当前运行时鱼竿 |

旧中文名与当前资源名的交集错误已在活动源码中统一修正：`大罐子鱼 → 试剂瓶`、`抱抱蝾 → 抱抱蜥`、`黑鲫鱼 → 黑鲤鱼`。修正覆盖鱼种环境表、等级表、鱼竿分类表和精确鱼竿档案；旧发布目录不参与构建，仍可回滚。

## 天气检测

候选上下文现在通过托管 `WorldTimeController.GetCurrentWeather()` 读取天气枚举，并保留未知位的数值。天气标签已加入 `64 → 流星雨`。秋季小屋实机日志已记录：

```text
[FishPeek] 运行时鱼竿白名单已加载：河边(轻/重)=29/32，冰洞(轻/重)=17/13，池塘(轻/重)=22/17。
[FishPeek] 运行时鱼种基础数据已加载：70 种。
[FishPeek] 当前天气已解析：晴朗 (1)。
```

本次实机房间进入、F10 开启、控制器发现和进程稳定均通过；没有出现 `GameAssembly.dll`、`0xc0000005` 或新崩溃转储。天气位 64 已由当前鱼种 JSON 和代码映射覆盖，但尚未人为切换到流星雨天气验证动态转换。

## 复现命令

```powershell
Set-Location '<Party Animals install>'
dotnet build '.\Resource\_tools\PAFishPeekPlugin_native_poll_20260809\PAFishPeekPlugin.csproj' -c Release -p:PAAutoDeploy=false --no-restore
Start-Process '.\PartyAnimals.exe'
# 自定义房间 → 实验室 → 秋季小屋；进入后按 F10
Get-Content '.\BepInEx\LogOutput.log' -Tail 180
```

构建结果为 0 errors、4 个既有 nullable/unused-field warnings。活动 DLL 位于 `BepInEx\plugins\PAFishPeek\PAFishPeekPlugin.dll`；回滚副本位于 `Resource\_tools\PAFishPeekPlugin_native_poll_20260809\releases\`。

## 验证边界

- 已验证：基础与秋季资源同时读取、鱼竿/鱼种运行时表加载、当前天气 1 读取、秋季小屋进入、F10 开启、进程稳定。
- 已修正：三组旧中文名造成的等级、环境和分类表交集失配。
- 待验证：天气从其他位切换到 64、多人同时咬钩、三种仅有静态回退鱼的服务器实际遭遇数据。
