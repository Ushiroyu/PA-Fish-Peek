using System.Collections.Generic;

namespace PAFishPeekPlugin;

public sealed partial class FishPeekPlugin
{
    private static readonly Dictionary<FishingModeContext, Dictionary<int, HashSet<string>>> BuiltInAccessoryFishByMode =
        new()
        {
            [FishingModeContext.Route] = new()
            {
                [1] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "\u4fdd\u6d01\u9c7c", "\u5149\u6591\u9c7c", "\u51b0\u6e9c\u5b50\u9c7c", "\u534e\u4e3d\u9cc5", "\u591c\u660e\u73e0", "\u5927\u5e08\u9f9f",
                    "\u5e73\u51e1\u9c7c", "\u5e7b\u5149\u9c7c", "\u6212\u6307", "\u62b1\u62b1\u8725", "\u62df\u5ca9\u9c7c", "\u62f3\u51fb\u867e",
                    "\u6559\u7ec3\u9c7c", "\u6d41\u6d6a\u5251\u5ba2", "\u70df\u82b1\u9c7c", "\u7261\u4e39\u9c7c", "\u72fc\u86db\u87f9", "\u732a\u9f3b\u9c7c",
                    "\u7532\u65b9\u6ee1\u610f\u9c7c", "\u7c89\u4e1d\u867e", "\u7c97\u7ea2\u7ebf", "\u82ad\u857e\u84ec\u84ec\u9c7c", "\u84dd\u773c\u6cea",
                    "\u89c6\u89c9\u7cfb", "\u94b1\u888b", "\u98de\u673a\u5934", "\u9cc5\u9cc5\u9c7c", "\u9ed1\u9ca4\u9c7c"
                },
                [2] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "\u4e0d\u660e\u5316\u77f3", "\u51b0\u6cb3\u9c9f", "\u5251\u9f99\u9c7c", "\u53d1\u8d22\u9ca4", "\u5446\u5446\u9cb6", "\u5730\u5305\u5929\u9c7c",
                    "\u8bd5\u5242\u74f6", "\u5947\u602a\u7684\u4e1c\u897f", "\u5a03\u5a03\u9c7c", "\u62f7\u8d1d\u9c7c", "\u6781\u9053\u9c7c",
                    "\u679c\u51bb\u9c7c", "\u6c34\u6cd5\u8001", "\u6d77\u5e95\u8c5a", "\u7194\u5ca9\u9c7c", "\u72d7\u5934\u9c7c", "\u7f57\u6c49\u9c7c",
                    "\u7f8e\u9aef\u516c", "\u8f90\u5c04\u9c88", "\u9676\u827a\u9c7c", "\u96f7\u9cd7", "\u9a97\u5b50\u9c7c", "\u9b3c\u5200\u9c7c",
                    "\u9b3c\u7259\u9c7c", "\u9b3c\u864e\u9c7c", "\u9cc4\u9738\u9c7c", "\u9ec4\u9e2d\u53eb",
                    "\u5409\u5229\u9c7c", "\u6cb3\u9a6c", "\u69b4\u83b2\u9f9f", "\u6d41\u661f\u9c7c"
                }
            },
            [FishingModeContext.Ice] = new()
            {
                [1] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "\u534e\u4e3d\u9cc5", "\u5927\u5e08\u9f9f", "\u5e7b\u5149\u9c7c", "\u62df\u5ca9\u9c7c", "\u62f3\u51fb\u867e", "\u6ce5\u7334\u5b50",
                    "\u72fc\u86db\u87f9", "\u7532\u65b9\u6ee1\u610f\u9c7c", "\u7c89\u4e1d\u867e", "\u7c97\u7ea2\u7ebf", "\u82ad\u857e\u84ec\u84ec\u9c7c",
                    "\u84dd\u773c\u6cea", "\u8def\u6613\u9c7c", "\u98de\u673a\u5934", "\u9cc5\u9cc5\u9c7c", "\u9ed1\u9ca4\u9c7c"
                },
                [2] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "\u4e03\u661f\u5200", "\u561f\u561f\u9c7c", "\u5730\u5305\u5929\u9c7c", "\u8bd5\u5242\u74f6", "\u6781\u9053\u9c7c",
                    "\u6c34\u6cd5\u8001", "\u7b77\u5b50\u9cdd", "\u8f90\u5c04\u9c88", "\u9676\u827a\u9c7c", "\u9b3c\u5200\u9c7c",
                    "\u9b3c\u7259\u9c7c", "\u9b3c\u864e\u9c7c", "\u9ec4\u9e2d\u53eb"
                }
            },
            [FishingModeContext.Pond] = new()
            {
                [1] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "\u52bf\u5229\u773c", "\u534e\u4e3d\u9cc5", "\u5927\u5e08\u9f9f", "\u5b87\u822a\u9c7c", "\u5e7b\u5149\u9c7c", "\u62df\u5ca9\u9c7c",
                    "\u62f3\u51fb\u867e", "\u72fc\u86db\u87f9", "\u7532\u65b9\u6ee1\u610f\u9c7c", "\u7c89\u4e1d\u867e", "\u7c97\u7ea2\u7ebf",
                    "\u82ad\u857e\u84ec\u84ec\u9c7c", "\u84dd\u773c\u6cea", "\u963f\u5b9d\u9c7c", "\u98de\u673a\u5934", "\u9cc5\u9cc5\u9c7c",
                    "\u9ed1\u9ca4\u9c7c", "\u5b9d\u6c14\u9c7c", "\u5929\u7ebf\u9c7c", "\u5203\u9c7c", "\u8def\u6613\u9c7c"
                },
                [2] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "\u5730\u5305\u5929\u9c7c", "\u8bd5\u5242\u74f6", "\u5c16\u9f7f\u9c7c", "\u6781\u9053\u9c7c", "\u6c34\u6cd5\u8001",
                    "\u8c61\u9f3b\u9c7c", "\u8f90\u5c04\u9c88", "\u9676\u827a\u9c7c", "\u9752\u74f7\u9c7c", "\u9b3c\u5200\u9c7c",
                    "\u9b3c\u7259\u9c7c", "\u9b3c\u864e\u9c7c", "\u9ec4\u9e2d\u53eb", "\u4e03\u661f\u5200", "\u561f\u561f\u9c7c",
                    "\u7b77\u5b50\u9cdd", "\u5947\u867e"
                }
            }
        };
}
