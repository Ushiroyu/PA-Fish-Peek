using Il2CppInterop.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PAFishPeekPlugin;

public sealed partial class FishPeekPlugin
{
    private static readonly bool EnableRuntimeResourceAugment = true;
    // Scene names identify seasonal cabin variants without invoking WorldTimeController.
    // Use the current autumn cabin only when the active scene does not expose a season.
    private const int DefaultCabinPondSeasonFlag = 4;

    private static Dictionary<string, FishSpeciesContextData>? _fishSpeciesContextByName;

    private static bool _locationResolverScanned;
    private static bool _locationResolverLogged;
    private static bool _locationResolverFailedLogged;
    private static IntPtr _seatClass = IntPtr.Zero;
    private static IntPtr _miSeatGetSpotLocation = IntPtr.Zero;
    private static int _offsetSeatSpotLocation = -1;
    private static IntPtr _miSeatGetSpot = IntPtr.Zero;
    private static int _offsetSeatSpot = -1;
    private static IntPtr _spotClass = IntPtr.Zero;
    private static IntPtr _miSpotGetLocation = IntPtr.Zero;
    private static int _offsetSpotLocation = -1;
    private static IntPtr _miControllerGetCurDetailedFishingSpot = IntPtr.Zero;
    private static IntPtr _detailedFishingSpotClass = IntPtr.Zero;
    private static bool _detailedFishingSpotIsValueType;
    private static int _offsetDetailedFishingSpotFishingSpot = -1;
    private static IntPtr _fishingSpotClass = IntPtr.Zero;
    private static IntPtr _miFishingSpotGetSpotLocation = IntPtr.Zero;
    private static int _offsetFishingSpotSpotLocation = -1;
    private static readonly Dictionary<int, int> _playerLocationFlagCache = new();
    private static readonly Dictionary<long, int> _controllerLocationFlagCache = new();
    private static readonly Dictionary<string, int> _playerNameLocationFlagCache = new(StringComparer.Ordinal);
    private static readonly object _predictionCacheLock = new();

    private static bool _rodResolverScanned;
    private static bool _rodResolverLogged;
    private static bool _rodResolverFailedLogged;
    private static IntPtr _miSeatGetAccessoryType = IntPtr.Zero;
    private static int _offsetSeatAccessoryType = -1;
    private static IntPtr _miSeatGetRod = IntPtr.Zero;
    private static int _offsetSeatRod = -1;
    private static IntPtr _miControllerGetCurrentRod = IntPtr.Zero;
    private static int _offsetControllerCurrentRod = -1;
    private static int _offsetControllerSeat = -1;
    private static IntPtr _rodRuntimeClass = IntPtr.Zero;
    private static IntPtr _miRodGetAccessoryType = IntPtr.Zero;
    private static int _offsetRodAccessoryType = -1;
    private static IntPtr _miRodGetUniqueId = IntPtr.Zero;
    private static int _offsetRodUniqueId = -1;
    private static IntPtr _rodUniqueIdClass = IntPtr.Zero;
    private static bool _rodUniqueIdIsValueType;
    private static IntPtr _miUniqueIdGetUid = IntPtr.Zero;
    private static int _offsetUniqueIdUid = -1;
    private static IntPtr _miRodGetName = IntPtr.Zero;
    private static int _offsetRodName = -1;
    private static IntPtr _miRodGetLastKnownGuid = IntPtr.Zero;
    private static int _offsetRodLastKnownGuid = -1;
    private static IntPtr _miControllerGetAccessoryType = IntPtr.Zero;
    private static readonly Dictionary<int, int> _playerAccessoryTypeCache = new();
    private static readonly Dictionary<int, ulong> _playerRodUniqueUidCache = new();
    private static readonly Dictionary<long, ulong> _controllerRodUniqueUidCache = new();
    private static readonly Dictionary<int, string> _playerRodAssetNameCache = new();
    private static readonly Dictionary<long, string> _controllerRodAssetNameCache = new();
    private static readonly Dictionary<int, string> _playerRodGuidCache = new();
    private static readonly Dictionary<long, string> _controllerRodGuidCache = new();
    private static readonly Dictionary<int, string> _playerRodIdentitySignatureCache = new();
    private static Dictionary<FishingModeContext, Dictionary<int, HashSet<string>>>? _runtimeAccessoryFishByMode;
    private static bool _runtimeAccessoryFishByModeLoaded;
    private static bool _runtimeAccessoryFishByModeLogged;
    private static Dictionary<string, RuntimeFishBasicData>? _runtimeFishBasicByName;
    private static bool _runtimeFishBasicLoaded;
    private static bool _runtimeFishBasicLogged;
    private static bool _weatherResolverScanned;
    private static bool _weatherResolverLogged;
    private static IntPtr _weatherControllerClass = IntPtr.Zero;
    private static IntPtr _miGetCurrentWeather = IntPtr.Zero;
    private static int _lastWeatherFlag = -1;
    private static bool _rodIdentityResolverLogged;
    private static bool _rodIdentityResolverFailedLogged;

    private static int _strictRejectNameCount;
    private static int _strictRejectLocationCount;
    private static int _strictRejectCandidateCount;
    private static float _nextStrictRejectLogRealtime;
    private static int _candidateRejectNoContextCount;
    private static int _candidateRejectModeCount;
    private static int _candidateRejectAccessoryCount;
    private static int _candidateRejectAllowedSetCount;
    private static int _candidateRejectFilteredCount;
    private static float _nextCandidateRejectLogRealtime;

    private enum FishingModeContext
    {
        Unknown = 0,
        Route = 1,
        Ice = 2,
        Pond = 3
    }

    private sealed class FishingEnvironmentContext
    {
        public FishingModeContext Mode = FishingModeContext.Unknown;
        public int LocationFlag;
        public int SeasonFlag;
        public int TimeFlag;
        public int WeatherFlag;
        public string LocationLabel = string.Empty;
        public string SceneName = string.Empty;
    }

    private sealed class FishSpeciesContextData
    {
        public FishSpeciesContextData(
            string name,
            int availableLocationMask,
            int activeSeasonMask,
            int activeTimeMask,
            int activeWeatherMask,
            float env0,
            float env1,
            float env2,
            float env3,
            float routeHugeRockWeight,
            float routePierWeight,
            float routeMidstreamWeight,
            float routeRiverBendWeight,
            float routeRiverBankWeight,
            float routeSunkenBoatWeight,
            float iceWeight,
            float pondWeight)
        {
            Name = name;
            AvailableLocationMask = availableLocationMask;
            ActiveSeasonMask = activeSeasonMask;
            ActiveTimeMask = activeTimeMask;
            ActiveWeatherMask = activeWeatherMask;
            Env0 = env0;
            Env1 = env1;
            Env2 = env2;
            Env3 = env3;
            RouteHugeRockWeight = routeHugeRockWeight;
            RoutePierWeight = routePierWeight;
            RouteMidstreamWeight = routeMidstreamWeight;
            RouteRiverBendWeight = routeRiverBendWeight;
            RouteRiverBankWeight = routeRiverBankWeight;
            RouteSunkenBoatWeight = routeSunkenBoatWeight;
            IceWeight = iceWeight;
            PondWeight = pondWeight;
        }

        public string Name { get; }
        public int AvailableLocationMask { get; }
        public int ActiveSeasonMask { get; }
        public int ActiveTimeMask { get; }
        public int ActiveWeatherMask { get; }
        public float Env0 { get; }
        public float Env1 { get; }
        public float Env2 { get; }
        public float Env3 { get; }
        public float RouteHugeRockWeight { get; }
        public float RoutePierWeight { get; }
        public float RouteMidstreamWeight { get; }
        public float RouteRiverBendWeight { get; }
        public float RouteRiverBankWeight { get; }
        public float RouteSunkenBoatWeight { get; }
        public float IceWeight { get; }
        public float PondWeight { get; }
    }

    private sealed class RuntimeFishBasicData
    {
        public int LevelBucket;
        public int AvailableLocationMask;
        public int ActiveSeasonMask;
        public int ActiveTimeMask;
        public int ActiveWeatherMask;
    }

    private static Dictionary<string, FishSpeciesContextData> BuildFishSpeciesContextByName()
    {
        var map = new Dictionary<string, FishSpeciesContextData>(StringComparer.Ordinal);
        if (BuiltInFishSpeciesContext == null)
        {
            return map;
        }

        foreach (FishSpeciesContextData fish in BuiltInFishSpeciesContext)
        {
            if (fish == null || string.IsNullOrWhiteSpace(fish.Name))
            {
                continue;
            }

            map[fish.Name] = fish;
        }

        return map;
    }

    private static Dictionary<string, FishSpeciesContextData> GetFishSpeciesContextMap()
    {
        if (_fishSpeciesContextByName == null)
        {
            _fishSpeciesContextByName = BuildFishSpeciesContextByName();
        }

        return _fishSpeciesContextByName;
    }

    private static FishingModeContext InferModeFromRodConfig(int compatibleLocation, int fishingType)
    {
        if ((compatibleLocation & 64) != 0)
        {
            return FishingModeContext.Ice;
        }

        if ((compatibleLocation & 128) != 0)
        {
            return FishingModeContext.Pond;
        }

        if ((compatibleLocation & 63) != 0 || (fishingType & 1) != 0 || (fishingType & 2) != 0)
        {
            return FishingModeContext.Route;
        }

        if ((fishingType & 4) != 0)
        {
            if ((compatibleLocation & 64) != 0)
            {
                return FishingModeContext.Ice;
            }

            if ((compatibleLocation & 128) != 0)
            {
                return FishingModeContext.Pond;
            }
        }

        return FishingModeContext.Unknown;
    }

