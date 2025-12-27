using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static MinecraftLauncher.MainForm;

namespace MinecraftLauncher
{
    public partial class MainForm : Form
    {
        #region 核心字段
        private string _mcRootPath;
        private List<McVersion> _localVersions = new List<McVersion>();
        private List<VersionInfo> _officialVersions = new List<VersionInfo>();
        private List<Account> _accounts = new List<Account>();
        private string _selectedAccountId = string.Empty;

        private VersionDownloader _versionDownloader;
        private FileChecker _fileChecker;
        private ConfigManager _configManager;
        private LoginManager _loginManager;
        private SkinManager _skinManager;
        private ProgressBar _progressBar;
        private readonly string _launcherVersion = "1.0";
        private readonly HttpClient _httpClient = new HttpClient();
        private Point _lastMousePos;

        // 控件成员变量
        private McComboBox _cboLocalVersion;
        private ComboBox _cboOfficialVersion;
        private Button _btnFixFiles;
        private Label _versionLabel;
        private TextBox _txtLog;
        private ComboBox _cboAccounts;
        private PictureBox _pbSkinPreview;
        private NumericUpDown _nudWidth;
        private NumericUpDown _nudHeight;
        private CheckBox _chkFullscreen;
        #endregion

        #region 强类型数据模型
        public class McVersion
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("mainClass")]
            public string MainClass { get; set; }

            [JsonProperty("assets")]
            public string Assets { get; set; }

            [JsonProperty("assetIndex")]
            public AssetIndex AssetIndex { get; set; } = new AssetIndex();

            [JsonProperty("libraries")]
            public List<Library> Libraries { get; set; } = new List<Library>();

            [JsonProperty("downloads")]
            public Downloads Downloads { get; set; } = new Downloads();

            [JsonProperty("minecraftArguments")]
            public string MinecraftArguments { get; set; }

            [JsonProperty("arguments")]
            public Arguments Arguments { get; set; } = new Arguments();

            [JsonProperty("minimumLauncherVersion")]
            public int MinimumLauncherVersion { get; set; } = 0;

            [JsonProperty("releaseTime")]
            public DateTime ReleaseTime { get; set; }

            [JsonProperty("inheritsFrom")]
            public string InheritsFrom { get; set; }

            [JsonProperty("logging")]
            public LoggingConfig Logging { get; set; } = new LoggingConfig();

