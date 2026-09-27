using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SmaliPatcherEx;

public partial class MainForm : Form
{
    private const string BAKSMALI_URL = "https://github.com/baksmali/smali/releases/download/3.0.9/baksmali-3.0.9-fat.jar";
    private const string SMALI_URL = "https://github.com/baksmali/smali/releases/download/3.0.9/smali-3.0.9-fat.jar";

    private static readonly string ToolDir =
        Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
        ?? AppDomain.CurrentDomain.BaseDirectory;

    private static readonly string BaksmaliJar = Path.Combine(ToolDir, "baksmali.jar");
    private static readonly string SmaliJar = Path.Combine(ToolDir, "smali.jar");

    private string? _servicesJarPath;
    private string _outputDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "SmaliPatcherEx_Output");
    private int _apiLevel = 36;
    private string _fingerprint = "";

    private Label _jarLabel = null!;
    private NumericUpDown _apiBox = null!;
    private TextBox _fpBox = null!;
    private TextBox _outBox = null!;
    private FlowLayoutPanel _patchFlow = null!;
    private Button _patchBtn = null!;
    private RichTextBox _logBox = null!;
    private ProgressBar _progress = null!;

    private readonly Dictionary<string, CheckBox> _checkBoxes = new();

    public MainForm()
    {
        InitializeComponent();
        BuildUI();
        PopulatePatches();
        Log("SmaliPatcherEx v2.0 — Android 16 Ready");
        Log($"Tool directory: {ToolDir}");
    }

    private void BuildUI()
    {
        Text = "SmaliPatcherEx v2.0 [Android 16 / API 36]";
        Size = new System.Drawing.Size(900, 800);
        MinimumSize = new System.Drawing.Size(800, 700);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = System.Drawing.Color.FromArgb(24, 24, 32);
        ForeColor = System.Drawing.Color.WhiteSmoke;
        Font = new System.Drawing.Font("Segoe UI", 9f);

        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = System.Drawing.Color.Transparent,
        };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 420));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(main);

        var top = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            BackColor = System.Drawing.Color.FromArgb(30, 30, 44)
        };
        main.Controls.Add(top, 0, 0);

        int y = 10;

        var title = new Label
        {
            Text = "SmaliPatcherEx v2.0",
            Font = new System.Drawing.Font("Segoe UI", 16, System.Drawing.FontStyle.Bold),
            ForeColor = System.Drawing.Color.FromArgb(80, 200, 255),
            AutoSize = true,
            Location = new System.Drawing.Point(10, y)
        };
        top.Controls.Add(title);
        y += 38;

        var jarBtn = new Button
        {
            Text = "Select services.jar",
            Size = new System.Drawing.Size(150, 28),
            Location = new System.Drawing.Point(10, y),
            BackColor = System.Drawing.Color.FromArgb(55, 55, 80),
            ForeColor = System.Drawing.Color.White,
            FlatStyle = FlatStyle.Flat
        };
        jarBtn.Click += JarBtn_Click;

        _jarLabel = new Label
        {
            Text = "No file selected",
            ForeColor = System.Drawing.Color.Gray,
            AutoSize = false,
            Size = new System.Drawing.Size(680, 28),
            Location = new System.Drawing.Point(170, y + 4)
        };

        top.Controls.Add(jarBtn);
        top.Controls.Add(_jarLabel);
        y += 38;

        var apiLbl = new Label
        {
            Text = "API Level:",
            AutoSize = true,
            Location = new System.Drawing.Point(10, y + 4)
        };

        _apiBox = new NumericUpDown
        {
            Location = new System.Drawing.Point(90, y),
            Size = new System.Drawing.Size(65, 26),
            Minimum = 21,
            Maximum = 99,
            Value = 36,
            BackColor = System.Drawing.Color.FromArgb(42, 42, 60),
            ForeColor = System.Drawing.Color.White
        };
        _apiBox.ValueChanged += (s, e) => _apiLevel = (int)_apiBox.Value;

        var fpLbl = new Label
        {
            Text = "Fingerprint (optional):",
            AutoSize = true,
            Location = new System.Drawing.Point(170, y + 4)
        };

        _fpBox = new TextBox
        {
            Location = new System.Drawing.Point(320, y),
            Size = new System.Drawing.Size(520, 26),
            PlaceholderText = "leave blank to skip",
            BackColor = System.Drawing.Color.FromArgb(42, 42, 60),
            ForeColor = System.Drawing.Color.White
        };
        _fpBox.TextChanged += (s, e) => _fingerprint = _fpBox.Text;

        top.Controls.AddRange(new Control[] { apiLbl, _apiBox, fpLbl, _fpBox });
        y += 36;

        var outLbl = new Label
        {
            Text = "Output:",
            AutoSize = true,
            Location = new System.Drawing.Point(10, y + 4)
        };

        _outBox = new TextBox
        {
            Location = new System.Drawing.Point(90, y),
            Size = new System.Drawing.Size(700, 26),
            Text = _outputDir,
            BackColor = System.Drawing.Color.FromArgb(42, 42, 60),
            ForeColor = System.Drawing.Color.White
        };
        _outBox.TextChanged += (s, e) => _outputDir = _outBox.Text;

        var outBtn = new Button
        {
            Text = "…",
            Size = new System.Drawing.Size(36, 26),
            Location = new System.Drawing.Point(798, y),
            BackColor = System.Drawing.Color.FromArgb(55, 55, 80),
            ForeColor = System.Drawing.Color.White,
            FlatStyle = FlatStyle.Flat
        };
        outBtn.Click += (s, e) =>
        {
            using var d = new FolderBrowserDialog();
            if (d.ShowDialog() == DialogResult.OK)
            {
                _outputDir = d.SelectedPath;
                _outBox.Text = _outputDir;
            }
        };

        top.Controls.AddRange(new Control[] { outLbl, _outBox, outBtn });
        y += 36;

        var pLbl = new Label
        {
            Text = "Smali Patches:",
            Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold),
            ForeColor = System.Drawing.Color.FromArgb(80, 200, 255),
            AutoSize = true,
            Location = new System.Drawing.Point(10, y + 3)
        };

        var allBtn = new Button
        {
            Text = "All",
            Size = new System.Drawing.Size(50, 24),
            Location = new System.Drawing.Point(130, y),
            BackColor = System.Drawing.Color.FromArgb(55, 55, 80),
            ForeColor = System.Drawing.Color.White,
            FlatStyle = FlatStyle.Flat
        };
        allBtn.Click += (s, e) =>
        {
            foreach (var c in _checkBoxes.Values) c.Checked = true;
        };

        var noneBtn = new Button
        {
            Text = "None",
            Size = new System.Drawing.Size(55, 24),
            Location = new System.Drawing.Point(188, y),
            BackColor = System.Drawing.Color.FromArgb(55, 55, 80),
            ForeColor = System.Drawing.Color.White,
            FlatStyle = FlatStyle.Flat
        };
        noneBtn.Click += (s, e) =>
        {
            foreach (var c in _checkBoxes.Values) c.Checked = false;
        };

        top.Controls.AddRange(new Control[] { pLbl, allBtn, noneBtn });
        y += 32;

        _patchFlow = new FlowLayoutPanel
        {
            Location = new System.Drawing.Point(10, y),
            Size = new System.Drawing.Size(860, 180),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = true,
            AutoScroll = true,
            BackColor = System.Drawing.Color.FromArgb(20, 20, 34),
            Padding = new Padding(4)
        };
        top.Controls.Add(_patchFlow);
        y += 188;

        _patchBtn = new Button
        {
            Text = "▶ Patch & Build Module",
            Size = new System.Drawing.Size(220, 40),
            Location = new System.Drawing.Point(10, y),
            BackColor = System.Drawing.Color.FromArgb(0, 130, 255),
            ForeColor = System.Drawing.Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _patchBtn.FlatAppearance.BorderSize = 0;
        _patchBtn.Click += PatchBtn_Click;
        top.Controls.Add(_patchBtn);

        var bottom = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 4, 10, 10),
            BackColor = System.Drawing.Color.FromArgb(14, 14, 22)
        };
        main.Controls.Add(bottom, 0, 1);

        _progress = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 6,
            Style = ProgressBarStyle.Marquee,
            Visible = false
        };

        _logBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = System.Drawing.Color.FromArgb(10, 10, 18),
            ForeColor = System.Drawing.Color.LightGreen,
            Font = new System.Drawing.Font("Consolas", 9f),
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Vertical
        };

        bottom.Controls.Add(_logBox);
        bottom.Controls.Add(_progress);
    }

    private void PopulatePatches()
    {
        _patchFlow.Controls.Clear();
        _checkBoxes.Clear();

        foreach (var p in PatchDefinitions.All)
        {
            var cb = new CheckBox
            {
                Text = $"{p.Name} — {p.Description}",
                Checked = true,
                AutoSize = false,
                Width = 840,
                Height = 22,
                ForeColor = System.Drawing.Color.WhiteSmoke,
                BackColor = System.Drawing.Color.Transparent
            };
            _checkBoxes[p.Name] = cb;
            _patchFlow.Controls.Add(cb);
        }
    }

    private void JarBtn_Click(object? sender, EventArgs e)
    {
        using var d = new OpenFileDialog
        {
            Title = "Select services.jar",
            Filter = "JAR files (*.jar)|*.jar|All files (*.*)|*.*"
        };

        if (d.ShowDialog() == DialogResult.OK)
        {
            _servicesJarPath = d.FileName;
            _jarLabel.Text = _servicesJarPath;
            _jarLabel.ForeColor = System.Drawing.Color.LightGreen;
            Log($"[+] Selected: {_servicesJarPath}");
        }
    }

    private async void PatchBtn_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_servicesJarPath) || !File.Exists(_servicesJarPath))
        {
            MessageBox.Show("Select services.jar first!", "Missing input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selected = _checkBoxes.Where(kv => kv.Value.Checked).Select(kv => kv.Key).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Select at least one patch!", "No patches", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _patchBtn.Enabled = false;
        _progress.Visible = true;
        _logBox.Clear();

        string? error = null;
        string? result = null;

        await Task.Run(() =>
        {
            try
            {
                result = RunPipeline(selected);
            }
            catch (Exception ex)
            {
                error = ex.ToString();
            }
        });

        _patchBtn.Enabled = true;
        _progress.Visible = false;

        if (error != null)
        {
            Log($"[EXCEPTION] {error}");
            MessageBox.Show($"Error:\n\n{error}", "SmaliPatcherEx Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        else if (result != null)
        {
            MessageBox.Show($"Done!\n\nModule: {result}", "Success!", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private string? RunPipeline(List<string> patches)
    {
        var work = Path.Combine(Path.GetTempPath(), $"smpx_{DateTime.Now:yyyyMMddHHmmss}");
        Directory.CreateDirectory(work);

        try
        {
            Log("═══════════════════════════════════════");
            Log($" SmaliPatcherEx v2.0 — API {_apiLevel}");
            Log("═══════════════════════════════════════");

            Log("\n[1/5] Checking Java...");
            var jv = Run("java", "-version", work);
            if (jv.Code != 0 && !jv.Stderr.Contains("version"))
                throw new Exception($"Java not found or not working.\n\nOutput:\n{jv.Stderr}\n\nMake sure Java 17 is installed and restart the app.");
            Log($"[✓] Java: {jv.Stderr.Split('\n')[0]}");

            Log("\n[2/5] Getting tools...");
            Directory.CreateDirectory(ToolDir);
            Download(BAKSMALI_URL, BaksmaliJar, "baksmali");
            Download(SMALI_URL, SmaliJar, "smali");

            Log("\n[3/5] Extracting DEX...");
            var dexDir = Path.Combine(work, "dex");
            Directory.CreateDirectory(dexDir);
            var dexFiles = ExtractDex(_servicesJarPath!, dexDir);
            if (dexFiles.Count == 0)
                throw new Exception("No classes.dex found inside services.jar!");
            Log($"[✓] {dexFiles.Count} dex file(s)");

            Log("\n[4/5] Disassembling...");
            var smaliRoots = new List<(string DexName, string DexPath, string SmaliDir)>();

            foreach (var dex in dexFiles)
            {
                var dexName = Path.GetFileName(dex);
                var suffix = Path.GetFileNameWithoutExtension(dexName).Replace("classes", "");
                if (string.IsNullOrWhiteSpace(suffix)) suffix = "1";

                var smaliDir = Path.Combine(work, $"smali_c{suffix}");
                Directory.CreateDirectory(smaliDir);

                Log($" baksmali ← {dexName} -> {Path.GetFileName(smaliDir)}");
                var r = Run("java", $"-jar \"{BaksmaliJar}\" disassemble --api {_apiLevel} --output \"{smaliDir}\" \"{dex}\"", work);
                if (r.Code != 0)
                    throw new Exception($"baksmali failed for {dexName}:\n{r.Stderr}");

                var sc = Directory.GetFiles(smaliDir, "*.smali", SearchOption.AllDirectories).Length;
                Log($"[✓] {dexName}: {sc} smali files");

                smaliRoots.Add((dexName, dex, smaliDir));
            }

            Log("\n[5a] Patching smali...");
            var applied = new List<string>();
            var patchedDexNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var root in smaliRoots)
            {
                Log($"[*] Scanning {root.DexName} ...");
                var engine = new PatchEngine(root.SmaliDir, _apiLevel);
                engine.Log += Log;

                var results = engine.RunAll(patches);
                var appliedHere = results.Values.Where(r => r.Applied).Select(r => r.Patch.Name).ToArray();

                if (appliedHere.Length > 0)
                {
                    applied.AddRange(appliedHere);
                    patchedDexNames.Add(root.DexName);
                    Log($"[✓] {root.DexName}: applied {string.Join(", ", appliedHere)}");
                }
                else
                {
                    Log($"[-] {root.DexName}: no matches");
                }
            }

            applied = applied.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            Log($"[✓] Applied: {applied.Count}");
            if (applied.Count == 0)
                throw new Exception("No patches applied — target classes not found in this services.jar.\nMake sure the jar matches your device build.");

            Log("\n[5b] Recompiling...");
            var rebuiltDir = Path.Combine(work, "rebuilt");
            Directory.CreateDirectory(rebuiltDir);

            var rebuiltDexMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var root in smaliRoots)
            {
                string outDex;

                if (patchedDexNames.Contains(root.DexName))
                {
                    outDex = Path.Combine(rebuiltDir, root.DexName);
                    Log($" smali -> {root.DexName}");

                    // IMPORTANT: no --api on assemble
                   // Assemble using the same Android API selected in the GUI.
var rs = Run("java", $"-jar \"{SmaliJar}\" assemble --api {_apiLevel} --output \"{outDex}\" \"{root.SmaliDir}\"", work);

if (rs.Code != 0 || !File.Exists(outDex))
    throw new Exception($"smali recompile failed for {root.DexName}:\n{rs.Stderr}");
                }
                else
                {
                    outDex = root.DexPath;
                    Log($"[=] Keeping original {root.DexName}");
                }

                rebuiltDexMap[root.DexName] = outDex;
            }

            var patchedJar = Path.Combine(work, "services.jar");
            RepackJar(_servicesJarPath!, rebuiltDexMap, patchedJar);

            Directory.CreateDirectory(_outputDir);
            var outJar = Path.Combine(_outputDir, "services.jar");
            File.Copy(patchedJar, outJar, true);
            var modZip = MagiskBuilder.Build(patchedJar, _outputDir, _fingerprint, applied.ToArray(), Log);

            Log("\n═══════════════════════════════════════");
            Log($"[✓] Module: {modZip}");
            Log($"[✓] JAR: {outJar}");
            Log("Flash via Magisk / KernelSU → Reboot");
            return modZip;
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { }
        }
    }

    private void Download(string url, string dest, string label)
    {
        if (File.Exists(dest))
        {
            Log($"[✓] {label}.jar present");
            return;
        }

        Log($"[*] Downloading {label}.jar ...");
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(120);
        var data = http.GetByteArrayAsync(url).GetAwaiter().GetResult();
        File.WriteAllBytes(dest, data);
        Log($"[✓] {label}.jar ({data.Length / 1024} KB)");
    }

    private List<string> ExtractDex(string jar, string outDir)
    {
        var result = new List<string>();

        using var zip = ZipFile.OpenRead(jar);
        foreach (var e in zip.Entries)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(e.Name, @"^classes\d*\.dex$"))
            {
                var dest = Path.Combine(outDir, e.Name);
                e.ExtractToFile(dest, true);
                result.Add(dest);
                Log($" extracted: {e.Name} ({e.Length / 1024} KB)");
            }
        }

        return result;
    }

    private void RepackJar(string src, Dictionary<string, string> dexMap, string out_)
    {
        var tmp = out_ + ".tmp";

        using (var s = ZipFile.OpenRead(src))
        using (var d = ZipFile.Open(tmp, ZipArchiveMode.Create))
        {
            foreach (var e in s.Entries)
            {
                if (dexMap.TryGetValue(e.Name, out var replacementDex))
                {
                    d.CreateEntryFromFile(replacementDex, e.Name, CompressionLevel.Optimal);
                    Log($" replaced: {e.Name}");
                }
                else
                {
                    var newEntry = d.CreateEntry(e.FullName, CompressionLevel.Optimal);
                    using var si = e.Open();
                    using var doi = newEntry.Open();
                    si.CopyTo(doi);
                }
            }
        }

        File.Move(tmp, out_, true);
        Log($"[✓] Repacked JAR ({new FileInfo(out_).Length / 1024} KB)");
    }

    private (int Code, string Stderr) Run(string exe, string args, string dir)
    {
        using var p = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = dir,
            }
        };

        var sb = new StringBuilder();

        p.ErrorDataReceived += (s, e) =>
        {
            if (e.Data != null)
            {
                sb.AppendLine(e.Data);
                Log($" {e.Data}");
            }
        };

        p.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null)
                Log($" {e.Data}");
        };

        p.Start();
        p.BeginErrorReadLine();
        p.BeginOutputReadLine();
        p.WaitForExit();

        return (p.ExitCode, sb.ToString());
    }

    private void Log(string msg)
    {
        if (InvokeRequired)
        {
            Invoke(() => Log(msg));
            return;
        }

        _logBox.AppendText(msg + "\n");
        _logBox.ScrollToCaret();
    }

    private void InitializeComponent()
    {
        SuspendLayout();
        AutoScaleDimensions = new System.Drawing.SizeF(7f, 15f);
        AutoScaleMode = AutoScaleMode.Font;
        ResumeLayout(false);
    }
}
