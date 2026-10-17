using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Hook;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PAFishPeekPlugin;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed partial class FishPeekPlugin : BasePlugin
{
    public const string PluginGuid = "local.partyanimals.fishpeek";
    public const string PluginName = "PA Fish Peek";
    public const string PluginVersion = "2.8.16.57";

    private const int MaxScanAttempts = 80;
    private const float ScanIntervalSeconds = 2f;
    private static readonly bool EnableNativeEncounterHook = false;
    private static readonly bool EnableNativeEncounterPolling = true;
    private static bool EnableLocalSelfEncounterHook = false;
    private static readonly bool EnableVerboseInternalLogs = false;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void EncounterCallbackDelegate(IntPtr thisPtr, IntPtr eventParamPtr, IntPtr extraContextPtr, IntPtr methodInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryBasicInformation
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public nuint RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [DllImport("kernel32.dll")]
    private static extern nuint VirtualQuery(IntPtr lpAddress, out MemoryBasicInformation lpBuffer, nuint dwLength);

    private const uint MemCommit = 0x1000;
    private const uint PageNoAccess = 0x01;
    private const uint PageGuard = 0x100;

    private static ManualLogSource? _log;
    private static Harmony? _harmony;

    private static bool _scanStopped;
    private static bool _tickPatched;
    private static bool _hooked;
    private static bool _managedEncounterPatched;
    private static bool _managedEncounterWaitingLogged;
    private static bool _managedAssemblyProbeLogged;
    private static int _scanAttempts;
    private static float _nextScanRealtime;
    private static float _nextManagedPatchResolveRealtime;
    private static int _hookInvokeCount;
    private static int _methodInfoMismatchCount;
    private static int _invalidPayloadCount;
    private static int _lastLogicFrame = -1;

    private const float NativeControllerDiscoveryIntervalSeconds = 0.75f;
    private const float ControllerNameWarmupIntervalSeconds = 2.5f;
    private const int NativeArrayHeaderSize64 = 32;
    private static bool _nativeEncounterPollingReady;
    private static bool _nativeEncounterPollingErrorLogged;
    private static float _nextNativeControllerDiscoveryRealtime;
    private static float _nextControllerNameWarmupRealtime;
    private static bool _controllerNameWarmupLogged;
    private static IntPtr _miNativeEncounterGetter = IntPtr.Zero;
    private static IntPtr _classUnityResources = IntPtr.Zero;
    private static IntPtr _miResourcesFindObjectsOfTypeAll = IntPtr.Zero;
    private static IntPtr _nativeControllerSystemTypeObject = IntPtr.Zero;
    private static int _nativePollPassCount;
    private static int _nativePollNullArrayCount;
    private static int _nativePollValueCount;
    private static int _nativePollParseFailureCount;
    private static int _nativeControllerDiscoveryCount;
    private static readonly Dictionary<long, string> _nativePollEncounterSignatures = new();
    private static readonly Dictionary<long, IntPtr> _nativeControllerWeakHandles = new();
    private static readonly HashSet<long> _nativePollSeenControllers = new();
    private static readonly List<long> _nativePollStaleControllers = new();

    private static INativeDetour? _callbackDetour;
    private static EncounterCallbackDelegate? _callbackOriginal;
    private static EncounterCallbackDelegate? _callbackHookKeepAlive;
    private static INativeDetour? _selfEncounterDetour;
    private static EncounterCallbackDelegate? _selfEncounterOriginal;
    private static EncounterCallbackDelegate? _selfEncounterHookKeepAlive;
    private static INativeDetour? _spotReplyDetour;
    private static EncounterCallbackDelegate? _spotReplyOriginal;
    private static EncounterCallbackDelegate? _spotReplyHookKeepAlive;

    private static IntPtr _targetMethodInfo = IntPtr.Zero;
    private static IntPtr _targetFunction = IntPtr.Zero;
    private static IntPtr _selfEncounterMethodInfo = IntPtr.Zero;
    private static IntPtr _selfEncounterFunction = IntPtr.Zero;
    private static IntPtr _spotReplyMethodInfo = IntPtr.Zero;
    private static IntPtr _spotReplyFunction = IntPtr.Zero;
    private static IntPtr _targetControllerClass = IntPtr.Zero;
    private static IntPtr _waitingFlowControllerClass = IntPtr.Zero;
    private static IntPtr _miGetCurFishingPlayerSeat = IntPtr.Zero;
    private static IntPtr _miGetMainController = IntPtr.Zero;
    private static IntPtr _miSeatGetMainController = IntPtr.Zero;
    private static IntPtr _miSeatGetGamerInfo = IntPtr.Zero;
    private static IntPtr _miGetPlayerName = IntPtr.Zero;
    private static IntPtr _miGetControllerGlobalId = IntPtr.Zero;
    private static IntPtr _miGetMainControllerClientId = IntPtr.Zero;
    private static IntPtr _miGetGamerInfo = IntPtr.Zero;
    private static IntPtr _miGamerInfoGetGamerName = IntPtr.Zero;
    private static IntPtr _staticGameInfoClass = IntPtr.Zero;
    private static IntPtr _miGetSelfClientId = IntPtr.Zero;
    private static int _offsetSeatMainController = -1;
    private static int _offsetControllerGlobalId = -1;
    private static int _offsetFlowGameController = -1;
    private static bool _nameResolverReadyLogged;
    private static bool _nameResolverFailLogged;
    private static bool _gamerInfoNameFallbackLogged;

    private static IntPtr _eventStructClass = IntPtr.Zero;
    private static IntPtr _encounterStructClass = IntPtr.Zero;
    private static bool _eventStructIsValueType;
    private static bool _encounterStructIsValueType;

    private static int _offsetEventFishEncounter = -1;
    private static int _offsetEventGlobalId = -1;
    private static int _offsetEncounterTier = -1;
    private static int _offsetEncounterLevel = -1;
    private static int _offsetEncounterBodyType = -1;
    private static int _minReadableBytes;
    private static readonly int Il2CppObjectHeaderSize = IntPtr.Size * 2;

    private static string _lastEmitKey = string.Empty;
    private static long _lastEmitTick;
    private static readonly Dictionary<int, long> _playerLastEmitTick = new();
    private static readonly Dictionary<long, bool> _methodAcceptCache = new();
    private static readonly Dictionary<long, bool> _selfEncounterMethodAcceptCache = new();
    private static readonly Dictionary<long, bool> _spotReplyMethodAcceptCache = new();
    private static readonly object _playerTickLock = new();
    private static readonly Dictionary<long, string> _controllerPtrToName = new();
    private static readonly Dictionary<int, string> _controllerIdToName = new();
    private static readonly Dictionary<long, int> _controllerPtrToGlobalId = new();
    private static readonly Dictionary<int, string> _controllerIdToClientId = new();
    private static readonly object _nameCacheLock = new();
    private static readonly Queue<PendingEncounterItem> _pendingEncounterQueue = new();
    private static readonly object _pendingEncounterLock = new();
    private static float _nextSelfClientIdRefreshRealtime;
    private static float _nextSelfEncounterHookResolveRealtime;
    private static string _selfClientIdToken = string.Empty;
    private static int _selfEncounterHookInvokeCount;
    private static int _spotReplyHookInvokeCount;
    private static string _managedEncounterSource = string.Empty;
    private static bool _managedEncounterErrorLogged;
    private static bool _managedSpotErrorLogged;
    private static bool _managedRodErrorLogged;

    private enum BiteInfoDisplayMode
    {
        Off = 0,
        SelfOnly = 1,
        All = 2
    }

    private static BiteInfoDisplayMode _biteInfoDisplayMode = BiteInfoDisplayMode.All;

    private const float OverlayMessageHoldSeconds = 10f;
    private const float OverlayMessageFadeInSeconds = 0.18f;
    private const float OverlayMessageFadeOutSeconds = 1.1f;
    private const float OverlayLineGapPixels = 6f;
    private const int OverlayMaxMessages = 12;
    private const float OverlayBaseFontSize = 34f;
    private const float OverlayFontSizePerLevel = 6f;
    private const float ModeHintHoldSeconds = 1.4f;
    private const float ModeHintFadeSeconds = 0.35f;
    private const float ModeHintFontSize = 36f;
    private static bool EnableEncounterHookFeature = false;
    private static bool _encounterOutputEnabled;
    private const float HotkeyDebounceSeconds = 0.22f;
    private const KeyCode BiteInfoModeToggleKey = KeyCode.F11;
    private const KeyCode EncounterHookToggleKey = KeyCode.F10;
    private static readonly string[] OverlayPreferredFontNames =
    {
        "LetsParty-SDF_Chinese",
        "LetsParty-Heavy SDF",
        "cjkFonts_allseto_v1_11 SDF"
    };
    private static GameObject? _overlayCanvasGo;
    private static RectTransform? _overlayRootRect;
    private static RectTransform? _modeHintRect;
    private static TextMeshProUGUI? _modeHintLabel;
    private static readonly List<OverlayMessageItem> _overlayMessages = new();
    private static readonly List<TextMeshProUGUI> _overlayLabelPool = new();
    private static TMP_FontAsset? _overlayFontAsset;
    private static bool _overlayFallbackFontsBound;
    private static bool _overlayLayoutDirty;
    private static float _nextOverlayFontSearchRealtime;
    private static bool _overlayFontBoundLogged;
    private static FontStyles _overlayFontStyle = FontStyles.Bold;
    private static float _overlayOutlineWidth = 0.24f;
    private static Color32 _overlayOutlineColor = new(0, 0, 0, 225);
    private static int _lastScreenWidth;
    private static int _lastScreenHeight;
    private static float _modeHintShownRealtime = -999f;
    private static float _modeHintExpireRealtime = -999f;
    private static float _nextBiteInfoToggleRealtime;
    private static float _nextEncounterToggleRealtime;

    private sealed class OverlayMessageItem
    {
        public float CreatedAtRealtime;
        public float ExpireAtRealtime;
        public float LastAppliedAlpha = -1f;
        public int LevelBucket;
        public bool SuppressOutline;
        public TextMeshProUGUI? Label;
    }

    private sealed class PendingEncounterItem
    {
        public IntPtr ControllerPtr;
        public int GlobalId;
        public float Tier;
        public float Level;
        public int BodyType;
    }

    public override void Load()
    {
        _log = Log;
        _harmony = new Harmony(PluginGuid);
        TryEnableUtf8Console();

        TryPatchMainThreadTicker();

        _log?.LogMessage($"{PluginName} 已加载（主线程逐帧遭遇检测版）。[{PluginVersion}]");
        _log?.LogMessage($"[FishPeek] 咬钩显示切换键：{BiteInfoModeToggleKey}（{DescribeBiteInfoMode(_biteInfoDisplayMode)}）。");
        _log?.LogMessage($"[FishPeek] 预测开关切换键：{EncounterHookToggleKey}（默认关闭）。");
        _log?.LogMessage("[FishPeek] 今日可售鱼干功能已移除。");
        _log?.LogMessage("[FishPeek] 咬钩 callback/setter detour：禁用。");
        _log?.LogMessage("[FishPeek] 预测来源：低频发现控制器 + 主线程逐帧检查 fishJustEncounteredParam。");
    }

    private static void TryEnableUtf8Console()
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
        }
        catch
        {
            // ignored
        }
    }

    public override bool Unload()
    {
        DestroyOverlayUi();
        ReleaseAllNativeControllerHandles();

        try
        {
            _callbackDetour?.Dispose();
            _callbackDetour = null;
        }
        catch
        {
            // ignored
        }

        try
        {
            _selfEncounterDetour?.Dispose();
            _selfEncounterDetour = null;
        }
        catch
        {
            // ignored
        }

        try
        {
            _spotReplyDetour?.Dispose();
            _spotReplyDetour = null;
        }
        catch
        {
            // ignored
        }

        try
        {
            _harmony?.UnpatchSelf();
        }
        catch
        {
            // ignored
        }

        return true;
    }

    private static void TryPatchMainThreadTicker()
    {
        if (_harmony == null || _tickPatched)
        {
            return;
        }

        MethodInfo? update = AccessTools.Method(typeof(EventSystem), "Update");
        MethodInfo? postfix = AccessTools.Method(typeof(FishPeekPlugin), nameof(EventSystemUpdatePostfix));
        if (update == null || postfix == null)
        {
            _log?.LogWarning("[FishPeek] 未找到 EventSystem.Update 主线程驱动挂点。");
            return;
        }

        try
        {
            _harmony.Patch(update, postfix: new HarmonyMethod(postfix));
            _tickPatched = true;
            _log?.LogMessage("[FishPeek] 已挂接 EventSystem.Update 主线程驱动。");
        }
        catch (Exception ex)
        {
            _log?.LogWarning("[FishPeek] 挂接 EventSystem.Update 主线程驱动失败：" + ex.Message);
        }
    }

    public static void EventSystemUpdatePostfix()
    {
        try
        {
            int frame = Time.frameCount;
            if (frame == _lastLogicFrame)
            {
                return;
            }

            _lastLogicFrame = frame;

            float now = Time.realtimeSinceStartup;
            TryHandleEncounterHookToggleKey();
            if (EnableNativeEncounterPolling && EnableEncounterHookFeature && !_scanStopped && !_nativeEncounterPollingReady && now >= _nextScanRealtime)
            {
                _nextScanRealtime = now + ScanIntervalSeconds;
                TryNativeScan("tick");
            }

            if (EnableEncounterHookFeature && _nativeEncounterPollingReady)
            {
                PollNativeEncounterControllers(now);
            }

            // Explicit safety invariant for this experiment. The old branch remains
            // present for easy rollback, but the callback detour cannot be installed.
            if (EnableNativeEncounterHook && EnableEncounterHookFeature && !_scanStopped && !_hooked && now >= _nextScanRealtime)
            {
                _nextScanRealtime = now + ScanIntervalSeconds;
                TryNativeScan("legacy-detour-disabled");
            }

            if (EnableLocalSelfEncounterHook && _hooked && _selfEncounterDetour == null && now >= _nextSelfEncounterHookResolveRealtime)
            {
                _nextSelfEncounterHookResolveRealtime = now + ScanIntervalSeconds;
                TryResolveAndHookSelfEncounterCallback();
            }

            TryHandleBiteInfoModeToggleKey();
            DrainPendingEncounters();
            UpdateOverlayUi(now);
        }
        catch
        {
            // ignored
        }
    }

    private static void TryHandleBiteInfoModeToggleKey()
    {
        try
        {
            if (!Input.GetKeyDown(BiteInfoModeToggleKey))
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (now < _nextBiteInfoToggleRealtime)
            {
                return;
            }

            _nextBiteInfoToggleRealtime = now + HotkeyDebounceSeconds;

            _biteInfoDisplayMode = _biteInfoDisplayMode switch
            {
                BiteInfoDisplayMode.Off => BiteInfoDisplayMode.SelfOnly,
                BiteInfoDisplayMode.SelfOnly => BiteInfoDisplayMode.All,
                _ => BiteInfoDisplayMode.Off
            };

            _log?.LogMessage($"[FishPeek] 咬钩显示：{DescribeBiteInfoMode(_biteInfoDisplayMode)}。");
            ShowModeHint($"\u54ac\u94a9\u663e\u793a\uff1a{DescribeBiteInfoModeCn(_biteInfoDisplayMode)}");
        }
        catch
        {
            // ignored
        }
    }

    private static string DescribeBiteInfoMode(BiteInfoDisplayMode mode)
    {
        return mode switch
        {
            BiteInfoDisplayMode.Off => "不显示",
            BiteInfoDisplayMode.SelfOnly => "仅自己",
            _ => "显示所有"
        };
    }

    private static string DescribeBiteInfoModeCn(BiteInfoDisplayMode mode)
    {
        return mode switch
        {
            BiteInfoDisplayMode.Off => "\u4e0d\u663e\u793a",
            BiteInfoDisplayMode.SelfOnly => "\u4ec5\u81ea\u5df1",
            _ => "\u663e\u793a\u6240\u6709"
        };
    }

    private static void TryHandleEncounterHookToggleKey()
    {
        try
        {
            if (!Input.GetKeyDown(EncounterHookToggleKey))
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (now < _nextEncounterToggleRealtime)
            {
                return;
            }

            _nextEncounterToggleRealtime = now + HotkeyDebounceSeconds;

            if (!EnableEncounterHookFeature)
            {
                EnableEncounterHookFeature = true;
                _scanStopped = false;
                _scanAttempts = 0;
                _nextScanRealtime = 0f;
                _log?.LogMessage("[FishPeek] 主线程遭遇轮询：开启（等待解析控制器）。");
            }

            _encounterOutputEnabled = !_encounterOutputEnabled;

            if (_encounterOutputEnabled)
            {
                // Warm the prediction tables when the user enables prediction so
                // updated seasonal resources and the current weather are visible
                // in the log before the first bite arrives.
                _ = GetAccessoryFishByModeMap();
                _ = GetRuntimeFishBasicMap();
                TryResolveCurrentWeatherFlag(out _);
            }

            if (!_nativeEncounterPollingReady)
            {
                _log?.LogMessage($"[FishPeek] 预测输出：{(_encounterOutputEnabled ? "开启" : "关闭")}（轮询器等待解析；detour 禁用）。");
                ShowModeHint($"\u9884\u6d4b\u8f93\u51fa\uff1a{(_encounterOutputEnabled ? "\u5f00" : "\u5173")}");
                return;
            }

            _log?.LogMessage($"[FishPeek] 预测输出：{(_encounterOutputEnabled ? "开启" : "关闭")}（主线程轮询保持运行）。");
            ShowModeHint($"\u9884\u6d4b\u8f93\u51fa\uff1a{(_encounterOutputEnabled ? "\u5f00" : "\u5173")}");
        }
        catch
        {
            // ignored
        }
    }

    private static void TryPatchManagedEncounterCallback()
    {
        if (_harmony == null || _managedEncounterPatched)
        {
            return;
        }

        try
        {
            Type? controllerType = FindLoadedType("MiniGame.FishingGame.FishingGameController");
            if (controllerType == null)
            {
                if (!_managedEncounterWaitingLogged)
                {
                    _managedEncounterWaitingLogged = true;
                    _log?.LogMessage("[FishPeek] 正在等待钓鱼控制器互操作类型加载...");
                    LogRelevantLoadedAssemblies();
                }
                return;
            }

            _managedEncounterWaitingLogged = false;

            MethodInfo? encounterSetter =
                AccessTools.PropertySetter(controllerType, "fishJustEncounteredParam") ??
                AccessTools.Method(controllerType, "set_fishJustEncounteredParam");
            MethodInfo? spotSetter =
                AccessTools.PropertySetter(controllerType, "curDetailedFishingSpot") ??
                AccessTools.Method(controllerType, "set_curDetailedFishingSpot");
            MethodInfo? rodSetter =
                AccessTools.PropertySetter(controllerType, "currentRod") ??
                AccessTools.Method(controllerType, "set_currentRod");
            MethodInfo? encounterPostfix = AccessTools.Method(typeof(FishPeekPlugin), nameof(ManagedEncounterSetterPostfix));
            MethodInfo? spotPostfix = AccessTools.Method(typeof(FishPeekPlugin), nameof(ManagedDetailedSpotSetterPostfix));
            MethodInfo? rodPostfix = AccessTools.Method(typeof(FishPeekPlugin), nameof(ManagedCurrentRodSetterPostfix));
            if (encounterSetter == null || encounterPostfix == null)
            {
                return;
            }

            _harmony.Patch(encounterSetter, postfix: new HarmonyMethod(encounterPostfix));
            if (spotSetter != null && spotPostfix != null)
            {
                _harmony.Patch(spotSetter, postfix: new HarmonyMethod(spotPostfix));
            }

            if (rodSetter != null && rodPostfix != null)
            {
                _harmony.Patch(rodSetter, postfix: new HarmonyMethod(rodPostfix));
            }

            _managedEncounterPatched = true;
            _managedEncounterSource = $"{encounterSetter.DeclaringType?.FullName}::{encounterSetter.Name}";
            if (spotSetter != null)
            {
                _managedEncounterSource += " | set_curDetailedFishingSpot";
            }

            if (rodSetter != null)
            {
                _managedEncounterSource += " | set_currentRod";
            }

            _log?.LogMessage("[FishPeek] 已挂接托管控制器观察器。");
        }
        catch (Exception ex)
        {
            _log?.LogWarning("[FishPeek] 挂接托管控制器观察器失败：" + ex.Message);
        }
    }

    private static Type? FindLoadedType(string fullName)
    {
        Assembly[] assemblies;
        try
        {
            assemblies = AppDomain.CurrentDomain.GetAssemblies();
        }
        catch
        {
            return null;
        }

        for (int ai = 0; ai < assemblies.Length; ai++)
        {
            Assembly asm = assemblies[ai];
            try
            {
                Type? type = asm.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }
            catch
            {
                // ignored
            }
        }

        return null;
    }

    private static void LogRelevantLoadedAssemblies()
    {
        if (_managedAssemblyProbeLogged)
        {
            return;
        }

        _managedAssemblyProbeLogged = true;

        try
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var names = new List<string>();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    string? name = assemblies[i].GetName().Name;
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (name.IndexOf("PartyAnimals", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("MiniGame", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Recreate", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        names.Add(name);
                    }
                }
                catch
                {
                    // ignored
                }
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            if (EnableVerboseInternalLogs)
            {
                _log?.LogMessage("[FishPeek] 已加载相关程序集：" + (names.Count == 0 ? "（无）" : string.Join(", ", names)));
            }
        }
        catch (Exception ex)
        {
            if (EnableVerboseInternalLogs)
            {
                _log?.LogWarning("[FishPeek] 相关程序集探测失败：" + ex.Message);
            }
        }
    }

    private static void ManagedEncounterSetterPostfix(object __instance, object? __0)
    {
        try
        {
            if (!_encounterOutputEnabled || __0 == null)
            {
                return;
            }

            if (__instance is not Il2CppObjectBase il2CppInstance)
            {
                return;
            }

            if (!TryExtractEncounterFromManagedPayload(__0, out int globalId, out float tier, out float level, out int bodyType))
            {
                return;
            }

            IntPtr controllerPtr = il2CppInstance.Pointer;
            if (controllerPtr == IntPtr.Zero)
            {
                return;
            }

            if (globalId <= 0 && !TryGetControllerGlobalId(controllerPtr, out globalId))
            {
                return;
            }

            RememberControllerGlobalId(controllerPtr, globalId);
            QueueEncounter(controllerPtr, globalId, tier, level, bodyType);
        }
        catch (Exception ex) when (!_managedEncounterErrorLogged)
        {
            _managedEncounterErrorLogged = true;
            _log?.LogWarning("[FishPeek] 托管咬钩 setter 后处理失败：" + ex.Message);
        }
    }

    private static void ManagedDetailedSpotSetterPostfix(object __instance, object? __0)
    {
        try
        {
            if (__instance is not Il2CppObjectBase il2CppInstance)
            {
                return;
            }

            IntPtr controllerPtr = il2CppInstance.Pointer;
            if (controllerPtr == IntPtr.Zero ||
                !TryGetControllerGlobalId(controllerPtr, out int globalId) ||
                globalId <= 0)
            {
                return;
            }

            RememberControllerGlobalId(controllerPtr, globalId);
            if (TryExtractLocationFlagFromManagedDetailedSpot(__0, out int locationFlag))
            {
                CachePlayerLocationFlag(globalId, locationFlag, controllerPtr);
            }
        }
        catch (Exception ex) when (!_managedSpotErrorLogged)
        {
            _managedSpotErrorLogged = true;
            _log?.LogWarning("[FishPeek] 托管详细钓点 setter 后处理失败：" + ex.Message);
        }
    }

    private static void ManagedCurrentRodSetterPostfix(object __instance, object? __0)
    {
        try
        {
            if (__instance is not Il2CppObjectBase il2CppInstance)
            {
                return;
            }

            IntPtr controllerPtr = il2CppInstance.Pointer;
            if (controllerPtr == IntPtr.Zero ||
                !TryGetControllerGlobalId(controllerPtr, out int globalId) ||
                globalId <= 0)
            {
                return;
            }

            RememberControllerGlobalId(controllerPtr, globalId);
            if (TryExtractAccessoryTypeFromManagedRod(__0, out int accessoryType))
            {
                if (accessoryType != 0)
                {
                    CachePlayerAccessoryType(globalId, accessoryType);
                }
            }
        }
        catch (Exception ex) when (!_managedRodErrorLogged)
        {
            _managedRodErrorLogged = true;
            _log?.LogWarning("[FishPeek] 托管当前鱼竿 setter 后处理失败：" + ex.Message);
        }
    }

    private static bool TryExtractEncounterFromManagedPayload(object payload, out int globalId, out float tier, out float level, out int bodyType)
    {
        globalId = 0;
        tier = 0f;
        level = 0f;
        bodyType = 0;

        object current = payload;
        if (TryReadNumericMember(current, out int payloadGlobalId, "miniGameControllerGlobalId", "MiniGameControllerGlobalId", "m_MiniGameControllerGlobalId"))
        {
            globalId = payloadGlobalId;
        }

        if (TryReadMemberObject(current, out object? encounterParam, "fishEncounterParam", "FishEncounterParam", "m_FishEncounterParam") &&
            encounterParam != null)
        {
            current = encounterParam;
        }

        if (!TryReadNumericMember(current, out tier, "fishTier", "FishTier", "m_FishTier"))
        {
            return false;
        }

        if (!TryReadNumericMember(current, out level, "fishLevel", "FishLevel", "m_FishLevel"))
        {
            return false;
        }

        if (!TryReadNumericMember(current, out bodyType, "fishBodyType", "FishBodyType", "m_FishBodyType", "bodyType", "BodyType"))
        {
            return false;
        }

        return IsValidEncounter(tier, level, bodyType);
    }

    private static bool TryExtractLocationFlagFromManagedDetailedSpot(object? detailedSpotValue, out int locationFlag)
    {
        locationFlag = 0;
        if (detailedSpotValue == null)
        {
            return false;
        }

        if (!TryReadMemberObject(detailedSpotValue, out object? fishingSpot, "fishingSpot", "FishingSpot", "m_FishingSpot") ||
            fishingSpot == null)
        {
            return false;
        }

        if (!TryReadNumericMember(fishingSpot, out locationFlag, "spotLocation", "SpotLocation", "m_spotLocation", "location", "Location", "locationFlag", "LocationFlag"))
        {
            return false;
        }

        locationFlag = NormalizeFlagValue(locationFlag, new[] { 1, 2, 4, 8, 16, 32, 64, 128 });
        return locationFlag != 0;
    }

    private static bool TryExtractAccessoryTypeFromManagedRod(object? rodValue, out int accessoryType)
    {
        accessoryType = 0;
        if (rodValue == null)
        {
            return false;
        }

        if (!TryReadNumericMember(rodValue, out accessoryType, "accessoryType", "AccessoryType", "m_accessoryType", "currentAccessoryType", "CurrentAccessoryType"))
        {
            return false;
        }

        accessoryType = NormalizeAccessoryType(accessoryType);
        return accessoryType != 0;
    }

    private static void RememberControllerGlobalId(IntPtr controllerPtr, int globalId)
    {
        if (controllerPtr == IntPtr.Zero || globalId <= 0)
        {
            return;
        }

        lock (_nameCacheLock)
        {
            _controllerPtrToGlobalId[controllerPtr.ToInt64()] = globalId;
            if (_controllerPtrToGlobalId.Count > 256)
            {
                _controllerPtrToGlobalId.Clear();
            }
        }
    }

    private static bool TryReadMemberObject(object instance, out object? value, params string[] memberNames)
    {
        value = null;
        if (instance == null)
        {
            return false;
        }

        Type type = instance.GetType();
        for (int i = 0; i < memberNames.Length; i++)
        {
            string name = memberNames[i];
            try
            {
                PropertyInfo? property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null)
                {
                    value = property.GetValue(instance, null);
                    if (value != null)
                    {
                        return true;
                    }
                }

                FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    value = field.GetValue(instance);
                    if (value != null)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        return false;
    }

    private static bool TryReadNumericMember(object instance, out int value, params string[] memberNames)
    {
        value = 0;
        if (!TryReadMemberObject(instance, out object? raw, memberNames) || raw == null)
        {
            return false;
        }

        try
        {
            value = Convert.ToInt32(raw);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadNumericMember(object instance, out float value, params string[] memberNames)
    {
        value = 0f;
        if (!TryReadMemberObject(instance, out object? raw, memberNames) || raw == null)
        {
            return false;
        }

        try
        {
            value = Convert.ToSingle(raw);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryNativeScan(string reason)
    {
        if (_scanStopped || _hooked || _nativeEncounterPollingReady)
        {
            return;
        }

        _scanAttempts++;

        ScanResult scan;
        try
        {
            scan = NativeScanForEncounterCallback();
        }
        catch (Exception ex)
        {
            if (_scanAttempts <= 2 || _scanAttempts % 10 == 0)
            {
                _log?.LogWarning($"[FishPeek] 原生扫描失败（{reason}）：" + ex.Message);
            }

            StopWhenLimitReached();
            return;
        }

        if (!string.IsNullOrEmpty(scan.Error))
        {
            if (_scanAttempts <= 2 || _scanAttempts % 10 == 0)
            {
                _log?.LogWarning($"[FishPeek] 原生扫描报错（{reason}）：{scan.Error}");
            }

            StopWhenLimitReached();
            return;
        }

        if (_scanAttempts == 1 && EnableVerboseInternalLogs)
        {
            _log?.LogMessage($"[FishPeek] 原生扫描开始：程序集={scan.AssemblyCount}，类={scan.ClassCount}，方法={scan.MethodCount}");
        }

        if (!scan.Found)
        {
            if (_scanAttempts % 12 == 0 && EnableVerboseInternalLogs)
            {
                _log?.LogMessage($"[FishPeek] 原生扫描进行中（{reason}）：程序集={scan.AssemblyCount}，类={scan.ClassCount}，方法={scan.MethodCount}");
            }

            StopWhenLimitReached();
            return;
        }

        _targetMethodInfo = scan.MethodInfoPtr;
        _targetFunction = scan.MethodFunctionPtr;
        _targetControllerClass = scan.ControllerClassPtr;
        _eventStructClass = scan.EventStructClassPtr;
        _encounterStructClass = scan.EncounterStructClassPtr;
        _eventStructIsValueType = scan.EventStructIsValueType;
        _encounterStructIsValueType = scan.EncounterStructIsValueType;

        _offsetEventFishEncounter = scan.OffsetEventFishEncounter;
        _offsetEventGlobalId = scan.OffsetEventGlobalId;
        _offsetEncounterTier = scan.OffsetEncounterTier;
        _offsetEncounterLevel = scan.OffsetEncounterLevel;
        _offsetEncounterBodyType = scan.OffsetEncounterBodyType;

        _minReadableBytes = ComputeMinReadBytes();
        TryResolvePlayerNameMethods();

        if (EnableVerboseInternalLogs)
        {
            _log?.LogMessage(
                $"[FishPeek] 已找到咬钩回调：{scan.FoundImage}::{scan.FoundNamespace}.{scan.FoundClass}.{scan.FoundMethod} " +
                $"fn=0x{_targetFunction.ToInt64():X} p0={scan.Param0TypeName}");

            _log?.LogMessage(
                $"[FishPeek] 结构偏移：event(enc={_offsetEventFishEncounter},gid={_offsetEventGlobalId}) " +
                $"encounter(tier={_offsetEncounterTier},level={_offsetEncounterLevel},body={_offsetEncounterBodyType}) " +
                $"minRead={_minReadableBytes} evtVT={_eventStructIsValueType} encVT={_encounterStructIsValueType}");
        }

        if (EnableNativeEncounterPolling)
        {
            TryInitializeNativeEncounterPolling(scan);
            return;
        }

        TryHookEncounterCallback();
    }

    private static void TryInitializeNativeEncounterPolling(ScanResult scan)
    {
        try
        {
            _miNativeEncounterGetter = FindZeroArgMethod(
                _targetControllerClass,
                new[] { "get_fishJustEncounteredParam", "get_FishJustEncounteredParam" },
                "FishEncounterParam");

            _classUnityResources = FindClassByNameAnyImage("Resources", "UnityEngine");
            _miResourcesFindObjectsOfTypeAll = FindOneArgMethod(
                _classUnityResources,
                "FindObjectsOfTypeAll",
                "System.Type",
                "UnityEngine.Object");

            IntPtr controllerType = IL2CPP.il2cpp_class_get_type(_targetControllerClass);
            _nativeControllerSystemTypeObject = controllerType == IntPtr.Zero
                ? IntPtr.Zero
                : IL2CPP.il2cpp_type_get_object(controllerType);

            if (_miNativeEncounterGetter == IntPtr.Zero ||
                _classUnityResources == IntPtr.Zero ||
                _miResourcesFindObjectsOfTypeAll == IntPtr.Zero ||
                _nativeControllerSystemTypeObject == IntPtr.Zero)
            {
                _scanStopped = true;
                _log?.LogWarning(
                    $"[FishPeek] 主线程轮询初始化失败：getter=0x{_miNativeEncounterGetter.ToInt64():X} " +
                    $"Resources=0x{_classUnityResources.ToInt64():X} findAll=0x{_miResourcesFindObjectsOfTypeAll.ToInt64():X} " +
                    $"type=0x{_nativeControllerSystemTypeObject.ToInt64():X}。");
                return;
            }

            _nativePollEncounterSignatures.Clear();
            ReleaseAllNativeControllerHandles();
            _nativePollSeenControllers.Clear();
            _nativePollStaleControllers.Clear();
            _nativeEncounterPollingReady = true;
            _scanStopped = true;
            _nextNativeControllerDiscoveryRealtime = 0f;
            _log?.LogMessage(
                $"[FishPeek] 主线程遭遇检测已就绪：{scan.FoundImage}::{scan.FoundNamespace}.{scan.FoundClass}.get_fishJustEncounteredParam；" +
                $"控制器发现间隔={NativeControllerDiscoveryIntervalSeconds:0.00}s，字段检查=逐帧，callback/setter detour=OFF。");
        }
        catch (Exception ex)
        {
            _scanStopped = true;
            _log?.LogWarning("[FishPeek] 主线程轮询初始化异常：" + ex.Message);
        }
    }

    private static unsafe void PollNativeEncounterControllers(float now)
    {
        if (!_nativeEncounterPollingReady)
        {
            return;
        }

        if (now >= _nextNativeControllerDiscoveryRealtime)
        {
            _nextNativeControllerDiscoveryRealtime = now + NativeControllerDiscoveryIntervalSeconds;
            RefreshNativeEncounterControllers();
        }

        PollCachedNativeEncounterControllers();
    }

    private static unsafe void RefreshNativeEncounterControllers()
    {
        if (_miResourcesFindObjectsOfTypeAll == IntPtr.Zero ||
            _nativeControllerSystemTypeObject == IntPtr.Zero)
        {
            return;
        }

        IntPtr arrayPtr = InvokeOneArgObjectMethod(
            _miResourcesFindObjectsOfTypeAll,
            IntPtr.Zero,
            _nativeControllerSystemTypeObject);
        if (arrayPtr == IntPtr.Zero)
        {
            _nativePollNullArrayCount++;
            if (_nativePollNullArrayCount <= 3)
            {
                _log?.LogWarning($"[FishPeek] FindObjectsOfTypeAll 返回空指针 #{_nativePollNullArrayCount}。");
            }
            return;
        }

        uint rawCount;
        try
        {
            rawCount = IL2CPP.il2cpp_array_length(arrayPtr);
        }
        catch
        {
            return;
        }

        if (rawCount > 128)
        {
            if (!_nativeEncounterPollingErrorLogged)
            {
                _nativeEncounterPollingErrorLogged = true;
                _log?.LogWarning($"[FishPeek] 主线程轮询拒绝异常控制器数组：count={rawCount}。");
            }
            return;
        }

        int count = (int)rawCount;
        _nativeControllerDiscoveryCount++;

        _nativePollSeenControllers.Clear();
        IntPtr arrayStart = IntPtr.Add(arrayPtr, NativeArrayHeaderSize64);
        int arrayBytes = Math.Max(IntPtr.Size, count * IntPtr.Size);
        if (count > 0 && !IsReadable(arrayStart, arrayBytes))
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            IntPtr controllerPtr;
            try
            {
                controllerPtr = Marshal.ReadIntPtr(arrayStart, i * IntPtr.Size);
            }
            catch
            {
                continue;
            }

            if (controllerPtr == IntPtr.Zero || !IsTargetControllerInstance(controllerPtr))
            {
                continue;
            }

            long controllerKey = controllerPtr.ToInt64();
            _nativePollSeenControllers.Add(controllerKey);

            if (_nativeControllerWeakHandles.TryGetValue(controllerKey, out IntPtr existingHandle))
            {
                IntPtr existingTarget = GetNativeControllerHandleTarget(existingHandle);
                if (existingTarget == controllerPtr)
                {
                    continue;
                }

                FreeNativeControllerHandle(controllerKey);
            }

            IntPtr weakHandle;
            try
            {
                weakHandle = IL2CPP.il2cpp_gchandle_new_weakref(controllerPtr, false);
            }
            catch
            {
                weakHandle = IntPtr.Zero;
            }

            if (weakHandle != IntPtr.Zero)
            {
                _nativeControllerWeakHandles[controllerKey] = weakHandle;
            }
        }

        _nativePollStaleControllers.Clear();
        foreach (long controllerKey in _nativeControllerWeakHandles.Keys)
        {
            if (!_nativePollSeenControllers.Contains(controllerKey))
            {
                _nativePollStaleControllers.Add(controllerKey);
            }
        }

        for (int i = 0; i < _nativePollStaleControllers.Count; i++)
        {
            FreeNativeControllerHandle(_nativePollStaleControllers[i]);
        }

        WarmControllerNameMappings();

        if (_nativeControllerDiscoveryCount <= 3 || _nativeControllerDiscoveryCount % 40 == 0)
        {
            _log?.LogMessage(
                $"[FishPeek] 控制器发现 #{_nativeControllerDiscoveryCount}：当前={count}，有效句柄={_nativeControllerWeakHandles.Count}，" +
                $"getterValue={_nativePollValueCount}，parseFail={_nativePollParseFailureCount}。");
        }
    }

    private static void WarmControllerNameMappings()
    {
        float now = Time.realtimeSinceStartup;
        if (now < _nextControllerNameWarmupRealtime)
        {
            return;
        }

        _nextControllerNameWarmupRealtime = now + ControllerNameWarmupIntervalSeconds;
        int resolvedCount = 0;

        foreach (IntPtr handle in _nativeControllerWeakHandles.Values)
        {
            IntPtr controllerPtr = GetNativeControllerHandleTarget(handle);
            if (controllerPtr == IntPtr.Zero ||
                !TryGetControllerGlobalId(controllerPtr, out int globalId) ||
                globalId <= 0)
            {
                continue;
            }

            int beforeCount;
            lock (_nameCacheLock)
            {
                beforeCount = _controllerIdToName.Count;
            }

            TryLearnNameMappingFromController(controllerPtr, globalId);

            lock (_nameCacheLock)
            {
                if (_controllerIdToName.Count > beforeCount || _controllerIdToName.ContainsKey(globalId))
                {
                    resolvedCount++;
                }
            }
        }

        if (resolvedCount > 0 && !_controllerNameWarmupLogged)
        {
            _controllerNameWarmupLogged = true;
            _log?.LogMessage($"[FishPeek] 已预热多人昵称映射：{resolvedCount} 个控制器。");
        }
    }

    private static void PollCachedNativeEncounterControllers()
    {
        _nativePollPassCount++;
        _nativePollStaleControllers.Clear();

        foreach (KeyValuePair<long, IntPtr> entry in _nativeControllerWeakHandles)
        {
            long controllerKey = entry.Key;
            IntPtr controllerPtr = GetNativeControllerHandleTarget(entry.Value);
            if (controllerPtr == IntPtr.Zero)
            {
                _nativePollStaleControllers.Add(controllerKey);
                continue;
            }

            bool readOk = TryInvokeNativeEncounterGetter(
                controllerPtr,
                out bool hasValue,
                out float tier,
                out float level,
                out int bodyType);
            if (!readOk)
            {
                _nativePollParseFailureCount++;
                if (_nativePollParseFailureCount <= 3)
                {
                    _log?.LogWarning($"[FishPeek] 遭遇 getter 解析失败 #{_nativePollParseFailureCount}：controller=0x{controllerKey:X}。");
                }
                continue;
            }

            if (!hasValue)
            {
                _nativePollEncounterSignatures.Remove(controllerKey);
                continue;
            }

            if (!TryGetControllerGlobalId(controllerPtr, out int globalId) || globalId <= 0)
            {
                continue;
            }

            string signature = $"{globalId}|{tier:0.000}|{level:0.000}|{bodyType}";
            if (_nativePollEncounterSignatures.TryGetValue(controllerKey, out string? previous) &&
                string.Equals(previous, signature, StringComparison.Ordinal))
            {
                continue;
            }

            _nativePollEncounterSignatures[controllerKey] = signature;
            _nativePollValueCount++;
            if (_nativePollValueCount <= 8)
            {
                _log?.LogMessage(
                    $"[FishPeek] 轮询捕获遭遇 #{_nativePollValueCount}：gid={globalId} tier={tier:0.###} level={level:0.###} body={bodyType}。");
            }

            if (_encounterOutputEnabled)
            {
                RememberControllerGlobalId(controllerPtr, globalId);
                QueueEncounter(controllerPtr, globalId, tier, level, bodyType);
            }
        }

        for (int i = 0; i < _nativePollStaleControllers.Count; i++)
        {
            FreeNativeControllerHandle(_nativePollStaleControllers[i]);
        }
    }

    private static IntPtr GetNativeControllerHandleTarget(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        try
        {
            return IL2CPP.il2cpp_gchandle_get_target(handle);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static void FreeNativeControllerHandle(long controllerKey)
    {
        int removedGlobalId = 0;
        if (_nativeControllerWeakHandles.TryGetValue(controllerKey, out IntPtr handle))
        {
            try
            {
                if (handle != IntPtr.Zero)
                {
                    IL2CPP.il2cpp_gchandle_free(handle);
                }
            }
            catch
            {
                // ignored
            }

            _nativeControllerWeakHandles.Remove(controllerKey);
        }

        _nativePollEncounterSignatures.Remove(controllerKey);
        lock (_nameCacheLock)
        {
            _controllerPtrToGlobalId.TryGetValue(controllerKey, out removedGlobalId);
            _controllerPtrToName.Remove(controllerKey);
            _controllerPtrToGlobalId.Remove(controllerKey);
            if (removedGlobalId > 0)
            {
                _controllerIdToName.Remove(removedGlobalId);
                _controllerIdToClientId.Remove(removedGlobalId);
            }
        }

        if (removedGlobalId > 0)
        {
            lock (_playerTickLock)
            {
                _playerLastEmitTick.Remove(removedGlobalId);
            }
        }
    }

    private static void ReleaseAllNativeControllerHandles()
    {
        foreach (IntPtr handle in _nativeControllerWeakHandles.Values)
        {
            try
            {
                if (handle != IntPtr.Zero)
                {
                    IL2CPP.il2cpp_gchandle_free(handle);
                }
            }
            catch
            {
                // ignored
            }
        }

        _nativeControllerWeakHandles.Clear();
        _nativePollEncounterSignatures.Clear();
    }

    private static unsafe bool TryInvokeNativeEncounterGetter(
        IntPtr controllerPtr,
        out bool hasValue,
        out float tier,
        out float level,
        out int bodyType)
    {
        hasValue = false;
        tier = 0f;
        level = 0f;
        bodyType = 0;

        if (_miNativeEncounterGetter == IntPtr.Zero || controllerPtr == IntPtr.Zero)
        {
            return false;
        }

        IntPtr exception = IntPtr.Zero;
        IntPtr boxed;
        try
        {
            boxed = IL2CPP.il2cpp_runtime_invoke(_miNativeEncounterGetter, controllerPtr, null, ref exception);
        }
        catch
        {
            return false;
        }

        if (exception != IntPtr.Zero)
        {
            return false;
        }

        if (boxed == IntPtr.Zero)
        {
            return true;
        }

        hasValue = true;

        IntPtr unboxed = IntPtr.Zero;
        try
        {
            unboxed = IL2CPP.il2cpp_object_unbox(boxed);
        }
        catch
        {
            // handled by the boxed-layout fallback below
        }

        if (unboxed != IntPtr.Zero && TryReadNativeEncounterValue(unboxed, true, out tier, out level, out bodyType))
        {
            return true;
        }

        if (TryReadNativeEncounterValue(boxed, false, out tier, out level, out bodyType))
        {
            return true;
        }

        // Defensive fallback for runtimes that box Nullable<T> itself instead of
        // boxing its underlying FishEncounterParam value.
        IntPtr boxedClass;
        try
        {
            boxedClass = IL2CPP.il2cpp_object_get_class(boxed);
        }
        catch
        {
            return false;
        }

        IntPtr hasValueField = FindField(
            boxedClass,
            new[] { "has_value", "hasValue", "m_HasValue" },
            "System.Boolean");
        if (hasValueField != IntPtr.Zero)
        {
            int hasValueOffset = (int)IL2CPP.il2cpp_field_get_offset(hasValueField);
            if (IsReadable(IntPtr.Add(boxed, hasValueOffset), 1) && Marshal.ReadByte(boxed, hasValueOffset) == 0)
            {
                hasValue = false;
                return true;
            }
        }

        IntPtr valueField = FindField(
            boxedClass,
            new[] { "value", "m_Value" },
            "FishEncounterParam");
        if (valueField == IntPtr.Zero)
        {
            return false;
        }

        int valueOffset = (int)IL2CPP.il2cpp_field_get_offset(valueField);
        IntPtr embeddedValue = IntPtr.Add(boxed, valueOffset);
        return TryReadNativeEncounterValue(embeddedValue, true, out tier, out level, out bodyType);
    }

    private static bool TryReadNativeEncounterValue(
        IntPtr valuePtr,
        bool unboxedValueType,
        out float tier,
        out float level,
        out int bodyType)
    {
        tier = 0f;
        level = 0f;
        bodyType = 0;

        int tierOffset = unboxedValueType
            ? NormalizeOffset(_offsetEncounterTier, _encounterStructIsValueType)
            : _offsetEncounterTier;
        int levelOffset = unboxedValueType
            ? NormalizeOffset(_offsetEncounterLevel, _encounterStructIsValueType)
            : _offsetEncounterLevel;
        int bodyOffset = unboxedValueType
            ? NormalizeOffset(_offsetEncounterBodyType, _encounterStructIsValueType)
            : _offsetEncounterBodyType;
        int required = Math.Max(tierOffset + 4, Math.Max(levelOffset + 4, bodyOffset + 4));
        if (valuePtr == IntPtr.Zero || required <= 0 || !IsReadable(valuePtr, required))
        {
            return false;
        }

        try
        {
            tier = ReadFloat(valuePtr, tierOffset);
            level = ReadFloat(valuePtr, levelOffset);
            bodyType = ReadInt32(valuePtr, bodyOffset);
            return IsValidEncounter(tier, level, bodyType);
        }
        catch
        {
            return false;
        }
    }

    private static unsafe IntPtr InvokeOneArgObjectMethod(IntPtr method, IntPtr instance, IntPtr arg0Object)
    {
        if (method == IntPtr.Zero || arg0Object == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        try
        {
            void** args = stackalloc void*[1];
            // Il2Cpp runtime_invoke expects a reference-type argument as the
            // object pointer itself. Only value-type arguments use an address
            // to temporary value storage.
            args[0] = (void*)arg0Object;
            IntPtr exception = IntPtr.Zero;
            IntPtr result = IL2CPP.il2cpp_runtime_invoke(method, instance, args, ref exception);
            return exception == IntPtr.Zero ? result : IntPtr.Zero;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static unsafe IntPtr FindOneArgMethod(
        IntPtr klass,
        string methodName,
        string parameterTypeContains,
        string? returnTypeContains)
    {
        if (klass == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        for (IntPtr current = klass; current != IntPtr.Zero; current = IL2CPP.il2cpp_class_get_parent(current))
        {
            IntPtr iter = IntPtr.Zero;
            while (true)
            {
                IntPtr method = IL2CPP.il2cpp_class_get_methods(current, ref iter);
                if (method == IntPtr.Zero)
                {
                    break;
                }

                if (IL2CPP.il2cpp_method_get_param_count(method) != 1 ||
                    !string.Equals(PtrToAnsi(IL2CPP.il2cpp_method_get_name(method)), methodName, StringComparison.Ordinal))
                {
                    continue;
                }

                string parameterType = PtrToAnsi(IL2CPP.il2cpp_type_get_name(IL2CPP.il2cpp_method_get_param(method, 0)));
                if (parameterType.IndexOf(parameterTypeContains, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(returnTypeContains))
                {
                    string returnType = PtrToAnsi(IL2CPP.il2cpp_type_get_name(IL2CPP.il2cpp_method_get_return_type(method)));
                    if (returnType.IndexOf(returnTypeContains, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                }

                return method;
            }
        }

        return IntPtr.Zero;
    }

    private static void StopWhenLimitReached()
    {
        if (_scanAttempts < MaxScanAttempts)
        {
            return;
        }

        _scanStopped = true;
        _log?.LogMessage($"[FishPeek] 原生扫描已停止：尝试 {MaxScanAttempts} 次后仍未解析到目标。");
    }

    private static void TryHookEncounterCallback()
    {
        if (_hooked)
        {
            return;
        }

        if (_targetFunction == IntPtr.Zero)
        {
            _scanStopped = true;
            _log?.LogWarning("[FishPeek] 回调函数指针为空，停止挂接。");
            return;
        }

        if (_offsetEventFishEncounter < 0 || _offsetEncounterTier < 0 || _offsetEncounterLevel < 0 || _offsetEncounterBodyType < 0)
        {
            _scanStopped = true;
            _log?.LogWarning("[FishPeek] 必需偏移不完整，跳过挂接。");
            return;
        }

        try
        {
            _callbackHookKeepAlive = EncounterCallbackHook;
            _callbackDetour = INativeDetour.CreateAndApply(_targetFunction, _callbackHookKeepAlive, out EncounterCallbackDelegate original);
            _callbackOriginal = original;
            _hooked = true;
            _log?.LogMessage("[FishPeek] 已挂接咬钩回调。");
            if (EnableLocalSelfEncounterHook)
            {
                TryResolveAndHookSelfEncounterCallback();
            }
        }
        catch (Exception ex)
        {
            _scanStopped = true;
            _log?.LogWarning("[FishPeek] 挂接咬钩回调失败：" + ex.Message);
        }
    }

    private static unsafe bool IsAcceptedMethodInfo(IntPtr methodInfo)
    {
        if (methodInfo == IntPtr.Zero)
        {
            return false;
        }

        long key = methodInfo.ToInt64();
        if (_methodAcceptCache.TryGetValue(key, out bool cached))
        {
            return cached;
        }

        bool accepted = false;
        try
        {
            if (_targetMethodInfo != IntPtr.Zero && methodInfo == _targetMethodInfo)
            {
                accepted = true;
            }
            else
            {
                string name = PtrToAnsi(IL2CPP.il2cpp_method_get_name(methodInfo));
                uint paramCount = IL2CPP.il2cpp_method_get_param_count(methodInfo);
                if (paramCount == 2 && string.Equals(name, "Callback", StringComparison.Ordinal))
                {
                    IntPtr p0 = IL2CPP.il2cpp_method_get_param(methodInfo, 0);
                    string p0Type = PtrToAnsi(IL2CPP.il2cpp_type_get_name(p0));
                    accepted = p0Type.IndexOf("FishingGameEncounterFishNotify", StringComparison.Ordinal) >= 0;
                }
            }
        }
        catch
        {
            accepted = false;
        }

        // keep cache bounded
        if (_methodAcceptCache.Count > 512)
        {
            _methodAcceptCache.Clear();
        }

        _methodAcceptCache[key] = accepted;
        return accepted;
    }

    private static unsafe bool IsAcceptedSpotReplyMethodInfo(IntPtr methodInfo)
    {
        if (methodInfo == IntPtr.Zero)
        {
            return false;
        }

        long key = methodInfo.ToInt64();
        if (_spotReplyMethodAcceptCache.TryGetValue(key, out bool cached))
        {
            return cached;
        }

        bool accepted = false;
        try
        {
            if (_spotReplyMethodInfo != IntPtr.Zero && methodInfo == _spotReplyMethodInfo)
            {
                accepted = true;
            }
            else
            {
                string name = PtrToAnsi(IL2CPP.il2cpp_method_get_name(methodInfo));
                uint paramCount = IL2CPP.il2cpp_method_get_param_count(methodInfo);
                if (paramCount == 2 && string.Equals(name, "Callback", StringComparison.Ordinal))
                {
                    IntPtr p0 = IL2CPP.il2cpp_method_get_param(methodInfo, 0);
                    string p0Type = PtrToAnsi(IL2CPP.il2cpp_type_get_name(p0));
                    accepted = p0Type.IndexOf("FishingGameFishingSpotReply", StringComparison.Ordinal) >= 0;
                }
            }
        }
        catch
        {
            accepted = false;
        }

        if (_spotReplyMethodAcceptCache.Count > 512)
        {
            _spotReplyMethodAcceptCache.Clear();
        }

        _spotReplyMethodAcceptCache[key] = accepted;
        return accepted;
    }

    private static unsafe bool IsAcceptedSelfEncounterMethodInfo(IntPtr methodInfo)
    {
        if (methodInfo == IntPtr.Zero)
        {
            return false;
        }

        long key = methodInfo.ToInt64();
        if (_selfEncounterMethodAcceptCache.TryGetValue(key, out bool cached))
        {
            return cached;
        }

        bool accepted = false;
        try
        {
            if (_selfEncounterMethodInfo != IntPtr.Zero && methodInfo == _selfEncounterMethodInfo)
            {
                accepted = true;
            }
            else
            {
                string name = PtrToAnsi(IL2CPP.il2cpp_method_get_name(methodInfo));
                uint paramCount = IL2CPP.il2cpp_method_get_param_count(methodInfo);
                if (paramCount == 2 && string.Equals(name, "Callback", StringComparison.Ordinal))
                {
                    IntPtr p0 = IL2CPP.il2cpp_method_get_param(methodInfo, 0);
                    string p0Type = PtrToAnsi(IL2CPP.il2cpp_type_get_name(p0));
                    accepted = p0Type.IndexOf("FishingGameEncounterFishNotify", StringComparison.Ordinal) >= 0;
                }
            }
        }
        catch
        {
            accepted = false;
        }

        if (_selfEncounterMethodAcceptCache.Count > 512)
        {
            _selfEncounterMethodAcceptCache.Clear();
        }

        _selfEncounterMethodAcceptCache[key] = accepted;
        return accepted;
    }

    private static bool IsTargetControllerInstance(IntPtr thisPtr)
    {
        if (thisPtr == IntPtr.Zero || _targetControllerClass == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            IntPtr klass = IL2CPP.il2cpp_object_get_class(thisPtr);
            return klass == _targetControllerClass;
        }
        catch
        {
            return false;
        }
    }

    private static unsafe void EncounterCallbackHook(IntPtr thisPtr, IntPtr eventParamPtr, IntPtr extraContextPtr, IntPtr methodInfo)
    {
        try
        {
            // Bridge hardening:
            // avoid extra IL2CPP method/class lookups inside the callback itself,
            // and consume the payload before invoking the original callback.
            if (_targetMethodInfo != IntPtr.Zero && methodInfo != IntPtr.Zero && methodInfo != _targetMethodInfo)
            {
                _methodInfoMismatchCount++;
                return;
            }

            if (thisPtr == IntPtr.Zero || eventParamPtr == IntPtr.Zero)
            {
                return;
            }

            _hookInvokeCount++;
            if (_hookInvokeCount <= 3 && EnableVerboseInternalLogs)
            {
                _log?.LogMessage($"[FishPeek] 咬钩回调触发 #{_hookInvokeCount} this=0x{thisPtr.ToInt64():X} param=0x{eventParamPtr.ToInt64():X}");
            }

            bool directOk = TryReadEncounterAt(eventParamPtr, false, out int directGid, out float directTier, out float directLevel, out int directBody)
                || TryReadEncounterAt(eventParamPtr, true, out directGid, out directTier, out directLevel, out directBody);
            if (directOk)
            {
                QueueEncounter(thisPtr, directGid, directTier, directLevel, directBody);
                return;
            }

            IntPtr derefPtr = IntPtr.Zero;
            if (IsReadable(eventParamPtr, IntPtr.Size))
            {
                try
                {
                    derefPtr = Marshal.ReadIntPtr(eventParamPtr);
                    int derefGid = 0;
                    float derefTier = 0f;
                    float derefLevel = 0f;
                    int derefBody = 0;
                    bool derefOk = derefPtr != IntPtr.Zero &&
                                   (TryReadEncounterAt(derefPtr, false, out derefGid, out derefTier, out derefLevel, out derefBody)
                                    || TryReadEncounterAt(derefPtr, true, out derefGid, out derefTier, out derefLevel, out derefBody));
                    if (derefOk)
                    {
                        QueueEncounter(thisPtr, derefGid, derefTier, derefLevel, derefBody);
                        return;
                    }
                }
                catch
                {
                    // Keep callback bridge silent on bad deref; just fall through to invalid payload.
                }
            }

            _invalidPayloadCount++;
            if (_invalidPayloadCount <= 8 && EnableVerboseInternalLogs)
            {
                bool rawA = TryReadEncounterRaw(eventParamPtr, false, out int rawAGid, out float rawATier, out float rawALevel, out int rawABody);
                bool rawB = TryReadEncounterRaw(derefPtr, true, out int rawBGid, out float rawBTier, out float rawBLevel, out int rawBBody);
                _log?.LogMessage(
                    $"[FishPeek] 无效载荷（#{_invalidPayloadCount}）：param=0x{eventParamPtr.ToInt64():X}, deref=0x{derefPtr.ToInt64():X}, " +
                    $"A(read={rawA},gid={rawAGid},tier={rawATier:0.###},level={rawALevel:0.###},body={rawABody}) " +
                    $"B(read={rawB},gid={rawBGid},tier={rawBTier:0.###},level={rawBLevel:0.###},body={rawBBody})");
            }
        }
        catch (Exception ex)
        {
            _log?.LogWarning("[FishPeek] 咬钩回调桥接失败：" + ex.Message);
        }
        finally
        {
            _callbackOriginal?.Invoke(thisPtr, eventParamPtr, extraContextPtr, methodInfo);
        }
    }

    private static unsafe void SelfEncounterCallbackHook(IntPtr thisPtr, IntPtr eventParamPtr, IntPtr extraContextPtr, IntPtr methodInfo)
    {
        _selfEncounterOriginal?.Invoke(thisPtr, eventParamPtr, extraContextPtr, methodInfo);

        if (!IsAcceptedSelfEncounterMethodInfo(methodInfo))
        {
            return;
        }

        if (thisPtr == IntPtr.Zero || _waitingFlowControllerClass == IntPtr.Zero)
        {
            return;
        }

        try
        {
            IntPtr klass = IL2CPP.il2cpp_object_get_class(thisPtr);
            if (klass != _waitingFlowControllerClass)
            {
                return;
            }
        }
        catch
        {
            return;
        }

        if (eventParamPtr == IntPtr.Zero || _offsetFlowGameController < 0)
        {
            return;
        }

        IntPtr controllerPtr = IntPtr.Zero;
        if (!TryReadObjectField(thisPtr, _offsetFlowGameController, out controllerPtr) || controllerPtr == IntPtr.Zero)
        {
            return;
        }

        _selfEncounterHookInvokeCount++;
        if (_selfEncounterHookInvokeCount <= 3 && EnableVerboseInternalLogs)
        {
            _log?.LogMessage(
                $"[FishPeek] 本地等待流回调触发 #{_selfEncounterHookInvokeCount} flow=0x{thisPtr.ToInt64():X} controller=0x{controllerPtr.ToInt64():X}");
        }

        bool directOk = TryReadEncounterAt(eventParamPtr, false, out int directGid, out float directTier, out float directLevel, out int directBody)
            || TryReadEncounterAt(eventParamPtr, true, out directGid, out directTier, out directLevel, out directBody);
        if (directOk)
        {
            if (!TryGetControllerGlobalId(controllerPtr, out int controllerGlobalId) ||
                controllerGlobalId <= 0 ||
                controllerGlobalId == directGid)
            {
                QueueEncounter(controllerPtr, directGid, directTier, directLevel, directBody);
            }

            return;
        }

        if (!IsReadable(eventParamPtr, IntPtr.Size))
        {
            return;
        }

        try
        {
            IntPtr derefPtr = Marshal.ReadIntPtr(eventParamPtr);
            bool derefOk = TryReadEncounterAt(derefPtr, false, out int derefGid, out float derefTier, out float derefLevel, out int derefBody)
                || TryReadEncounterAt(derefPtr, true, out derefGid, out derefTier, out derefLevel, out derefBody);
            if (!derefOk)
            {
                return;
            }

            if (!TryGetControllerGlobalId(controllerPtr, out int controllerGlobalId) ||
                controllerGlobalId <= 0 ||
                controllerGlobalId == derefGid)
            {
                QueueEncounter(controllerPtr, derefGid, derefTier, derefLevel, derefBody);
            }
        }
        catch
        {
            // ignored
        }
    }

    private static int ComputeMinReadBytes()
    {
        int eventNeedBox = Math.Max(
            _offsetEventFishEncounter + 4,
            _offsetEventGlobalId >= 0 ? _offsetEventGlobalId + 4 : 0);
        int encounterNeedBox = Math.Max(
            _offsetEncounterTier + 4,
            Math.Max(_offsetEncounterLevel + 4, _offsetEncounterBodyType + 4));

        int eventNeedRaw = Math.Max(
            NormalizeOffset(_offsetEventFishEncounter, _eventStructIsValueType) + 4,
            _offsetEventGlobalId >= 0 ? NormalizeOffset(_offsetEventGlobalId, _eventStructIsValueType) + 4 : 0);
        int encounterNeedRaw = Math.Max(
            NormalizeOffset(_offsetEncounterTier, _encounterStructIsValueType) + 4,
            Math.Max(
                NormalizeOffset(_offsetEncounterLevel, _encounterStructIsValueType) + 4,
                NormalizeOffset(_offsetEncounterBodyType, _encounterStructIsValueType) + 4));

        int needBox = Math.Max(eventNeedBox + encounterNeedBox, 64);
        int needRaw = Math.Max(eventNeedRaw + encounterNeedRaw, 64);
        return Math.Min(needBox, needRaw);
    }

    private static unsafe ScanResult NativeScanForEncounterCallback()
    {
        var result = new ScanResult();

        IntPtr domain = IL2CPP.il2cpp_domain_get();
        if (domain == IntPtr.Zero)
        {
            result.Error = "il2cpp_domain_get() returned null";
            return result;
        }

        uint asmCount = 0;
        IntPtr* asms = IL2CPP.il2cpp_domain_get_assemblies(domain, ref asmCount);
        result.AssemblyCount = (int)asmCount;
        if (asms == null || asmCount == 0)
        {
            result.Error = "il2cpp_domain_get_assemblies returned empty";
            return result;
        }

        for (uint ai = 0; ai < asmCount; ai++)
        {
            IntPtr asm = asms[ai];
            if (asm == IntPtr.Zero)
            {
                continue;
            }

            IntPtr image = IL2CPP.il2cpp_assembly_get_image(asm);
            if (image == IntPtr.Zero)
            {
                continue;
            }

            string imageName = PtrToAnsi(IL2CPP.il2cpp_image_get_name(image));
            uint classCount = IL2CPP.il2cpp_image_get_class_count(image);
            result.ClassCount += (int)classCount;

            for (uint ci = 0; ci < classCount; ci++)
            {
                IntPtr klass = IL2CPP.il2cpp_image_get_class(image, ci);
                if (klass == IntPtr.Zero)
                {
                    continue;
                }

                string className = PtrToAnsi(IL2CPP.il2cpp_class_get_name(klass));
                string ns = PtrToAnsi(IL2CPP.il2cpp_class_get_namespace(klass));

                if (!string.Equals(className, "FishingGameController", StringComparison.Ordinal) ||
                    !string.Equals(ns, "MiniGame.FishingGame", StringComparison.Ordinal))
                {
                    continue;
                }

                IntPtr iter = IntPtr.Zero;
                while (true)
                {
                    IntPtr method = IL2CPP.il2cpp_class_get_methods(klass, ref iter);
                    if (method == IntPtr.Zero)
                    {
                        break;
                    }

                    result.MethodCount++;

                    uint paramCount = IL2CPP.il2cpp_method_get_param_count(method);
                    if (paramCount != 2)
                    {
                        continue;
                    }

                    IntPtr p0Type = IL2CPP.il2cpp_method_get_param(method, 0);
                    string p0TypeName = PtrToAnsi(IL2CPP.il2cpp_type_get_name(p0Type));
                    if (p0TypeName.IndexOf("FishingGameEncounterFishNotify", StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }

                    IntPtr p0Class = IL2CPP.il2cpp_type_get_class_or_element_class(p0Type);
                    if (p0Class == IntPtr.Zero)
                    {
                        continue;
                    }

                    IntPtr fishEncounterField = FindField(
                        p0Class,
                        new[] { "fishEncounterParam", "<fishEncounterParam>k__BackingField", "m_fishEncounterParam" },
                        "FishEncounterParam");
                    if (fishEncounterField == IntPtr.Zero)
                    {
                        continue;
                    }

                    IntPtr globalIdField = FindField(
                        p0Class,
                        new[] { "miniGameControllerGlobalId", "<miniGameControllerGlobalId>k__BackingField", "m_miniGameControllerGlobalId" },
                        "System.Int32");

                    IntPtr fishEncounterFieldType = IL2CPP.il2cpp_field_get_type(fishEncounterField);
                    IntPtr fishEncounterClass = IL2CPP.il2cpp_type_get_class_or_element_class(fishEncounterFieldType);
                    if (fishEncounterClass == IntPtr.Zero)
                    {
                        continue;
                    }

                    IntPtr tierField = FindField(fishEncounterClass, new[] { "fishTier", "FishTier", "m_FishTier" }, "System.Single");
                    IntPtr levelField = FindField(fishEncounterClass, new[] { "fishLevel", "FishLevel", "m_FishLevel" }, "System.Single");
                    IntPtr bodyTypeField = FindField(fishEncounterClass, new[] { "fishBodyType", "FishBodyType", "m_FishBodyType" }, null);
                    if (tierField == IntPtr.Zero || levelField == IntPtr.Zero || bodyTypeField == IntPtr.Zero)
                    {
                        continue;
                    }

                    var nativeMethod = UnityVersionHandler.Wrap((Il2CppMethodInfo*)method);
                    IntPtr fn = nativeMethod?.MethodPointer ?? IntPtr.Zero;
                    if (fn == IntPtr.Zero)
                    {
                        continue;
                    }

                    result.Found = true;
                    result.FoundImage = imageName;
                    result.FoundNamespace = ns;
                    result.FoundClass = className;
                    result.FoundMethod = PtrToAnsi(IL2CPP.il2cpp_method_get_name(method));
                    result.Param0TypeName = p0TypeName;

                    result.MethodInfoPtr = method;
                    result.MethodFunctionPtr = fn;
                    result.ControllerClassPtr = klass;

                    result.EventStructClassPtr = p0Class;
                    result.EncounterStructClassPtr = fishEncounterClass;
                    result.EventStructIsValueType = IL2CPP.il2cpp_class_is_valuetype(p0Class);
                    result.EncounterStructIsValueType = IL2CPP.il2cpp_class_is_valuetype(fishEncounterClass);
                    result.OffsetEventFishEncounter = (int)IL2CPP.il2cpp_field_get_offset(fishEncounterField);
                    result.OffsetEventGlobalId = globalIdField == IntPtr.Zero ? -1 : (int)IL2CPP.il2cpp_field_get_offset(globalIdField);
                    result.OffsetEncounterTier = (int)IL2CPP.il2cpp_field_get_offset(tierField);
                    result.OffsetEncounterLevel = (int)IL2CPP.il2cpp_field_get_offset(levelField);
                    result.OffsetEncounterBodyType = (int)IL2CPP.il2cpp_field_get_offset(bodyTypeField);
                    return result;
                }
            }
        }

        return result;
    }

    private static unsafe IntPtr FindField(IntPtr klass, string[] preferredNames, string? typeContains)
    {
        IntPtr fallback = IntPtr.Zero;
        for (IntPtr k = klass; k != IntPtr.Zero; k = IL2CPP.il2cpp_class_get_parent(k))
        {
            IntPtr iter = IntPtr.Zero;
            while (true)
            {
                IntPtr field = IL2CPP.il2cpp_class_get_fields(k, ref iter);
                if (field == IntPtr.Zero)
                {
                    break;
                }

                string name = PtrToAnsi(IL2CPP.il2cpp_field_get_name(field));
                for (int i = 0; i < preferredNames.Length; i++)
                {
                    if (string.Equals(name, preferredNames[i], StringComparison.OrdinalIgnoreCase))
                    {
                        return field;
                    }
                }

                if (fallback != IntPtr.Zero || string.IsNullOrEmpty(typeContains))
                {
                    continue;
                }

                IntPtr t = IL2CPP.il2cpp_field_get_type(field);
                string tName = PtrToAnsi(IL2CPP.il2cpp_type_get_name(t));
                if (tName.IndexOf(typeContains, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fallback = field;
                }
            }
        }

        return fallback;
    }

    private static bool IsReadable(IntPtr ptr, int bytes)
    {
        if (ptr == IntPtr.Zero || bytes <= 0)
        {
            return false;
        }

        if (VirtualQuery(ptr, out MemoryBasicInformation mbi, (nuint)Marshal.SizeOf<MemoryBasicInformation>()) == 0)
        {
            return false;
        }

        if (mbi.State != MemCommit)
        {
            return false;
        }

        if ((mbi.Protect & (PageNoAccess | PageGuard)) != 0)
        {
            return false;
        }

        long start = ptr.ToInt64();
        long end = start + bytes;
        long regionStart = mbi.BaseAddress.ToInt64();
        long regionEnd = regionStart + (long)mbi.RegionSize;

        return start >= regionStart && end <= regionEnd;
    }

    private static unsafe float ReadFloat(IntPtr ptr, int offset)
    {
        return *((float*)((byte*)ptr + offset));
    }

    private static unsafe int ReadInt32(IntPtr ptr, int offset)
    {
        return *((int*)((byte*)ptr + offset));
    }

    private static int NormalizeOffset(int rawOffset, bool isValueType)
    {
        if (!isValueType)
        {
            return rawOffset;
        }

        int shifted = rawOffset - Il2CppObjectHeaderSize;
        return shifted >= 0 ? shifted : rawOffset;
    }

    private static bool TryReadEncounterRaw(IntPtr eventBasePtr, bool assumeUnboxedValueType, out int globalId, out float tier, out float level, out int bodyType)
    {
        globalId = 0;
        tier = 0f;
        level = 0f;
        bodyType = 0;

        if (eventBasePtr == IntPtr.Zero || !IsReadable(eventBasePtr, _minReadableBytes))
        {
            return false;
        }

        try
        {
            int eventFishOffset = assumeUnboxedValueType
                ? NormalizeOffset(_offsetEventFishEncounter, _eventStructIsValueType)
                : _offsetEventFishEncounter;
            int eventGidOffset = assumeUnboxedValueType
                ? NormalizeOffset(_offsetEventGlobalId, _eventStructIsValueType)
                : _offsetEventGlobalId;
            int encTierOffset = assumeUnboxedValueType
                ? NormalizeOffset(_offsetEncounterTier, _encounterStructIsValueType)
                : _offsetEncounterTier;
            int encLevelOffset = assumeUnboxedValueType
                ? NormalizeOffset(_offsetEncounterLevel, _encounterStructIsValueType)
                : _offsetEncounterLevel;
            int encBodyOffset = assumeUnboxedValueType
                ? NormalizeOffset(_offsetEncounterBodyType, _encounterStructIsValueType)
                : _offsetEncounterBodyType;

            if (_offsetEventGlobalId >= 0)
            {
                globalId = ReadInt32(eventBasePtr, eventGidOffset);
            }

            IntPtr encPtr = IntPtr.Add(eventBasePtr, eventFishOffset);
            tier = ReadFloat(encPtr, encTierOffset);
            level = ReadFloat(encPtr, encLevelOffset);
            bodyType = ReadInt32(encPtr, encBodyOffset);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidEncounter(float tier, float level, int bodyType)
    {
        if (!float.IsFinite(tier) || !float.IsFinite(level))
        {
            return false;
        }

        // Relaxed validation:
        // keep false-memory values out, but don't assume fixed tier/body.
        if (tier < 0.5f || tier > 10f)
        {
            return false;
        }

        if (level < 1f || level > 6.5f)
        {
            return false;
        }

        return bodyType >= 0 && bodyType <= 5;
    }

    private static bool TryReadEncounterAt(IntPtr eventBasePtr, bool assumeUnboxedValueType, out int globalId, out float tier, out float level, out int bodyType)
    {
        if (!TryReadEncounterRaw(eventBasePtr, assumeUnboxedValueType, out globalId, out tier, out level, out bodyType))
        {
            return false;
        }

        return IsValidEncounter(tier, level, bodyType);
    }

    private static void QueueEncounter(IntPtr controllerPtr, int globalId, float tier, float level, int bodyType)
    {
        if (globalId <= 0)
        {
            return;
        }

        lock (_pendingEncounterLock)
        {
            _pendingEncounterQueue.Enqueue(new PendingEncounterItem
            {
                ControllerPtr = controllerPtr,
                GlobalId = globalId,
                Tier = tier,
                Level = level,
                BodyType = bodyType
            });

            while (_pendingEncounterQueue.Count > 128)
            {
                _pendingEncounterQueue.Dequeue();
            }
        }
    }

    private static void DrainPendingEncounters()
    {
        while (true)
        {
            PendingEncounterItem? item = null;
            lock (_pendingEncounterLock)
            {
                if (_pendingEncounterQueue.Count == 0)
                {
                    return;
                }

                item = _pendingEncounterQueue.Dequeue();
            }

            if (item == null)
            {
                continue;
            }

            try
            {
                IntPtr resolvedControllerPtr = ResolveControllerPtrForPlayer(item.ControllerPtr, item.GlobalId);

                EmitEncounter(resolvedControllerPtr, item.GlobalId, item.Tier, item.Level, item.BodyType);
            }
            catch (Exception ex)
            {
                _log?.LogWarning("[FishPeek] 处理排队咬钩事件失败：" + ex.Message);
            }
        }
    }

    private static void EmitEncounter(IntPtr controllerPtr, int globalId, float tier, float level, int bodyType)
    {
        if (!_encounterOutputEnabled)
        {
            return;
        }

        string key = globalId + "|" + tier.ToString("0.00") + "|" + level.ToString("0.00") + "|" + bodyType;
        long now = Environment.TickCount64;
        if (key == _lastEmitKey && now - _lastEmitTick < 500)
        {
            return;
        }

        _lastEmitKey = key;
        _lastEmitTick = now;

        int playerLocalId = globalId;
        lock (_playerTickLock)
        {
            if (_playerLastEmitTick.TryGetValue(playerLocalId, out long lastTick) && now - lastTick < 1500)
            {
                return;
            }

            _playerLastEmitTick[playerLocalId] = now;
            if (_playerLastEmitTick.Count > 128)
            {
                _playerLastEmitTick.Clear();
            }
        }

        if (!ShouldDisplayBiteInfo(playerLocalId))
        {
            return;
        }

        if (controllerPtr != IntPtr.Zero)
        {
            TryLearnNameMappingFromController(controllerPtr, playerLocalId);
        }

        string playerDisplayName = ResolvePlayerDisplayName(controllerPtr, playerLocalId);
        if (!TryBuildPredictionMessage(controllerPtr, playerDisplayName, playerLocalId, level, out string message, out int levelBucket, out bool suppressOutline))
        {
            return;
        }

        _log?.LogMessage(message);
        PushOverlayMessage(message, levelBucket, suppressOutline);
    }

    private static void TryLearnNameMappingFromController(IntPtr controllerPtr, int globalId)
    {
        if (controllerPtr == IntPtr.Zero || globalId <= 0)
        {
            return;
        }

        bool hasName;
        lock (_nameCacheLock)
        {
            hasName = _controllerIdToName.TryGetValue(globalId, out string? cachedName) &&
                      !string.IsNullOrWhiteSpace(cachedName);
        }

        if (hasName)
        {
            return;
        }

        if (TryGetControllerGlobalId(controllerPtr, out int liveGlobalId) &&
            liveGlobalId > 0 &&
            liveGlobalId != globalId)
        {
            return;
        }

        string? resolved = TryResolvePlayerName(controllerPtr);
        if (string.IsNullOrWhiteSpace(resolved))
        {
            return;
        }

        long controllerKey = controllerPtr.ToInt64();
        lock (_nameCacheLock)
        {
            _controllerPtrToName[controllerKey] = resolved;
            if (_controllerPtrToName.Count > 256)
            {
                _controllerPtrToName.Clear();
            }

            _controllerIdToName[globalId] = resolved;
            if (_controllerIdToName.Count > 512)
            {
                _controllerIdToName.Clear();
            }

            _controllerPtrToGlobalId[controllerKey] = globalId;
            if (_controllerPtrToGlobalId.Count > 256)
            {
                _controllerPtrToGlobalId.Clear();
            }
        }

        if (_biteInfoDisplayMode != BiteInfoDisplayMode.SelfOnly)
        {
            return;
        }

        string? clientIdToken = TryResolveControllerClientId(controllerPtr);
        if (string.IsNullOrWhiteSpace(clientIdToken))
        {
            return;
        }

        lock (_nameCacheLock)
        {
            _controllerIdToClientId[globalId] = clientIdToken;
            if (_controllerIdToClientId.Count > 512)
            {
                _controllerIdToClientId.Clear();
            }
        }
    }

    private static string ResolvePlayerDisplayName(IntPtr controllerPtr, int globalId)
    {
        long controllerKey = controllerPtr.ToInt64();
        if (controllerPtr != IntPtr.Zero &&
            (globalId <= 0 ||
             !TryGetControllerGlobalId(controllerPtr, out int liveGlobalId) ||
             liveGlobalId <= 0 ||
             liveGlobalId == globalId))
        {
            string? liveName = TryResolvePlayerName(controllerPtr);
            if (!string.IsNullOrWhiteSpace(liveName))
            {
                lock (_nameCacheLock)
                {
                    if (controllerKey != 0)
                    {
                        _controllerPtrToName[controllerKey] = liveName;
                        if (_controllerPtrToName.Count > 256)
                        {
                            _controllerPtrToName.Clear();
                        }
                    }

                    if (globalId > 0)
                    {
                        _controllerIdToName[globalId] = liveName;
                        if (_controllerIdToName.Count > 512)
                        {
                            _controllerIdToName.Clear();
                        }
                    }
                }

                return liveName;
            }
        }

        lock (_nameCacheLock)
        {
            if (controllerKey != 0 &&
                _controllerPtrToName.TryGetValue(controllerKey, out string? cachedByPtr) &&
                !string.IsNullOrWhiteSpace(cachedByPtr))
            {
                return cachedByPtr;
            }

            if (globalId > 0 &&
                _controllerIdToName.TryGetValue(globalId, out string? cachedById) &&
                !string.IsNullOrWhiteSpace(cachedById))
            {
                return cachedById;
            }
        }

        return globalId > 0 ? $"\u73a9\u5bb6{globalId}" : "\u73a9\u5bb6";
    }


    private static string? TryResolvePlayerNameFromKnownControllers(int globalId)
    {
        if (globalId <= 0)
        {
            return null;
        }

        List<IntPtr>? candidates = null;
        lock (_nameCacheLock)
        {
            foreach (KeyValuePair<long, int> entry in _controllerPtrToGlobalId)
            {
                if (entry.Value != globalId)
                {
                    continue;
                }

                candidates ??= new List<IntPtr>(4);
                candidates.Add(new IntPtr(entry.Key));
                if (candidates.Count >= 8)
                {
                    break;
                }
            }
        }

        if (candidates == null)
        {
            return null;
        }

        foreach (IntPtr candidatePtr in candidates)
        {
            if (!IsLikelyLiveControllerObject(candidatePtr))
            {
                continue;
            }

            if (TryGetControllerGlobalId(candidatePtr, out int liveGlobalId) &&
                liveGlobalId > 0 &&
                liveGlobalId != globalId)
            {
                continue;
            }

            string? resolved = TryResolvePlayerName(candidatePtr);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                lock (_nameCacheLock)
                {
                    _controllerPtrToName[candidatePtr.ToInt64()] = resolved;
                    _controllerIdToName[globalId] = resolved;
                    if (_controllerIdToName.Count > 512)
                    {
                        _controllerIdToName.Clear();
                    }
                }

                return resolved;
            }
        }

        return null;
    }
    private static bool ShouldDisplayBiteInfo(int globalId)
    {
        if (_biteInfoDisplayMode == BiteInfoDisplayMode.All)
        {
            return true;
        }

        if (_biteInfoDisplayMode == BiteInfoDisplayMode.Off)
        {
            return false;
        }

        return IsSelfPlayerGlobalId(globalId);
    }

    private static bool IsSelfPlayerGlobalId(int globalId)
    {
        if (globalId <= 0 || !TryRefreshSelfClientId())
        {
            return false;
        }

        string? mappedClientId;
        lock (_nameCacheLock)
        {
            if (!_controllerIdToClientId.TryGetValue(globalId, out mappedClientId) ||
                string.IsNullOrWhiteSpace(mappedClientId))
            {
                return false;
            }
        }

        return string.Equals(mappedClientId, _selfClientIdToken, StringComparison.Ordinal);
    }

    private static bool TryRefreshSelfClientId()
    {
        float now = Time.realtimeSinceStartup;
        if (now < _nextSelfClientIdRefreshRealtime)
        {
            return !string.IsNullOrWhiteSpace(_selfClientIdToken);
        }

        _nextSelfClientIdRefreshRealtime = now + 1.25f;

        if (_staticGameInfoClass == IntPtr.Zero)
        {
            _staticGameInfoClass = FindClassByNameAnyImage("StaticGameInfo", null);
        }

        if (_staticGameInfoClass == IntPtr.Zero)
        {
            return false;
        }

        if (_miGetSelfClientId == IntPtr.Zero)
        {
            _miGetSelfClientId = FindZeroArgMethod(
                _staticGameInfoClass,
                new[] { "get_selfClientId", "get_SelfClientId", "get_selfClientID", "get_SelfClientID" },
                null);
        }

        if (_miGetSelfClientId == IntPtr.Zero)
        {
            return false;
        }

        IntPtr raw = InvokeStaticZeroArgMethod(_miGetSelfClientId);
        string? token = TryConvertIl2CppValueToStableToken(raw);
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        _selfClientIdToken = token;
        return true;
    }

    private static string? TryResolveControllerClientId(IntPtr controllerPtr)
    {
        if (controllerPtr == IntPtr.Zero)
        {
            return null;
        }

        TryResolvePlayerNameMethods();
        if (_miGetCurFishingPlayerSeat == IntPtr.Zero)
        {
            return null;
        }

        IntPtr seatPtr = InvokeZeroArgMethod(_miGetCurFishingPlayerSeat, controllerPtr);

        if (seatPtr == IntPtr.Zero)
        {
            return null;
        }

        IntPtr mainControllerPtr = TryResolveMainControllerPtrFromSeat(seatPtr);
        if (mainControllerPtr == IntPtr.Zero || _miGetMainControllerClientId == IntPtr.Zero)
        {
            return null;
        }

        IntPtr raw = InvokeZeroArgMethod(_miGetMainControllerClientId, mainControllerPtr);
        return TryConvertIl2CppValueToStableToken(raw);
    }

    private static string? TryConvertIl2CppValueToStableToken(IntPtr valuePtr)
    {
        if (valuePtr == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            string? asString = IL2CPP.Il2CppStringToManaged(valuePtr);
            if (!string.IsNullOrWhiteSpace(asString))
            {
                return asString.Trim();
            }
        }
        catch
        {
            // ignored
        }

        try
        {
            IntPtr klass = IL2CPP.il2cpp_object_get_class(valuePtr);
            string className = PtrToAnsi(IL2CPP.il2cpp_class_get_name(klass));
            IntPtr unboxed = IL2CPP.il2cpp_object_unbox(valuePtr);
            if (unboxed == IntPtr.Zero)
            {
                return null;
            }

            if ((className == "Int32" || className == "UInt32") && IsReadable(unboxed, sizeof(int)))
            {
                return Marshal.ReadInt32(unboxed).ToString();
            }

            if ((className == "Int64" || className == "UInt64") && IsReadable(unboxed, sizeof(long)))
            {
                return Marshal.ReadInt64(unboxed).ToString();
            }

            if (className == "Single" && IsReadable(unboxed, sizeof(float)))
            {
                float v = Marshal.PtrToStructure<float>(unboxed);
                return v.ToString("0.###");
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private static void UpdateOverlayUi(float nowRealtime)
    {
        if (!EnsureOverlayUi())
        {
            return;
        }

        EnsureModeHintUi();
        UpdateModeHintVisual(nowRealtime);

        bool changed = UpdateOverlayRootRectIfNeeded();

        for (int i = _overlayMessages.Count - 1; i >= 0; i--)
        {
            OverlayMessageItem item = _overlayMessages[i];
            if (item.Label == null || nowRealtime >= item.ExpireAtRealtime)
            {
                ReleaseOverlayLabel(item.Label);
                item.Label = null;
                _overlayMessages.RemoveAt(i);
                changed = true;
            }
            else
            {
                ApplyOverlayItemVisual(item, nowRealtime);
            }
        }

        if (changed || _overlayLayoutDirty)
        {
            ReflowOverlayMessages();
            _overlayLayoutDirty = false;
        }
    }

    private static void PushOverlayMessage(string message, float levelRaw, bool suppressOutline)
    {
        if (string.IsNullOrWhiteSpace(message) || !EnsureOverlayUi() || _overlayRootRect == null)
        {
            return;
        }

        float now = Time.realtimeSinceStartup;
        int levelBucket = NormalizeLevelBucket(levelRaw);
        EnsureOverlayFontAsset();

        while (_overlayMessages.Count >= OverlayMaxMessages)
        {
            OverlayMessageItem oldest = _overlayMessages[0];
            ReleaseOverlayLabel(oldest.Label);
            oldest.Label = null;
            _overlayMessages.RemoveAt(0);
        }

        TextMeshProUGUI? label = AcquireOverlayLabel();
        if (label == null)
        {
            return;
        }

        label.text = message;
        label.enabled = true;
        ApplyOverlayTextStyle(label, levelBucket, suppressOutline);

        OverlayMessageItem item = new()
        {
            CreatedAtRealtime = now,
            ExpireAtRealtime = now + OverlayMessageHoldSeconds + OverlayMessageFadeOutSeconds,
            LevelBucket = levelBucket,
            SuppressOutline = suppressOutline,
            Label = label
        };
        ApplyOverlayItemVisual(item, now);
        _overlayMessages.Add(item);
        _overlayLayoutDirty = true;
    }

    private static bool EnsureOverlayUi()
    {
        if (_overlayCanvasGo != null && _overlayRootRect != null)
        {
            return true;
        }

        try
        {
            _overlayCanvasGo = new GameObject("PA_FishPeek_OverlayCanvas");
            Canvas canvas = _overlayCanvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            canvas.pixelPerfect = false;
            CanvasScaler scaler = _overlayCanvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Object.DontDestroyOnLoad(_overlayCanvasGo);

            GameObject rootGo = new("PA_FishPeek_OverlayRoot");
            _overlayRootRect = rootGo.AddComponent<RectTransform>();
            _overlayRootRect.SetParent(_overlayCanvasGo.transform, false);
            _overlayRootRect.anchorMin = new Vector2(0f, 1f);
            _overlayRootRect.anchorMax = new Vector2(0f, 1f);
            _overlayRootRect.pivot = new Vector2(0f, 1f);
            _overlayRootRect.anchoredPosition = new Vector2(24f, -140f);
            _overlayRootRect.sizeDelta = new Vector2(720f, 420f);

            _lastScreenWidth = 0;
            _lastScreenHeight = 0;
            _overlayFontAsset = null;
            _nextOverlayFontSearchRealtime = 0f;
            _overlayFontBoundLogged = false;
            UpdateOverlayRootRectIfNeeded();
            EnsureModeHintUi();
            return true;
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[FishPeek] 覆盖层初始化失败：{ex.Message}");
            DestroyOverlayUi();
            return false;
        }
    }

    private static void DestroyOverlayUi()
    {
        for (int i = _overlayMessages.Count - 1; i >= 0; i--)
        {
            OverlayMessageItem item = _overlayMessages[i];
            if (item.Label != null)
            {
                Object.Destroy(item.Label.gameObject);
            }
        }

        _overlayMessages.Clear();

        for (int i = _overlayLabelPool.Count - 1; i >= 0; i--)
        {
            TextMeshProUGUI? label = _overlayLabelPool[i];
            if (label != null)
            {
                Object.Destroy(label.gameObject);
            }
        }

        _overlayLabelPool.Clear();

        if (_overlayCanvasGo != null)
        {
            Object.Destroy(_overlayCanvasGo);
            _overlayCanvasGo = null;
        }

        _overlayRootRect = null;
        _modeHintRect = null;
        _modeHintLabel = null;
        _overlayFontAsset = null;
        _overlayLayoutDirty = false;
        _nextOverlayFontSearchRealtime = 0f;
        _overlayFontBoundLogged = false;
        _modeHintShownRealtime = -999f;
        _modeHintExpireRealtime = -999f;
    }

    private static TextMeshProUGUI? AcquireOverlayLabel()
    {
        if (_overlayRootRect == null)
        {
            return null;
        }

        while (_overlayLabelPool.Count > 0)
        {
            int lastIndex = _overlayLabelPool.Count - 1;
            TextMeshProUGUI? pooled = _overlayLabelPool[lastIndex];
            _overlayLabelPool.RemoveAt(lastIndex);
            if (pooled == null)
            {
                continue;
            }

            pooled.gameObject.SetActive(true);
            pooled.enabled = true;
            return pooled;
        }

        GameObject lineGo = new("PA_FishPeek_Line");
        RectTransform lineRect = lineGo.AddComponent<RectTransform>();
        lineRect.SetParent(_overlayRootRect, false);
        return lineGo.AddComponent<TextMeshProUGUI>();
    }

    private static void ReleaseOverlayLabel(TextMeshProUGUI? label)
    {
        if (label == null)
        {
            return;
        }

        if (_overlayLabelPool.Contains(label))
        {
            return;
        }

        label.text = string.Empty;
        label.enabled = false;
        label.gameObject.SetActive(false);
        _overlayLabelPool.Add(label);
    }

    private static bool UpdateOverlayRootRectIfNeeded()
    {
        if (_overlayRootRect == null)
        {
            return false;
        }

        if (_lastScreenWidth == Screen.width && _lastScreenHeight == Screen.height)
        {
            return false;
        }

        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;

        Rect safe = Screen.safeArea;
        float leftOffset = safe.xMin + Mathf.Clamp(safe.width * 0.012f, 14f, 36f);
        float topInset = Mathf.Max(0f, Screen.height - safe.yMax);
        float topOffset = topInset + Mathf.Clamp(safe.height * 0.145f, 96f, 180f);

        float maxWidth = Mathf.Max(220f, safe.width - leftOffset - 12f);
        float panelWidth = Mathf.Min(Mathf.Max(360f, safe.width * 0.46f), maxWidth);

        float maxHeight = Mathf.Max(140f, safe.height - topOffset - 8f);
        float panelHeight = Mathf.Min(Mathf.Max(180f, safe.height * 0.55f), maxHeight);

        if (_overlayRootRect != null)
        {
            _overlayRootRect.anchoredPosition = new Vector2(leftOffset, -topOffset);
            _overlayRootRect.sizeDelta = new Vector2(panelWidth, panelHeight);
        }

        if (_modeHintRect != null)
        {
            float hintTop = topInset + Mathf.Clamp(safe.height * 0.022f, 14f, 32f);
            float hintWidth = Mathf.Clamp(safe.width * 0.64f, 540f, 1380f);
            _modeHintRect.anchoredPosition = new Vector2(0f, -hintTop);
            _modeHintRect.sizeDelta = new Vector2(hintWidth, 78f);
        }

        return true;
    }

    private static void EnsureModeHintUi()
    {
        if (_overlayCanvasGo == null || _modeHintRect != null)
        {
            return;
        }

        try
        {
            GameObject rootGo = new("PA_FishPeek_ModeHint");
            _modeHintRect = rootGo.AddComponent<RectTransform>();
            _modeHintRect.SetParent(_overlayCanvasGo.transform, false);
            _modeHintRect.anchorMin = new Vector2(0.5f, 1f);
            _modeHintRect.anchorMax = new Vector2(0.5f, 1f);
            _modeHintRect.pivot = new Vector2(0.5f, 1f);
            _modeHintRect.anchoredPosition = new Vector2(0f, -26f);
            _modeHintRect.sizeDelta = new Vector2(980f, 78f);
            GameObject labelGo = new("PA_FishPeek_ModeHintLabel");
            RectTransform labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.SetParent(_modeHintRect, false);
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = Vector2.zero;

            _modeHintLabel = labelGo.AddComponent<TextMeshProUGUI>();
            _modeHintLabel.text = string.Empty;
            _modeHintLabel.enabled = false;
            ApplyModeHintTextStyle(_modeHintLabel);
            UpdateOverlayRootRectIfNeeded();
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[FishPeek] 模式提示界面初始化失败：{ex.Message}");
            if (_modeHintRect != null)
            {
                Object.Destroy(_modeHintRect.gameObject);
            }

            _modeHintRect = null;
            _modeHintLabel = null;
        }
    }

    private static void ShowModeHint(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || !EnsureOverlayUi())
        {
            return;
        }

        EnsureModeHintUi();
        if (_modeHintLabel == null)
        {
            return;
        }

        _modeHintLabel.text = text;
        _modeHintLabel.enabled = true;
        ApplyModeHintTextStyle(_modeHintLabel);
        _modeHintShownRealtime = Time.realtimeSinceStartup;
        _modeHintExpireRealtime = _modeHintShownRealtime + ModeHintHoldSeconds + ModeHintFadeSeconds;
        UpdateModeHintVisual(_modeHintShownRealtime);
    }

    private static void UpdateModeHintVisual(float nowRealtime)
    {
        if (_modeHintLabel == null)
        {
            return;
        }

        if (nowRealtime >= _modeHintExpireRealtime)
        {
            _modeHintLabel.enabled = false;
            return;
        }

        if (!_modeHintLabel.enabled)
        {
            _modeHintLabel.enabled = true;
        }

        float elapsed = nowRealtime - _modeHintShownRealtime;
        float alpha = 1f;
        if (elapsed > ModeHintHoldSeconds)
        {
            alpha = 1f - Mathf.Clamp01((elapsed - ModeHintHoldSeconds) / ModeHintFadeSeconds);
        }

        Color c = _modeHintLabel.color;
        c.a = alpha;
        _modeHintLabel.color = c;

        Color oc = _modeHintLabel.outlineColor;
        oc.a = Mathf.Clamp01(alpha);
        _modeHintLabel.outlineColor = oc;

    }

    private static void ApplyModeHintTextStyle(TextMeshProUGUI label)
    {
        if (label == null)
        {
            return;
        }

        EnsureOverlayFontAsset();
        if (_overlayFontAsset != null)
        {
            label.font = _overlayFontAsset;
        }

        label.fontSize = ModeHintFontSize;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color32(255, 255, 255, 245);
        label.outlineWidth = 0.27f;
        label.outlineColor = new Color32(0, 0, 0, 220);
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.alignment = TextAlignmentOptions.Top;
        label.raycastTarget = false;
    }

    private static void EnsureOverlayFontAsset()
    {
        if (_overlayFontAsset != null)
        {
            return;
        }

        float now = Time.realtimeSinceStartup;
        if (now < _nextOverlayFontSearchRealtime)
        {
            return;
        }

        _nextOverlayFontSearchRealtime = now + 5f;

        try
        {
            TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (fonts == null || fonts.Length == 0)
            {
                return;
            }

            TMP_FontAsset? selected = null;
            foreach (string preferredName in OverlayPreferredFontNames)
            {
                for (int i = 0; i < fonts.Length; i++)
                {
                    TMP_FontAsset font = fonts[i];
                    if (font != null &&
                        string.Equals(font.name, preferredName, StringComparison.OrdinalIgnoreCase))
                    {
                        selected = font;
                        break;
                    }
                }

                if (selected != null)
                {
                    break;
                }
            }

            if (selected == null)
            {
                for (int i = 0; i < fonts.Length; i++)
                {
                    TMP_FontAsset font = fonts[i];
                    if (font == null)
                    {
                        continue;
                    }

                    if (font.name.IndexOf("LetsParty", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        font.name.IndexOf("cjkFonts_allseto", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        selected = font;
                        break;
                    }
                }
            }

            if (selected == null)
            {
                return;
            }

            _overlayFontAsset = selected;
            if (!_overlayFontBoundLogged)
            {
                _overlayFontBoundLogged = true;
                _log?.LogMessage($"[FishPeek] Overlay font locked: {selected.name}, baseSize={OverlayBaseFontSize:0.##}");
            }

            for (int i = 0; i < _overlayMessages.Count; i++)
            {
                OverlayMessageItem item = _overlayMessages[i];
                if (item.Label != null)
                {
                    ApplyOverlayTextStyle(item.Label, item.LevelBucket, item.SuppressOutline);
                    ApplyOverlayItemVisual(item, Time.realtimeSinceStartup);
                }
            }

            if (_modeHintLabel != null)
            {
                ApplyModeHintTextStyle(_modeHintLabel);
                UpdateModeHintVisual(Time.realtimeSinceStartup);
            }
        }
        catch
        {
            // ignored
        }
    }

    private static unsafe void SpotReplyCallbackHook(IntPtr thisPtr, IntPtr eventParamPtr, IntPtr extraContextPtr, IntPtr methodInfo)
    {
        _spotReplyOriginal?.Invoke(thisPtr, eventParamPtr, extraContextPtr, methodInfo);

        if (!IsAcceptedSpotReplyMethodInfo(methodInfo))
        {
            return;
        }

        if (!IsTargetControllerInstance(thisPtr))
        {
            return;
        }

        _spotReplyHookInvokeCount++;
        if (_spotReplyHookInvokeCount <= 3 && EnableVerboseInternalLogs)
        {
            _log?.LogMessage($"[FishPeek] 钓点回复回调触发 #{_spotReplyHookInvokeCount} this=0x{thisPtr.ToInt64():X} param=0x{eventParamPtr.ToInt64():X}");
        }

        int playerLocalId = 0;
        if (!TryGetControllerGlobalId(thisPtr, out playerLocalId) || playerLocalId <= 0)
        {
            return;
        }

        lock (_nameCacheLock)
        {
            _controllerPtrToGlobalId[thisPtr.ToInt64()] = playerLocalId;
            if (_controllerPtrToGlobalId.Count > 256)
            {
                _controllerPtrToGlobalId.Clear();
            }
        }

        if (TryResolveLocationFlag(thisPtr, out int locationRaw))
        {
            int locationFlag = NormalizeFlagValue(locationRaw, new[] { 1, 2, 4, 8, 16, 32, 64, 128 });
            if (locationFlag != 0)
            {
                CachePlayerLocationFlag(playerLocalId, locationFlag, thisPtr);
            }
        }

        if (TryResolveCurrentRodAccessoryType(thisPtr, out int accessoryType) && accessoryType != 0)
        {
            CachePlayerAccessoryType(playerLocalId, accessoryType);
        }

        if (IsSelfPlayerGlobalId(playerLocalId))
        {
            _ = TryResolveRodIdentityForPlayer(thisPtr, playerLocalId, out _, out _, out _);
        }
    }

    private static unsafe void TryResolveAndHookSelfEncounterCallback()
    {
        if (_selfEncounterDetour != null)
        {
            return;
        }

        try
        {
            _waitingFlowControllerClass = FindClassByNameAnyImage("WaitingFlowControllerForClient", "MiniGame.FishingGame.FlowControl");
            if (_waitingFlowControllerClass == IntPtr.Zero)
            {
                return;
            }

            if (_offsetFlowGameController < 0)
            {
                IntPtr flowBaseClass = FindClassByNameAnyImage("FishingGameFlowController", "MiniGame.FishingGame.FlowControl");
                IntPtr flowControllerField = FindField(
                    flowBaseClass != IntPtr.Zero ? flowBaseClass : _waitingFlowControllerClass,
                    new[] { "m_GameController", "gameController", "m_gameController" },
                    "FishingGameController");
                if (flowControllerField != IntPtr.Zero)
                {
                    _offsetFlowGameController = (int)IL2CPP.il2cpp_field_get_offset(flowControllerField);
                }
            }

            if (_offsetFlowGameController < 0)
            {
                _log?.LogWarning("[FishPeek] 已跳过本地等待流回调挂接：未解析到游戏控制器字段。");
                return;
            }

            IntPtr iter = IntPtr.Zero;
            while (true)
            {
                IntPtr method = IL2CPP.il2cpp_class_get_methods(_waitingFlowControllerClass, ref iter);
                if (method == IntPtr.Zero)
                {
                    break;
                }

                if (IL2CPP.il2cpp_method_get_param_count(method) != 2)
                {
                    continue;
                }

                string methodName = PtrToAnsi(IL2CPP.il2cpp_method_get_name(method));
                if (!string.Equals(methodName, "Callback", StringComparison.Ordinal))
                {
                    continue;
                }

                IntPtr p0Type = IL2CPP.il2cpp_method_get_param(method, 0);
                string p0TypeName = PtrToAnsi(IL2CPP.il2cpp_type_get_name(p0Type));
                if (p0TypeName.IndexOf("FishingGameEncounterFishNotify", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                var nativeMethod = UnityVersionHandler.Wrap((Il2CppMethodInfo*)method);
                IntPtr fn = nativeMethod?.MethodPointer ?? IntPtr.Zero;
                if (fn == IntPtr.Zero)
                {
                    continue;
                }

                _selfEncounterMethodInfo = method;
                _selfEncounterFunction = fn;
                _selfEncounterHookKeepAlive = SelfEncounterCallbackHook;
                _selfEncounterDetour = INativeDetour.CreateAndApply(_selfEncounterFunction, _selfEncounterHookKeepAlive, out EncounterCallbackDelegate original);
                _selfEncounterOriginal = original;
                _log?.LogMessage("[FishPeek] 已挂接本地等待流回调。");
                return;
            }
        }
        catch (Exception ex)
        {
            _log?.LogWarning("[FishPeek] 挂接本地等待流回调失败：" + ex.Message);
        }
    }

    private static unsafe void TryResolveAndHookSpotReplyCallback()
    {
        if (_spotReplyDetour != null || _targetControllerClass == IntPtr.Zero)
        {
            return;
        }

        try
        {
            IntPtr iter = IntPtr.Zero;
            while (true)
            {
                IntPtr method = IL2CPP.il2cpp_class_get_methods(_targetControllerClass, ref iter);
                if (method == IntPtr.Zero)
                {
                    break;
                }

                if (IL2CPP.il2cpp_method_get_param_count(method) != 2)
                {
                    continue;
                }

                string methodName = PtrToAnsi(IL2CPP.il2cpp_method_get_name(method));
                if (!string.Equals(methodName, "Callback", StringComparison.Ordinal))
                {
                    continue;
                }

                IntPtr p0Type = IL2CPP.il2cpp_method_get_param(method, 0);
                string p0TypeName = PtrToAnsi(IL2CPP.il2cpp_type_get_name(p0Type));
                if (p0TypeName.IndexOf("FishingGameFishingSpotReply", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                var nativeMethod = UnityVersionHandler.Wrap((Il2CppMethodInfo*)method);
                IntPtr fn = nativeMethod?.MethodPointer ?? IntPtr.Zero;
                if (fn == IntPtr.Zero)
                {
                    continue;
                }

                _spotReplyMethodInfo = method;
                _spotReplyFunction = fn;
                _spotReplyHookKeepAlive = SpotReplyCallbackHook;
                _spotReplyDetour = INativeDetour.CreateAndApply(_spotReplyFunction, _spotReplyHookKeepAlive, out EncounterCallbackDelegate original);
                _spotReplyOriginal = original;
                _log?.LogMessage("[FishPeek] 已挂接钓点回复回调。");
                return;
            }
        }
        catch (Exception ex)
        {
            _log?.LogWarning("[FishPeek] 挂接钓点回复回调失败：" + ex.Message);
        }
    }

    private static void TryAttachOverlayFallbackFonts()
    {
        if (_overlayFontAsset == null || _overlayFallbackFontsBound)
        {
            return;
        }

        try
        {
            _overlayFallbackFontsBound = true;
        }
        catch
        {
            // ignored
        }
    }

    private static int NormalizeLevelBucket(float levelRaw)
    {
        int lv = Mathf.RoundToInt(levelRaw);
        if (lv < 1)
        {
            lv = 1;
        }
        else if (lv > 6)
        {
            lv = 6;
        }

        return lv;
    }

    private static string GetLevelDisplayText(int levelBucket)
    {
        return levelBucket switch
        {
            1 => "\u4e00",
            2 => "\u4e8c",
            3 => "\u4e09",
            4 => "\u56db",
            5 => "\u4e94",
            6 => "\u516d",
            _ => levelBucket.ToString()
        };
    }

    private static Color GetOverlayTextColorByLevel(int levelBucket)
    {
        switch (levelBucket)
        {
            case 1:
                return new Color32(255, 255, 255, 255);
            case 2:
                return new Color32(55, 214, 103, 255);
            case 3:
                return new Color32(60, 150, 255, 255);
            case 4:
                return new Color32(170, 110, 255, 255);
            case 5:
                return new Color32(255, 163, 35, 255);
            default:
                return new Color32(255, 64, 64, 255);
        }
    }

    private static float GetOverlayFontSizeByLevel(int levelBucket)
    {
        return OverlayBaseFontSize + Mathf.Max(0, levelBucket - 1) * OverlayFontSizePerLevel;
    }

    private static float EvaluateOverlayAlpha(OverlayMessageItem item, float nowRealtime)
    {
        float elapsed = nowRealtime - item.CreatedAtRealtime;
        if (elapsed <= 0f)
        {
            return 0f;
        }

        if (elapsed < OverlayMessageFadeInSeconds)
        {
            return Mathf.Clamp01(elapsed / OverlayMessageFadeInSeconds);
        }

        if (elapsed < OverlayMessageHoldSeconds)
        {
            return 1f;
        }

        float fadeOutElapsed = elapsed - OverlayMessageHoldSeconds;
        if (fadeOutElapsed >= OverlayMessageFadeOutSeconds)
        {
            return 0f;
        }

        return 1f - Mathf.Clamp01(fadeOutElapsed / OverlayMessageFadeOutSeconds);
    }

    private static void ApplyOverlayItemVisual(OverlayMessageItem item, float nowRealtime)
    {
        if (item.Label == null)
        {
            return;
        }

        float alpha = EvaluateOverlayAlpha(item, nowRealtime);
        if (Mathf.Abs(item.LastAppliedAlpha - alpha) < 0.01f)
        {
            return;
        }

        Color textColor = GetOverlayTextColorByLevel(item.LevelBucket);
        textColor.a = alpha;
        item.Label.color = textColor;

        Color32 outlineColor = _overlayOutlineColor;
        if (item.SuppressOutline)
        {
            outlineColor.a = 0;
        }
        else
        {
            outlineColor.a = (byte)Mathf.Clamp(Mathf.RoundToInt(_overlayOutlineColor.a * alpha), 0, 255);
        }

        item.Label.outlineColor = outlineColor;
        item.LastAppliedAlpha = alpha;
    }

    private static void ApplyOverlayTextStyle(TextMeshProUGUI label, int levelBucket, bool suppressOutline)
    {
        if (label == null)
        {
            return;
        }

        EnsureOverlayFontAsset();
        if (_overlayFontAsset != null)
        {
            label.font = _overlayFontAsset;
        }

        label.fontSize = GetOverlayFontSizeByLevel(levelBucket);
        label.fontStyle = _overlayFontStyle;
        label.color = GetOverlayTextColorByLevel(levelBucket);
        label.outlineWidth = suppressOutline ? 0f : _overlayOutlineWidth;
        label.outlineColor = suppressOutline ? new Color32(0, 0, 0, 0) : _overlayOutlineColor;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.raycastTarget = false;
    }

    private static void ReflowOverlayMessages()
    {
        if (_overlayRootRect == null)
        {
            return;
        }

        float width = Mathf.Max(240f, _overlayRootRect.rect.width);
        float y = 0f;
        for (int i = 0; i < _overlayMessages.Count; i++)
        {
            OverlayMessageItem item = _overlayMessages[i];
            if (item.Label == null)
            {
                continue;
            }

            RectTransform rt = item.Label.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, -y);
            rt.sizeDelta = new Vector2(width, 0f);

            float h = Mathf.Max(item.Label.fontSize + 8f, item.Label.preferredHeight);
            rt.sizeDelta = new Vector2(width, h);
            y += h + OverlayLineGapPixels;
        }
    }

    private static bool TryGetControllerGlobalId(IntPtr controllerPtr, out int controllerGlobalId)
    {
        controllerGlobalId = 0;
        if (controllerPtr == IntPtr.Zero)
        {
            return false;
        }

        long controllerKey = controllerPtr.ToInt64();
        lock (_nameCacheLock)
        {
            if (_controllerPtrToGlobalId.TryGetValue(controllerKey, out int cached) && cached > 0)
            {
                controllerGlobalId = cached;
                return true;
            }
        }

        if (_offsetControllerGlobalId >= 0 &&
            TryReadInt32Field(controllerPtr, _offsetControllerGlobalId, out int fieldValue) &&
            fieldValue > 0)
        {
            RememberControllerGlobalId(controllerPtr, fieldValue);
            controllerGlobalId = fieldValue;
            return true;
        }

        TryResolvePlayerNameMethods();
        if (_miGetControllerGlobalId == IntPtr.Zero ||
            !TryInvokeZeroArgIntMethod(_miGetControllerGlobalId, controllerPtr, out int resolved) ||
            resolved <= 0)
        {
            return false;
        }

        lock (_nameCacheLock)
        {
            _controllerPtrToGlobalId[controllerKey] = resolved;
            if (_controllerPtrToGlobalId.Count > 256)
            {
                _controllerPtrToGlobalId.Clear();
            }
        }

        controllerGlobalId = resolved;
        return true;
    }

    private static string? TryResolvePlayerName(IntPtr controllerPtr)
    {
        if (controllerPtr == IntPtr.Zero)
        {
            return null;
        }

        TryResolvePlayerNameMethods();
        if (_miGetCurFishingPlayerSeat == IntPtr.Zero ||
            (_miGetPlayerName == IntPtr.Zero && _miGamerInfoGetGamerName == IntPtr.Zero))
        {
            return null;
        }

        try
        {
            IntPtr seatPtr = InvokeZeroArgMethod(_miGetCurFishingPlayerSeat, controllerPtr);

            if (seatPtr == IntPtr.Zero)
            {
                return null;
            }

            IntPtr mainControllerPtr = TryResolveMainControllerPtrFromSeat(seatPtr);
            if (mainControllerPtr != IntPtr.Zero && _miGetPlayerName != IntPtr.Zero)
            {
                IntPtr playerNamePtr = InvokeZeroArgMethod(_miGetPlayerName, mainControllerPtr);
                if (playerNamePtr != IntPtr.Zero)
                {
                    string? rawName = IL2CPP.Il2CppStringToManaged(playerNamePtr);
                    if (!string.IsNullOrWhiteSpace(rawName))
                    {
                        return rawName.Trim();
                    }
                }
            }

            IntPtr gamerInfoPtr = IntPtr.Zero;
            if (_miSeatGetGamerInfo != IntPtr.Zero)
            {
                gamerInfoPtr = InvokeZeroArgMethod(_miSeatGetGamerInfo, seatPtr);
            }

            if (gamerInfoPtr == IntPtr.Zero && mainControllerPtr != IntPtr.Zero && _miGetGamerInfo != IntPtr.Zero)
            {
                gamerInfoPtr = InvokeZeroArgMethod(_miGetGamerInfo, mainControllerPtr);
            }

            if (gamerInfoPtr != IntPtr.Zero && _miGamerInfoGetGamerName != IntPtr.Zero)
            {
                IntPtr gamerNamePtr = InvokeZeroArgMethod(_miGamerInfoGetGamerName, gamerInfoPtr);
                if (gamerNamePtr != IntPtr.Zero)
                {
                    string? rawGamerName = IL2CPP.Il2CppStringToManaged(gamerNamePtr);
                    if (!string.IsNullOrWhiteSpace(rawGamerName))
                    {
                        if (!_gamerInfoNameFallbackLogged)
                        {
                            _gamerInfoNameFallbackLogged = true;
                            _log?.LogMessage("[FishPeek] 已通过 gamerInfo.gamerName 回退解析到玩家昵称。");
                        }

                        return rawGamerName.Trim();
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static unsafe IntPtr InvokeZeroArgMethod(IntPtr method, IntPtr instance)
    {
        if (method == IntPtr.Zero || instance == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        try
        {
            IntPtr exc = IntPtr.Zero;
            IntPtr value = IL2CPP.il2cpp_runtime_invoke(method, instance, null, ref exc);
            return exc == IntPtr.Zero ? value : IntPtr.Zero;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static bool TryInvokeZeroArgStringMethod(IntPtr method, IntPtr instance, out string value)
    {
        value = string.Empty;
        if (method == IntPtr.Zero || instance == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            IntPtr raw = InvokeZeroArgMethod(method, instance);
            return TryReadStringObject(raw, out value);
        }
        catch
        {
            value = string.Empty;
            return false;
        }
    }

    private static bool TryReadStringObjectField(IntPtr instance, int offset, out string value)
    {
        value = string.Empty;
        if (instance == IntPtr.Zero || offset < 0)
        {
            return false;
        }

        try
        {
            if (!TryReadObjectField(instance, offset, out IntPtr raw) || raw == IntPtr.Zero)
            {
                return false;
            }

            return TryReadStringObject(raw, out value);
        }
        catch
        {
            value = string.Empty;
            return false;
        }
    }

    private static bool TryReadStringObject(IntPtr raw, out string value)
    {
        value = string.Empty;
        if (raw == IntPtr.Zero || !TryGetObjectClass(raw, out IntPtr klass) || !IsSystemStringClass(klass))
        {
            return false;
        }

        try
        {
            string? text = IL2CPP.Il2CppStringToManaged(raw);
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            value = text.Trim();
            return true;
        }
        catch
        {
            value = string.Empty;
            return false;
        }
    }

    private static unsafe IntPtr InvokeStaticZeroArgMethod(IntPtr method)
    {
        if (method == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        try
        {
            IntPtr exc = IntPtr.Zero;
            IntPtr value = IL2CPP.il2cpp_runtime_invoke(method, IntPtr.Zero, null, ref exc);
            return exc == IntPtr.Zero ? value : IntPtr.Zero;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static IntPtr TryResolveMainControllerPtrFromSeat(IntPtr seatPtr)
    {
        if (seatPtr == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        if (_offsetSeatMainController >= 0)
        {
            try
            {
                int need = _offsetSeatMainController + IntPtr.Size;
                if (need > 0 && IsReadable(seatPtr, need))
                {
                    IntPtr fromField = Marshal.ReadIntPtr(IntPtr.Add(seatPtr, _offsetSeatMainController));
                    if (fromField != IntPtr.Zero)
                    {
                        return fromField;
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        return InvokeZeroArgMethod(_miGetMainController, seatPtr);
    }

    private static unsafe bool TryInvokeZeroArgIntMethod(IntPtr method, IntPtr instance, out int value)
    {
        value = 0;
        if (method == IntPtr.Zero || instance == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            IntPtr exc = IntPtr.Zero;
            IntPtr boxed = IL2CPP.il2cpp_runtime_invoke(method, instance, null, ref exc);
            if (exc != IntPtr.Zero || boxed == IntPtr.Zero)
            {
                return false;
            }

            IntPtr unboxed = IL2CPP.il2cpp_object_unbox(boxed);
            if (unboxed == IntPtr.Zero || !IsReadable(unboxed, sizeof(int)))
            {
                return false;
            }

            value = Marshal.ReadInt32(unboxed);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryResolvePlayerNameMethods()
    {
        if (_targetControllerClass == IntPtr.Zero)
        {
            return;
        }

        bool hasSeatAccessor = _miGetCurFishingPlayerSeat != IntPtr.Zero;
        bool hasMainControllerPath = _offsetSeatMainController >= 0 ||
                                     _miGetMainController != IntPtr.Zero ||
                                     _miSeatGetMainController != IntPtr.Zero;
        bool hasNameSource = _miGetPlayerName != IntPtr.Zero || _miGamerInfoGetGamerName != IntPtr.Zero;
        bool hasControllerGlobalId = _miGetControllerGlobalId != IntPtr.Zero;

        if (hasSeatAccessor && hasMainControllerPath && hasNameSource && hasControllerGlobalId)
        {
            return;
        }

        try
        {
            if (_miGetCurFishingPlayerSeat == IntPtr.Zero)
            {
                _miGetCurFishingPlayerSeat = FindZeroArgMethod(
                    _targetControllerClass,
                    new[] { "get_curFishingPlayerSeat", "get_CurFishingPlayerSeat", "get_currentFishingPlayerSeat" },
                    "FishingGamePlayerSeat");
            }

            if (_miGetControllerGlobalId == IntPtr.Zero)
            {
                _miGetControllerGlobalId = FindZeroArgMethod(
                    _targetControllerClass,
                    new[] { "get_globalId", "get_GlobalId" },
                    "System.Int32");
            }

            if (_offsetControllerGlobalId < 0)
            {
                IntPtr controllerGlobalIdField = FindField(
                    _targetControllerClass,
                    new[] { "globalId", "m_globalId", "<globalId>k__BackingField" },
                    "System.Int32");
                if (controllerGlobalIdField != IntPtr.Zero)
                {
                    _offsetControllerGlobalId = (int)IL2CPP.il2cpp_field_get_offset(controllerGlobalIdField);
                }
            }

            IntPtr seatClass = GetMethodReturnClass(_miGetCurFishingPlayerSeat);

            if (seatClass != IntPtr.Zero)
            {
                if (_offsetSeatMainController < 0)
                {
                    IntPtr seatMainField = FindField(
                        seatClass,
                        new[] { "mainController", "<mainController>k__BackingField", "m_mainController" },
                        "MainController");
                    if (seatMainField != IntPtr.Zero)
                    {
                        _offsetSeatMainController = (int)IL2CPP.il2cpp_field_get_offset(seatMainField);
                    }
                }

                if (_miSeatGetMainController == IntPtr.Zero)
                {
                    _miSeatGetMainController = FindZeroArgMethod(
                        seatClass,
                        new[] { "get_mainController", "get_MainController" },
                        "MainController");
                }

                if (_miSeatGetGamerInfo == IntPtr.Zero)
                {
                    _miSeatGetGamerInfo = FindZeroArgMethod(
                        seatClass,
                        new[] { "get_gamerInfo", "get_GamerInfo" },
                        null);
                }

                if (_miGetMainController == IntPtr.Zero)
                {
                    _miGetMainController = _miSeatGetMainController;
                }
            }

            IntPtr mainControllerClass = IntPtr.Zero;
            if (_miSeatGetMainController != IntPtr.Zero)
            {
                mainControllerClass = GetMethodReturnClass(_miSeatGetMainController);
            }
            else if (_miGetMainController != IntPtr.Zero)
            {
                mainControllerClass = GetMethodReturnClass(_miGetMainController);
            }

            if (mainControllerClass == IntPtr.Zero && _targetControllerClass != IntPtr.Zero)
            {
                mainControllerClass = FindClassByNameAnyImage("MainController", "Character");
            }

            if (_miGetPlayerName == IntPtr.Zero && mainControllerClass != IntPtr.Zero)
            {
                _miGetPlayerName = FindZeroArgMethod(
                    mainControllerClass,
                    new[] { "get_playerName", "get_PlayerName", "get_name" },
                    "System.String");
            }

            if (_miGetMainControllerClientId == IntPtr.Zero && mainControllerClass != IntPtr.Zero)
            {
                _miGetMainControllerClientId = FindZeroArgMethod(
                    mainControllerClass,
                    new[] { "get_clientId", "get_ClientId", "get_clientID", "get_ClientID" },
                    null);
            }

            IntPtr gamerInfoClass = IntPtr.Zero;
            if (_miGetGamerInfo == IntPtr.Zero && mainControllerClass != IntPtr.Zero)
            {
                _miGetGamerInfo = FindZeroArgMethod(
                    mainControllerClass,
                    new[] { "get_gamerInfo", "get_GamerInfo" },
                    null);
            }

            if (_miGetGamerInfo != IntPtr.Zero)
            {
                gamerInfoClass = GetMethodReturnClass(_miGetGamerInfo);
            }

            if (gamerInfoClass == IntPtr.Zero && _miSeatGetGamerInfo != IntPtr.Zero)
            {
                gamerInfoClass = GetMethodReturnClass(_miSeatGetGamerInfo);
            }

            if (_miGamerInfoGetGamerName == IntPtr.Zero && gamerInfoClass != IntPtr.Zero)
            {
                _miGamerInfoGetGamerName = FindZeroArgMethod(
                    gamerInfoClass,
                    new[] { "get_gamerName", "get_GamerName", "get_playerName", "get_PlayerName", "get_name" },
                    "System.String");
            }
        }
        catch
        {
            // ignored
        }

        hasSeatAccessor = _miGetCurFishingPlayerSeat != IntPtr.Zero;
        hasMainControllerPath = _offsetSeatMainController >= 0 ||
                                _miGetMainController != IntPtr.Zero ||
                                _miSeatGetMainController != IntPtr.Zero;
        hasNameSource = _miGetPlayerName != IntPtr.Zero || _miGamerInfoGetGamerName != IntPtr.Zero;
        hasControllerGlobalId = _miGetControllerGlobalId != IntPtr.Zero;

        if (hasSeatAccessor && hasMainControllerPath && hasNameSource && hasControllerGlobalId)
        {
            if (!_nameResolverReadyLogged)
            {
                _nameResolverReadyLogged = true;
                _log?.LogMessage("[FishPeek] 玩家昵称解析器已就绪。");
            }
            return;
        }

        if (_miGetCurFishingPlayerSeat != IntPtr.Zero &&
            hasMainControllerPath &&
            hasNameSource)
        {
            if (!_nameResolverReadyLogged)
            {
                _nameResolverReadyLogged = true;
                _log?.LogMessage("[FishPeek] 玩家昵称解析器部分就绪。");
            }
            return;
        }

        if (!_nameResolverFailLogged)
        {
            _nameResolverFailLogged = true;
            _log?.LogMessage("[FishPeek] 玩家昵称解析器未就绪。");
        }
    }

    private static IntPtr GetMethodReturnClass(IntPtr method)
    {
        if (method == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        try
        {
            IntPtr returnType = IL2CPP.il2cpp_method_get_return_type(method);
            return IL2CPP.il2cpp_type_get_class_or_element_class(returnType);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static IntPtr GetFieldTypeClass(IntPtr field)
    {
        if (field == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        try
        {
            IntPtr fieldType = IL2CPP.il2cpp_field_get_type(field);
            return fieldType == IntPtr.Zero ? IntPtr.Zero : IL2CPP.il2cpp_type_get_class_or_element_class(fieldType);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static bool IsValueTypeClass(IntPtr klass)
    {
        if (klass == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            return IL2CPP.il2cpp_class_is_valuetype(klass);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetObjectClass(IntPtr objPtr, out IntPtr klass)
    {
        klass = IntPtr.Zero;
        if (objPtr == IntPtr.Zero || !IsReadable(objPtr, IntPtr.Size))
        {
            return false;
        }

        try
        {
            klass = Marshal.ReadIntPtr(objPtr);
            if (klass == IntPtr.Zero || !IsReadable(klass, IntPtr.Size * 2))
            {
                klass = IntPtr.Zero;
                return false;
            }

            return true;
        }
        catch
        {
            klass = IntPtr.Zero;
            return false;
        }
    }

    private static bool IsSystemStringClass(IntPtr klass)
    {
        if (klass == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            string className = PtrToAnsi(IL2CPP.il2cpp_class_get_name(klass));
            if (!string.Equals(className, "String", StringComparison.Ordinal))
            {
                return false;
            }

            string classNamespace = PtrToAnsi(IL2CPP.il2cpp_class_get_namespace(klass));
            return string.Equals(classNamespace, "System", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static unsafe IntPtr FindClassByNameAnyImage(string className, string? namespaceContains)
    {
        try
        {
            IntPtr domain = IL2CPP.il2cpp_domain_get();
            if (domain == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            uint asmCount = 0;
            IntPtr* asms = IL2CPP.il2cpp_domain_get_assemblies(domain, ref asmCount);
            if (asms == null || asmCount == 0)
            {
                return IntPtr.Zero;
            }

            for (uint ai = 0; ai < asmCount; ai++)
            {
                IntPtr asm = asms[ai];
                if (asm == IntPtr.Zero)
                {
                    continue;
                }

                IntPtr image = IL2CPP.il2cpp_assembly_get_image(asm);
                if (image == IntPtr.Zero)
                {
                    continue;
                }

                uint classCount = IL2CPP.il2cpp_image_get_class_count(image);
                for (uint ci = 0; ci < classCount; ci++)
                {
                    IntPtr klass = IL2CPP.il2cpp_image_get_class(image, ci);
                    if (klass == IntPtr.Zero)
                    {
                        continue;
                    }

                    string foundClass = PtrToAnsi(IL2CPP.il2cpp_class_get_name(klass));
                    if (!string.Equals(foundClass, className, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(namespaceContains))
                    {
                        return klass;
                    }

                    string foundNs = PtrToAnsi(IL2CPP.il2cpp_class_get_namespace(klass));
                    if (foundNs.IndexOf(namespaceContains, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return klass;
                    }
                }
            }
        }
        catch
        {
            return IntPtr.Zero;
        }

        return IntPtr.Zero;
    }

    private static unsafe IntPtr FindZeroArgMethod(IntPtr klass, string[] preferredNames, string? returnTypeContains)
    {
        for (IntPtr k = klass; k != IntPtr.Zero; k = IL2CPP.il2cpp_class_get_parent(k))
        {
            foreach (string candidate in preferredNames)
            {
                IntPtr exact = IL2CPP.il2cpp_class_get_method_from_name(k, candidate, 0);
                if (exact != IntPtr.Zero)
                {
                    return exact;
                }
            }

            IntPtr fallback = IntPtr.Zero;
            IntPtr iter = IntPtr.Zero;
            while (true)
            {
                IntPtr method = IL2CPP.il2cpp_class_get_methods(k, ref iter);
                if (method == IntPtr.Zero)
                {
                    break;
                }

                if (IL2CPP.il2cpp_method_get_param_count(method) != 0)
                {
                    continue;
                }

                string methodName = PtrToAnsi(IL2CPP.il2cpp_method_get_name(method));
                foreach (string candidate in preferredNames)
                {
                    if (string.Equals(methodName, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        return method;
                    }
                }

                if (fallback != IntPtr.Zero || string.IsNullOrEmpty(returnTypeContains))
                {
                    continue;
                }

                string returnTypeName = PtrToAnsi(IL2CPP.il2cpp_type_get_name(IL2CPP.il2cpp_method_get_return_type(method)));
                if (returnTypeName.IndexOf(returnTypeContains, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fallback = method;
                }
            }

            if (fallback != IntPtr.Zero)
            {
                return fallback;
            }
        }

        return IntPtr.Zero;
    }

    private static string PtrToAnsi(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
        {
            return string.Empty;
        }

        try
        {
            return Marshal.PtrToStringAnsi(ptr) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed class ScanResult
    {
        public bool Found;
        public string Error = string.Empty;

        public int AssemblyCount;
        public int ClassCount;
        public int MethodCount;

        public string FoundImage = string.Empty;
        public string FoundNamespace = string.Empty;
        public string FoundClass = string.Empty;
        public string FoundMethod = string.Empty;
        public string Param0TypeName = string.Empty;

        public IntPtr MethodInfoPtr = IntPtr.Zero;
        public IntPtr MethodFunctionPtr = IntPtr.Zero;
        public IntPtr ControllerClassPtr = IntPtr.Zero;
        public IntPtr EventStructClassPtr = IntPtr.Zero;
        public IntPtr EncounterStructClassPtr = IntPtr.Zero;
        public bool EventStructIsValueType;
        public bool EncounterStructIsValueType;

        public int OffsetEventFishEncounter = -1;
        public int OffsetEventGlobalId = -1;
        public int OffsetEncounterTier = -1;
        public int OffsetEncounterLevel = -1;
        public int OffsetEncounterBodyType = -1;
    }
}

