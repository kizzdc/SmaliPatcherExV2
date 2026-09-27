using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SmaliPatcherEx
{
    public class SmaliPatch
    {
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public string FileGlob { get; init; } = "";
        public string Search { get; init; } = "";
        public string Replace { get; init; } = "";
        public int AndroidMin { get; init; } = 1;
        public int AndroidMax { get; init; } = 99;
        public bool Multi { get; init; } = false;
    }

    public class PatchResult
    {
        public SmaliPatch Patch { get; init; } = null!;
        public bool Applied { get; set; }
        public List<string> Files { get; } = new();
        public string Reason { get; set; } = "";
    }

    public static class PatchDefinitions
    {
        /*
         * This list mirrors the public SmaliPatcherEx CLI/GitHub patch set.
         * The original C# repository currently ships only a "template" entry,
         * which is why the GUI cannot produce a meaningfully patched module.
         *
         * IMPORTANT:
         * .NET replacement syntax uses $1/$2/$3 rather than Python's \1/\2/\3.
         */

        private const string IsMockProviderSearch =
            @"(\.method[^\n]*isMockProvider\([^)]*\)Z[^\n]*\n" +
            @"(?:[ \t]*\.(?:registers|locals)[^\n]*\n)?)" +
            @"(.*?)" +
            @"(\.end method)";

        private const string IsMockProviderReplace =
            "$1    const/4 v0, 0x1\n    return v0\n$3";

        private const string UntrustedTouchSearch =
            @"(\.method[^\n]*areTokensVisible[^\n]*\)Z[^\n]*\n" +
            @"(?:[ \t]*\.(?:registers|locals)[^\n]*\n)?)" +
            @"(.*?)" +
            @"(\.end method)";

        private const string UntrustedTouchReplace =
            "$1    const/4 v0, 0x1\n    return v0\n$3";

        public static readonly List<SmaliPatch> All = new()
        {
            new SmaliPatch
            {
                Name = "mock_location_appops",
                Description = "Mock Location AppOps bypass",
                FileGlob = "AppOpsService.smali",
                Search =
                    @"(\.method[^\n]*checkOp\([^)]*\)I[^\n]*\n" +
                    @"(?:[ \t]*\.(?:registers|locals)[^\n]*\n)?)" +
                    @"(.*?)" +
                    @"(\.end method)",
                Replace = "$1    const/4 v0, 0x0\n    return v0\n$3",
                AndroidMin = 29,
                AndroidMax = 99
            },

            new SmaliPatch
            {
                Name = "mock_location_isprovider",
                Description = "isMockProvider always true",
                FileGlob = "LocationManagerService.smali",
                Search = IsMockProviderSearch,
                Replace = IsMockProviderReplace,
                AndroidMin = 31,
                AndroidMax = 99
            },

            new SmaliPatch
            {
                Name = "mock_location_provider_manager",
                Description = "LocationProviderManager bypass",
                FileGlob = "LocationProviderManager.smali",
                Search = IsMockProviderSearch,
                Replace = IsMockProviderReplace,
                AndroidMin = 33,
                AndroidMax = 99
            },

            new SmaliPatch
            {
                Name = "mock_location_appops_helper",
                Description = "AppOpsHelper bypass",
                FileGlob = "AppOpsHelper.smali",
                Search =
                    @"(\.method[^\n]*noteOp\([^)]*\)I[^\n]*\n" +
                    @"(?:[ \t]*\.(?:registers|locals)[^\n]*\n)?)" +
                    @"(.*?)" +
                    @"(\.end method)",
                Replace = "$1    const/4 v0, 0x0\n    return v0\n$3",
                AndroidMin = 34,
                AndroidMax = 99
            },

            new SmaliPatch
            {
                Name = "mock_permission_dpm",
                Description = "DevicePolicyManager bypass",
                FileGlob = "DevicePolicyManagerService.smali",
                Search =
                    @"(\.method[^\n]*hasUserSetup\(\)Z[^\n]*\n" +
                    @"(?:[ \t]*\.(?:registers|locals)[^\n]*\n)?)" +
                    @"(.*?)" +
                    @"(\.end method)",
                Replace = "$1    const/4 v0, 0x1\n    return v0\n$3",
                AndroidMin = 21,
                AndroidMax = 99
            },

            new SmaliPatch
            {
                Name = "mock_permission_restrictions",
                Description = "UserRestrictionsUtils bypass",
                FileGlob = "UserRestrictionsUtils.smali",
                Search =
                    @"(\.method[^\n]*hasUserRestriction\([^)]*\)Z[^\n]*\n" +
                    @"(?:[ \t]*\.(?:registers|locals)[^\n]*\n)?)" +
                    @"(.*?)" +
                    @"(\.end method)",
                Replace = "$1    const/4 v0, 0x0\n    return v0\n$3",
                AndroidMin = 30,
                AndroidMax = 99
            },

            new SmaliPatch
            {
                Name = "gnss_mock_provider",
                Description = "GnssManagerService bypass",
                FileGlob = "GnssManagerService.smali",
                Search = IsMockProviderSearch,
                Replace = IsMockProviderReplace,
                AndroidMin = 33,
                AndroidMax = 99
            },

            new SmaliPatch
            {
                Name = "gnss_location_provider_legacy",
                Description = "GnssLocationProvider legacy",
                FileGlob = "GnssLocationProvider.smali",
                Search = IsMockProviderSearch,
                Replace = IsMockProviderReplace,
                AndroidMin = 28,
                AndroidMax = 31
            },

            new SmaliPatch
            {
                Name = "signature_spoofing_pms",
                Description = "PackageManagerService spoof hook",
                FileGlob = "PackageManagerService.smali",
                Search =
                    @"(\.method[^\n]*getPackageInfo[^\n]*\n" +
                    @"(?:[ \t]*\.(?:registers|locals)[^\n]*\n)?)" +
                    @"(.*?)" +
                    @"(\.end method)",
                Replace = "$1$2    # smpx: signature spoof hook point\n$3",
                AndroidMin = 21,
                AndroidMax = 33
            },

            new SmaliPatch
            {
                Name = "signature_spoofing_computer",
                Description = "ComputerEngine spoof hook (A14+ PM refactor)",
                FileGlob = "ComputerEngine.smali",
                Search =
                    @"(\.method[^\n]*computePackageInfo[^\n]*\n" +
                    @"(?:[ \t]*\.(?:registers|locals)[^\n]*\n)?)" +
                    @"(.*?)" +
                    @"(\.end method)",
                Replace = "$1$2    # smpx: signature spoof hook point\n$3",
                AndroidMin = 34,
                AndroidMax = 99
            },

            new SmaliPatch
            {
                Name = "signature_spoofing_snapshot",
                Description = "PackageInfoSnapshot spoof hook",
                FileGlob = "PackageInfoSnapshot.smali",
                Search =
                    @"(\.method[^\n]*getSignatures[^\n]*\n" +
                    @"(?:[ \t]*\.(?:registers|locals)[^\n]*\n)?)" +
                    @"(.*?)" +
                    @"(\.end method)",
                Replace = "$1$2    # smpx: signature spoof hook point\n$3",
                AndroidMin = 34,
                AndroidMax = 99
            },

            new SmaliPatch
            {
                Name = "no_permission_review",
                Description = "Skip REVIEW_REQUIRED flag marker",
                FileGlob = "*Permission*.smali",
                Search = @"REVIEW_REQUIRED",
                Replace = "REVIEW_REQUIRED_IGNORED",
                AndroidMin = 29,
                AndroidMax = 99,
                Multi = true
            },

            new SmaliPatch
            {
                Name = "doze_whitelist",
                Description = "DeviceIdleController whitelist always-true",
                FileGlob = "DeviceIdleController.smali",
                Search =
                    @"(\.method[^\n]*isAllowlist[^\n]*\)Z[^\n]*\n" +
                    @"(?:[ \t]*\.(?:registers|locals)[^\n]*\n)?)" +
                    @"(.*?)" +
                    @"(\.end method)",
                Replace = "$1    const/4 v0, 0x1\n    return v0\n$3",
                AndroidMin = 23,
                AndroidMax = 99
            },

            new SmaliPatch
            {
                Name = "untrusted_touch",
                Description = "InputManagerService bypass",
                FileGlob = "InputManagerService.smali",
                Search = UntrustedTouchSearch,
                Replace = UntrustedTouchReplace,
                AndroidMin = 31,
                AndroidMax = 99
            },

            new SmaliPatch
            {
                Name = "untrusted_touch_wms",
                Description = "WindowManagerService bypass",
                FileGlob = "WindowManagerService.smali",
                Search = UntrustedTouchSearch,
                Replace = UntrustedTouchReplace,
                AndroidMin = 31,
                AndroidMax = 99
            },

            new SmaliPatch
            {
                Name = "overlay_any",
                Description = "Allow unsigned overlays",
                FileGlob = "OverlayManagerService.smali",
                Search =
                    @"(\.method[^\n]*assertCompatibleOverlayTarget[^\n]*\)V[^\n]*\n" +
                    @"(?:[ \t]*\.(?:registers|locals)[^\n]*\n)?)" +
                    @"(.*?)" +
                    @"(\.end method)",
                Replace = "$1    return-void\n$3",
                AndroidMin = 29,
                AndroidMax = 99
            }
        };

        public static readonly Dictionary<string, SmaliPatch> Map =
            All.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }

    public class PatchEngine
    {
        private readonly string smaliRoot;
        private readonly int api;

        public Action<string>? Log { get; set; }

        public PatchEngine(string smaliRoot, int api)
        {
            this.smaliRoot = smaliRoot;
            this.api = api;
        }

        private void Write(string msg) => Log?.Invoke(msg);

        private IEnumerable<string> GlobFiles(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern) || !Directory.Exists(smaliRoot))
                return Enumerable.Empty<string>();

            var normalized = pattern.Replace('\\', '/').Trim();

            if (!normalized.Contains('/'))
                return Directory.GetFiles(smaliRoot, normalized, SearchOption.AllDirectories);

            var parts = normalized.Split('/');
            var fileName = parts[^1];
            var subPath = string.Join("/", parts.Take(parts.Length - 1));

            return Directory.GetFiles(smaliRoot, fileName, SearchOption.AllDirectories)
                .Where(f => f.Replace('\\', '/')
                    .Contains(subPath, StringComparison.OrdinalIgnoreCase));
        }

        public PatchResult Apply(SmaliPatch patch)
        {
            var result = new PatchResult { Patch = patch };

            if (api < patch.AndroidMin || api > patch.AndroidMax)
            {
                result.Reason = $"API {api} out of range ({patch.AndroidMin}-{patch.AndroidMax})";
                return result;
            }

            var targets = GlobFiles(patch.FileGlob).ToList();
            bool anyFile = false;

            foreach (var file in targets)
            {
                anyFile = true;
                var text = File.ReadAllText(file);
                string patched = text;
                int count = 0;

                var options = RegexOptions.Multiline | RegexOptions.Singleline;

                if (patch.Multi)
                {
                    count = Regex.Matches(text, patch.Search, options).Count;
                    if (count > 0)
                        patched = Regex.Replace(text, patch.Search, patch.Replace, options);
                }
                else
                {
                    var match = Regex.Match(text, patch.Search, options, TimeSpan.FromSeconds(10));
                    if (match.Success)
                    {
                        patched = Regex.Replace(
                            text,
                            patch.Search,
                            patch.Replace,
                            options,
                            TimeSpan.FromSeconds(10));

                        count = patched != text ? 1 : 0;
                    }
                }

                if (patched != text)
                {
                    File.WriteAllText(file, patched);
                    result.Files.Add(
                        Path.GetFileName(file) +
                        (count > 1 ? $" ({count}x)" : ""));

                    result.Applied = true;
                }
            }

            if (!anyFile)
                result.Reason = "No file matched " + patch.FileGlob;
            else if (!result.Applied)
                result.Reason = "Pattern not found in matched files";

            return result;
        }

        public Dictionary<string, PatchResult> RunAll(IEnumerable<string> names)
        {
            var results = new Dictionary<string, PatchResult>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var name in names)
            {
                if (!PatchDefinitions.Map.TryGetValue(name, out var patch))
                {
                    Write("! Unknown patch: " + name);
                    continue;
                }

                Write("Applying " + name + " ...");
                var r = Apply(patch);
                results[name] = r;

                if (r.Applied)
                    Write("+ " + name + ": " + string.Join(", ", r.Files));
                else
                    Write("- " + name + ": skipped (" + r.Reason + ")");
            }

            return results;
        }
    }
}