            public override string ToString() => Id;
        }

        public class AssetIndex
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("sha1")]
            public string Sha1 { get; set; }

            [JsonProperty("size")]
            public long Size { get; set; }

            [JsonProperty("url")]
            public string Url { get; set; }
        }

        public class Library
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("downloads")]
            public LibraryDownloads Downloads { get; set; } = new LibraryDownloads();

            [JsonProperty("rules")]
            public List<Rule> Rules { get; set; }

            [JsonProperty("natives")]
            public JObject Natives { get; set; }
        }

        public class Rule
        {
            [JsonProperty("action")]
            public string Action { get; set; } = "allow";

            [JsonProperty("os")]
            public OSInfo OS { get; set; }
        }

        public class OSInfo
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("version")]
            public string Version { get; set; }

            [JsonProperty("arch")]
            public string Architecture { get; set; }
        }

        public class LibraryDownloads
        {
            [JsonProperty("artifact")]
            public Artifact Artifact { get; set; } = new Artifact();

            [JsonProperty("classifiers")]
            public JObject Classifiers { get; set; }
        }

        public class Artifact
        {
            [JsonProperty("path")]
            public string Path { get; set; }

            [JsonProperty("sha1")]
            public string Sha1 { get; set; }

            [JsonProperty("url")]
            public string Url { get; set; }

            [JsonProperty("size")]
            public long Size { get; set; }
        }

        public class Downloads
        {
            [JsonProperty("client")]
            public ClientDownload Client { get; set; } = new ClientDownload();

            [JsonProperty("client_mappings")]
            public ClientDownload ClientMappings { get; set; }

            [JsonProperty("server")]
            public ClientDownload Server { get; set; }

            [JsonProperty("server_mappings")]
            public ClientDownload ServerMappings { get; set; }
        }

        public class ClientDownload
        {
            [JsonProperty("url")]
            public string Url { get; set; }

            [JsonProperty("sha1")]
            public string Sha1 { get; set; }

            [JsonProperty("size")]
            public long Size { get; set; }
        }

        public class Arguments
        {
            [JsonProperty("game")]
            public List<JToken> Game { get; set; } = new List<JToken>();

            [JsonProperty("jvm")]
            public List<JToken> Jvm { get; set; } = new List<JToken>();
        }

        public class LoggingConfig
        {
            [JsonProperty("client")]
            public LoggingEntry Client { get; set; } = new LoggingEntry();
        }

        public class LoggingEntry
        {
            [JsonProperty("argument")]
            public string Argument { get; set; }

            [JsonProperty("file")]
            public LoggingFile File { get; set; } = new LoggingFile();

            [JsonProperty("type")]
            public string Type { get; set; }
        }

        public class LoggingFile
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("sha1")]
            public string Sha1 { get; set; }

            [JsonProperty("size")]
            public long Size { get; set; }

            [JsonProperty("url")]
            public string Url { get; set; }
        }

        public class VersionManifest
        {
            [JsonProperty("latest")]
            public LatestVersions Latest { get; set; } = new LatestVersions();

            [JsonProperty("versions")]
            public List<VersionInfo> Versions { get; set; } = new List<VersionInfo>();
        }

        public class LatestVersions
        {
            [JsonProperty("release")]
            public string Release { get; set; }

            [JsonProperty("snapshot")]
            public string Snapshot { get; set; }
        }

        public class VersionInfo
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("url")]
            public string Url { get; set; }

            [JsonProperty("releaseTime")]
            public DateTime ReleaseTime { get; set; }

            public override string ToString() => $"{Id} ({Type})";
        }

        public class LauncherConfig
        {
            public string Username { get; set; } = "Player";
            public string SelectedVersion { get; set; } = "";
            public string JavaPath { get; set; } = "java.exe";
            public int MinMemory { get; set; } = 1024;
            public int MaxMemory { get; set; } = 2048;
            public bool IsOfflineMode { get; set; } = true;
            public string SkinUrl { get; set; } = "";
            public string GameDir { get; set; } = "";
            public bool EnableLogging { get; set; } = true;
            public bool IgnoreJavaVersionCheck { get; set; } = false;
            public int WindowWidth { get; set; } = 854;
            public int WindowHeight { get; set; } = 480;
            public bool Fullscreen { get; set; } = false;
            public string Language { get; set; } = "zh_CN";
        }

        public class Account
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string Username { get; set; } = "Player";
            public string Uuid { get; set; } = Guid.NewGuid().ToString().Replace("-", "");
            public string Token { get; set; } = "0";
            public bool IsOffline { get; set; } = true;
            public string SkinPath { get; set; } = "";

            public override string ToString() => Username;
        }
        #endregion

        #region 构造函数与初始化
        public MainForm()
        {
            InitializeComponent();
            this.FormBorderStyle = FormBorderStyle.None;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.SizeGripStyle = SizeGripStyle.Hide;
            // 不再添加自定义标题栏和拖动事件
            InitializeLauncherCore();
            InitializeLauncherUI();
            LoadAccounts();
            AutoDetectJava();
            // 不再处理Resize和MouseDown事件
        }


        private void MainForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.Location = new Point(
                    this.Location.X + (e.X - _lastMousePos.X),
                    this.Location.Y + (e.Y - _lastMousePos.Y));
            }
        }

        private void InitializeLauncherCore()
        {
            var defaultGameDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
            _configManager = new ConfigManager(defaultGameDir);
            _mcRootPath = string.IsNullOrEmpty(_configManager.GetConfig().GameDir) ? defaultGameDir : _configManager.GetConfig().GameDir;

            _fileChecker = new FileChecker(Log);
            _loginManager = new LoginManager(Log);
            _versionDownloader = new VersionDownloader(_mcRootPath, Log, UpdateProgress);
            _skinManager = new SkinManager(_mcRootPath, Log);

            // 确保必要目录存在
            EnsureDirectoryExists(Path.Combine(_mcRootPath, "versions"));
            EnsureDirectoryExists(Path.Combine(_mcRootPath, "libraries"));
            EnsureDirectoryExists(Path.Combine(_mcRootPath, "assets"));
            EnsureDirectoryExists(Path.Combine(_mcRootPath, "launcher_profiles"));
        }

        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Log($"创建目录: {path}");
            }
        }

        // 修改 InitializeLauncherUI 方法，优化内容区布局和美化标题栏
        private void InitializeLauncherUI()
        {
            int margin = 15;
            int groupSpacing = 12;

            // 美化自定义标题栏
            Panel pnlHeader = new Panel
            {
                Size = new Size(this.Width, 56),
                Location = new Point(0, 0),
                BackColor = Color.FromArgb(51, 102, 204),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            pnlHeader.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    pnlHeader.ClientRectangle,
                    Color.FromArgb(51, 102, 204),
                    Color.FromArgb(33, 60, 120),
                    90f))
                {
                    g.FillRectangle(brush, pnlHeader.ClientRectangle);
                }
                // 阴影
                using (var shadowBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
                {
                    g.FillRectangle(shadowBrush, 0, pnlHeader.Height - 4, pnlHeader.Width, 4);
                }
            };
            // 可选Logo（如无资源可注释）
            // PictureBox pbLogo = new PictureBox
            // {
            //     Image = Properties.Resources.LauncherLogo,
            //     Size = new Size(32, 32),
            //     Location = new Point(10, 12),
            //     SizeMode = PictureBoxSizeMode.Zoom,
            //     BackColor = Color.Transparent
            // };
            // pnlHeader.Controls.Add(pbLogo);

            Label lblTitle = new Label
            {
                Text = $"MineLauncher {_launcherVersion}",
                Font = new Font("微软雅黑", 16, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 14),
                AutoSize = true,
                BackColor = Color.Transparent
            };

            Button btnMin = new Button
            {
                Text = "—",
                FlatStyle = FlatStyle.Flat,
                Size = new Size(36, 28),
                Location = new Point(this.Width - 90, 14),
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            btnMin.FlatAppearance.BorderSize = 0;
            btnMin.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 80, 120);
            btnMin.FlatAppearance.MouseDownBackColor = Color.FromArgb(60, 60, 100);
            btnMin.Region = new Region(new Rectangle(0, 0, btnMin.Width, btnMin.Height));
            btnMin.Click += (s, e) => this.WindowState = FormWindowState.Minimized;

            Button btnClose = new Button
            {
                Text = "×",
                FlatStyle = FlatStyle.Flat,
                Size = new Size(36, 28),
                Location = new Point(this.Width - 48, 14),
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 60, 60);
            btnClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(160, 40, 40);
            btnClose.Region = new Region(new Rectangle(0, 0, btnClose.Width, btnClose.Height));
            btnClose.Click += (s, e) => this.Close();

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(btnMin);
            pnlHeader.Controls.Add(btnClose);

            pnlHeader.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) _lastMousePos = e.Location; };
            pnlHeader.MouseMove += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    this.Location = new Point(
                        this.Location.X + (e.X - _lastMousePos.X),
                        this.Location.Y + (e.Y - _lastMousePos.Y));
                }
            };

            // 内容区采用Panel包裹，支持滚动
            Panel pnlContent = new Panel
            {
                Location = new Point(0, pnlHeader.Bottom),
                Size = new Size(this.Width, this.Height - pnlHeader.Height),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                AutoScroll = true,
                BackColor = Color.White
            };

            // 游戏目录
            GroupBox gbGameDir = new GroupBox
            {
                Text = "游戏目录",
                Location = new Point(margin, margin),
                Size = new Size(pnlContent.Width - 2 * margin, 56),
                Font = new Font("微软雅黑", 9, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            TextBox txtGameDir = new TextBox
            {
                Location = new Point(20, 25),
                Size = new Size(gbGameDir.Width - 120, 25),
                Text = _mcRootPath,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Button btnBrowseDir = new Button
            {
                Text = "浏览",
                Location = new Point(gbGameDir.Width - 90, 23),
                Size = new Size(80, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnBrowseDir.Click += (s, e) =>
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        txtGameDir.Text = fbd.SelectedPath;
                        _mcRootPath = fbd.SelectedPath;
                        _configManager.UpdateConfig(c => c.GameDir = fbd.SelectedPath);
                        _versionDownloader.UpdateGameDir(fbd.SelectedPath);
                        LoadLocalVersions(_cboLocalVersion);
                    }
                }
            };
            gbGameDir.Controls.Add(txtGameDir);
            gbGameDir.Controls.Add(btnBrowseDir);

            // 版本管理
            GroupBox gbVersions = new GroupBox
            {
                Text = "版本管理",
                Location = new Point(margin, gbGameDir.Bottom + groupSpacing),
                Size = new Size(pnlContent.Width - 2 * margin, 100),
                Font = new Font("微软雅黑", 9, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            Label lblLocalVersion = new Label
            {
                Text = "本地版本:",
                Location = new Point(20, 30),
                AutoSize = true
            };
            _cboLocalVersion = new McComboBox
            {
                Location = new Point(100, 27),
                Size = new Size((gbVersions.Width - 80) / 2, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            Label lblOfficialVersion = new Label
            {
                Text = "官方版本:",
                Location = new Point(gbVersions.Width / 2 + 20, 30),
                AutoSize = true
            };
            _cboOfficialVersion = new ComboBox
            {
                Location = new Point(gbVersions.Width / 2 + 100, 27),
                Size = new Size((gbVersions.Width - 80) / 2, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            Button btnDownloadVersion = new Button
            {
                Text = "下载选中版本",
                Location = new Point(gbVersions.Width - 90, 25),
                Size = new Size(80, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(33, 150, 243),
                ForeColor = Color.White
            };
            btnDownloadVersion.Click += async (s, e) =>
            {
                var selected = _cboOfficialVersion.SelectedItem as VersionInfo;
                if (selected != null)
                {
                    await DownloadAndInstallVersion(selected);
                }
            };

            Button btnRefreshVersions = new Button
            {
                Text = "刷新版本列表",
                Location = new Point(20, 65),
                Size = new Size(120, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White
            };
            btnRefreshVersions.Click += async (s, e) =>
            {
                await RefreshOfficialVersions(_cboOfficialVersion);
                LoadLocalVersions(_cboLocalVersion);
            };

            _versionLabel = new Label
            {
                Text = "未加载版本信息",
                Location = new Point(150, 70),
                AutoSize = true,
                ForeColor = Color.Gray
            };
            gbVersions.Controls.Add(lblLocalVersion);
            gbVersions.Controls.Add(_cboLocalVersion);
            gbVersions.Controls.Add(lblOfficialVersion);
            gbVersions.Controls.Add(_cboOfficialVersion);
            gbVersions.Controls.Add(btnDownloadVersion);
            gbVersions.Controls.Add(btnRefreshVersions);
            gbVersions.Controls.Add(_versionLabel);

            // 账户与皮肤、游戏设置纵向排列
            int halfWidth = (pnlContent.Width - 3 * margin) / 2;
            GroupBox gbAccount = new GroupBox
            {
                Text = "账户与皮肤",
                Location = new Point(margin, gbVersions.Bottom + groupSpacing),
                Size = new Size(halfWidth, 140),
                Font = new Font("微软雅黑", 9, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            GroupBox gbSettings = new GroupBox
            {
                Text = "游戏设置",
                Location = new Point(margin, gbAccount.Bottom + groupSpacing),
                Size = new Size(halfWidth, 170),
                Font = new Font("微软雅黑", 9, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            Label lblAccounts = new Label
            {
                Text = "账户:",
                Location = new Point(20, 30),
                AutoSize = true
            };
            _cboAccounts = new ComboBox
            {
                Location = new Point(80, 27),
                Size = new Size(200, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _cboAccounts.SelectedIndexChanged += (s, e) =>
            {
                var selectedAccount = _cboAccounts.SelectedItem as Account;
                if (selectedAccount != null)
                {
                    _selectedAccountId = selectedAccount.Id;
                    UpdateSkinPreview(selectedAccount);
                }
            };

            Button btnAddAccount = new Button
            {
                Text = "添加账户",
                Location = new Point(290, 25),
                Size = new Size(90, 30)
            };
            btnAddAccount.Click += (s, e) => ShowAddAccountDialog();

            Button btnRemoveAccount = new Button
            {
                Text = "删除账户",
                Location = new Point(290, 60),
                Size = new Size(90, 30)
            };
            btnRemoveAccount.Click += (s, e) => RemoveSelectedAccount();

            Label lblSkin = new Label
            {
                Text = "皮肤预览:",
                Location = new Point(20, 70),
                AutoSize = true
            };
            _pbSkinPreview = new PictureBox
            {
                Location = new Point(80, 65),
                Size = new Size(100, 100),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.StretchImage
            };

            Button btnChangeSkin = new Button
            {
                Text = "更换皮肤",
                Location = new Point(190, 110),
                Size = new Size(90, 30)
            };
            btnChangeSkin.Click += (s, e) => ChangeSkinForSelectedAccount();

            gbAccount.Controls.Add(lblAccounts);
            gbAccount.Controls.Add(_cboAccounts);
            gbAccount.Controls.Add(btnAddAccount);
            gbAccount.Controls.Add(btnRemoveAccount);
            gbAccount.Controls.Add(lblSkin);
            gbAccount.Controls.Add(_pbSkinPreview);
            gbAccount.Controls.Add(btnChangeSkin);

            // 游戏设置区域
            Label lblJavaPath = new Label
            {
                Text = "Java路径:",
                Location = new Point(20, 30),
                AutoSize = true
            };
            TextBox txtJavaPath = new TextBox
            {
                Name = "txtJavaPath",
                Location = new Point(100, 27),
                Size = new Size((halfWidth - 40), 25),
                Text = _configManager.GetConfig().JavaPath
            };
            Button btnBrowseJava = new Button
            {
                Text = "浏览",
                Location = new Point(360, 25),
                Size = new Size(50, 28)
            };
            btnBrowseJava.Click += (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Filter = "Java可执行文件|java.exe";
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        txtJavaPath.Text = ofd.FileName;
                    }
                }
            };

            Label lblMemory = new Label
            {
                Text = "内存设置 (MB):",
                Location = new Point(20, 70),
                AutoSize = true
            };
            Label lblMinMem = new Label
            {
                Text = "最小:",
                Location = new Point(120, 70),
                AutoSize = true
            };
            TextBox txtMinMem = new TextBox
            {
                Name = "txtMinMem",
                Location = new Point(160, 67),
                Size = new Size(80, 25),
                Text = _configManager.GetConfig().MinMemory.ToString()
            };
            Label lblMaxMem = new Label
            {
                Text = "最大:",
                Location = new Point(250, 70),
                AutoSize = true
            };
            TextBox txtMaxMem = new TextBox
            {
                Name = "txtMaxMem",
                Location = new Point(290, 67),
                Size = new Size(80, 25),
                Text = _configManager.GetConfig().MaxMemory.ToString()
            };

            Label lblResolution = new Label
            {
                Text = "分辨率:",
                Location = new Point(20, 110),
                AutoSize = true
            };
            _nudWidth = new NumericUpDown
            {
                Name = "nudWidth",
                Location = new Point(100, 107),
                Size = new Size(80, 25),
                Minimum = 640,
                Maximum = 3840,
                Value = _configManager.GetConfig().WindowWidth
            };
            Label lblX = new Label
            {
                Text = "×",
                Location = new Point(190, 110),
                AutoSize = true
            };
            _nudHeight = new NumericUpDown
            {
                Name = "nudHeight",
                Location = new Point(210, 107),
                Size = new Size(80, 25),
                Minimum = 480,
                Maximum = 2160,
                Value = _configManager.GetConfig().WindowHeight
            };
            _chkFullscreen = new CheckBox
            {
                Name = "chkFullscreen",
                Text = "全屏模式",
                Location = new Point(300, 110),
                Checked = _configManager.GetConfig().Fullscreen
            };

            CheckBox chkOfflineMode = new CheckBox
            {
                Name = "chkOfflineMode",
                Text = "离线模式",
                Location = new Point(20, 140),
                Checked = _configManager.GetConfig().IsOfflineMode
            };
            CheckBox chkIgnoreJavaCheck = new CheckBox
            {
                Name = "chkIgnoreJavaCheck",
                Text = "忽略Java版本检查",
                Location = new Point(120, 140),
                Checked = _configManager.GetConfig().IgnoreJavaVersionCheck
            };

            gbSettings.Controls.Add(lblJavaPath);
            gbSettings.Controls.Add(txtJavaPath);
            gbSettings.Controls.Add(btnBrowseJava);
            gbSettings.Controls.Add(lblMemory);
            gbSettings.Controls.Add(lblMinMem);
            gbSettings.Controls.Add(txtMinMem);
            gbSettings.Controls.Add(lblMaxMem);
            gbSettings.Controls.Add(txtMaxMem);
            gbSettings.Controls.Add(lblResolution);
            gbSettings.Controls.Add(_nudWidth);
            gbSettings.Controls.Add(lblX);
            gbSettings.Controls.Add(_nudHeight);
            gbSettings.Controls.Add(_chkFullscreen);
            gbSettings.Controls.Add(chkOfflineMode);
            gbSettings.Controls.Add(chkIgnoreJavaCheck);

            // 日志区
            GroupBox gbLog = new GroupBox
            {
                Text = "运行日志",
                Location = new Point(margin, gbSettings.Bottom + groupSpacing),
                Size = new Size(pnlContent.Width - 2 * margin, 140),
                Font = new Font("微软雅黑", 9, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _progressBar = new ProgressBar { Name = "progressBar", Location = new Point(10, 25), Width = gbLog.Width - 20, Height = 20, Visible = false, Style = ProgressBarStyle.Continuous, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _txtLog = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(10, 50),
                Size = new Size(gbLog.Width - 20, gbLog.Height - 80),
                Font = new Font("Consolas", 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                BackColor = Color.WhiteSmoke
            };
            Label lblStatus = new Label
            {
                Name = "lblStatus",
                Text = "状态: 就绪",
                Location = new Point(10, gbLog.Height - 25),
                AutoSize = true,
                ForeColor = Color.Green,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            gbLog.Controls.Add(_progressBar);
            gbLog.Controls.Add(_txtLog);
            gbLog.Controls.Add(lblStatus);

            // 按钮区
            Panel pnlButtons = new Panel
            {
                Location = new Point(margin, gbLog.Bottom + groupSpacing),
                Size = new Size(pnlContent.Width - 2 * margin, 48),
                BorderStyle = BorderStyle.None,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            TextBox txtUsername = new TextBox
            {
                Name = "txtUsername",
                Location = new Point(620, 15),
                Size = new Size(150, 25),
                Text = _configManager.GetConfig().Username
            };
            Label lblUsername = new Label
            {
                Text = "用户名:",
                Location = new Point(560, 18),
                AutoSize = true
            };

            Button btnLaunch = new Button
            {
                Text = "启动游戏",
                Location = new Point(20, 10),
                Width = 120,
                Height = 40,
                Font = new Font("微软雅黑", 12, FontStyle.Bold),
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            Button btnSaveConfig = new Button { Text = "保存配置", Location = new Point(150, 10), Width = 100, Height = 40, Font = new Font("微软雅黑", 9), BackColor = Color.FromArgb(33, 150, 243), ForeColor = Color.White, Cursor = Cursors.Hand };
            Button btnCheckFiles = new Button { Text = "检查文件", Location = new Point(260, 10), Width = 100, Height = 40, Font = new Font("微软雅黑", 9), BackColor = Color.FromArgb(255, 193, 7), ForeColor = Color.White, Cursor = Cursors.Hand };
            Button btnClearCache = new Button { Text = "清理缓存", Location = new Point(370, 10), Width = 100, Height = 40, Font = new Font("微软雅黑", 9), BackColor = Color.FromArgb(156, 39, 176), ForeColor = Color.White, Cursor = Cursors.Hand };

            _btnFixFiles = new Button
            {
                Text = "补全缺失文件",
                Location = new Point(480, 10),
                Width = 120,
                Height = 40,
                Font = new Font("微软雅黑", 9),
                BackColor = Color.FromArgb(244, 67, 54),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            _btnFixFiles.Click += async (s, e) => await FixMissingFiles();

            btnLaunch.Click += async (s, e) => await LaunchGame();
            btnSaveConfig.Click += (s, e) => SaveCurrentConfig(txtUsername, txtMinMem, txtMaxMem, txtJavaPath, chkOfflineMode, chkIgnoreJavaCheck, _cboLocalVersion, txtGameDir, _chkFullscreen);
            btnCheckFiles.Click += async (s, e) => await CheckFiles(_cboLocalVersion);
            btnClearCache.Click += (s, e) => ClearCache();

            pnlButtons.Controls.Add(btnLaunch);
            pnlButtons.Controls.Add(btnSaveConfig);
            pnlButtons.Controls.Add(btnCheckFiles);
            pnlButtons.Controls.Add(btnClearCache);
            pnlButtons.Controls.Add(_btnFixFiles);
            pnlButtons.Controls.Add(lblUsername);
            pnlButtons.Controls.Add(txtUsername);

            pnlContent.Controls.Add(gbGameDir);
            pnlContent.Controls.Add(gbVersions);
            pnlContent.Controls.Add(gbAccount);
            pnlContent.Controls.Add(gbSettings);
            pnlContent.Controls.Add(gbLog);
            pnlContent.Controls.Add(pnlButtons);

            // 添加标题栏和内容区到窗体
            this.Controls.Add(pnlHeader);
            this.Controls.Add(pnlContent);

            // 初始化加载
            LoadLocalVersions(_cboLocalVersion);
            _ = RefreshOfficialVersions(_cboOfficialVersion);
            UpdateStatus("就绪");
        }
        #endregion

        #region 检测与下载Java
        // 添加Java自动下载功能
        private async Task DownloadJava()
        {
            try
            {
                UpdateStatus("正在准备下载Java...");
                string javaDownloadUrl = GetJavaDownloadUrl();
                if (string.IsNullOrEmpty(javaDownloadUrl))
                {
                    Log("无法获取Java下载地址");
                    return;
                }

                string tempDir = Path.Combine(Path.GetTempPath(), "MinecraftLauncher", "Java");
                EnsureDirectoryExists(tempDir);
                string installerPath = Path.Combine(tempDir, "java_installer.exe");

                Log("开始下载Java...");
                using (var client = new WebClient())
                {
                    client.DownloadProgressChanged += (s, e) =>
                    {
                        UpdateProgress(e.ProgressPercentage);
                    };
                    await client.DownloadFileTaskAsync(javaDownloadUrl, installerPath);
                }

                Log("下载完成，开始安装Java...");
                UpdateStatus("正在安装Java...");

                // 运行安装程序
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo(installerPath, "/s") // 静默安装参数
                    {
                        UseShellExecute = true
                    }
                };
                process.Start();
                await process.WaitForExitAsync();

                Log("Java安装完成");
                UpdateStatus("Java安装完成");

                // 重新检测Java路径
                AutoDetectJava();
            }
            catch (Exception ex)
            {
                Log($"Java下载或安装失败: {ex.Message}");
                UpdateStatus("Java安装失败");
            }
        }

        // 获取适合当前系统的Java下载地址
        private string GetJavaDownloadUrl()
        {
            // 根据系统架构返回相应的Java下载地址
            // 这里使用AdoptOpenJDK作为示例，实际应用中可能需要更新链接
            if (Environment.Is64BitOperatingSystem)
            {
                return "https://github.com/adoptium/temurin17-binaries/releases/download/jdk-17.0.9%2B9/OpenJDK17U-jre_x64_windows_hotspot_17.0.9_9.msi";
            }
            else
            {
                return "https://github.com/adoptium/temurin17-binaries/releases/download/jdk-17.0.9%2B9/OpenJDK17U-jre_x86-32_windows_hotspot_17.0.9_9.msi";
            }
        }

        private void AutoDetectJava()
        {
            try
            {
                // 尝试从注册表查找Java
                var javaPath = GetJavaPathFromRegistry();
                if (!string.IsNullOrEmpty(javaPath) && File.Exists(javaPath))
                {
                    var txtJavaPath = this.Controls.Find("txtJavaPath", true).FirstOrDefault() as TextBox;
                    if (txtJavaPath != null)
                    {
                        txtJavaPath.Text = javaPath;
                        _configManager.UpdateConfig(c => c.JavaPath = javaPath);
                        Log($"自动检测到Java路径: {javaPath}");
                        return;
                    }
                }

                // 尝试从系统环境变量查找
                var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
                if (!string.IsNullOrEmpty(javaHome))
                {
                    javaPath = Path.Combine(javaHome, "bin", "java.exe");
                    if (File.Exists(javaPath))
                    {
                        var txtJavaPath = this.Controls.Find("txtJavaPath", true).FirstOrDefault() as TextBox;
                        if (txtJavaPath != null)
                        {
                            txtJavaPath.Text = javaPath;
                            _configManager.UpdateConfig(c => c.JavaPath = javaPath);
                            Log($"从环境变量检测到Java路径: {javaPath}");
                            return;
                        }
                    }
                }

                Log("未检测到Java，请手动设置Java路径或点击下载按钮自动安装");
                var gbSettings = this.Controls.OfType<GroupBox>().First(g => g.Text == "游戏设置");
                Button btnDownloadJava = new Button
                {
                    Text = "下载Java",
                    Location = new Point(220, 140),
                    Size = new Size(90, 25)
                };
                btnDownloadJava.Click += async (s, e) => await DownloadJava();
                gbSettings.Controls.Add(btnDownloadJava);
            }
            catch (Exception ex)
            {
                Log($"Java自动检测失败: {ex.Message}");
            }
        }

        private string GetJavaPathFromRegistry()
        {
            try
            {
                // 检查64位注册表
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\JavaSoft\Java Runtime Environment"))
                {
                    if (key != null)
                    {
                        var currentVersion = key.GetValue("CurrentVersion")?.ToString();
                        if (currentVersion != null)
                        {
                            using (var versionKey = key.OpenSubKey(currentVersion))
                            {
                                if (versionKey != null)
                                {
                                    var javaHome = versionKey.GetValue("JavaHome")?.ToString();
                                    if (javaHome != null)
                                    {
                                        return Path.Combine(javaHome, "bin", "java.exe");
                                    }
                                }
                            }
                        }
                    }
                }

                // 检查32位注册表
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\JavaSoft\Java Runtime Environment"))
                {
                    if (key != null)
                    {
                        var currentVersion = key.GetValue("CurrentVersion")?.ToString();
                        if (currentVersion != null)
                        {
                            using (var versionKey = key.OpenSubKey(currentVersion))
                            {
                                if (versionKey != null)
                                {
                                    var javaHome = versionKey.GetValue("JavaHome")?.ToString();
                                    if (javaHome != null)
                                    {
                                        return Path.Combine(javaHome, "bin", "java.exe");
                                    }
                                }
                            }
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
        #endregion

        #region 窗口大小调整事件
        private void MainForm_Resize(object sender, EventArgs e)
        {
            // 调整控件位置和大小以适应新窗口尺寸
            if (this.WindowState == FormWindowState.Minimized)
                return;

            var pnlHeader = this.Controls.OfType<Panel>().FirstOrDefault(p => p.Height >= 48);
            var pnlContent = this.Controls.OfType<Panel>().FirstOrDefault(p => p.AutoScroll);

            if (pnlHeader != null)
                pnlHeader.Width = this.Width;
            if (pnlContent != null)
            {
                pnlContent.Size = new Size(this.Width, this.Height - pnlHeader.Height);
                foreach (Control ctrl in pnlContent.Controls)
                {
                    if (ctrl is GroupBox gb)
                        gb.Width = pnlContent.Width - 2 * 15;
                    if (ctrl is Panel panel)
                        panel.Width = pnlContent.Width - 2 * 15;
                }
            }
        }
        #endregion

        #region 状态与日志管理
        private void Log(string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(Log), message);
                return;
            }

            if (_configManager.GetConfig().EnableLogging)
            {
                string logLine = $"[{DateTime.Now:HH:mm:ss}] {message}\r\n";
                _txtLog.AppendText(logLine);
                _txtLog.ScrollToCaret();

                // 保存到日志文件
                try
                {
                    string logDir = Path.Combine(_mcRootPath, "launcher_logs");
                    if (!Directory.Exists(logDir))
                        Directory.CreateDirectory(logDir);

                    string logFile = Path.Combine(logDir, $"launcher_{DateTime.Now:yyyyMMdd}.log");
                    File.AppendAllText(logFile, logLine, Encoding.UTF8);
                }
                catch { }
            }
        }

        private void UpdateStatus(string message, bool isError = false)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string, bool>(UpdateStatus), message, isError);
                return;
            }

            var lblStatus = this.Controls.Find("lblStatus", true).FirstOrDefault() as Label;
            if (lblStatus != null)
            {
                lblStatus.Text = $"状态: {message}";
                lblStatus.ForeColor = isError ? Color.Red : (message.Contains("成功") || message.Contains("就绪") ? Color.Green : Color.Black);
            }
        }

        private void UpdateProgress(int percentage)
        {
            if (_progressBar.InvokeRequired)
            {
                _progressBar.Invoke(new Action<int>(UpdateProgress), percentage);
                return;
            }

            _progressBar.Visible = true;
            _progressBar.Value = percentage;
            UpdateStatus($"进度: {percentage}%");
        }
        #endregion

        #region 版本管理
        private void LoadLocalVersions(ComboBox comboBox)
        {
            try
            {
                comboBox.Items.Clear();
                _localVersions.Clear();

                string versionsDir = Path.Combine(_mcRootPath, "versions");
                if (!Directory.Exists(versionsDir))
                {
                    Log("版本目录不存在");
                    return;
                }

                foreach (var dir in Directory.GetDirectories(versionsDir))
                {
                    string versionId = Path.GetFileName(dir);
                    string jsonPath = Path.Combine(dir, $"{versionId}.json");

                    if (File.Exists(jsonPath))
                    {
                        try
                        {
                            string jsonContent = File.ReadAllText(jsonPath);
                            var version = JsonConvert.DeserializeObject<McVersion>(jsonContent);
                            if (version != null)
                            {
                                _localVersions.Add(version);
                                comboBox.Items.Add(version);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"解析版本 {versionId} 失败: {ex.Message}");
                        }
                    }
                }

                // 选中配置中保存的版本
                var config = _configManager.GetConfig();
                if (!string.IsNullOrEmpty(config.SelectedVersion))
                {
                    for (int i = 0; i < comboBox.Items.Count; i++)
                    {
                        if (((McVersion)comboBox.Items[i]).Id == config.SelectedVersion)
                        {
                            comboBox.SelectedIndex = i;
                            break;
                        }
                    }
                }

                _versionLabel.Text = $"共 {comboBox.Items.Count} 个本地版本";
                Log($"加载了 {comboBox.Items.Count} 个本地版本");
            }
            catch (Exception ex)
            {
                Log($"加载本地版本失败: {ex.Message}");
            }
        }

        private async Task RefreshOfficialVersions(ComboBox comboBox)
        {
            try
            {
                UpdateStatus("正在获取官方版本列表...");
                comboBox.Items.Clear();
                _officialVersions.Clear();

                string manifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest.json";
                var response = await _httpClient.GetAsync(manifestUrl);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                var manifest = JsonConvert.DeserializeObject<VersionManifest>(json);

                if (manifest?.Versions != null)
                {
                    _officialVersions = manifest.Versions.OrderByDescending(v => v.ReleaseTime).ToList();
                    foreach (var version in _officialVersions)
                    {
                        comboBox.Items.Add(version);
                    }

                    // 默认选中最新正式版
                    var latestRelease = manifest.Latest.Release;
                    for (int i = 0; i < comboBox.Items.Count; i++)
                    {
                        if (((VersionInfo)comboBox.Items[i]).Id == latestRelease)
                        {
                            comboBox.SelectedIndex = i;
                            break;
                        }
                    }

                    Log($"获取到 {_officialVersions.Count} 个官方版本");
                    UpdateStatus("官方版本列表获取成功");
                }
            }
            catch (Exception ex)
            {
                Log($"获取官方版本列表失败: {ex.Message}");
                UpdateStatus("获取官方版本列表失败", true);
            }
        }

        private async Task DownloadAndInstallVersion(VersionInfo versionInfo)
        {
            try
            {
                UpdateStatus($"正在下载版本 {versionInfo.Id}...");
                _progressBar.Visible = true;
                _progressBar.Value = 0;

                // 下载版本JSON
                var response = await _httpClient.GetAsync(versionInfo.Url);
                response.EnsureSuccessStatusCode();
                string versionJson = await response.Content.ReadAsStringAsync();
                UpdateProgress(20);

                // 解析版本信息
                var version = JsonConvert.DeserializeObject<McVersion>(versionJson);
                if (version == null)
                {
                    throw new Exception("版本信息解析失败");
                }

                // 创建版本目录
                string versionDir = Path.Combine(_mcRootPath, "versions", version.Id);
                EnsureDirectoryExists(versionDir);
                File.WriteAllText(Path.Combine(versionDir, $"{version.Id}.json"), versionJson);
                UpdateProgress(30);

                // 下载客户端JAR
                if (!string.IsNullOrEmpty(version.Downloads.Client.Url))
                {
                    string jarPath = Path.Combine(versionDir, $"{version.Id}.jar");
                    await DownloadFile(version.Downloads.Client.Url, jarPath, 30, 70);
                }
                UpdateProgress(70);

                // 下载依赖库
                await _versionDownloader.DownloadLibraries(version);
                UpdateProgress(85);

                // 下载资源索引
                if (version.AssetIndex != null && !string.IsNullOrEmpty(version.AssetIndex.Url))
                {
                    await _versionDownloader.DownloadAssetIndex(version.AssetIndex);
                }
                UpdateProgress(95);

                // 刷新本地版本列表
                LoadLocalVersions(_cboLocalVersion);
                UpdateProgress(100);
                UpdateStatus($"版本 {version.Id} 下载安装成功");
                Log($"版本 {version.Id} 下载安装完成");
            }
            catch (Exception ex)
            {
                Log($"版本下载失败: {ex.Message}");
                UpdateStatus("版本下载失败", true);
            }
            finally
            {
                _progressBar.Visible = false;
            }
        }

        private async Task DownloadFile(string url, string savePath, int startProgress, int endProgress)
        {
            try
            {
                var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? -1;
                long downloadedBytes = 0;

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(savePath, FileMode.Create))
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        downloadedBytes += bytesRead;

                        if (totalBytes > 0)
                        {
                            int progress = (int)(startProgress + (endProgress - startProgress) * (double)downloadedBytes / totalBytes);
                            UpdateProgress(progress);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"文件下载失败 {url}: {ex.Message}");
                throw;
            }
        }
        #endregion

        #region 账户与皮肤管理
        private void LoadAccounts()
        {
            try
            {
                string accountsPath = Path.Combine(_mcRootPath, "launcher_profiles", "accounts.json");
                if (File.Exists(accountsPath))
                {
                    string json = File.ReadAllText(accountsPath);
                    _accounts = JsonConvert.DeserializeObject<List<Account>>(json) ?? new List<Account>();
                }
                else
                {
                    // 添加默认账户
                    _accounts = new List<Account>
                    {
                        new Account { Username = _configManager.GetConfig().Username }
                    };
                    SaveAccounts();
                }

                _cboAccounts.Items.Clear();
                foreach (var account in _accounts)
                {
                    _cboAccounts.Items.Add(account);
                }

                if (_cboAccounts.Items.Count > 0)
                {
                    _cboAccounts.SelectedIndex = 0;
                    _selectedAccountId = _accounts[0].Id;
                    UpdateSkinPreview(_accounts[0]);
                }
            }
            catch (Exception ex)
            {
                Log($"加载账户失败: {ex.Message}");
                _accounts = new List<Account>();
            }
        }

        private void SaveAccounts()
        {
            try
            {
                string accountsDir = Path.Combine(_mcRootPath, "launcher_profiles");
                EnsureDirectoryExists(accountsDir);
                string accountsPath = Path.Combine(accountsDir, "accounts.json");
                File.WriteAllText(accountsPath, JsonConvert.SerializeObject(_accounts, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Log($"保存账户失败: {ex.Message}");
            }
        }

        private void ShowAddAccountDialog()
        {
            using (var dialog = new Form
            {
                Text = "添加账户",
                Size = new Size(300, 200),
                StartPosition = FormStartPosition.CenterParent
            })
            {
                Label lblUsername = new Label { Text = "用户名:", Location = new Point(20, 30) };
                TextBox txtUsername = new TextBox { Location = new Point(100, 30), Size = new Size(150, 25) };
                CheckBox chkOffline = new CheckBox { Text = "离线账户", Location = new Point(100, 60), Checked = true };
                Button btnAdd = new Button { Text = "添加", Location = new Point(100, 100), Size = new Size(80, 30) };
                Button btnCancel = new Button { Text = "取消", Location = new Point(200, 100), Size = new Size(80, 30) };

                btnAdd.Click += (s, e) =>
                {
                    string username = txtUsername.Text.Trim();
                    if (string.IsNullOrEmpty(username))
                    {
                        MessageBox.Show("请输入用户名", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    var account = new Account
                    {
                        Username = username,
                        IsOffline = chkOffline.Checked
                    };

                    _accounts.Add(account);
                    SaveAccounts();
                    LoadAccounts();
                    dialog.Close();
                };

                btnCancel.Click += (s, e) => dialog.Close();

                dialog.Controls.Add(lblUsername);
                dialog.Controls.Add(txtUsername);
                dialog.Controls.Add(chkOffline);
                dialog.Controls.Add(btnAdd);
                dialog.Controls.Add(btnCancel);

                dialog.ShowDialog(this);
            }
        }

        private void RemoveSelectedAccount()
        {
            if (_cboAccounts.SelectedItem is Account account && _accounts.Count > 1)
            {
                if (MessageBox.Show($"确定要删除账户 {account.Username} 吗?", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _accounts.Remove(account);
                    SaveAccounts();
                    LoadAccounts();
                }
            }
            else if (_accounts.Count <= 1)
            {
                MessageBox.Show("至少保留一个账户", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void UpdateSkinPreview(Account account)
        {
            try
            {
                if (!string.IsNullOrEmpty(account.SkinPath) && File.Exists(account.SkinPath))
                {
                    _pbSkinPreview.Image = Image.FromFile(account.SkinPath);
                }
                else if (!string.IsNullOrEmpty(account.Username))
                {
                    // 尝试加载默认皮肤或在线皮肤
                    _pbSkinPreview.Image = _skinManager.GetDefaultSkin(account.Username);
                }
            }
            catch
            {
                _pbSkinPreview.Image = _skinManager.GetDefaultSkin("default");
            }
        }

        private void ChangeSkinForSelectedAccount()
        {
            var selectedAccount = _cboAccounts.SelectedItem as Account;
            if (selectedAccount == null) return;

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "皮肤文件|*.png";
                ofd.Title = "选择皮肤文件";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // 验证皮肤尺寸
                        using (var img = Image.FromFile(ofd.FileName))
                        {
                            if ((img.Width == 64 && img.Height == 32) || (img.Width == 64 && img.Height == 64) ||
                                (img.Width == 128 && img.Height == 64) || (img.Width == 128 && img.Height == 128))
                            {
                                // 保存皮肤副本
                                string skinDir = Path.Combine(_mcRootPath, "skins");
                                EnsureDirectoryExists(skinDir);
                                string skinPath = Path.Combine(skinDir, $"{selectedAccount.Id}.png");
                                File.Copy(ofd.FileName, skinPath, true);

                                // 更新账户信息
                                selectedAccount.SkinPath = skinPath;
                                SaveAccounts();
                                UpdateSkinPreview(selectedAccount);
                                Log($"已更换 {selectedAccount.Username} 的皮肤");
                            }
                            else
                            {
                                MessageBox.Show("无效的皮肤文件尺寸，正确尺寸应为64x32, 64x64, 128x64或128x128", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"更换皮肤失败: {ex.Message}");
                        MessageBox.Show($"更换皮肤失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
        #endregion

        #region 核心方法
        private async Task FixMissingFiles()
        {
            var selectedVersion = _cboLocalVersion.SelectedItem as McVersion;
            if (selectedVersion == null)
            {
                MessageBox.Show("请选择版本", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                _btnFixFiles.Enabled = false;
                UpdateStatus($"正在补全 {selectedVersion.Id} 文件...");
                Log($"开始补全版本 {selectedVersion.Id}");

                var versionDir = Path.Combine(_mcRootPath, "versions", selectedVersion.Id);
                var jarPath = Path.Combine(versionDir, $"{selectedVersion.Id}.jar");
                var jsonPath = Path.Combine(versionDir, $"{selectedVersion.Id}.json");

                // 下载核心文件
                if (!File.Exists(jsonPath) && !string.IsNullOrEmpty(selectedVersion.Downloads.Client.Url))
                {
                    await DownloadFile(selectedVersion.Downloads.Client.Url.Replace(".jar", ".json"), jsonPath, 10, 20);
                }

                if (!File.Exists(jarPath) && !string.IsNullOrEmpty(selectedVersion.Downloads.Client.Url))
                {
                    await DownloadFile(selectedVersion.Downloads.Client.Url, jarPath, 20, 40);
                }

                // 补全依赖库
                await _versionDownloader.DownloadLibraries(selectedVersion);

                // 补全资源
                if (selectedVersion.AssetIndex != null && !string.IsNullOrEmpty(selectedVersion.AssetIndex.Url))
                {
                    await _versionDownloader.DownloadAssetIndex(selectedVersion.AssetIndex);
                    await _versionDownloader.DownloadAssets(selectedVersion.AssetIndex.Id);
                }

                UpdateStatus($"{selectedVersion.Id} 文件补全完成");
                Log($"{selectedVersion.Id} 文件补全成功");
            }
            catch (Exception ex)
            {
                UpdateStatus("文件补全失败", true);
                Log($"文件补全失败: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"补全失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnFixFiles.Enabled = true;
            }
        }

        private async Task CheckFiles(McComboBox comboBox)
        {
            var selectedVersion = comboBox.SelectedItem as McVersion;
            if (selectedVersion == null)
            {
                MessageBox.Show("请选择版本", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                UpdateStatus($"正在检查 {selectedVersion.Id} 文件完整性...");
                Log($"开始检查版本 {selectedVersion.Id} 文件");

                int missingCount = 0;

                // 检查核心文件
                var jarPath = Path.Combine(_mcRootPath, "versions", selectedVersion.Id, $"{selectedVersion.Id}.jar");
                if (!File.Exists(jarPath))
                {
                    Log($"缺失核心文件: {jarPath}");
                    missingCount++;
                }

                // 检查依赖库
                foreach (var lib in selectedVersion.Libraries)
                {
                    if (lib.Downloads?.Artifact != null && !string.IsNullOrEmpty(lib.Downloads.Artifact.Path))
                    {
                        string libPath = Path.Combine(_mcRootPath, "libraries", lib.Downloads.Artifact.Path.Replace('/', '\\'));
                        if (!File.Exists(libPath))
                        {
                            Log($"缺失依赖库: {libPath} (库名: {lib.Name})");
                            missingCount++;
                        }
                    }
                }

                // 检查资源索引
                if (selectedVersion.AssetIndex != null)
                {
                    string assetIndexPath = Path.Combine(_mcRootPath, "assets", "indexes", $"{selectedVersion.AssetIndex.Id}.json");
                    if (!File.Exists(assetIndexPath))
                    {
                        Log($"缺失资源索引: {assetIndexPath}");
                        missingCount++;
                    }
                }

                UpdateStatus($"文件检查完成，共发现 {missingCount} 个缺失文件");
                Log($"文件检查完成，缺失 {missingCount} 个文件");

                if (missingCount > 0)
                {
                    if (MessageBox.Show($"发现 {missingCount} 个缺失文件，是否立即补全？", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        await FixMissingFiles();
                    }
                }
                else
                {
                    MessageBox.Show("所有文件完整", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus("文件检查失败", true);
                Log($"文件检查失败: {ex.Message}");
                MessageBox.Show($"检查失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClearCache()
        {
            try
            {
                UpdateStatus("正在清理缓存...");

                // 清理下载缓存
                string cacheDir = Path.Combine(_mcRootPath, "launcher_cache");
                if (Directory.Exists(cacheDir))
                {
                    Directory.Delete(cacheDir, true);
                    Log("下载缓存已清理");
                }

                // 清理临时文件
                string tempDir = Path.Combine(_mcRootPath, "temp");
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                    Log("临时文件已清理");
                }

                // 清理日志
                string logDir = Path.Combine(_mcRootPath, "launcher_logs");
                if (Directory.Exists(logDir))
                {
                    foreach (var file in Directory.GetFiles(logDir, "*.log").Where(f => File.GetCreationTime(f) < DateTime.Now.AddDays(-7)))
                    {
                        File.Delete(file);
                        Log($"已删除旧日志: {Path.GetFileName(file)}");
                    }
                }

                UpdateStatus("缓存清理完成");
                MessageBox.Show("缓存清理完成", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                UpdateStatus("缓存清理失败", true);
                Log($"清理缓存失败: {ex.Message}");
                MessageBox.Show($"清理失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveCurrentConfig(TextBox txtUsername, TextBox txtMinMem, TextBox txtMaxMem, TextBox txtJavaPath, CheckBox chkOfflineMode, CheckBox chkIgnoreJavaCheck, ComboBox cboLocalVersion, TextBox txtGameDir, CheckBox chkFullscreen)
        {
            try
            {
                var config = _configManager.GetConfig();
                config.Username = txtUsername.Text.Trim();
                config.JavaPath = txtJavaPath.Text.Trim();
                config.IsOfflineMode = chkOfflineMode.Checked;
                config.IgnoreJavaVersionCheck = chkIgnoreJavaCheck?.Checked ?? false;
                config.GameDir = txtGameDir.Text.Trim();
                config.WindowWidth = (int)_nudWidth.Value;
                config.WindowHeight = (int)_nudHeight.Value;
                config.Fullscreen = chkFullscreen?.Checked ?? false;

                if (int.TryParse(txtMinMem.Text.Trim(), out int minMem) && minMem >= 512)
                    config.MinMemory = minMem;

                if (int.TryParse(txtMaxMem.Text.Trim(), out int maxMem) && maxMem >= config.MinMemory)
                    config.MaxMemory = maxMem;

                if (cboLocalVersion.SelectedItem is McVersion selectedVersion)
                    config.SelectedVersion = selectedVersion.Id;

                _configManager.SaveConfig();
                Log("配置已保存");
                UpdateStatus("配置保存成功");
                MessageBox.Show("配置已保存", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log($"保存配置失败: {ex.Message}");
                MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LaunchGame()
        {
            try
            {
                // 获取控件
                var txtUsername = this.Controls.Find("txtUsername", true).FirstOrDefault() as TextBox;
                var txtMinMem = this.Controls.Find("txtMinMem", true).FirstOrDefault() as TextBox;
                var txtMaxMem = this.Controls.Find("txtMaxMem", true).FirstOrDefault() as TextBox;
                var txtJavaPath = this.Controls.Find("txtJavaPath", true).FirstOrDefault() as TextBox;
                var chkOfflineMode = this.Controls.Find("chkOfflineMode", true).FirstOrDefault() as CheckBox;
                var chkIgnoreJavaCheck = this.Controls.Find("chkIgnoreJavaCheck", true).FirstOrDefault() as CheckBox;

                // 空值校验
                if (txtUsername == null || txtMinMem == null || txtMaxMem == null || txtJavaPath == null || chkOfflineMode == null || chkIgnoreJavaCheck == null)
                {
                    Log("错误：控件加载失败");
                    MessageBox.Show("界面初始化异常，请重启启动器", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var selectedVersion = _cboLocalVersion.SelectedItem as McVersion;
                if (selectedVersion == null)
                {
                    MessageBox.Show("请选择本地游戏版本", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 基础参数校验
                string username = txtUsername.Text.Trim();
                if (string.IsNullOrEmpty(username))
                {
                    MessageBox.Show("请输入用户名", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (!int.TryParse(txtMinMem.Text.Trim(), out int minMem) || minMem < 512)
                {
                    MessageBox.Show("最小内存必须≥512MB", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (!int.TryParse(txtMaxMem.Text.Trim(), out int maxMem) || maxMem < minMem)
                {
                    MessageBox.Show("最大内存必须≥最小内存", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                string javaExePath = txtJavaPath.Text.Trim();
                if (!File.Exists(javaExePath))
                {
                    MessageBox.Show($"Java路径不存在：{javaExePath}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Java版本检查
                if (!chkIgnoreJavaCheck.Checked)
                {
                    var javaVersion = GetJavaVersion(javaExePath);
                    Log($"检测到Java版本: {javaVersion}");

                    if (IsJavaVersionIncompatible(javaVersion, selectedVersion))
                    {
                        if (MessageBox.Show($"检测到不兼容的Java版本 ({javaVersion})，可能导致游戏无法启动。是否继续？", "警告", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                        {
                            return;
                        }
                    }
                }

                // 登录处理
                string uuid = "";
                string token = "";

                // 使用选中的账户信息
                if (_cboAccounts.SelectedItem is Account selectedAccount)
                {
                    username = selectedAccount.Username;
                    uuid = selectedAccount.Uuid;
                    token = selectedAccount.Token;
                }
                else if (chkOfflineMode.Checked)
                {
                    var offlineLogin = _loginManager.OfflineLogin(username);
                    username = offlineLogin.username;
                    uuid = offlineLogin.uuid;
                    token = offlineLogin.token;
                    Log($"离线登录成功: {username} (UUID: {uuid})");
                }
                else
                {
                    Log("提示：Mojang登录API已停用，建议使用离线模式");
                    MessageBox.Show("Mojang登录API已停用，请使用离线模式", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 检查核心JAR
                string jarPath = Path.Combine(_mcRootPath, "versions", selectedVersion.Id, $"{selectedVersion.Id}.jar");
                if (!File.Exists(jarPath))
                {
                    MessageBox.Show($"核心文件缺失：{jarPath}\n请点击「补全缺失文件」修复", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 准备启动
                UpdateStatus("正在启动游戏...");
                string nativesDir = Path.Combine(_mcRootPath, "versions", selectedVersion.Id, $"{selectedVersion.Id}-natives");
                if (!Directory.Exists(nativesDir)) Directory.CreateDirectory(nativesDir);

                // 构建参数
                var finalArgs = BuildLaunchArguments(selectedVersion, username, uuid, token, minMem, maxMem);

                // 启动进程
                Process gameProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = javaExePath,
                        Arguments = string.Join(" ", finalArgs),
                        WorkingDirectory = _mcRootPath,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = false,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    },
                    EnableRaisingEvents = true
                };

                gameProcess.OutputDataReceived += (s, ev) => { if (!string.IsNullOrEmpty(ev.Data)) Log(ev.Data); };
                gameProcess.ErrorDataReceived += (s, ev) => { if (!string.IsNullOrEmpty(ev.Data)) Log($"[错误] {ev.Data}"); };
                gameProcess.Exited += (s, ev) =>
                {
                    Log($"游戏进程退出 (退出码: {gameProcess.ExitCode})");
                    UpdateStatus(gameProcess.ExitCode == 0 ? "游戏正常退出" : "游戏异常退出", gameProcess.ExitCode != 0);
                    gameProcess.Dispose();
                };

                gameProcess.Start();
                gameProcess.BeginOutputReadLine();
                gameProcess.BeginErrorReadLine();

                Log($"游戏进程启动成功 (PID: {gameProcess.Id})");
                UpdateStatus("游戏已启动");
                this.WindowState = FormWindowState.Minimized;
            }
            catch (Exception ex)
            {
                UpdateStatus("启动失败", true);
                Log($"启动失败: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"启动失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool IsJavaVersionIncompatible(string javaVersion, McVersion mcVersion)
        {
            try
            {
                // 简单的版本兼容判断逻辑
                var javaVerMatch = Regex.Match(javaVersion, @"^(\d+)\.");
                if (!javaVerMatch.Success) return false;

                int javaMajorVersion = int.Parse(javaVerMatch.Groups[1].Value);

                // Minecraft 1.17+ 需要 Java 16+
                if (mcVersion.Id.StartsWith("1.17") && javaMajorVersion < 16)
                    return true;

                // Minecraft 1.18+ 需要 Java 17+
                if (mcVersion.Id.StartsWith("1.18") && javaMajorVersion < 17)
                    return true;

                // 更早版本通常需要 Java 8
                if (javaMajorVersion > 8 && (mcVersion.Id.StartsWith("1.12") || mcVersion.Id.StartsWith("1.11") || mcVersion.Id.StartsWith("1.10")))
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        private string GetJavaVersion(string javaPath)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = javaPath,
                        Arguments = "-version",
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                var output = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return Regex.Match(output, @"version ""([^""]+)""").Groups[1].Value;
            }
            catch { return "未知版本"; }
        }
        #endregion

        #region 辅助方法
        private List<string> BuildLaunchArguments(McVersion version, string username, string uuid, string token, int minMem, int maxMem)
        {
            var jvmArgs = new List<string>();
            string nativesDir = Path.Combine(_mcRootPath, "versions", version.Id, $"{version.Id}-natives");
            string classpath = GetClasspath(version);

            // JVM参数（纯Java虚拟机参数）
            jvmArgs.Add($"-Xms{minMem}M");
            jvmArgs.Add($"-Xmx{maxMem}M");
            jvmArgs.Add($"-Djava.library.path={nativesDir}");
            jvmArgs.Add("-cp");
            jvmArgs.Add(classpath);
            jvmArgs.Add("-Dfile.encoding=UTF-8");
            jvmArgs.Add("-Dsun.awt.noerasebackground=true");
            jvmArgs.Add("-Dsun.java2d.noddraw=true");

            // 强制固定主类（核心修复）
            string mainClass = "net.minecraft.client.main.Main";
            if (!string.IsNullOrEmpty(version.MainClass) && version.MainClass.StartsWith("net.minecraft"))
            {
                mainClass = version.MainClass;
            }

            // 游戏参数（去重，仅添加一次）
            var gameArgs = new List<string>
            {
                "--username", username,
                "--version", version.Id,
                "--gameDir", _mcRootPath,
                "--assetsDir", Path.Combine(_mcRootPath, "assets"),
                "--assetIndex", version.AssetIndex?.Id ?? version.Assets,
                "--uuid", uuid,
                "--accessToken", token,
                "--userType", "mojang",
                "--versionType", version.Type ?? "release",
                "--width", _nudWidth.Value.ToString(),
                "--height", _nudHeight.Value.ToString(),
                "--fullscreen", _chkFullscreen.Checked.ToString().ToLower(),
                "--language", _configManager.GetConfig().Language
            };

            // 合并参数（严格顺序：JVM → 主类 → 游戏参数）
            var finalArgs = new List<string>();
            finalArgs.AddRange(jvmArgs);
            finalArgs.Add(mainClass);
            finalArgs.AddRange(gameArgs);

            // 日志输出参数验证
            Log($"=== 参数构建完成 ===");
            Log($"主类: {mainClass}");
            Log($"游戏版本: {version.Id}");
            Log($"启动参数总数: {finalArgs.Count}");
            Log($"完整参数: {string.Join(" ", finalArgs.Take(20))}..."); // 只打印前20个避免过长

            return finalArgs.Where(arg => !string.IsNullOrWhiteSpace(arg)).ToList();
        }

        private string GetClasspath(McVersion version)
        {
            var classpathList = new List<string>();
            string libsDir = Path.Combine(_mcRootPath, "libraries");
            string versionJar = Path.Combine(_mcRootPath, "versions", version.Id, $"{version.Id}.jar");

            // 添加核心JAR
            if (File.Exists(versionJar))
            {
                classpathList.Add(versionJar);
            }
            else
            {
                Log($"警告：核心JAR缺失 {versionJar}");
            }

            // 添加依赖库
            if (version.Libraries != null)
            {
                foreach (var lib in version.Libraries)
                {
                    if (lib.Downloads?.Artifact != null && !string.IsNullOrEmpty(lib.Downloads.Artifact.Path))
                    {
                        string libPath = Path.Combine(libsDir, lib.Downloads.Artifact.Path.Replace('/', '\\'));
                        if (File.Exists(libPath))
                        {
                            classpathList.Add(libPath);
                        }
                        else
                        {
                            Log($"警告：依赖库缺失 {libPath} (库名: {lib.Name})");
                        }
                    }
                }
            }

            // 拼接类路径
            string classpath = string.Join(Path.PathSeparator.ToString(), classpathList);
            Log($"类路径构建完成：{classpathList.Count} 个文件");
            return classpath;
        }
        #endregion

        #region 窗体初始化
        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ClientSize = new Size(800, 700);
            this.Name = "MainForm";
            this.Load += new EventHandler(this.MainForm_Load);
            this.ResumeLayout(false);
        }

        private void MainForm_Load(object sender, EventArgs e) { }
        #endregion
    }

    #region 工具类
    public class ConfigManager
    {
        private readonly string _configPath;
        private LauncherConfig _config;

        public ConfigManager(string gameDir)
        {
            string configDir = Path.Combine(gameDir, "launcher_profiles");
            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);

            _configPath = Path.Combine(configDir, "launcher_config.json");
            LoadConfig();
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    _config = JsonConvert.DeserializeObject<LauncherConfig>(json);
                }

                if (_config == null)
                    _config = new LauncherConfig();
            }
            catch
            {
                _config = new LauncherConfig();
            }
        }

        public void SaveConfig()
        {
            try
            {
                File.WriteAllText(_configPath, JsonConvert.SerializeObject(_config, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存配置失败: {ex.Message}");
            }
        }

        public LauncherConfig GetConfig() => _config;

        public void UpdateConfig(Action<LauncherConfig> updateAction)
        {
            updateAction(_config);
            SaveConfig();
        }
    }

    public class LoginManager
    {
        private readonly Action<string> _logCallback;

        public LoginManager(Action<string> logCallback)
        {
            _logCallback = logCallback;
        }

        public (string username, string uuid, string token) OfflineLogin(string username)
        {
            username = string.IsNullOrEmpty(username) ? "Player" : username;
            var uuid = Guid.NewGuid().ToString().Replace("-", "");
            return (username, uuid, "0");
        }

        public async Task<(bool success, string username, string uuid, string token)> MojangLoginAsync(string email, string password)
        {
            // Mojang登录API已停用，返回失败
            _logCallback("Mojang登录API已停用，无法使用在线登录");
            return (false, "", "", "");
        }
    }

    public class VersionDownloader
    {
        private string _mcRootPath;
        private readonly Action<string> _log;
        private readonly Action<int> _updateProgress;
        private readonly HttpClient _httpClient = new HttpClient();

        public VersionDownloader(string mcRootPath, Action<string> logCallback, Action<int> progressCallback)
        {
            _mcRootPath = mcRootPath;
            _log = logCallback;
            _updateProgress = progressCallback;
        }

        public void UpdateGameDir(string newGameDir)
        {
            _mcRootPath = newGameDir;
        }

        public async Task DownloadLibraries(McVersion version)
        {
            if (version.Libraries == null || !version.Libraries.Any())
            {
                _log("没有需要下载的依赖库");
                return;
            }

            string libsDir = Path.Combine(_mcRootPath, "libraries");
            int total = version.Libraries.Count;
            int downloaded = 0;

            foreach (var lib in version.Libraries)
            {
                downloaded++;
                int progress = (int)((double)downloaded / total * 30) + 40; // 40-70%区间
                _updateProgress(progress);

                if (lib.Downloads?.Artifact == null || string.IsNullOrEmpty(lib.Downloads.Artifact.Url))
                    continue;

                string libPath = Path.Combine(libsDir, lib.Downloads.Artifact.Path.Replace('/', '\\'));
                if (File.Exists(libPath))
                {
                    // 校验文件哈希
                    if (await VerifyFileHash(libPath, lib.Downloads.Artifact.Sha1))
                    {
                        _log($"依赖库 {lib.Name} 已存在且完整");
                        continue;
                    }
                    _log($"依赖库 {lib.Name} 哈希校验失败，重新下载");
                }

                try
                {
                    _log($"正在下载依赖库: {lib.Name} ({downloaded}/{total})");
                    EnsureDirectoryExists(Path.GetDirectoryName(libPath));

                    using (var response = await _httpClient.GetAsync(lib.Downloads.Artifact.Url))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(libPath, FileMode.Create))
                        {
                            await stream.CopyToAsync(fileStream);
                        }
                    }

                    // 下载完成后再次校验
                    if (!await VerifyFileHash(libPath, lib.Downloads.Artifact.Sha1))
                    {
                        throw new Exception("依赖库下载后校验失败");
                    }
                }
                catch (Exception ex)
                {
                    _log($"下载依赖库 {lib.Name} 失败: {ex.Message}");
                }
            }
        }

        public async Task DownloadAssetIndex(AssetIndex assetIndex)
        {
            try
            {
                string indexDir = Path.Combine(_mcRootPath, "assets", "indexes");
                EnsureDirectoryExists(indexDir);
                string indexPath = Path.Combine(indexDir, $"{assetIndex.Id}.json");

                if (File.Exists(indexPath))
                {
                    if (await VerifyFileHash(indexPath, assetIndex.Sha1))
                    {
                        _log($"资源索引 {assetIndex.Id} 已存在且完整");
                        return;
                    }
                    _log($"资源索引 {assetIndex.Id} 哈希校验失败，重新下载");
                }

                _log($"正在下载资源索引: {assetIndex.Id}");
                using (var response = await _httpClient.GetAsync(assetIndex.Url))
                {
                    response.EnsureSuccessStatusCode();
                    string content = await response.Content.ReadAsStringAsync();
                    File.WriteAllText(indexPath, content);
                }

                if (!await VerifyFileHash(indexPath, assetIndex.Sha1))
                {
                    throw new Exception("资源索引下载后校验失败");
                }
            }
            catch (Exception ex)
            {
                _log($"下载资源索引失败: {ex.Message}");
                throw;
            }
        }

        public async Task DownloadAssets(string assetIndexId)
        {
            try
            {
                string indexPath = Path.Combine(_mcRootPath, "assets", "indexes", $"{assetIndexId}.json");
                if (!File.Exists(indexPath))
                {
                    _log($"资源索引 {assetIndexId} 不存在，无法下载资源");
                    return;
                }

                string json = File.ReadAllText(indexPath);
                var assets = JsonConvert.DeserializeObject<JObject>(json)?["objects"] as JObject;
                if (assets == null || !assets.HasValues)
                {
                    _log("资源索引中没有找到资源信息");
                    return;
                }

                string assetsDir = Path.Combine(_mcRootPath, "assets", "objects");
                EnsureDirectoryExists(assetsDir);

                int total = assets.Count;
                int downloaded = 0;

                foreach (var asset in assets)
                {
                    downloaded++;
                    int progress = (int)((double)downloaded / total * 15) + 70; // 70-85%区间
                    _updateProgress(progress);

                    string hash = asset.Value["hash"].ToString();
                    string assetPath = Path.Combine(assetsDir, hash.Substring(0, 2), hash);

                    if (File.Exists(assetPath))
                    {
                        // 校验文件大小
                        long expectedSize = asset.Value["size"].Value<long>();
                        if (new FileInfo(assetPath).Length == expectedSize)
                        {
                            continue;
                        }
                        _log($"资源 {asset.Key} 大小不匹配，重新下载");
                    }

                    try
                    {
                        string url = $"https://resources.download.minecraft.net/{hash.Substring(0, 2)}/{hash}";
                        EnsureDirectoryExists(Path.GetDirectoryName(assetPath));

                        using (var response = await _httpClient.GetAsync(url))
                        {
                            response.EnsureSuccessStatusCode();
                            using (var stream = await response.Content.ReadAsStreamAsync())
                            using (var fileStream = new FileStream(assetPath, FileMode.Create))
                            {
                                await stream.CopyToAsync(fileStream);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _log($"下载资源 {asset.Key} 失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _log($"下载资源失败: {ex.Message}");
                throw;
            }
        }

        private async Task<bool> VerifyFileHash(string filePath, string expectedSha1)
        {
            if (string.IsNullOrEmpty(expectedSha1) || !File.Exists(filePath))
                return false;

            try
            {
                using (var sha1 = SHA1.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hashBytes = await Task.Run(() => sha1.ComputeHash(stream));
                    string actualSha1 = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                    return actualSha1 == expectedSha1;
                }
            }
            catch
            {
                return false;
            }
        }

        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }

    public class FileChecker
    {
        private readonly Action<string> _log;

        public FileChecker(Action<string> logCallback)
        {
            _log = logCallback;
        }

        public bool VerifyFileIntegrity(string filePath, string expectedHash)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _log($"文件不存在: {filePath}");
                    return false;
                }

                using (var sha1 = SHA1.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hashBytes = sha1.ComputeHash(stream);
                    string actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                    return actualHash == expectedHash;
                }
            }
            catch (Exception ex)
            {
                _log($"验证文件 {filePath} 失败: {ex.Message}");
                return false;
            }
        }
    }

    public class SkinManager
    {
        private readonly string _mcRootPath;
        private readonly Action<string> _log;
        private readonly Dictionary<string, Image> _skinCache = new Dictionary<string, Image>();

        public SkinManager(string mcRootPath, Action<string> logCallback)
        {
            _mcRootPath = mcRootPath;
            _log = logCallback;
        }

        public Image GetDefaultSkin(string username)
        {
            try
            {
                if (_skinCache.TryGetValue(username, out var cachedSkin))
                    return cachedSkin;

                // 创建简单的默认皮肤
                var bmp = new Bitmap(64, 32);
                using (var g = Graphics.FromImage(bmp))
                {
                    // 皮肤底色 - 使用用户名哈希生成独特颜色
                    int hash = username.GetHashCode();
                    var color = Color.FromArgb(255, (hash & 0xFF), (hash >> 8) & 0xFF, (hash >> 16) & 0xFF);

                    // 头部
                    g.FillRectangle(new SolidBrush(color), 8, 8, 8, 8);
                    // 身体
                    g.FillRectangle(new SolidBrush(color), 20, 20, 8, 12);
                    // 手臂
                    g.FillRectangle(new SolidBrush(color), 4, 20, 8, 12);
                    g.FillRectangle(new SolidBrush(color), 36, 20, 8, 12);
                    // 腿
                    g.FillRectangle(new SolidBrush(color), 16, 32, 8, 12);
                    g.FillRectangle(new SolidBrush(color), 28, 32, 8, 12);
                }

                _skinCache[username] = bmp;
                return bmp;
            }
            catch (Exception ex)
            {
                _log($"生成默认皮肤失败: {ex.Message}");
                return new Bitmap(64, 32);
            }
        }
    }

    // 自定义ComboBox用于版本选择
    public class McComboBox : ComboBox { }
    #endregion

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}