    private static string NormalizePathIdSpeciesName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return string.Empty;
        }

        string text = rawName.Trim();
        if (!text.Contains(';'))
        {
            return text;
        }

        string[] segments = text.Split(';', StringSplitOptions.RemoveEmptyEntries);
        int bestKey = int.MinValue;
        string bestName = text;
        foreach (string segmentRaw in segments)
        {
            string segment = segmentRaw.Trim();
            int colon = segment.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            if (!int.TryParse(segment.Substring(0, colon).Trim(), out int key))
            {
                continue;
            }

            string value = segment.Substring(colon + 1).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (key >= bestKey)
            {
                bestKey = key;
                bestName = value;
            }
        }

        return bestName.Trim();
    }

    private static bool TryLoadPathIdFishNameMap(string pathIdFile, out Dictionary<long, string> result)
    {
        return TryLoadPathIdFishNameMaps(pathIdFile, out result, out _);
    }

    private static bool TryLoadPathIdFishNameMaps(
        string pathIdFile,
        out Dictionary<long, string> result,
        out Dictionary<string, string> englishToFishName)
    {
        result = new Dictionary<long, string>();
        englishToFishName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(pathIdFile) || !File.Exists(pathIdFile))
        {
            return false;
        }

        string[] lines = File.ReadAllLines(pathIdFile, Encoding.UTF8);
        foreach (string lineRaw in lines)
        {
            if (string.IsNullOrWhiteSpace(lineRaw))
            {
                continue;
            }

            string line = lineRaw.Trim();
            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            string[] parts = line.Split("->", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 3)
            {
                continue;
            }

            string englishKey = parts[0].Trim();
            string name = NormalizePathIdSpeciesName(parts[1]);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(englishKey))
            {
                englishToFishName[englishKey] = name;
            }

            string pathIdText = parts[parts.Length - 1];
            if (!long.TryParse(pathIdText, out long pathId))
            {
                continue;
            }

            result[pathId] = name;
        }

        return result.Count > 0 || englishToFishName.Count > 0;
    }

    private static string? TryFindPredictionResourceRoot()
    {
        var candidates = new List<string>(16);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddCandidate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string full;
            try
            {
                full = Path.GetFullPath(value);
            }
            catch
            {
                return;
            }

            if (visited.Add(full))
            {
                candidates.Add(full);
            }
        }

        AddCandidate(Directory.GetCurrentDirectory());
        AddCandidate(AppContext.BaseDirectory);
        AddCandidate(Path.GetDirectoryName(typeof(FishPeekPlugin).Assembly.Location));

        foreach (string seed in candidates.ToArray())
        {
            string? cur = seed;
            for (int i = 0; i < 8 && !string.IsNullOrWhiteSpace(cur); i++)
            {
                AddCandidate(cur);
                cur = Path.GetDirectoryName(cur);
            }
        }

        foreach (string root in candidates)
        {
            string monoDirAsResource = Path.Combine(root, "PartyAnimalsAsset", "MonoBehaviour");
            string pathIdAsResource = Path.Combine(root, "path_id.txt");
            if (Directory.Exists(monoDirAsResource) && File.Exists(pathIdAsResource))
            {
                return root;
            }

            string resourceRoot = Path.Combine(root, "Resource");
            string monoDir = Path.Combine(resourceRoot, "PartyAnimalsAsset", "MonoBehaviour");
            string pathId = Path.Combine(resourceRoot, "path_id.txt");
            if (Directory.Exists(monoDir) && File.Exists(pathId))
            {
                return resourceRoot;
            }
        }

        return null;
    }

    private static bool TryLoadRuntimeAccessoryFishByMode(out Dictionary<FishingModeContext, Dictionary<int, HashSet<string>>> map)
    {
        map = new Dictionary<FishingModeContext, Dictionary<int, HashSet<string>>>
        {
            [FishingModeContext.Route] = new Dictionary<int, HashSet<string>>
            {
                [1] = new HashSet<string>(StringComparer.Ordinal),
                [2] = new HashSet<string>(StringComparer.Ordinal)
            },
            [FishingModeContext.Ice] = new Dictionary<int, HashSet<string>>
            {
                [1] = new HashSet<string>(StringComparer.Ordinal),
                [2] = new HashSet<string>(StringComparer.Ordinal)
            },
            [FishingModeContext.Pond] = new Dictionary<int, HashSet<string>>
            {
                [1] = new HashSet<string>(StringComparer.Ordinal),
                [2] = new HashSet<string>(StringComparer.Ordinal)
            }
        };

        string? resourceRoot = TryFindPredictionResourceRoot();
        if (string.IsNullOrWhiteSpace(resourceRoot))
        {
            return false;
        }

        string pathIdFile = Path.Combine(resourceRoot, "path_id.txt");
        string[] monoDirs = GetPredictionMonoBehaviourDirectories(resourceRoot);
        if (!TryLoadPathIdFishNameMap(pathIdFile, out Dictionary<long, string> pathIdToFishName) ||
            monoDirs.Length == 0)
        {
            return false;
        }

        var rodFiles = new List<string>();
        foreach (string monoDir in monoDirs)
        {
            rodFiles.AddRange(Directory.GetFiles(monoDir, "*_Rod.json", SearchOption.TopDirectoryOnly));
        }
        int added = 0;
        for (int i = 0; i < rodFiles.Count; i++)
        {
            string rodFile = rodFiles[i];
            JsonDocument? doc = null;
            try
            {
                doc = JsonDocument.Parse(File.ReadAllText(rodFile, Encoding.UTF8));
                JsonElement root = doc.RootElement;

                if (!root.TryGetProperty("accessoryType", out JsonElement accessoryEl) ||
                    !accessoryEl.TryGetInt32(out int accessoryType) ||
                    (accessoryType != 1 && accessoryType != 2))
                {
                    continue;
                }

                int fishingType = 0;
                if (root.TryGetProperty("fishingType", out JsonElement fishingTypeEl))
                {
                    fishingTypeEl.TryGetInt32(out fishingType);
                }

                int compatibleLocation = 0;
                if (root.TryGetProperty("compatibleLocation", out JsonElement locationEl))
                {
                    locationEl.TryGetInt32(out compatibleLocation);
                }

                FishingModeContext mode = InferModeFromRodConfig(compatibleLocation, fishingType);
                if (mode == FishingModeContext.Unknown ||
                    !map.TryGetValue(mode, out Dictionary<int, HashSet<string>>? modeMap) ||
                    !modeMap.TryGetValue(accessoryType, out HashSet<string>? bucket))
                {
                    continue;
                }

                if (!root.TryGetProperty("fishSpeciesWeightOffsetTable", out JsonElement tableEl) ||
                    !tableEl.TryGetProperty("m_KvpList", out JsonElement kvpListEl) ||
                    kvpListEl.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (JsonElement kv in kvpListEl.EnumerateArray())
                {
                    if (!kv.TryGetProperty("key", out JsonElement keyEl) ||
                        !keyEl.TryGetProperty("m_PathID", out JsonElement pathIdEl) ||
                        !pathIdEl.TryGetInt64(out long pathId))
                    {
                        continue;
                    }

                    if (!pathIdToFishName.TryGetValue(pathId, out string? fishName) || string.IsNullOrWhiteSpace(fishName))
                    {
                        continue;
                    }

                    if (bucket.Add(fishName.Trim()))
                    {
                        added++;
                    }
                }
            }
            catch
            {
                // ignored, continue other rod files
            }
            finally
            {
                doc?.Dispose();
            }
        }

        return added > 0;
    }

    private static bool TryLoadRuntimeFishBasicByName(out Dictionary<string, RuntimeFishBasicData> map)
    {
        map = new Dictionary<string, RuntimeFishBasicData>(StringComparer.Ordinal);
        string? resourceRoot = TryFindPredictionResourceRoot();
        if (string.IsNullOrWhiteSpace(resourceRoot))
        {
            return false;
        }

        string pathIdFile = Path.Combine(resourceRoot, "path_id.txt");
        string[] monoDirs = GetPredictionMonoBehaviourDirectories(resourceRoot);
        if (!TryLoadPathIdFishNameMaps(pathIdFile, out _, out Dictionary<string, string> englishToFishName) ||
            monoDirs.Length == 0)
        {
            return false;
        }

        int added = 0;
        foreach (KeyValuePair<string, string> pair in englishToFishName)
        {
            string englishName = pair.Key;
            string fishName = pair.Value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(englishName) || string.IsNullOrWhiteSpace(fishName))
            {
                continue;
            }

            string fishFile = string.Empty;
            for (int i = monoDirs.Length - 1; i >= 0; i--)
            {
                string candidate = Path.Combine(monoDirs[i], englishName + ".json");
                if (File.Exists(candidate))
                {
                    fishFile = candidate;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(fishFile))
            {
                continue;
            }

            JsonDocument? doc = null;
            try
            {
                doc = JsonDocument.Parse(File.ReadAllText(fishFile, Encoding.UTF8));
                JsonElement root = doc.RootElement;

                if (!root.TryGetProperty("encounterParam", out JsonElement encounterEl) ||
                    !encounterEl.TryGetProperty("fishLevel", out JsonElement fishLevelEl))
                {
                    continue;
                }

                float fishLevel = 0f;
                if (fishLevelEl.ValueKind == JsonValueKind.Number)
                {
                    fishLevel = fishLevelEl.GetSingle();
                }

                int availableLocation = 0;
                if (root.TryGetProperty("availableLocation", out JsonElement locationEl))
                {
                    locationEl.TryGetInt32(out availableLocation);
                }

                int activeSeason = 0;
                if (root.TryGetProperty("activeSeason", out JsonElement seasonEl))
                {
                    seasonEl.TryGetInt32(out activeSeason);
                }

                int activeTime = 0;
                if (root.TryGetProperty("activeTime", out JsonElement timeEl))
                {
                    timeEl.TryGetInt32(out activeTime);
                }

                int activeWeather = 0;
                if (root.TryGetProperty("activeWeather", out JsonElement weatherEl))
                {
                    weatherEl.TryGetInt32(out activeWeather);
                }

                map[fishName] = new RuntimeFishBasicData
                {
                    LevelBucket = NormalizeLevelBucket(fishLevel),
                    AvailableLocationMask = availableLocation,
                    ActiveSeasonMask = activeSeason,
                    ActiveTimeMask = activeTime,
                    ActiveWeatherMask = activeWeather
                };
                added++;
            }
            catch
            {
                // ignored
            }
            finally
            {
                doc?.Dispose();
            }
        }

        return added > 0;
    }

    private static string[] GetPredictionMonoBehaviourDirectories(string resourceRoot)
    {
        var directories = new List<string>(2);
        string[] candidates =
        {
            Path.Combine(resourceRoot, "PartyAnimalsAsset", "MonoBehaviour"),
            Path.Combine(resourceRoot, "FishingAutumnData", "MonoBehaviour")
        };

        foreach (string candidate in candidates)
        {
            if (Directory.Exists(candidate) && !directories.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                directories.Add(candidate);
            }
        }

        return directories.ToArray();
    }

    private static Dictionary<FishingModeContext, Dictionary<int, HashSet<string>>> GetAccessoryFishByModeMap()
    {
        if (!EnableRuntimeResourceAugment)
        {
            if (!_runtimeAccessoryFishByModeLogged)
            {
                _runtimeAccessoryFishByModeLogged = true;
                _log?.LogMessage("[FishPeek] 已禁用运行时鱼竿白名单读取；仅使用插件内置白名单。");
            }

            return BuiltInAccessoryFishByMode;
        }

        if (!_runtimeAccessoryFishByModeLoaded)
        {
            _runtimeAccessoryFishByModeLoaded = true;
            try
            {
                if (TryLoadRuntimeAccessoryFishByMode(out Dictionary<FishingModeContext, Dictionary<int, HashSet<string>>> loaded))
                {
                    MergeAccessoryFishByMode(loaded, BuiltInAccessoryFishByMode);
                    _runtimeAccessoryFishByMode = loaded;
                }
            }
            catch
            {
                _runtimeAccessoryFishByMode = null;
            }

            if (!_runtimeAccessoryFishByModeLogged)
            {
                _runtimeAccessoryFishByModeLogged = true;
                if (_runtimeAccessoryFishByMode != null)
                {
                    int routeLight = _runtimeAccessoryFishByMode.TryGetValue(FishingModeContext.Route, out Dictionary<int, HashSet<string>>? r) && r.TryGetValue(1, out HashSet<string>? rl) ? rl.Count : 0;
                    int routeHeavy = _runtimeAccessoryFishByMode.TryGetValue(FishingModeContext.Route, out Dictionary<int, HashSet<string>>? r2) && r2.TryGetValue(2, out HashSet<string>? rh) ? rh.Count : 0;
                    int iceLight = _runtimeAccessoryFishByMode.TryGetValue(FishingModeContext.Ice, out Dictionary<int, HashSet<string>>? i) && i.TryGetValue(1, out HashSet<string>? il) ? il.Count : 0;
                    int iceHeavy = _runtimeAccessoryFishByMode.TryGetValue(FishingModeContext.Ice, out Dictionary<int, HashSet<string>>? i2) && i2.TryGetValue(2, out HashSet<string>? ih) ? ih.Count : 0;
                    int pondLight = _runtimeAccessoryFishByMode.TryGetValue(FishingModeContext.Pond, out Dictionary<int, HashSet<string>>? p) && p.TryGetValue(1, out HashSet<string>? pl) ? pl.Count : 0;
                    int pondHeavy = _runtimeAccessoryFishByMode.TryGetValue(FishingModeContext.Pond, out Dictionary<int, HashSet<string>>? p2) && p2.TryGetValue(2, out HashSet<string>? ph) ? ph.Count : 0;
                    _log?.LogMessage($"[FishPeek] 运行时鱼竿白名单已加载：河边(轻/重)={routeLight}/{routeHeavy}，冰洞(轻/重)={iceLight}/{iceHeavy}，池塘(轻/重)={pondLight}/{pondHeavy}。");
                }
                else
                {
                    _log?.LogMessage("[FishPeek] 运行时鱼竿白名单读取失败；回退到插件内置白名单。");
                }
            }
        }

        return _runtimeAccessoryFishByMode ?? BuiltInAccessoryFishByMode;
    }

    private static void MergeAccessoryFishByMode(
        Dictionary<FishingModeContext, Dictionary<int, HashSet<string>>> target,
        Dictionary<FishingModeContext, Dictionary<int, HashSet<string>>> fallback)
    {
        foreach (KeyValuePair<FishingModeContext, Dictionary<int, HashSet<string>>> modeEntry in fallback)
        {
            if (!target.TryGetValue(modeEntry.Key, out Dictionary<int, HashSet<string>>? targetModeMap) || targetModeMap == null)
            {
                targetModeMap = new Dictionary<int, HashSet<string>>();
                target[modeEntry.Key] = targetModeMap;
            }

            foreach (KeyValuePair<int, HashSet<string>> accessoryEntry in modeEntry.Value)
            {
                if (!targetModeMap.TryGetValue(accessoryEntry.Key, out HashSet<string>? targetBucket) || targetBucket == null)
                {
                    targetBucket = new HashSet<string>(StringComparer.Ordinal);
                    targetModeMap[accessoryEntry.Key] = targetBucket;
                }

                foreach (string fishName in accessoryEntry.Value)
                {
                    if (!string.IsNullOrWhiteSpace(fishName))
                    {
                        targetBucket.Add(fishName.Trim());
                    }
                }
            }
        }
    }

    private static Dictionary<string, RuntimeFishBasicData>? GetRuntimeFishBasicMap()
    {
        if (!EnableRuntimeResourceAugment)
        {
            if (!_runtimeFishBasicLogged)
            {
                _runtimeFishBasicLogged = true;
                _log?.LogMessage("[FishPeek] 已禁用运行时鱼种上下文读取；仅使用插件内置鱼种数据。");
            }

            return null;
        }

        if (!_runtimeFishBasicLoaded)
        {
            _runtimeFishBasicLoaded = true;
            try
            {
                if (TryLoadRuntimeFishBasicByName(out Dictionary<string, RuntimeFishBasicData> loaded))
                {
                    _runtimeFishBasicByName = loaded;
                }
            }
            catch
            {
                _runtimeFishBasicByName = null;
            }

            if (!_runtimeFishBasicLogged)
            {
                _runtimeFishBasicLogged = true;
                if (_runtimeFishBasicByName != null)
                {
                    _log?.LogMessage($"[FishPeek] 运行时鱼种基础数据已加载：{_runtimeFishBasicByName.Count} 种。");
                }
                else
                {
                    _log?.LogMessage("[FishPeek] 运行时鱼种基础数据读取失败；回退到内置等级/上下文表。");
                }
            }
        }

        return _runtimeFishBasicByName;
    }

    private static bool TryGetRuntimeFishBasic(string fishName, out RuntimeFishBasicData basic)
    {
        basic = new RuntimeFishBasicData();
        Dictionary<string, RuntimeFishBasicData>? runtimeMap = GetRuntimeFishBasicMap();
        if (runtimeMap == null || string.IsNullOrWhiteSpace(fishName))
        {
            return false;
        }

        if (!runtimeMap.TryGetValue(fishName.Trim(), out RuntimeFishBasicData? runtimeBasic) || runtimeBasic == null)
        {
            return false;
        }

        basic = runtimeBasic;
        return true;
    }

    private static string BuildStablePlayerLabel(int playerLocalId)
    {
        return playerLocalId > 0 ? $"玩家{playerLocalId}" : "玩家";
    }


    private static void NoteStrictReject(string reason)
    {
        switch (reason)
        {
            case "name":
                _strictRejectNameCount++;
                break;
            case "location":
                _strictRejectLocationCount++;
                break;
            case "candidate":
                _strictRejectCandidateCount++;
                break;
        }

        float now = Time.realtimeSinceStartup;
        if (now < _nextStrictRejectLogRealtime)
        {
            return;
        }

        _nextStrictRejectLogRealtime = now + 5f;
        if (_strictRejectNameCount == 0 && _strictRejectLocationCount == 0 && _strictRejectCandidateCount == 0)
        {
            return;
        }

        _log?.LogMessage($"[FishPeek] 严格门控拒绝（最近5秒）：昵称={_strictRejectNameCount}，地点={_strictRejectLocationCount}，候选={_strictRejectCandidateCount}");
        _strictRejectNameCount = 0;
        _strictRejectLocationCount = 0;
        _strictRejectCandidateCount = 0;
    }

    private static void NoteCandidateRejectDetail(string reason)
    {
        switch (reason)
        {
            case "context":
                _candidateRejectNoContextCount++;
                break;
            case "mode":
                _candidateRejectModeCount++;
                break;
            case "accessory":
                _candidateRejectAccessoryCount++;
                break;
            case "allow":
                _candidateRejectAllowedSetCount++;
                break;
            case "filtered":
                _candidateRejectFilteredCount++;
                break;
        }

        float now = Time.realtimeSinceStartup;
        if (now < _nextCandidateRejectLogRealtime)
        {
            return;
        }

        _nextCandidateRejectLogRealtime = now + 5f;
        if (_candidateRejectNoContextCount == 0 &&
            _candidateRejectModeCount == 0 &&
            _candidateRejectAccessoryCount == 0 &&
            _candidateRejectAllowedSetCount == 0 &&
            _candidateRejectFilteredCount == 0)
        {
            return;
        }

        _log?.LogMessage(
            $"[FishPeek] 候选拒绝明细（最近5秒）：上下文={_candidateRejectNoContextCount}，模式={_candidateRejectModeCount}，竿型={_candidateRejectAccessoryCount}，白名单={_candidateRejectAllowedSetCount}，筛空={_candidateRejectFilteredCount}");

        _candidateRejectNoContextCount = 0;
        _candidateRejectModeCount = 0;
        _candidateRejectAccessoryCount = 0;
        _candidateRejectAllowedSetCount = 0;
        _candidateRejectFilteredCount = 0;
    }
    private static bool IsGenericPlayerPlaceholder(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        if (name == "玩家")
        {
            return true;
        }

        if (!name.StartsWith("玩家", StringComparison.Ordinal))
        {
            return false;
        }

        if (name.Length == 2)
        {
            return true;
        }

        for (int i = 2; i < name.Length; i++)
        {
            if (!char.IsDigit(name[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidLocationFlagForCache(int locationFlag)
    {
        return locationFlag is 1 or 2 or 4 or 8 or 16 or 32 or 64 or 128;
    }

    private static void CachePlayerLocationFlag(int playerLocalId, int locationFlag, IntPtr controllerPtr = default, string? playerDisplayName = null)
    {
        if (!IsValidLocationFlagForCache(locationFlag))
        {
            return;
        }

        lock (_predictionCacheLock)
        {
            if (playerLocalId > 0)
            {
                _playerLocationFlagCache[playerLocalId] = locationFlag;
            }

            if (controllerPtr != IntPtr.Zero)
            {
                _controllerLocationFlagCache[controllerPtr.ToInt64()] = locationFlag;
            }

            if (!string.IsNullOrWhiteSpace(playerDisplayName))
            {
                _playerNameLocationFlagCache[playerDisplayName.Trim()] = locationFlag;
            }

            if (_playerLocationFlagCache.Count > 512)
            {
                _playerLocationFlagCache.Clear();
            }

            if (_controllerLocationFlagCache.Count > 512)
            {
                _controllerLocationFlagCache.Clear();
            }

            if (_playerNameLocationFlagCache.Count > 512)
            {
                _playerNameLocationFlagCache.Clear();
            }
        }
    }

    private static bool TryGetCachedPlayerLocation(int playerLocalId, out int locationFlag, out string locationLabel)
    {
        locationFlag = 0;
        locationLabel = string.Empty;
        if (playerLocalId <= 0)
        {
            return false;
        }

        lock (_predictionCacheLock)
        {
            if (!_playerLocationFlagCache.TryGetValue(playerLocalId, out locationFlag) || locationFlag == 0)
            {
                locationFlag = 0;
                return false;
            }
        }

        locationLabel = GetLocationLabel(locationFlag);
        return !string.IsNullOrWhiteSpace(locationLabel);
    }

    private static bool TryGetCachedControllerLocation(IntPtr controllerPtr, out int locationFlag, out string locationLabel)
    {
        locationFlag = 0;
        locationLabel = string.Empty;
        if (controllerPtr == IntPtr.Zero)
        {
            return false;
        }

        long key = controllerPtr.ToInt64();
        lock (_predictionCacheLock)
        {
            if (!_controllerLocationFlagCache.TryGetValue(key, out locationFlag) || !IsValidLocationFlagForCache(locationFlag))
            {
                locationFlag = 0;
                return false;
            }
        }

        locationLabel = GetLocationLabel(locationFlag);
        return !string.IsNullOrWhiteSpace(locationLabel);
    }

    private static bool TryGetCachedPlayerNameLocation(string playerDisplayName, out int locationFlag, out string locationLabel)
    {
        locationFlag = 0;
        locationLabel = string.Empty;
        if (string.IsNullOrWhiteSpace(playerDisplayName))
        {
            return false;
        }

        string key = playerDisplayName.Trim();
        lock (_predictionCacheLock)
        {
            if (!_playerNameLocationFlagCache.TryGetValue(key, out locationFlag) || !IsValidLocationFlagForCache(locationFlag))
            {
                locationFlag = 0;
                return false;
            }
        }

        locationLabel = GetLocationLabel(locationFlag);
        return !string.IsNullOrWhiteSpace(locationLabel);
    }

    private static bool TryGetGenericLocationLabelByMode(IntPtr controllerPtr, out string locationLabel)
    {
        locationLabel = string.Empty;
        if (TryBuildFishingEnvironmentContext(controllerPtr, out FishingEnvironmentContext? context) && context != null)
        {
            switch (ResolveMode(context))
            {
                case FishingModeContext.Ice:
                    locationLabel = "冰洞";
                    return true;
                case FishingModeContext.Pond:
                    locationLabel = "池塘";
                    return true;
                case FishingModeContext.Route:
                    locationLabel = "河边";
                    return true;
            }
        }

        string sceneName = TryGetActiveSceneName();
        switch (InferModeFromSceneName(sceneName))
        {
            case FishingModeContext.Ice:
                locationLabel = "冰洞";
                return true;
            case FishingModeContext.Pond:
                locationLabel = "池塘";
                return true;
            case FishingModeContext.Route:
                locationLabel = "河边";
                return true;
            default:
                return false;
        }
    }

    private static void CachePlayerAccessoryType(int playerLocalId, int accessoryType)
    {
        if (playerLocalId <= 0 || (accessoryType != 1 && accessoryType != 2))
        {
            return;
        }

        lock (_predictionCacheLock)
        {
            _playerAccessoryTypeCache[playerLocalId] = accessoryType;
            if (_playerAccessoryTypeCache.Count > 512)
            {
                _playerAccessoryTypeCache.Clear();
            }
        }
    }

    private static bool TryGetCachedPlayerAccessoryType(int playerLocalId, out int accessoryType)
    {
        accessoryType = 0;
        if (playerLocalId <= 0)
        {
            return false;
        }

        lock (_predictionCacheLock)
        {
            if (!_playerAccessoryTypeCache.TryGetValue(playerLocalId, out accessoryType))
            {
                accessoryType = 0;
                return false;
            }
        }

        return accessoryType == 1 || accessoryType == 2;
    }

    private static void CachePlayerRodIdentity(int playerLocalId, ulong uniqueUid, string? rodAssetName, string? rodGuid, IntPtr controllerPtr = default)
    {
        string normalizedName = string.IsNullOrWhiteSpace(rodAssetName) ? string.Empty : rodAssetName.Trim();
        string normalizedGuid = string.IsNullOrWhiteSpace(rodGuid) ? string.Empty : rodGuid.Trim();
        if (playerLocalId <= 0 &&
            controllerPtr == IntPtr.Zero &&
            uniqueUid == 0 &&
            string.IsNullOrWhiteSpace(normalizedName) &&
            string.IsNullOrWhiteSpace(normalizedGuid))
        {
            return;
        }

        bool changed = false;
        string signature = uniqueUid.ToString() + "|" + normalizedName + "|" + normalizedGuid;

        lock (_predictionCacheLock)
        {
            if (playerLocalId > 0)
            {
                if (!_playerRodIdentitySignatureCache.TryGetValue(playerLocalId, out string? existingSignature) ||
                    !string.Equals(existingSignature, signature, StringComparison.Ordinal))
                {
                    _playerRodIdentitySignatureCache[playerLocalId] = signature;
                    changed = true;
                }

                if (uniqueUid != 0)
                {
                    _playerRodUniqueUidCache[playerLocalId] = uniqueUid;
                }

                if (!string.IsNullOrWhiteSpace(normalizedName))
                {
                    _playerRodAssetNameCache[playerLocalId] = normalizedName;
                }

                if (!string.IsNullOrWhiteSpace(normalizedGuid))
                {
                    _playerRodGuidCache[playerLocalId] = normalizedGuid;
                }
            }

            if (controllerPtr != IntPtr.Zero)
            {
                long controllerKey = controllerPtr.ToInt64();
                if (uniqueUid != 0)
                {
                    _controllerRodUniqueUidCache[controllerKey] = uniqueUid;
                }

                if (!string.IsNullOrWhiteSpace(normalizedName))
                {
                    _controllerRodAssetNameCache[controllerKey] = normalizedName;
                }

                if (!string.IsNullOrWhiteSpace(normalizedGuid))
                {
                    _controllerRodGuidCache[controllerKey] = normalizedGuid;
                }
            }

            if (_playerRodIdentitySignatureCache.Count > 512)
            {
                _playerRodIdentitySignatureCache.Clear();
            }

            if (_playerRodUniqueUidCache.Count > 512)
            {
                _playerRodUniqueUidCache.Clear();
            }

            if (_controllerRodUniqueUidCache.Count > 512)
            {
                _controllerRodUniqueUidCache.Clear();
            }

            if (_playerRodAssetNameCache.Count > 512)
            {
                _playerRodAssetNameCache.Clear();
            }

            if (_controllerRodAssetNameCache.Count > 512)
            {
                _controllerRodAssetNameCache.Clear();
            }

            if (_playerRodGuidCache.Count > 512)
            {
                _playerRodGuidCache.Clear();
            }

            if (_controllerRodGuidCache.Count > 512)
            {
                _controllerRodGuidCache.Clear();
            }
        }

        if (changed && playerLocalId > 0)
        {
            _log?.LogMessage(
                $"[FishPeek] 鱼竿身份已缓存：玩家={playerLocalId}，唯一ID={uniqueUid}。");
        }
    }

    private static bool TryGetCachedPlayerRodIdentity(int playerLocalId, out ulong uniqueUid, out string rodAssetName, out string rodGuid)
    {
        uniqueUid = 0;
        rodAssetName = string.Empty;
        rodGuid = string.Empty;
        if (playerLocalId <= 0)
        {
            return false;
        }

        lock (_predictionCacheLock)
        {
            _playerRodUniqueUidCache.TryGetValue(playerLocalId, out uniqueUid);
            _playerRodAssetNameCache.TryGetValue(playerLocalId, out rodAssetName);
            _playerRodGuidCache.TryGetValue(playerLocalId, out rodGuid);
        }

        return uniqueUid != 0 || !string.IsNullOrWhiteSpace(rodAssetName) || !string.IsNullOrWhiteSpace(rodGuid);
    }

    private static bool TryResolveRodIdentityForPlayer(IntPtr controllerPtr, int playerLocalId, out ulong uniqueUid, out string rodAssetName, out string rodGuid)
    {
        uniqueUid = 0;
        rodAssetName = string.Empty;
        rodGuid = string.Empty;

        IntPtr playerControllerPtr = ResolveControllerPtrForPlayer(controllerPtr, playerLocalId);
        if (playerControllerPtr != IntPtr.Zero &&
            TryResolveCurrentRodIdentity(playerControllerPtr, out uniqueUid, out rodAssetName, out rodGuid))
        {
            CachePlayerRodIdentity(playerLocalId, uniqueUid, rodAssetName, rodGuid, playerControllerPtr);
            return true;
        }

        if (TryGetCachedPlayerRodIdentity(playerLocalId, out uniqueUid, out rodAssetName, out rodGuid))
        {
            return true;
        }

        if (controllerPtr != IntPtr.Zero &&
            controllerPtr != playerControllerPtr &&
            TryResolveCurrentRodIdentity(controllerPtr, out uniqueUid, out rodAssetName, out rodGuid))
        {
            CachePlayerRodIdentity(playerLocalId, uniqueUid, rodAssetName, rodGuid, controllerPtr);
            return true;
        }

        return TryGetCachedPlayerRodIdentity(playerLocalId, out uniqueUid, out rodAssetName, out rodGuid);
    }

    private static bool TryResolveExactRodProfileForPlayer(IntPtr controllerPtr, int playerLocalId, out ExactRodProfile? rodProfile)
    {
        rodProfile = null;
        if (!IsSelfPlayerGlobalId(playerLocalId))
        {
            return false;
        }

        if (!TryResolveRodIdentityForPlayer(controllerPtr, playerLocalId, out ulong uniqueUid, out string rodAssetName, out string rodGuid))
        {
            return false;
        }

        return TryGetBuiltInExactRodProfile(uniqueUid, rodGuid, rodAssetName, out rodProfile) &&
               rodProfile != null;
    }

    private static IntPtr ResolveControllerPtrForPlayer(IntPtr controllerPtr, int playerLocalId)
    {
        if (playerLocalId <= 0)
        {
            return controllerPtr;
        }

        if (controllerPtr != IntPtr.Zero &&
            IsLikelyLiveControllerObject(controllerPtr) &&
            TryGetControllerGlobalId(controllerPtr, out int currentGlobalId) &&
            currentGlobalId == playerLocalId)
        {
            return controllerPtr;
        }

        List<IntPtr>? candidates = null;
        lock (_nameCacheLock)
        {
            foreach (KeyValuePair<long, int> entry in _controllerPtrToGlobalId)
            {
                if (entry.Value != playerLocalId)
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

        if (candidates != null)
        {
            foreach (IntPtr candidatePtr in candidates)
            {
                if (!IsLikelyLiveControllerObject(candidatePtr))
                {
                    continue;
                }

                if (TryGetControllerGlobalId(candidatePtr, out int liveGlobalId) &&
                    liveGlobalId == playerLocalId)
                {
                    return candidatePtr;
                }
            }
        }

        return IntPtr.Zero;
    }

    private static bool TryResolveAccessoryTypeForPlayer(IntPtr controllerPtr, int playerLocalId, out int accessoryType)
    {
        accessoryType = 0;

        IntPtr playerControllerPtr = ResolveControllerPtrForPlayer(controllerPtr, playerLocalId);
        if (playerControllerPtr != IntPtr.Zero &&
            TryResolveCurrentRodAccessoryType(playerControllerPtr, out accessoryType))
        {
            accessoryType = NormalizeAccessoryType(accessoryType);
            if (accessoryType != 0)
            {
                CachePlayerAccessoryType(playerLocalId, accessoryType);
                return true;
            }
        }

        if (TryGetCachedPlayerAccessoryType(playerLocalId, out int cachedAccessory))
        {
            accessoryType = NormalizeAccessoryType(cachedAccessory);
            if (accessoryType != 0)
            {
                return true;
            }
        }

        if (controllerPtr != IntPtr.Zero &&
            controllerPtr != playerControllerPtr &&
            TryResolveCurrentRodAccessoryType(controllerPtr, out accessoryType))
        {
            accessoryType = NormalizeAccessoryType(accessoryType);
            if (accessoryType != 0)
            {
                CachePlayerAccessoryType(playerLocalId, accessoryType);
                return true;
            }
        }

        accessoryType = 0;
        return false;
    }

    private static bool TryGetStrictLocationLabel(IntPtr controllerPtr, int playerLocalId, out string locationLabel, out int locationFlag)
    {
        locationLabel = string.Empty;
        locationFlag = 0;
        IntPtr playerControllerPtr = ResolveControllerPtrForPlayer(controllerPtr, playerLocalId);
        if (playerControllerPtr == IntPtr.Zero)
        {
            playerControllerPtr = controllerPtr;
        }

        if (playerControllerPtr == IntPtr.Zero)
        {
            return false;
        }

        if (!TryBuildFishingEnvironmentContext(playerControllerPtr, out FishingEnvironmentContext? context) || context == null)
        {
            return false;
        }

        if (context.LocationFlag == 0 || string.IsNullOrWhiteSpace(context.LocationLabel))
        {
            return false;
        }

        locationFlag = context.LocationFlag;
        locationLabel = context.LocationLabel;
        return true;
    }

    private static bool TryBuildPredictionMessage(IntPtr controllerPtr, string playerDisplayName, int playerLocalId, float levelRaw, out string message, out int levelBucket, out bool suppressOutline)
    {
        message = string.Empty;
        levelBucket = NormalizeLevelBucket(levelRaw);
        suppressOutline = levelBucket >= 4;
        IntPtr playerControllerPtr = ResolveControllerPtrForPlayer(controllerPtr, playerLocalId);
        if (IsSelfPlayerGlobalId(playerLocalId))
        {
            _ = TryResolveRodIdentityForPlayer(playerControllerPtr, playerLocalId, out _, out _, out _);
        }

        if (IsGenericPlayerPlaceholder(playerDisplayName))
        {
            string? resolvedName = TryResolvePlayerNameFromKnownControllers(playerLocalId);
            if (!string.IsNullOrWhiteSpace(resolvedName) && !IsGenericPlayerPlaceholder(resolvedName))
            {
                playerDisplayName = resolvedName;
            }
            else
            {
                NoteStrictReject("name");
                // "显示所有"时不能因为昵称尚未解析就丢掉其他玩家的遭遇。
                // globalId 是当前 FishingGameController 的稳定临时身份，可作为无延迟回退标签。
                if (_biteInfoDisplayMode != BiteInfoDisplayMode.All)
                {
                    return false;
                }

                playerDisplayName = BuildStablePlayerLabel(playerLocalId);
            }
        }

        string locationLabel;
        int locationFlag = 0;
        if (TryGetStrictLocationLabel(playerControllerPtr, playerLocalId, out locationLabel, out locationFlag))
        {
            CachePlayerLocationFlag(playerLocalId, locationFlag, playerControllerPtr, playerDisplayName);
        }
        else if (TryGetCachedControllerLocation(playerControllerPtr, out int cachedControllerLocationFlag, out string cachedControllerLocationLabel))
        {
            locationFlag = cachedControllerLocationFlag;
            locationLabel = cachedControllerLocationLabel;
        }
        else if (TryGetCachedPlayerLocation(playerLocalId, out int cachedLocationFlag, out string cachedLocationLabel))
        {
            locationFlag = cachedLocationFlag;
            locationLabel = cachedLocationLabel;
        }
        else if (TryGetCachedPlayerNameLocation(playerDisplayName, out int cachedNameLocationFlag, out string cachedNameLocationLabel))
        {
            locationFlag = cachedNameLocationFlag;
            locationLabel = cachedNameLocationLabel;
        }
        else if (TryGetGenericLocationLabelByMode(playerControllerPtr, out string genericLocationLabel))
        {
            NoteStrictReject("location");
            locationFlag = 0;
            locationLabel = genericLocationLabel;
        }
        else
        {
            NoteStrictReject("location");
            locationLabel = "河边";
        }

        string levelText = GetLevelDisplayText(levelBucket);
        string emphasis = new string('!', Mathf.Max(0, levelBucket - 3));

        if (levelBucket <= 1)
        {
            message = $"{playerDisplayName}在{locationLabel}咬钩{levelText}级鱼{emphasis}";
            return true;
        }

        if (!TryGetCandidateDisplay(playerControllerPtr, playerLocalId, levelBucket, locationFlag, out string candidateDisplay) || string.IsNullOrWhiteSpace(candidateDisplay))
        {
            NoteStrictReject("candidate");
            message = $"{playerDisplayName}在{locationLabel}咬钩{levelText}级鱼{emphasis}";
            return true;
        }

        message = $"{playerDisplayName}在{locationLabel}咬钩{levelText}级鱼，可能是{candidateDisplay}{emphasis}";
        return true;
    }

    private static bool TryGetCandidateDisplay(IntPtr controllerPtr, int playerLocalId, int levelBucket, int preferredLocationFlag, out string candidateDisplay)
    {
        candidateDisplay = string.Empty;

        if (levelBucket <= 1)
        {
            return false;
        }

        FishingEnvironmentContext? context = BuildDefaultFishingEnvironmentContext();
        if (context == null)
        {
            NoteCandidateRejectDetail("context");
            return false;
        }

        IntPtr playerControllerPtr = ResolveControllerPtrForPlayer(controllerPtr, playerLocalId);
        if (playerControllerPtr == IntPtr.Zero)
        {
            playerControllerPtr = controllerPtr;
        }
        FishingEnvironmentContext? strictContext = null;
        bool hasStrictContext =
            playerControllerPtr != IntPtr.Zero &&
            TryBuildFishingEnvironmentContext(playerControllerPtr, out strictContext) &&
            strictContext != null;
        if (hasStrictContext)
        {
            context = strictContext!;
            if (context.LocationFlag != 0)
            {
                CachePlayerLocationFlag(playerLocalId, context.LocationFlag, playerControllerPtr);
            }
        }

        if (context.LocationFlag == 0 && preferredLocationFlag != 0)
        {
            context.LocationFlag = preferredLocationFlag;
            context.LocationLabel = GetLocationLabel(preferredLocationFlag);
            ApplyLocationMode(context);
        }
        else if (context.LocationFlag == 0 && TryGetCachedControllerLocation(playerControllerPtr, out int cachedControllerLocationFlag, out string cachedControllerLocationLabel))
        {
            context.LocationFlag = cachedControllerLocationFlag;
            context.LocationLabel = cachedControllerLocationLabel;
            ApplyLocationMode(context);
        }
        else if (context.LocationFlag == 0 && TryGetCachedPlayerLocation(playerLocalId, out int cachedLocationFlag, out string cachedLocationLabel))
        {
            context.LocationFlag = cachedLocationFlag;
            context.LocationLabel = cachedLocationLabel;
            ApplyLocationMode(context);
        }
        else if (context.LocationFlag == 0)
        {
            string? resolvedName = TryResolvePlayerNameFromKnownControllers(playerLocalId);
            if (!string.IsNullOrWhiteSpace(resolvedName) &&
                TryGetCachedPlayerNameLocation(resolvedName, out int cachedNameLocationFlag, out string cachedNameLocationLabel))
            {
                context.LocationFlag = cachedNameLocationFlag;
                context.LocationLabel = cachedNameLocationLabel;
                ApplyLocationMode(context);
            }
        }

        FishingModeContext candidateMode = ResolveMode(context);
        if (candidateMode == FishingModeContext.Unknown)
        {
            candidateMode = FishingModeContext.Route;
            context.Mode = candidateMode;
        }

        if (TryResolveExactRodProfileForPlayer(playerControllerPtr, playerLocalId, out ExactRodProfile? exactRodProfile) &&
            exactRodProfile != null)
        {
            ApplyExactRodProfileContextFallback(context, exactRodProfile);
            string[] exactRodCandidates = CollectSortedCandidates(exactRodProfile, levelBucket, context);
            if (exactRodCandidates.Length == 0 && context.LocationFlag != 0)
            {
                FishingEnvironmentContext relaxedContext = CloneFishingEnvironmentContext(context);
                relaxedContext.LocationFlag = 0;
                relaxedContext.LocationLabel = string.Empty;
                exactRodCandidates = CollectSortedCandidates(exactRodProfile, levelBucket, relaxedContext);
            }

            if (exactRodCandidates.Length > 0)
            {
                candidateDisplay = FormatAlternativeChineseList(exactRodCandidates);
                return !string.IsNullOrWhiteSpace(candidateDisplay);
            }
        }

        Dictionary<FishingModeContext, Dictionary<int, HashSet<string>>> accessoryFishMap = GetAccessoryFishByModeMap();
        if (candidateMode == FishingModeContext.Unknown ||
            !accessoryFishMap.TryGetValue(candidateMode, out Dictionary<int, HashSet<string>>? modeMap) ||
            modeMap == null ||
            modeMap.Count == 0)
        {
            NoteCandidateRejectDetail("mode");
            return false;
        }

        if (!TryResolveAccessoryTypeForPlayer(playerControllerPtr, playerLocalId, out int accessoryType))
        {
            NoteCandidateRejectDetail("accessory");
            return false;
        }

        accessoryType = NormalizeAccessoryType(accessoryType);
        if (accessoryType == 0)
        {
            NoteCandidateRejectDetail("accessory");
            return false;
        }

        CachePlayerAccessoryType(playerLocalId, accessoryType);

        string[] sortedCandidateNames = CollectSortedCandidates(modeMap, accessoryType, levelBucket, context);
        if (sortedCandidateNames.Length == 0)
        {
            int oppositeAccessory = accessoryType == 1 ? 2 : 1;
            string[] oppositeCandidates = CollectSortedCandidates(modeMap, oppositeAccessory, levelBucket, context);
            if (oppositeCandidates.Length > 0)
            {
                accessoryType = oppositeAccessory;
                CachePlayerAccessoryType(playerLocalId, accessoryType);
                sortedCandidateNames = oppositeCandidates;
            }
        }

        if (sortedCandidateNames.Length == 0)
        {
            if (modeMap.TryGetValue(accessoryType, out HashSet<string>? allowSet) && allowSet != null && allowSet.Count > 0)
            {
                NoteCandidateRejectDetail("filtered");
            }
            else
            {
                NoteCandidateRejectDetail("allow");
            }
            return false;
        }

        candidateDisplay = FormatAlternativeChineseList(sortedCandidateNames);
        return !string.IsNullOrWhiteSpace(candidateDisplay);
    }

    private static void ApplyExactRodProfileContextFallback(FishingEnvironmentContext context, ExactRodProfile rodProfile)
    {
        if (context == null || rodProfile == null)
        {
            return;
        }

        if (context.Mode == FishingModeContext.Unknown)
        {
            context.Mode = InferModeFromRodConfig(rodProfile.CompatibleLocation, rodProfile.FishingType);
        }

        if (context.SeasonFlag == 0)
        {
            if (rodProfile.CompatibleSeason is 1 or 2 or 4 or 8)
            {
                context.SeasonFlag = rodProfile.CompatibleSeason;
                return;
            }

            switch (context.Mode)
            {
                case FishingModeContext.Ice:
                    context.SeasonFlag = 8;
                    break;
            }
        }
    }

    private static FishingEnvironmentContext CloneFishingEnvironmentContext(FishingEnvironmentContext context)
    {
        return new FishingEnvironmentContext
        {
            Mode = context.Mode,
            LocationFlag = context.LocationFlag,
            SeasonFlag = context.SeasonFlag,
            TimeFlag = context.TimeFlag,
            WeatherFlag = context.WeatherFlag,
            LocationLabel = context.LocationLabel,
            SceneName = context.SceneName
        };
    }

    private static string[] CollectSortedCandidates(Dictionary<int, HashSet<string>> modeMap, int accessoryType, int levelBucket, FishingEnvironmentContext context)
    {
        if (!modeMap.TryGetValue(accessoryType, out HashSet<string>? allowedFish) ||
            allowedFish == null ||
            allowedFish.Count == 0)
        {
            return Array.Empty<string>();
        }

        var candidates = new HashSet<string>(StringComparer.Ordinal);
        foreach (string fishName in allowedFish)
        {
            if (!TryGetFishLevelByName(fishName, out int fishLevelBucket) || fishLevelBucket != levelBucket)
            {
                continue;
            }

            bool locationOk;
            if (GetFishSpeciesContextMap().TryGetValue(fishName, out FishSpeciesContextData? fish) && fish != null)
            {
                locationOk = DoesFishMatchFishingSpotContext(fish, context);
            }
            else
            {
                locationOk = DoesRuntimeFishMatchFishingSpotContext(fishName, context);
            }

            if (locationOk)
            {
                candidates.Add(fishName);
            }
        }

        if (candidates.Count == 0)
        {
            return Array.Empty<string>();
        }

        return SortCandidateNamesByContext(new List<string>(candidates).ToArray(), context);
    }

    private static string[] CollectSortedCandidates(ExactRodProfile rodProfile, int levelBucket, FishingEnvironmentContext context)
    {
        if (rodProfile == null || rodProfile.FishWhitelist == null || rodProfile.FishWhitelist.Length == 0)
        {
            return Array.Empty<string>();
        }

        var candidates = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (ExactRodFishEntry entry in rodProfile.FishWhitelist)
        {
            string fishName = entry?.FishName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(fishName))
            {
                continue;
            }

            if (!TryGetFishLevelByName(fishName, out int fishLevelBucket) || fishLevelBucket != levelBucket)
            {
                continue;
            }

            bool locationOk;
            if (GetFishSpeciesContextMap().TryGetValue(fishName, out FishSpeciesContextData? fish) && fish != null)
            {
                locationOk = DoesFishMatchFishingSpotContext(fish, context);
            }
            else
            {
                locationOk = DoesRuntimeFishMatchFishingSpotContext(fishName, context);
            }

            if (!locationOk)
            {
                continue;
            }

            float rodWeight = entry.WeightOffset <= 0f ? 0.01f : entry.WeightOffset;
            if (candidates.TryGetValue(fishName, out float existingWeight))
            {
                if (rodWeight > existingWeight)
                {
                    candidates[fishName] = rodWeight;
                }
            }
            else
            {
                candidates[fishName] = rodWeight;
            }
        }

        if (candidates.Count == 0)
        {
            return Array.Empty<string>();
        }

        return SortCandidateNamesByContext(candidates, context);
    }

    private static FishingEnvironmentContext BuildDefaultFishingEnvironmentContext()
    {
        string sceneName = TryGetActiveSceneName();
        return new FishingEnvironmentContext
        {
            SceneName = sceneName,
            Mode = InferModeFromSceneName(sceneName),
            SeasonFlag = InferSeasonFlagFromSceneName(sceneName)
        };
    }

    private static FishingModeContext ResolveMode(FishingEnvironmentContext context)
    {
        if (context.Mode != FishingModeContext.Unknown)
        {
            return context.Mode;
        }

        return context.LocationFlag switch
        {
            64 => FishingModeContext.Ice,
            128 => FishingModeContext.Pond,
            1 or 2 or 4 or 8 or 16 or 32 => FishingModeContext.Route,
            _ => FishingModeContext.Unknown
        };
    }

    private static bool TryBuildFishingEnvironmentContext(IntPtr controllerPtr, out FishingEnvironmentContext? context)
    {
        context = BuildDefaultFishingEnvironmentContext();
        TryResolveLocationMethods();

        if (TryResolveLocationFlag(controllerPtr, out int locationRaw))
        {
            int normalizedLocation = NormalizeFlagValue(locationRaw, new[] { 1, 2, 4, 8, 16, 32, 64, 128 });
            if (normalizedLocation != 0)
            {
                context.LocationFlag = normalizedLocation;
                context.LocationLabel = GetLocationLabel(normalizedLocation);
                FishingModeContext locationMode = InferModeFromLocationFlag(normalizedLocation);
                if (locationMode != FishingModeContext.Unknown)
                {
                    context.Mode = locationMode;
                }
            }
        }

        if (TryResolveCurrentWeatherFlag(out int weatherFlag))
        {
            context.WeatherFlag = weatherFlag;
        }

        if (context.LocationFlag == 64)
        {
            context.SeasonFlag = 8;
        }
        else if (context.LocationFlag == 128 && context.SeasonFlag == 0)
        {
            context.SeasonFlag = DefaultCabinPondSeasonFlag;
        }
        bool hasContext =
            context.Mode != FishingModeContext.Unknown ||
            context.LocationFlag != 0 ||
            context.SeasonFlag != 0;
        if (!hasContext)
        {
            context = null;
            return false;
        }

        return true;
    }

    private static bool TryResolveCurrentWeatherFlag(out int weatherFlag)
    {
        weatherFlag = 0;
        if (!_weatherResolverScanned)
        {
            _weatherResolverScanned = true;
            try
            {
                _weatherControllerClass = FindClassByNameAnyImage("WorldTimeController", null);
                if (_weatherControllerClass != IntPtr.Zero)
                {
                    _miGetCurrentWeather = FindZeroArgMethod(
                        _weatherControllerClass,
                        new[] { "GetCurrentWeather", "get_currentWeather", "get_CurrentWeather" },
                        "WeatherType");
                }
            }
            catch
            {
                _weatherControllerClass = IntPtr.Zero;
                _miGetCurrentWeather = IntPtr.Zero;
            }
        }

        if (_miGetCurrentWeather == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            IntPtr boxedWeather = InvokeStaticZeroArgMethod(_miGetCurrentWeather);
            if (boxedWeather == IntPtr.Zero)
            {
                return false;
            }

            IntPtr unboxedWeather = IL2CPP.il2cpp_object_unbox(boxedWeather);
            if (unboxedWeather == IntPtr.Zero || !IsReadable(unboxedWeather, sizeof(int)))
            {
                return false;
            }

            int value = Marshal.ReadInt32(unboxedWeather);
            if (value <= 0 || value > 1024)
            {
                return false;
            }

            weatherFlag = value;
            if (!_weatherResolverLogged || _lastWeatherFlag != value)
            {
                _weatherResolverLogged = true;
                _lastWeatherFlag = value;
                _log?.LogMessage($"[FishPeek] 当前天气已解析：{DescribeWeatherFlag(value)} ({value})。");
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string DescribeWeatherFlag(int weatherFlag)
    {
        return weatherFlag switch
        {
            1 => "晴朗",
            2 => "雾",
            4 => "小雪",
            8 => "大雪",
            16 => "小雨",
            32 => "大雨",
            64 => "流星雨",
            _ => "天气位"
        };
    }

    private static void TryResolveLocationMethods()
    {
        if (_locationResolverScanned || _targetControllerClass == IntPtr.Zero)
        {
            return;
        }

        _locationResolverScanned = true;

        try
        {
            if (_miGetCurFishingPlayerSeat != IntPtr.Zero && _seatClass == IntPtr.Zero)
            {
                _seatClass = GetMethodReturnClass(_miGetCurFishingPlayerSeat);
            }

            if (_seatClass != IntPtr.Zero)
            {
                _miSeatGetSpotLocation = FindZeroArgMethod(
                    _seatClass,
                    new[] { "get_spotLocation", "get_SpotLocation", "get_locationFlag", "get_LocationFlag", "get_location", "get_Location" },
                    "System.Int32");

                if (_offsetSeatSpotLocation < 0)
                {
                    IntPtr seatLocationField = FindField(
                        _seatClass,
                        new[] { "spotLocation", "m_spotLocation", "locationFlag", "m_locationFlag", "location", "m_location", "LocationFlag" },
                        "System.Int32");
                    if (seatLocationField != IntPtr.Zero)
                    {
                        _offsetSeatSpotLocation = (int)IL2CPP.il2cpp_field_get_offset(seatLocationField);
                    }
                }

                _miSeatGetSpot = FindZeroArgMethod(
                    _seatClass,
                    new[] { "get_spot", "get_Spot", "get_currentSpot", "get_CurrentSpot", "get_fishingSpot", "get_FishingSpot" },
                    null);

                if (_offsetSeatSpot < 0)
                {
                    IntPtr seatSpotField = FindField(
                        _seatClass,
                        new[] { "spot", "m_spot", "currentSpot", "m_currentSpot", "fishingSpot", "m_fishingSpot" },
                        null);
                    if (seatSpotField != IntPtr.Zero)
                    {
                        _offsetSeatSpot = (int)IL2CPP.il2cpp_field_get_offset(seatSpotField);
                    }
                }
            }

            _spotClass = GetMethodReturnClass(_miSeatGetSpot);
            if (_spotClass != IntPtr.Zero)
            {
                _miSpotGetLocation = FindZeroArgMethod(
                    _spotClass,
                    new[] { "get_spotLocation", "get_SpotLocation", "get_locationFlag", "get_LocationFlag", "get_location", "get_Location" },
                    "System.Int32");

                if (_offsetSpotLocation < 0)
                {
                    IntPtr spotLocationField = FindField(
                        _spotClass,
                        new[] { "spotLocation", "m_spotLocation", "locationFlag", "m_locationFlag", "location", "m_location", "LocationFlag" },
                        "System.Int32");
                    if (spotLocationField != IntPtr.Zero)
                    {
                        _offsetSpotLocation = (int)IL2CPP.il2cpp_field_get_offset(spotLocationField);
                    }
                }
            }

            _miControllerGetCurDetailedFishingSpot = FindZeroArgMethod(
                _targetControllerClass,
                new[] { "get_curDetailedFishingSpot", "get_CurDetailedFishingSpot" },
                "DetailedFishingSpot");

            if (_offsetControllerSeat < 0)
            {
                IntPtr controllerSeatField = FindField(
                    _targetControllerClass,
                    new[] { "curFishingPlayerSeat", "m_curFishingPlayerSeat", "currentFishingPlayerSeat", "m_currentFishingPlayerSeat" },
                    null);
                if (controllerSeatField != IntPtr.Zero)
                {
                    _offsetControllerSeat = (int)IL2CPP.il2cpp_field_get_offset(controllerSeatField);
                }
            }
            if (_miControllerGetCurDetailedFishingSpot != IntPtr.Zero)
            {
                _detailedFishingSpotClass = GetMethodReturnClass(_miControllerGetCurDetailedFishingSpot);
            }

            if (_detailedFishingSpotClass != IntPtr.Zero)
            {
                _detailedFishingSpotIsValueType = IL2CPP.il2cpp_class_is_valuetype(_detailedFishingSpotClass);
                if (_offsetDetailedFishingSpotFishingSpot < 0)
                {
                    IntPtr fishingSpotField = FindField(
                        _detailedFishingSpotClass,
                        new[] { "fishingSpot", "m_fishingSpot" },
                        null);
                    if (fishingSpotField != IntPtr.Zero)
                    {
                        _offsetDetailedFishingSpotFishingSpot = (int)IL2CPP.il2cpp_field_get_offset(fishingSpotField);
                        if (_fishingSpotClass == IntPtr.Zero)
                        {
                            IntPtr fishingSpotFieldType = IL2CPP.il2cpp_field_get_type(fishingSpotField);
                            _fishingSpotClass = IL2CPP.il2cpp_type_get_class_or_element_class(fishingSpotFieldType);
                        }
                    }
                }
            }

            if (_fishingSpotClass == IntPtr.Zero)
            {
                _fishingSpotClass = FindClassByNameAnyImage("FishingSpot", "MiniGame.FishingGame");
            }

            if (_fishingSpotClass != IntPtr.Zero)
            {
                _miFishingSpotGetSpotLocation = FindZeroArgMethod(
                    _fishingSpotClass,
                    new[] { "get_spotLocation", "get_SpotLocation", "get_locationFlag", "get_LocationFlag", "get_location", "get_Location" },
                    "Location");

                if (_offsetFishingSpotSpotLocation < 0)
                {
                    IntPtr spotLocationField = FindField(
                        _fishingSpotClass,
                        new[] { "spotLocation", "m_spotLocation", "locationFlag", "m_locationFlag", "location", "m_location", "LocationFlag" },
                        null);
                    if (spotLocationField != IntPtr.Zero)
                    {
                        _offsetFishingSpotSpotLocation = (int)IL2CPP.il2cpp_field_get_offset(spotLocationField);
                    }
                }
            }
        }
        catch
        {
            // ignored
        }

        bool ready =
            _miSeatGetSpotLocation != IntPtr.Zero ||
            _offsetSeatSpotLocation >= 0 ||
            _miSeatGetSpot != IntPtr.Zero ||
            _offsetSeatSpot >= 0 ||
            _miSpotGetLocation != IntPtr.Zero ||
            _offsetSpotLocation >= 0 ||
            _miControllerGetCurDetailedFishingSpot != IntPtr.Zero ||
            _offsetDetailedFishingSpotFishingSpot >= 0 ||
            _miFishingSpotGetSpotLocation != IntPtr.Zero ||
            _offsetFishingSpotSpotLocation >= 0;

        if (ready && !_locationResolverLogged)
        {
            _locationResolverLogged = true;
            _log?.LogMessage("[FishPeek] 地点解析器已就绪。");
        }
        else if (!ready && !_locationResolverFailedLogged)
        {
            _locationResolverFailedLogged = true;
            _log?.LogMessage("[FishPeek] 地点解析器未就绪；地点文本会回退为“某处”。");
        }
    }

    private static bool TryResolveLocationFlag(IntPtr controllerPtr, out int locationFlag)
    {
        locationFlag = 0;
        if (!IsLikelyLiveControllerObject(controllerPtr))
        {
            return false;
        }

        try
        {
            if (_miControllerGetCurDetailedFishingSpot != IntPtr.Zero &&
                _offsetDetailedFishingSpotFishingSpot >= 0)
            {
                IntPtr detailedSpotObj = InvokeZeroArgMethod(_miControllerGetCurDetailedFishingSpot, controllerPtr);
                if (detailedSpotObj != IntPtr.Zero)
                {
                    IntPtr detailedSpotData = detailedSpotObj;
                    int spotFieldOffset = _offsetDetailedFishingSpotFishingSpot;
                    if (_detailedFishingSpotIsValueType)
                    {
                        IntPtr unboxed = IL2CPP.il2cpp_object_unbox(detailedSpotObj);
                        if (unboxed != IntPtr.Zero)
                        {
                            detailedSpotData = unboxed;
                            spotFieldOffset = NormalizeOffset(_offsetDetailedFishingSpotFishingSpot, true);
                        }
                    }

                    if (TryReadObjectField(detailedSpotData, spotFieldOffset, out IntPtr fishingSpotPtr) &&
                        fishingSpotPtr != IntPtr.Zero)
                    {
                        if (_miFishingSpotGetSpotLocation != IntPtr.Zero &&
                            TryInvokeZeroArgIntMethodFlexible(_miFishingSpotGetSpotLocation, fishingSpotPtr, out locationFlag))
                        {
                            return true;
                        }

                        if (_offsetFishingSpotSpotLocation >= 0 &&
                            TryReadInt32Field(fishingSpotPtr, _offsetFishingSpotSpotLocation, out locationFlag))
                        {
                            return true;
                        }
                    }
                }
            }

            IntPtr seatPtr = IntPtr.Zero;
            if (_miGetCurFishingPlayerSeat != IntPtr.Zero)
            {
                seatPtr = InvokeZeroArgMethod(_miGetCurFishingPlayerSeat, controllerPtr);
            }

            if (seatPtr == IntPtr.Zero && _offsetControllerSeat >= 0)
            {
                TryReadObjectField(controllerPtr, _offsetControllerSeat, out seatPtr);
            }

            if (seatPtr != IntPtr.Zero)
            {
                if (_offsetSeatSpotLocation >= 0 &&
                    TryReadInt32Field(seatPtr, _offsetSeatSpotLocation, out locationFlag))
                {
                    return true;
                }

                if (_miSeatGetSpotLocation != IntPtr.Zero &&
                    TryInvokeZeroArgIntMethodFlexible(_miSeatGetSpotLocation, seatPtr, out locationFlag))
                {
                    return true;
                }

                IntPtr spotPtr = IntPtr.Zero;
                if (_offsetSeatSpot >= 0)
                {
                    TryReadObjectField(seatPtr, _offsetSeatSpot, out spotPtr);
                }

                if (spotPtr == IntPtr.Zero && _miSeatGetSpot != IntPtr.Zero)
                {
                    spotPtr = InvokeZeroArgMethod(_miSeatGetSpot, seatPtr);
                }

                if (spotPtr != IntPtr.Zero)
                {
                    if (_offsetSpotLocation >= 0 && TryReadInt32Field(spotPtr, _offsetSpotLocation, out locationFlag))
                    {
                        return true;
                    }

                    if (_miSpotGetLocation != IntPtr.Zero &&
                        TryInvokeZeroArgIntMethodFlexible(_miSpotGetLocation, spotPtr, out locationFlag))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        catch
        {
            locationFlag = 0;
            return false;
        }
    }

    private static void TryResolveRodMethods()
    {
        if (_rodResolverScanned || _targetControllerClass == IntPtr.Zero || _miGetCurFishingPlayerSeat == IntPtr.Zero)
        {
            return;
        }

        _rodResolverScanned = true;

        try
        {
            if (_seatClass == IntPtr.Zero)
            {
                _seatClass = GetMethodReturnClass(_miGetCurFishingPlayerSeat);
            }

            if (_seatClass != IntPtr.Zero)
            {
                _miSeatGetAccessoryType = FindZeroArgMethod(
                    _seatClass,
                    new[] { "get_accessoryType", "get_AccessoryType", "get_currentAccessoryType", "get_CurrentAccessoryType" },
                    null);

                if (_offsetSeatAccessoryType < 0)
                {
                    IntPtr seatAccessoryField = FindField(
                        _seatClass,
                        new[] { "accessoryType", "m_accessoryType", "currentAccessoryType", "m_currentAccessoryType" },
                        null);
                    if (seatAccessoryField != IntPtr.Zero)
                    {
                        _offsetSeatAccessoryType = (int)IL2CPP.il2cpp_field_get_offset(seatAccessoryField);
                    }
                }

                _miSeatGetRod = FindZeroArgMethod(
                    _seatClass,
                    new[]
                    {
                        "get_rod", "get_Rod", "get_currentRod", "get_CurrentRod", "get_fishingRod", "get_FishingRod",
                        "get_currentFishingRod", "get_CurrentFishingRod", "get_gear", "get_Gear", "get_currentGear",
                        "get_CurrentGear", "get_fishingGear", "get_FishingGear", "get_currentFishingGear", "get_CurrentFishingGear"
                    },
                    null);

                if (_offsetSeatRod < 0)
                {
                    IntPtr seatRodField = FindField(
                        _seatClass,
                        new[] { "rod", "m_rod", "currentRod", "m_currentRod", "fishingRod", "m_fishingRod", "gear", "m_gear", "currentGear", "m_currentGear", "fishingGear", "m_fishingGear" },
                        null);
                    if (seatRodField != IntPtr.Zero)
                    {
                        _offsetSeatRod = (int)IL2CPP.il2cpp_field_get_offset(seatRodField);
                    }
                }
            }

            _miControllerGetAccessoryType = FindZeroArgMethod(
                _targetControllerClass,
                new[] { "get_accessoryType", "get_AccessoryType", "get_currentAccessoryType", "get_CurrentAccessoryType" },
                null);

            _miControllerGetCurrentRod = FindZeroArgMethod(
                _targetControllerClass,
                new[]
                {
                    "get_currentRod", "get_CurrentRod", "get_rod", "get_Rod", "get_fishingRod", "get_FishingRod",
                    "get_currentFishingRod", "get_CurrentFishingRod", "get_gear", "get_Gear", "get_currentGear",
                    "get_CurrentGear", "get_fishingGear", "get_FishingGear", "get_currentFishingGear", "get_CurrentFishingGear"
                },
                null);

            if (_offsetControllerCurrentRod < 0)
            {
                IntPtr controllerRodField = FindField(
                    _targetControllerClass,
                    new[] { "currentRod", "m_CurrentRod", "m_currentRod", "rod", "m_Rod", "m_rod", "fishingRod", "m_fishingRod", "currentFishingRod", "m_currentFishingRod", "gear", "m_gear", "currentGear", "m_currentGear" },
                    null);
                if (controllerRodField != IntPtr.Zero)
                {
                    _offsetControllerCurrentRod = (int)IL2CPP.il2cpp_field_get_offset(controllerRodField);
                }
            }

            _rodRuntimeClass = GetMethodReturnClass(_miSeatGetRod);
            if (_rodRuntimeClass == IntPtr.Zero)
            {
                _rodRuntimeClass = GetMethodReturnClass(_miControllerGetCurrentRod);
            }
            if (_rodRuntimeClass != IntPtr.Zero)
            {
                _miRodGetAccessoryType = FindZeroArgMethod(
                    _rodRuntimeClass,
                    new[] { "get_accessoryType", "get_AccessoryType", "get_currentAccessoryType", "get_CurrentAccessoryType" },
                    null);

                if (_offsetRodAccessoryType < 0)
                {
                    IntPtr rodAccessoryField = FindField(
                        _rodRuntimeClass,
                        new[] { "accessoryType", "m_accessoryType", "currentAccessoryType", "m_currentAccessoryType" },
                        null);
                    if (rodAccessoryField != IntPtr.Zero)
                    {
                        _offsetRodAccessoryType = (int)IL2CPP.il2cpp_field_get_offset(rodAccessoryField);
                    }
                }

                _miRodGetUniqueId = FindZeroArgMethod(
                    _rodRuntimeClass,
                    new[] { "get_uniqueId", "get_UniqueId", "get_configId", "get_ConfigId" },
                    "UniqueId");

                if (_offsetRodUniqueId < 0)
                {
                    IntPtr rodUniqueIdField = FindField(
                        _rodRuntimeClass,
                        new[] { "m_UniqueId", "uniqueId", "UniqueId", "configId", "m_configId" },
                        "UniqueId");
                    if (rodUniqueIdField != IntPtr.Zero)
                    {
                        _offsetRodUniqueId = (int)IL2CPP.il2cpp_field_get_offset(rodUniqueIdField);
                        if (_rodUniqueIdClass == IntPtr.Zero)
                        {
                            _rodUniqueIdClass = GetFieldTypeClass(rodUniqueIdField);
                        }
                    }
                }

                if (_rodUniqueIdClass == IntPtr.Zero)
                {
                    _rodUniqueIdClass = GetMethodReturnClass(_miRodGetUniqueId);
                }

                _rodUniqueIdIsValueType = IsValueTypeClass(_rodUniqueIdClass);

                if (_rodUniqueIdClass != IntPtr.Zero)
                {
                    _miUniqueIdGetUid = FindZeroArgMethod(
                        _rodUniqueIdClass,
                        new[] { "get_uid", "get_Uid", "get_value", "get_Value" },
                        null);

                    if (_offsetUniqueIdUid < 0)
                    {
                        IntPtr uniqueIdUidField = FindField(
                            _rodUniqueIdClass,
                            new[] { "uid", "m_uid", "Uid", "m_Uid", "value", "m_value", "Value" },
                            null);
                        if (uniqueIdUidField != IntPtr.Zero)
                        {
                            _offsetUniqueIdUid = (int)IL2CPP.il2cpp_field_get_offset(uniqueIdUidField);
                        }
                    }
                }

                _miRodGetName = FindZeroArgMethod(
                    _rodRuntimeClass,
                    new[] { "get_name", "get_Name" },
                    "System.String");

                if (_offsetRodName < 0)
                {
                    IntPtr rodNameField = FindField(
                        _rodRuntimeClass,
                        new[] { "m_Name", "name", "Name" },
                        "System.String");
                    if (rodNameField != IntPtr.Zero)
                    {
                        _offsetRodName = (int)IL2CPP.il2cpp_field_get_offset(rodNameField);
                    }
                }

                _miRodGetLastKnownGuid = FindZeroArgMethod(
                    _rodRuntimeClass,
                    new[] { "get_lastKnownGuid", "get_LastKnownGuid", "get_guid", "get_Guid" },
                    "System.String");

                if (_offsetRodLastKnownGuid < 0)
                {
                    IntPtr rodGuidField = FindField(
                        _rodRuntimeClass,
                        new[] { "m_LastKnownGuid", "lastKnownGuid", "LastKnownGuid", "m_Guid", "guid", "Guid" },
                        "System.String");
                    if (rodGuidField != IntPtr.Zero)
                    {
                        _offsetRodLastKnownGuid = (int)IL2CPP.il2cpp_field_get_offset(rodGuidField);
                    }
                }
            }
        }
        catch
        {
            // ignored
        }

        bool ready =
            _miSeatGetAccessoryType != IntPtr.Zero ||
            _offsetSeatAccessoryType >= 0 ||
            _miSeatGetRod != IntPtr.Zero ||
            _offsetSeatRod >= 0 ||
            _miControllerGetCurrentRod != IntPtr.Zero ||
            _offsetControllerCurrentRod >= 0 ||
            _miRodGetAccessoryType != IntPtr.Zero ||
            _offsetRodAccessoryType >= 0 ||
            _miControllerGetAccessoryType != IntPtr.Zero;
        bool identityReady =
            _miRodGetUniqueId != IntPtr.Zero ||
            _offsetRodUniqueId >= 0 ||
            _miUniqueIdGetUid != IntPtr.Zero ||
            _offsetUniqueIdUid >= 0 ||
            _miRodGetName != IntPtr.Zero ||
            _offsetRodName >= 0 ||
            _miRodGetLastKnownGuid != IntPtr.Zero ||
            _offsetRodLastKnownGuid >= 0;

        if (ready && !_rodResolverLogged)
        {
            _rodResolverLogged = true;
            _log?.LogMessage("[FishPeek] 鱼竿解析器已就绪。");
        }
        else if (!ready && !_rodResolverFailedLogged)
        {
            _rodResolverFailedLogged = true;
            _log?.LogMessage("[FishPeek] 鱼竿解析器未就绪；候选鱼将回退为纯等级文本。");
        }

        if (identityReady && !_rodIdentityResolverLogged)
        {
            _rodIdentityResolverLogged = true;
            _log?.LogMessage("[FishPeek] 具体鱼竿身份解析器已就绪。");
        }
        else if (!identityReady && !_rodIdentityResolverFailedLogged)
        {
            _rodIdentityResolverFailedLogged = true;
            _log?.LogMessage("[FishPeek] 具体鱼竿身份解析器未就绪；精确鱼竿唯一ID将保持未解析。");
        }
    }

    private static bool TryResolveCurrentRodPointer(IntPtr controllerPtr, out IntPtr rodPtr)
    {
        rodPtr = IntPtr.Zero;
        if (!IsLikelyLiveControllerObject(controllerPtr))
        {
            return false;
        }

        TryResolveRodMethods();

        try
        {
            if (_offsetControllerCurrentRod >= 0)
            {
                TryReadObjectField(controllerPtr, _offsetControllerCurrentRod, out rodPtr);
            }

            if (rodPtr == IntPtr.Zero && _miControllerGetCurrentRod != IntPtr.Zero)
            {
                rodPtr = InvokeZeroArgMethod(_miControllerGetCurrentRod, controllerPtr);
            }

            if (rodPtr == IntPtr.Zero && _miGetCurFishingPlayerSeat != IntPtr.Zero)
            {
                IntPtr seatPtr = InvokeZeroArgMethod(_miGetCurFishingPlayerSeat, controllerPtr);
                if (seatPtr != IntPtr.Zero)
                {
                    if (_offsetSeatRod >= 0)
                    {
                        TryReadObjectField(seatPtr, _offsetSeatRod, out rodPtr);
                    }

                    if (rodPtr == IntPtr.Zero && _miSeatGetRod != IntPtr.Zero)
                    {
                        rodPtr = InvokeZeroArgMethod(_miSeatGetRod, seatPtr);
                    }
                }
            }
        }
        catch
        {
            rodPtr = IntPtr.Zero;
        }

        return rodPtr != IntPtr.Zero;
    }

    private static bool TryResolveCurrentRodIdentity(IntPtr controllerPtr, out ulong uniqueUid, out string rodAssetName, out string rodGuid)
    {
        uniqueUid = 0;
        rodAssetName = string.Empty;
        rodGuid = string.Empty;
        if (!TryResolveCurrentRodPointer(controllerPtr, out IntPtr rodPtr))
        {
            return false;
        }

        bool gotUid = TryReadRodUniqueUid(rodPtr, out uniqueUid) && uniqueUid != 0;
        bool gotName = TryReadRodStringField(rodPtr, _miRodGetName, _offsetRodName, out rodAssetName);
        bool gotGuid = TryReadRodStringField(rodPtr, _miRodGetLastKnownGuid, _offsetRodLastKnownGuid, out rodGuid);
        return gotUid || gotName || gotGuid;
    }

    private static bool TryReadRodUniqueUid(IntPtr rodPtr, out ulong uniqueUid)
    {
        uniqueUid = 0;
        if (rodPtr == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (_miRodGetUniqueId != IntPtr.Zero)
            {
                IntPtr uniqueIdObj = InvokeZeroArgMethod(_miRodGetUniqueId, rodPtr);
                if (TryReadUniqueIdUidFromObject(uniqueIdObj, out uniqueUid) && uniqueUid != 0)
                {
                    return true;
                }
            }

            if (_offsetRodUniqueId >= 0)
            {
                if (_rodUniqueIdIsValueType)
                {
                    IntPtr uniqueIdValuePtr = IntPtr.Add(rodPtr, _offsetRodUniqueId);
                    if (TryReadUniqueIdUidFromValuePtr(uniqueIdValuePtr, out uniqueUid) && uniqueUid != 0)
                    {
                        return true;
                    }
                }
                else if (TryReadObjectField(rodPtr, _offsetRodUniqueId, out IntPtr uniqueIdObj) &&
                         TryReadUniqueIdUidFromObject(uniqueIdObj, out uniqueUid) &&
                         uniqueUid != 0)
                {
                    return true;
                }
            }
        }
        catch
        {
            uniqueUid = 0;
        }

        return false;
    }

    private static bool TryReadUniqueIdUidFromObject(IntPtr uniqueIdObj, out ulong uniqueUid)
    {
        uniqueUid = 0;
        if (uniqueIdObj == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (_miUniqueIdGetUid != IntPtr.Zero &&
                TryInvokeZeroArgUInt64MethodFlexible(_miUniqueIdGetUid, uniqueIdObj, out uniqueUid) &&
                uniqueUid != 0)
            {
                return true;
            }

            IntPtr valuePtr = _rodUniqueIdIsValueType ? IL2CPP.il2cpp_object_unbox(uniqueIdObj) : uniqueIdObj;
            return TryReadUniqueIdUidFromValuePtr(valuePtr, out uniqueUid) && uniqueUid != 0;
        }
        catch
        {
            uniqueUid = 0;
            return false;
        }
    }

    private static bool TryReadUniqueIdUidFromValuePtr(IntPtr uniqueIdValuePtr, out ulong uniqueUid)
    {
        uniqueUid = 0;
        if (uniqueIdValuePtr == IntPtr.Zero || _offsetUniqueIdUid < 0)
        {
            return false;
        }

        int uidOffset = NormalizeOffset(_offsetUniqueIdUid, _rodUniqueIdIsValueType);
        return TryReadUInt64Field(uniqueIdValuePtr, uidOffset, out uniqueUid) && uniqueUid != 0;
    }

    private static bool TryReadRodStringField(IntPtr rodPtr, IntPtr getter, int fieldOffset, out string value)
    {
        value = string.Empty;
        if (rodPtr == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (getter != IntPtr.Zero && TryInvokeZeroArgStringMethod(getter, rodPtr, out value))
            {
                return true;
            }

            if (fieldOffset >= 0 && TryReadStringObjectField(rodPtr, fieldOffset, out value))
            {
                return true;
            }
        }
        catch
        {
            value = string.Empty;
        }

        return false;
    }

    private static bool TryResolveCurrentRodAccessoryType(IntPtr controllerPtr, out int accessoryType)
    {
        accessoryType = 0;
        if (!IsLikelyLiveControllerObject(controllerPtr))
        {
            return false;
        }

        TryResolveRodMethods();

        try
        {
            IntPtr controllerRodPtr = IntPtr.Zero;
            if (_offsetControllerCurrentRod >= 0)
            {
                TryReadObjectField(controllerPtr, _offsetControllerCurrentRod, out controllerRodPtr);
            }

            if (controllerRodPtr == IntPtr.Zero && _miControllerGetCurrentRod != IntPtr.Zero)
            {
                controllerRodPtr = InvokeZeroArgMethod(_miControllerGetCurrentRod, controllerPtr);
            }

            if (controllerRodPtr != IntPtr.Zero)
            {
                if (_offsetRodAccessoryType >= 0 &&
                    TryReadInt32Field(controllerRodPtr, _offsetRodAccessoryType, out accessoryType))
                {
                    accessoryType = NormalizeAccessoryType(accessoryType);
                    if (accessoryType != 0)
                    {
                        return true;
                    }
                }

                if (_miRodGetAccessoryType != IntPtr.Zero &&
                    TryInvokeZeroArgIntMethodFlexible(_miRodGetAccessoryType, controllerRodPtr, out accessoryType))
                {
                    accessoryType = NormalizeAccessoryType(accessoryType);
                    if (accessoryType != 0)
                    {
                        return true;
                    }
                }
            }

            if (_miControllerGetAccessoryType != IntPtr.Zero &&
                TryInvokeZeroArgIntMethodFlexible(_miControllerGetAccessoryType, controllerPtr, out accessoryType))
            {
                accessoryType = NormalizeAccessoryType(accessoryType);
                if (accessoryType != 0)
                {
                    return true;
                }
            }
        }
        catch
        {
            accessoryType = 0;
        }

        return false;
    }

    private static bool IsLikelyLiveControllerObject(IntPtr controllerPtr)
    {
        return controllerPtr != IntPtr.Zero && IsReadable(controllerPtr, IntPtr.Size * 2);
    }


    private static bool TryReadInt32Field(IntPtr instance, int offset, out int value)
    {
        value = 0;
        if (instance == IntPtr.Zero || offset < 0)
        {
            return false;
        }

        try
        {
            IntPtr fieldPtr = IntPtr.Add(instance, offset);
            if (!IsReadable(fieldPtr, sizeof(int)))
            {
                return false;
            }

            value = Marshal.ReadInt32(fieldPtr);
            return true;
        }
        catch
        {
            value = 0;
            return false;
        }
    }

    private static bool TryReadUInt64Field(IntPtr instance, int offset, out ulong value)
    {
        value = 0;
        if (instance == IntPtr.Zero || offset < 0)
        {
            return false;
        }

        try
        {
            IntPtr fieldPtr = IntPtr.Add(instance, offset);
            if (!IsReadable(fieldPtr, sizeof(long)))
            {
                return false;
            }

            value = unchecked((ulong)Marshal.ReadInt64(fieldPtr));
            return value != 0;
        }
        catch
        {
            value = 0;
            return false;
        }
    }

    private static bool TryReadObjectField(IntPtr instance, int offset, out IntPtr value)
    {
        value = IntPtr.Zero;
        if (instance == IntPtr.Zero || offset < 0)
        {
            return false;
        }

        try
        {
            IntPtr fieldPtr = IntPtr.Add(instance, offset);
            if (!IsReadable(fieldPtr, IntPtr.Size))
            {
                return false;
            }

            value = Marshal.ReadIntPtr(fieldPtr);
            return value != IntPtr.Zero;
        }
        catch
        {
            value = IntPtr.Zero;
            return false;
        }
    }

    private static unsafe bool TryInvokeZeroArgIntMethodFlexible(IntPtr method, IntPtr instance, out int value)
    {
        value = 0;
        if (method == IntPtr.Zero || instance == IntPtr.Zero)
        {
            return false;
        }

        if (TryInvokeZeroArgIntMethod(method, instance, out value))
        {
            return true;
        }

        try
        {
            IntPtr exc = IntPtr.Zero;
            IntPtr raw = IL2CPP.il2cpp_runtime_invoke(method, instance, null, ref exc);
            if (exc != IntPtr.Zero || raw == IntPtr.Zero)
            {
                return false;
            }

            IntPtr unboxed = IL2CPP.il2cpp_object_unbox(raw);
            if (unboxed == IntPtr.Zero)
            {
                return false;
            }

            if (IsReadable(unboxed, sizeof(int)))
            {
                value = Marshal.ReadInt32(unboxed);
                return true;
            }

            if (IsReadable(unboxed, sizeof(short)))
            {
                value = Marshal.ReadInt16(unboxed);
                return true;
            }

            if (IsReadable(unboxed, sizeof(byte)))
            {
                value = Marshal.ReadByte(unboxed);
                return true;
            }
        }
        catch
        {
            value = 0;
        }

        return false;
    }

    private static unsafe bool TryInvokeZeroArgUInt64MethodFlexible(IntPtr method, IntPtr instance, out ulong value)
    {
        value = 0;
        if (method == IntPtr.Zero || instance == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            IntPtr exc = IntPtr.Zero;
            IntPtr raw = IL2CPP.il2cpp_runtime_invoke(method, instance, null, ref exc);
            if (exc != IntPtr.Zero || raw == IntPtr.Zero)
            {
                return false;
            }

            IntPtr unboxed = IL2CPP.il2cpp_object_unbox(raw);
            if (unboxed == IntPtr.Zero)
            {
                return false;
            }

            if (IsReadable(unboxed, sizeof(long)))
            {
                value = unchecked((ulong)Marshal.ReadInt64(unboxed));
                return value != 0;
            }

            if (IsReadable(unboxed, sizeof(int)))
            {
                value = unchecked((ulong)Marshal.ReadInt32(unboxed));
                return value != 0;
            }
        }
        catch
        {
            value = 0;
        }

        return false;
    }

    private static bool TryGetFishLevelByName(string fishName, out int levelBucket)
    {
        levelBucket = 0;
        if (string.IsNullOrWhiteSpace(fishName))
        {
            levelBucket = 0;
            return false;
        }

        string key = fishName.Trim();
        if (BuiltInFishLevelByName.TryGetValue(key, out levelBucket))
        {
            levelBucket = NormalizeLevelBucket(levelBucket);
            return true;
        }

        if (TryGetRuntimeFishBasic(key, out RuntimeFishBasicData basic))
        {
            levelBucket = NormalizeLevelBucket(basic.LevelBucket);
            return levelBucket > 0;
        }

        levelBucket = 0;
        return false;
    }

    private static bool DoesFishMatchLocationContext(FishSpeciesContextData fish, FishingEnvironmentContext context)
    {
        if (context.LocationFlag != 0)
        {
            return (fish.AvailableLocationMask & context.LocationFlag) != 0;
        }

        return ResolveMode(context) switch
        {
            FishingModeContext.Route => (fish.AvailableLocationMask & 63) != 0,
            FishingModeContext.Ice => (fish.AvailableLocationMask & 64) != 0,
            FishingModeContext.Pond => (fish.AvailableLocationMask & 128) != 0,
            _ => true
        };
    }

    private static bool DoesFishMatchFishingSpotContext(FishSpeciesContextData fish, FishingEnvironmentContext context)
    {
        if (fish == null || context == null)
        {
            return false;
        }

        // Spring and summer share pond location 128, but use different seasonal
        // pools. Pond matching therefore requires both the pond spot and season.
        // Other fishing spots keep the proven 2.8.16.42 location behavior.
        if (ResolveMode(context) == FishingModeContext.Pond)
        {
            return DoesFishMatchLocationContext(fish, context) &&
                   context.SeasonFlag != 0 &&
                   (fish.ActiveSeasonMask & context.SeasonFlag) != 0;
        }

        return DoesFishMatchLocationContext(fish, context);
    }

    private static bool DoesRuntimeFishMatchLocationContext(string fishName, FishingEnvironmentContext context)
    {
        if (!TryGetRuntimeFishBasic(fishName, out RuntimeFishBasicData basic))
        {
            return false;
        }

        if (context.LocationFlag != 0)
        {
            return (basic.AvailableLocationMask & context.LocationFlag) != 0;
        }

        return ResolveMode(context) switch
        {
            FishingModeContext.Route => (basic.AvailableLocationMask & 63) != 0,
            FishingModeContext.Ice => (basic.AvailableLocationMask & 64) != 0,
            FishingModeContext.Pond => (basic.AvailableLocationMask & 128) != 0,
            _ => true
        };
    }

    private static bool DoesRuntimeFishMatchFishingSpotContext(string fishName, FishingEnvironmentContext context)
    {
        if (!TryGetRuntimeFishBasic(fishName, out RuntimeFishBasicData basic))
        {
            return false;
        }

        if (ResolveMode(context) == FishingModeContext.Pond)
        {
            return DoesRuntimeFishMatchLocationContext(fishName, context) &&
                   context.SeasonFlag != 0 &&
                   (basic.ActiveSeasonMask & context.SeasonFlag) != 0;
        }

        return DoesRuntimeFishMatchLocationContext(fishName, context);
    }

    private static float GetEnvironmentFactor(FishSpeciesContextData fish, int matchCount)
    {
        return matchCount switch
        {
            <= 0 => fish.Env0,
            1 => fish.Env1,
            2 => fish.Env2,
            _ => fish.Env3
        };
    }

    private static float GetContextBaseWeight(FishSpeciesContextData fish, FishingEnvironmentContext context)
    {
        if (context.LocationFlag == 0)
        {
            return ResolveMode(context) switch
            {
                FishingModeContext.Route => Mathf.Max(
                    fish.RouteHugeRockWeight,
                    fish.RoutePierWeight,
                    fish.RouteMidstreamWeight,
                    fish.RouteRiverBendWeight,
                    fish.RouteRiverBankWeight,
                    fish.RouteSunkenBoatWeight),
                FishingModeContext.Ice => fish.IceWeight,
                FishingModeContext.Pond => fish.PondWeight,
                _ => 1f
            };
        }

        return context.LocationFlag switch
        {
            32 => fish.RouteHugeRockWeight,
            2 => fish.RoutePierWeight,
            8 => fish.RouteMidstreamWeight,
            1 => fish.RouteRiverBendWeight,
            4 => fish.RouteRiverBankWeight,
            16 => fish.RouteSunkenBoatWeight,
            64 => (fish.AvailableLocationMask & 64) != 0 ? fish.IceWeight : 0f,
            128 => (fish.AvailableLocationMask & 128) != 0 ? fish.PondWeight : 0f,
            _ => 1f
        };
    }

    private static float GetCandidateContextScore(string candidateName, FishingEnvironmentContext context, out int matchCount)
    {
        matchCount = 0;
        if (!GetFishSpeciesContextMap().TryGetValue(candidateName, out FishSpeciesContextData? fish) || fish == null)
        {
            if (!TryGetRuntimeFishBasic(candidateName, out RuntimeFishBasicData runtime))
            {
                return 0f;
            }

            if (context.SeasonFlag != 0 && (runtime.ActiveSeasonMask & context.SeasonFlag) != 0)
            {
                matchCount++;
            }

            if (context.TimeFlag != 0 && (runtime.ActiveTimeMask & context.TimeFlag) != 0)
            {
                matchCount++;
            }

            if (context.WeatherFlag != 0 && (runtime.ActiveWeatherMask & context.WeatherFlag) != 0)
            {
                matchCount++;
            }

            // Runtime fallback has no per-spot weights; keep neutral base and preserve env-match ordering.
            return matchCount switch
            {
                <= 0 => 0.95f,
                1 => 1.20f,
                2 => 1.60f,
                _ => 2.20f
            };
        }

        if (context.SeasonFlag != 0 && (fish.ActiveSeasonMask & context.SeasonFlag) != 0)
        {
            matchCount++;
        }

        if (context.TimeFlag != 0 && (fish.ActiveTimeMask & context.TimeFlag) != 0)
        {
            matchCount++;
        }

        if (context.WeatherFlag != 0 && (fish.ActiveWeatherMask & context.WeatherFlag) != 0)
        {
            matchCount++;
        }

        float baseWeight = GetContextBaseWeight(fish, context);
        float envFactor = GetEnvironmentFactor(fish, matchCount);
        if (baseWeight <= 0f)
        {
            baseWeight = 0.01f;
        }

        return baseWeight * envFactor;
    }

    private static string[] SortCandidateNamesByContext(string[] candidateNames, FishingEnvironmentContext context)
    {
        if (candidateNames.Length <= 1)
        {
            return candidateNames;
        }

        var ranked = new List<(string Name, float Score, int MatchCount, int Index)>(candidateNames.Length);
        for (int i = 0; i < candidateNames.Length; i++)
        {
            string candidateName = candidateNames[i];
            float score = GetCandidateContextScore(candidateName, context, out int matchCount);
            ranked.Add((candidateName, score, matchCount, i));
        }

        ranked.Sort((a, b) =>
        {
            int cmp = b.Score.CompareTo(a.Score);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = b.MatchCount.CompareTo(a.MatchCount);
            if (cmp != 0)
            {
                return cmp;
            }

            return a.Index.CompareTo(b.Index);
        });

        string[] sorted = new string[ranked.Count];
        for (int i = 0; i < ranked.Count; i++)
        {
            sorted[i] = ranked[i].Name;
        }

        return sorted;
    }

    private static string[] SortCandidateNamesByContext(IReadOnlyDictionary<string, float> candidateWeights, FishingEnvironmentContext context)
    {
        if (candidateWeights == null || candidateWeights.Count == 0)
        {
            return Array.Empty<string>();
        }

        if (candidateWeights.Count == 1)
        {
            foreach (KeyValuePair<string, float> kv in candidateWeights)
            {
                return new[] { kv.Key };
            }
        }

        var ranked = new List<(string Name, float Score, float RodWeight, int MatchCount, int Index)>(candidateWeights.Count);
        int index = 0;
        foreach (KeyValuePair<string, float> kv in candidateWeights)
        {
            float baseScore = GetCandidateContextScore(kv.Key, context, out int matchCount);
            float rodWeight = kv.Value <= 0f ? 0.01f : kv.Value;
            ranked.Add((kv.Key, baseScore * rodWeight, rodWeight, matchCount, index++));
        }

        ranked.Sort((a, b) =>
        {
            int cmp = b.Score.CompareTo(a.Score);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = b.RodWeight.CompareTo(a.RodWeight);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = b.MatchCount.CompareTo(a.MatchCount);
            if (cmp != 0)
            {
                return cmp;
            }

            return a.Index.CompareTo(b.Index);
        });

        string[] sorted = new string[ranked.Count];
        for (int i = 0; i < ranked.Count; i++)
        {
            sorted[i] = ranked[i].Name;
        }

        return sorted;
    }

    private static string FormatAlternativeChineseList(IReadOnlyList<string> names)
    {
        if (names.Count == 0)
        {
            return string.Empty;
        }

        if (names.Count == 1)
        {
            return names[0];
        }

        if (names.Count == 2)
        {
            return names[0] + "或" + names[1];
        }

        var builder = new StringBuilder();
        for (int i = 0; i < names.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(i == names.Count - 1 ? "或" : "、");
            }

            builder.Append(names[i]);
        }

        return builder.ToString();
    }

    private static int NormalizeFlagValue(int raw, int[] orderedFlags)
    {
        if (raw <= 0 || orderedFlags.Length == 0)
        {
            return 0;
        }

        foreach (int flag in orderedFlags)
        {
            if (raw == flag)
            {
                return raw;
            }
        }

        if ((raw & (raw - 1)) == 0)
        {
            return raw;
        }

        if (raw >= 0 && raw < orderedFlags.Length)
        {
            return orderedFlags[raw];
        }

        if (raw - 1 >= 0 && raw - 1 < orderedFlags.Length)
        {
            return orderedFlags[raw - 1];
        }

        return 0;
    }

    private static int NormalizeAccessoryType(int raw)
    {
        return raw switch
        {
            1 => 1,
            2 => 2,
            _ => 0
        };
    }

    private static string GetLocationLabel(int locationFlag)
    {
        return locationFlag switch
        {
            32 => "巨石",
            2 => "码头",
            8 => "河中央",
            1 => "河湾",
            4 => "岸边",
            16 => "沉船",
            64 => "冰洞",
            128 => "池塘",
            _ => string.Empty
        };
    }

    private static string TryGetActiveSceneName()
    {
        try
        {
            Scene activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid() ? activeScene.name ?? string.Empty : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static FishingModeContext InferModeFromSceneName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return FishingModeContext.Unknown;
        }

        string lower = sceneName.ToLowerInvariant();
        if (lower.Contains("pond") || lower.Contains("pool"))
        {
            return FishingModeContext.Pond;
        }

        if (lower.Contains("ice") || lower.Contains("snow") || lower.Contains("winter"))
        {
            return FishingModeContext.Ice;
        }

        if (lower.Contains("fishing") || lower.Contains("lodge") || lower.Contains("cabin") || lower.Contains("river"))
        {
            return FishingModeContext.Route;
        }

        return FishingModeContext.Unknown;
    }

    private static FishingModeContext InferModeFromLocationFlag(int locationFlag)
    {
        return locationFlag switch
        {
            64 => FishingModeContext.Ice,
            128 => FishingModeContext.Pond,
            1 or 2 or 4 or 8 or 16 or 32 => FishingModeContext.Route,
            _ => FishingModeContext.Unknown
        };
    }

    private static void ApplyLocationMode(FishingEnvironmentContext context)
    {
        FishingModeContext mode = InferModeFromLocationFlag(context.LocationFlag);
        if (mode != FishingModeContext.Unknown)
        {
            context.Mode = mode;
            if (mode == FishingModeContext.Pond && context.SeasonFlag == 0)
            {
                context.SeasonFlag = DefaultCabinPondSeasonFlag;
            }
            else if (mode == FishingModeContext.Ice)
            {
                context.SeasonFlag = 8;
            }
        }
    }

    private static int InferSeasonFlagFromSceneName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return 0;
        }

        string lower = sceneName.ToLowerInvariant();
        if (lower.Contains("spring"))
        {
            return 1;
        }

        if (lower.Contains("summer"))
        {
            return 2;
        }

        if (lower.Contains("autumn") || lower.Contains("fall"))
        {
            return 4;
        }

        if (lower.Contains("winter") || lower.Contains("snow") || lower.Contains("ice"))
        {
            return 8;
        }

        return 0;
    }
}










