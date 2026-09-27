using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace SmaliPatcherEx
{
    public static class MagiskBuilder
    {
        /*
         * Hybrid module layout:
         * - Magisk flash ZIP: META-INF/update-binary + updater-script
         * - Modern module managers: module.prop + customize.sh + service.sh
         * - Systemless overlay: system/framework/services.jar
         *
         * This keeps the repository's original design while making the output
         * a complete module ZIP instead of only a partially packaged archive.
         */

        private const string UpdateBinary = @"#!/sbin/sh
SKIPUNZIP=1

ui_print ""************************************""
ui_print ""      SmaliPatcherEx v2.1           ""
ui_print ""      Android 15 / 16               ""
ui_print ""************************************""

unzip -o ""$ZIPFILE"" -d ""$MODPATH"" >/dev/null 2>&1 || abort ""! Cannot extract module""

if [ -f ""$MODPATH/fingerprint"" ]; then
  fp_module=$(cat ""$MODPATH/fingerprint"")
  fp_system=$(getprop ro.build.fingerprint)
  if [ -n ""$fp_module"" ] && [ ""$fp_module"" != ""$fp_system"" ]; then
    ui_print ""! Fingerprint mismatch!""
    ui_print ""  Module: $fp_module""
    ui_print ""  Device: $fp_system""
    abort ""! Wrong device or firmware build""
  fi
  ui_print ""- Fingerprint: OK""
fi

set_perm_recursive ""$MODPATH"" 0 0 0755 0644
set_perm ""$MODPATH/customize.sh"" 0 0 0755
set_perm ""$MODPATH/service.sh"" 0 0 0755
set_perm ""$MODPATH/post-fs-data.sh"" 0 0 0755

ui_print ""- services.jar installed systemlessly""
ui_print ""- Reboot required""
";

        private const string UpdaterScript = "#MAGISK\n";

        private const string CustomizeSh = @"#!/system/bin/sh
ui_print ""- SmaliPatcherEx module""

if [ -f ""$MODPATH/fingerprint"" ]; then
  fp_module=$(cat ""$MODPATH/fingerprint"")
  fp_system=$(getprop ro.build.fingerprint)

  if [ -n ""$fp_module"" ] && [ ""$fp_module"" != ""$fp_system"" ]; then
    ui_print ""! Fingerprint mismatch""
    ui_print ""  Module: $fp_module""
    ui_print ""  Device: $fp_system""
    abort ""! This module was built for a different firmware""
  fi
fi

set_perm_recursive ""$MODPATH"" 0 0 0755 0644
set_perm ""$MODPATH/service.sh"" 0 0 0755
set_perm ""$MODPATH/post-fs-data.sh"" 0 0 0755
";

        private const string PostFsDataSh = @"#!/system/bin/sh
MODDIR=${0%/*}
MARKER=""$MODDIR/.cache_cleared""

# Clear stale compiled copies once after module installation/update.
# Do not do it every boot.
if [ ! -f ""$MARKER"" ]; then
  find /data/dalvik-cache -iname '*@services.jar*class*' -delete 2>/dev/null
  find /data/misc/apexdata -iname '*@services.jar*class*' -delete 2>/dev/null

  find /data/misc/apexdata -type f 2>/dev/null | \
    grep -i 'services.*\.vdex$' | while read f; do
      rm -f ""$f"" 2>/dev/null
    done

  touch ""$MARKER"" 2>/dev/null
fi
";

        private const string ServiceSh = @"#!/system/bin/sh
MODDIR=${0%/*}

# Keep only a small diagnostic record. No AppOps, GNSS or framework settings
# are changed here; this module only mounts the patched services.jar.
{
  echo ""SmaliPatcherEx module active""
  echo ""fingerprint=$(getprop ro.build.fingerprint)""
  echo ""api=$(getprop ro.build.version.sdk)""
} > ""$MODDIR/status.log"" 2>/dev/null
";

        public static string Build(
            string patchedJarPath,
            string outputDir,
            string fingerprint,
            string[] appliedPatches,
            Action<string>? log = null)
        {
            if (!File.Exists(patchedJarPath))
                throw new FileNotFoundException(
                    "Patched services.jar not found", patchedJarPath);

            Directory.CreateDirectory(outputDir);

            var outZip = Path.Combine(
                outputDir,
                "SmaliPatcherEx-module.zip");

            if (File.Exists(outZip))
                File.Delete(outZip);

            var desc = appliedPatches.Length > 0
                ? "Patches: " + string.Join(", ", appliedPatches)
                : "no patches applied";

            var moduleProp =
                "id=SmaliPatcherEx\n" +
                "name=SmaliPatcherEx\n" +
                "version=v2.1.0\n" +
                "versionCode=210\n" +
                "author=sabpprook+rebuild\n" +
                $"description=Android 15/16 systemless services.jar patch — {desc}\n";

            log?.Invoke("[*] Building complete module ZIP ...");

            using (var zip = ZipFile.Open(outZip, ZipArchiveMode.Create))
            {
                AddText(
                    zip,
                    "META-INF/com/google/android/update-binary",
                    UpdateBinary,
                    executable: true);

                AddText(
                    zip,
                    "META-INF/com/google/android/updater-script",
                    UpdaterScript,
                    executable: false);

                AddText(zip, "module.prop", moduleProp);
                AddText(zip, "customize.sh", CustomizeSh, executable: true);
                AddText(zip, "post-fs-data.sh", PostFsDataSh, executable: true);
                AddText(zip, "service.sh", ServiceSh, executable: true);

                if (!string.IsNullOrWhiteSpace(fingerprint))
                    AddText(zip, "fingerprint", fingerprint.Trim() + "\n");

                var jarEntry = zip.CreateEntry(
                    "system/framework/services.jar",
                    CompressionLevel.Optimal);

                using (var src = File.OpenRead(patchedJarPath))
                using (var dst = jarEntry.Open())
                    src.CopyTo(dst);

                SetUnixMode(jarEntry, executable: false);

                AddText(
                    zip,
                    "patches.txt",
                    string.Join("\n", appliedPatches) + "\n");
            }

            Validate(outZip, patchedJarPath);

            log?.Invoke($"[✓] Complete module: {outZip}");
            return outZip;
        }

        private static void Validate(
            string zipPath,
            string patchedJarPath)
        {
            using var zip = ZipFile.OpenRead(zipPath);

            string[] required =
            {
                "META-INF/com/google/android/update-binary",
                "META-INF/com/google/android/updater-script",
                "module.prop",
                "customize.sh",
                "post-fs-data.sh",
                "service.sh",
                "system/framework/services.jar"
            };

            foreach (var name in required)
            {
                var entry = zip.GetEntry(name);
                if (entry == null)
                    throw new InvalidDataException(
                        "Module validation failed, missing: " + name);
            }

            var jar = zip.GetEntry("system/framework/services.jar")!;
            var expected = new FileInfo(patchedJarPath).Length;

            if (jar.Length != expected)
                throw new InvalidDataException(
                    $"Module validation failed: services.jar size mismatch " +
                    $"({jar.Length} != {expected})");
        }

        private static void AddText(
            ZipArchive zip,
            string entryName,
            string content,
            bool executable = false)
        {
            var entry = zip.CreateEntry(
                entryName,
                CompressionLevel.Optimal);

            SetUnixMode(entry, executable);

            using var sw = new StreamWriter(
                entry.Open(),
                new UTF8Encoding(false));

            sw.NewLine = "\n";
            sw.Write(content.Replace("\r\n", "\n"));
        }

        private static void SetUnixMode(
            ZipArchiveEntry entry,
            bool executable)
        {
            /*
             * 0100755 => regular file + rwxr-xr-x
             * 0100644 => regular file + rw-r--r--
             */
            entry.ExternalAttributes = executable
                ? unchecked((int)0x81ED0000)
                : unchecked((int)0x81A40000);
        }
    }
}
