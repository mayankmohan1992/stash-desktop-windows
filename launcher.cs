using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Diagnostics;
using System.Windows.Forms;
using System.Drawing;
using System.Threading;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace StashLauncher
{
    static class Program
    {
        private static Mutex mutex = null;
        
        [STAThread]
        static void Main()
        {
            try
            {
                const string appName = "StashLauncher";
                bool createdNew;
                mutex = new Mutex(true, appName, out createdNew);
                
                if (!createdNew)
                {
                    // App is already running, open browser page and exit
                    try
                    {
                        Process.Start("http://localhost:5173");
                    }
                    catch {}
                    return;
                }
                
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                LauncherController controller = new LauncherController();
                if (controller.Initialize())
                {
                    controller.Start();
                    Application.Run(new ApplicationContext());
                }
            }
            catch (Exception ex)
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string stashDir = Path.Combine(appData, "Stash");
                Directory.CreateDirectory(stashDir);
                File.WriteAllText(Path.Combine(stashDir, "crash.log"), ex.ToString());
            }
        }
    }
    
    public class LauncherController
    {
        private NotifyIcon trayIcon;
        private ContextMenu trayMenu;
        private MenuItem runOnStartupMenuItem;
        
        private Process nodeProcess;
        private string stashDir;
        private string appDir;
        private string nodeDir;
        private string logFile;
        private string shaFile;
        
        private string nodeExePath = "node";
        private string npmCmdPath = "npm";
        private bool isPortableNode = false;
        
        private const string RepoOwner = "mayankmohan1992"; // Replace with your GitHub username
        private const string RepoName = "stash-desktop-windows"; // Replace with your repository name (e.g. stash-desktop-windows)
        private const string LauncherVersion = "1.0.2";
        private const string RegistryStartupKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string AppName = "Stash";

        public LauncherController()
        {
            // Set paths relative to %APPDATA%\Stash
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            stashDir = Path.Combine(appData, "Stash");
            appDir = Path.Combine(stashDir, "app");
            nodeDir = Path.Combine(stashDir, "node");
            logFile = Path.Combine(stashDir, "server.log");
            shaFile = Path.Combine(stashDir, "commit_sha.txt");
            
            Directory.CreateDirectory(stashDir);
            Directory.CreateDirectory(nodeDir);
        }

        public bool Initialize()
        {
            // Hook application exit events to cleanup processes
            Application.ApplicationExit += OnApplicationExit;
            SystemEvents.SessionEnding += OnSessionEnding;
            
            // Try to initialize Node paths (either system or portable)
            if (!SetupNode())
            {
                MessageBox.Show("Could not initialize Node.js environment. Stash requires Node.js to run.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            
            return true;
        }

        public void Start()
        {
            // Initialize Tray Icon
            InitTrayIcon();
            
            // Check for updates and run the server in a background thread to keep UI responsive
            Thread bgThread = new Thread(RunBackgroundStartup);
            bgThread.SetApartmentState(ApartmentState.STA);
            bgThread.IsBackground = true;
            bgThread.Start();
        }

        private void InitTrayIcon()
        {
            trayMenu = new ContextMenu();
            trayMenu.MenuItems.Add("Open Stash", OnOpenStash);
            trayMenu.MenuItems.Add("View Server Logs", OnViewLogs);
            trayMenu.MenuItems.Add("Force Check for Updates", OnForceUpdate);
            
            runOnStartupMenuItem = new MenuItem("Run on Windows Startup", OnToggleStartup);
            runOnStartupMenuItem.Checked = IsRunOnStartupEnabled();
            trayMenu.MenuItems.Add(runOnStartupMenuItem);
            
            trayMenu.MenuItems.Add("-");
            trayMenu.MenuItems.Add("Exit", OnExit);

            trayIcon = new NotifyIcon();
            trayIcon.Text = "Stash - Personal AI Inbox";
            
            // Extract application icon from the running assembly
            try
            {
                trayIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                trayIcon.Icon = SystemIcons.Application;
            }
            
            trayIcon.ContextMenu = trayMenu;
            trayIcon.Visible = true;
            
            // Double click opens the web app
            trayIcon.DoubleClick += OnOpenStash;
        }

        private bool SetupNode()
        {
            // 1. Check if Node is available on system PATH
            if (IsCommandAvailable("node"))
            {
                nodeExePath = "node";
                npmCmdPath = "npm";
                return true;
            }

            // 2. Check if portable Node is already downloaded in AppData
            string portableFolder = Path.Combine(nodeDir, "node-v20.11.1-win-x64");
            string portableExe = Path.Combine(portableFolder, "node.exe");
            string portableNpm = Path.Combine(portableFolder, "npm.cmd");

            if (File.Exists(portableExe) && File.Exists(portableNpm))
            {
                nodeExePath = portableExe;
                npmCmdPath = portableNpm;
                isPortableNode = true;
                AddFolderToPath(portableFolder);
                return true;
            }

            // 3. Download portable Node
            ProgressForm progress = new ProgressForm("Stash First Run Setup");
            bool downloadSuccess = false;
            Exception downloadError = null;

            Thread downloadThread = new Thread(() =>
            {
                try
                {
                    progress.UpdateProgress(5, "Downloading portable Node.js runtime (30MB)...");
                    string zipPath = Path.Combine(nodeDir, "node.zip");
                    
                    DownloadFileWithProgress("https://nodejs.org/dist/v20.11.1/node-v20.11.1-win-x64.zip", zipPath, (pct, msg) => {
                        progress.UpdateProgress(pct, "Downloading Node.js: " + msg);
                    });

                    progress.UpdateProgress(85, "Extracting Node.js runtime...");
                    if (Directory.Exists(portableFolder))
                    {
                        Directory.Delete(portableFolder, true);
                    }
                    
                    ZipFile.ExtractToDirectory(zipPath, nodeDir);
                    File.Delete(zipPath);
                    
                    if (File.Exists(portableExe) && File.Exists(portableNpm))
                    {
                        nodeExePath = portableExe;
                        npmCmdPath = portableNpm;
                        isPortableNode = true;
                        AddFolderToPath(portableFolder);
                        downloadSuccess = true;
                    }
                    else
                    {
                        downloadError = new Exception("Node.js extraction did not produce expected files.");
                    }
                }
                catch (Exception ex)
                {
                    downloadError = ex;
                }
                finally
                {
                    progress.Invoke(new Action(progress.Close));
                }
            });

            downloadThread.Start();
            progress.ShowDialog();

            if (downloadError != null)
            {
                MessageBox.Show("Failed to download Node.js: " + downloadError.Message, "Setup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return downloadSuccess;
        }

        private bool IsCommandAvailable(string cmd)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(cmd, "-v");
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit(2000);
                    return p.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private void AddFolderToPath(string folder)
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("PATH", folder + ";" + pathEnv, EnvironmentVariableTarget.Process);
        }

        private void RunBackgroundStartup()
        {
            try
            {
                // Check updates / setup code
                CheckAndPerformUpdate(false);
                
                // Start Node server
                StartServer();
                
                // Show notification bubble
                trayIcon.ShowBalloonTip(3000, "Stash Running", "Stash has started in the background. Double click the tray icon to open.", ToolTipIcon.Info);
                
                // Wait for port 5173 to be active and launch browser
                WaitForServerAndOpenBrowser();
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(Path.Combine(stashDir, "error.log"), ex.ToString());
                }
                catch {}
                MessageBox.Show("Error during startup: " + ex.Message, "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CheckAndPerformUpdate(bool force)
        {
            string currentSha = File.Exists(shaFile) ? File.ReadAllText(shaFile).Trim() : "";
            string latestSha = "";
            bool hasConnection = true;

            try
            {
                latestSha = FetchLatestCommitSha();
            }
            catch (Exception ex)
            {
                hasConnection = false;
                Debug.WriteLine("Update check failed: " + ex.Message);
            }

            // If we have no internet and no local installation, we cannot continue
            if (!hasConnection && !Directory.Exists(appDir))
            {
                throw new Exception("No internet connection detected, and no cached copy of Stash is available. Please connect to the internet and restart the application.");
            }

            // Skip update if offline or if SHA matches
            if (!hasConnection || (!force && latestSha == currentSha && Directory.Exists(appDir) && Directory.Exists(Path.Combine(appDir, "node_modules"))))
            {
                return;
            }

            // Update is required
            ProgressForm progress = new ProgressForm("Updating Stash");
            Exception updateError = null;

            Thread updateThread = new Thread(() =>
            {
                try
                {
                    progress.UpdateProgress(5, "Downloading latest Stash update from GitHub...");
                    string zipPath = Path.Combine(stashDir, "stash.zip");
                    
                    DownloadFileWithProgress(string.Format("https://github.com/{0}/{1}/archive/refs/heads/main.zip", RepoOwner, RepoName), zipPath, (pct, msg) => {
                        progress.UpdateProgress(pct, "Downloading update: " + msg);
                    });

                    progress.UpdateProgress(60, "Extracting update...");
                    ExtractStashZip(zipPath);
                    File.Delete(zipPath);

                    // Save new SHA
                    File.WriteAllText(shaFile, latestSha);

                    // Run npm install
                    progress.UpdateProgress(75, "Configuring project dependencies (npm install)...");
                    RunNpmInstall();

                    progress.UpdateProgress(100, "Update complete!");
                    Thread.Sleep(500);
                }
                catch (Exception ex)
                {
                    updateError = ex;
                }
                finally
                {
                    progress.Invoke(new Action(progress.Close));
                }
            });

            updateThread.Start();
            progress.ShowDialog();

            if (updateError != null)
            {
                MessageBox.Show("Failed to perform update: " + updateError.Message + "\nStarting local cached version.", "Update Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private string FetchLatestCommitSha()
        {
            // Ensure security protocols are configured for modern TLS
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format("https://api.github.com/repos/{0}/{1}/commits/main", RepoOwner, RepoName));
            request.UserAgent = "StashLauncher";
            request.Timeout = 5000;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                // Simple regex to parse SHA from JSON response to avoid external dll references
                Match match = Regex.Match(json, "\"sha\"\\s*:\\s*\"([a-f0-9]+)\"");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            return "";
        }

        private void DownloadFileWithProgress(string url, string destPath, Action<int, string> callback)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            
            using (WebClient wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", "StashLauncher");
                
                wc.DownloadProgressChanged += (s, e) => {
                    callback(e.ProgressPercentage, string.Format("{0}MB / {1}MB", e.BytesReceived / 1024 / 1024, e.TotalBytesToReceive / 1024 / 1024));
                };

                wc.DownloadFileAsync(new Uri(url), destPath);

                while (wc.IsBusy)
                {
                    Thread.Sleep(100);
                }
            }
        }

        private void ExtractStashZip(string zipPath)
        {
            string tempExtractDir = Path.Combine(stashDir, "app_temp");
            if (Directory.Exists(tempExtractDir))
            {
                Directory.Delete(tempExtractDir, true);
            }
            Directory.CreateDirectory(tempExtractDir);

            ZipFile.ExtractToDirectory(zipPath, tempExtractDir);

            string[] subdirs = Directory.GetDirectories(tempExtractDir);
            if (subdirs.Length > 0)
            {
                string sourceDir = subdirs[0]; // e.g. stash-desktop-windows-main

                Directory.CreateDirectory(appDir);

                // Clean up only the directories we update (preserve node_modules, models, data)
                string[] dirsToClean = { "public", "lib", ".github", "docker" };
                foreach (string dirName in dirsToClean)
                {
                    string targetPath = Path.Combine(appDir, dirName);
                    if (Directory.Exists(targetPath))
                    {
                        try { Directory.Delete(targetPath, true); } catch {}
                    }
                }

                // Clean up root files from the appDir (preserve folders)
                foreach (string filePath in Directory.GetFiles(appDir))
                {
                    try { File.Delete(filePath); } catch {}
                }

                MoveDirectoryContents(sourceDir, appDir);
            }

            if (Directory.Exists(tempExtractDir))
            {
                Directory.Delete(tempExtractDir, true);
            }
        }

        private void MoveDirectoryContents(string source, string target)
        {
            foreach (string dir in Directory.GetDirectories(source))
            {
                string targetSubdir = Path.Combine(target, Path.GetFileName(dir));
                Directory.CreateDirectory(targetSubdir);
                MoveDirectoryContents(dir, targetSubdir);
            }

            foreach (string file in Directory.GetFiles(source))
            {
                string targetFile = Path.Combine(target, Path.GetFileName(file));
                if (File.Exists(targetFile))
                {
                    File.Delete(targetFile);
                }
                File.Move(file, targetFile);
            }
        }

        private void RunNpmInstall()
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            if (npmCmdPath == "npm")
            {
                psi.FileName = "cmd.exe";
                psi.Arguments = "/c npm install --omit=dev";
            }
            else
            {
                psi.FileName = "cmd.exe";
                psi.Arguments = string.Format("/c \"{0}\" install --omit=dev", npmCmdPath);
            }
            psi.WorkingDirectory = appDir;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            using (Process p = Process.Start(psi))
            {
                // Read outputs to prevent process hanging due to full buffer
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();
                
                File.AppendAllText(logFile, "\n--- npm install stdout ---\n" + stdout);
                if (!string.IsNullOrEmpty(stderr))
                {
                    File.AppendAllText(logFile, "\n--- npm install stderr ---\n" + stderr);
                }
                
                if (p.ExitCode != 0)
                {
                    throw new Exception("npm install failed with exit code " + p.ExitCode + "\nError: " + stderr);
                }
            }
        }

        private void StartServer()
        {
            KillNode(); // Ensure no prior instances are lingering

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = nodeExePath;
            psi.Arguments = "server.js";
            psi.WorkingDirectory = appDir;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.EnvironmentVariables["STASH_LAUNCHER_VERSION"] = LauncherVersion;

            nodeProcess = new Process();
            nodeProcess.StartInfo = psi;
            nodeProcess.EnableRaisingEvents = true;

            // Route Node.js output to log file
            nodeProcess.OutputDataReceived += (s, e) => {
                if (e.Data != null) File.AppendAllText(logFile, e.Data + "\n");
            };
            nodeProcess.ErrorDataReceived += (s, e) => {
                if (e.Data != null) File.AppendAllText(logFile, "[Error] " + e.Data + "\n");
            };

            File.WriteAllText(logFile, string.Format("--- Server started at {0} ---\n", DateTime.Now));
            nodeProcess.Start();
            nodeProcess.BeginOutputReadLine();
            nodeProcess.BeginErrorReadLine();
        }

        private void WaitForServerAndOpenBrowser()
        {
            int retries = 40; // 20 seconds total
            while (retries > 0)
            {
                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://localhost:5173");
                    request.Timeout = 1000;
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        break; // Success!
                    }
                }
                catch (WebException ex)
                {
                    if (ex.Response != null)
                    {
                        break; // Got response (e.g. 404), server is alive!
                    }
                }
                catch (Exception)
                {
                    // Port not open yet
                }
                Thread.Sleep(500);
                retries--;
            }

            try
            {
                Process.Start("http://localhost:5173");
            }
            catch {}
        }

        private void KillNode()
        {
            if (nodeProcess != null && !nodeProcess.HasExited)
            {
                try
                {
                    nodeProcess.Kill();
                    nodeProcess.Dispose();
                }
                catch {}
                nodeProcess = null;
            }
            
            // Additional safety cleanup: find and kill any stray node processes running from our Stash folder
            try
            {
                foreach (var p in Process.GetProcessesByName("node"))
                {
                    try
                    {
                        string modulePath = p.MainModule.FileName;
                        if (modulePath.StartsWith(stashDir, StringComparison.OrdinalIgnoreCase))
                        {
                            p.Kill();
                            p.WaitForExit(1000);
                        }
                    }
                    catch {}
                }
            }
            catch {}
        }

        // --- Event Handlers ---
        private void OnOpenStash(object sender, EventArgs e)
        {
            try
            {
                Process.Start("http://localhost:5173");
            }
            catch {}
        }

        private void OnViewLogs(object sender, EventArgs e)
        {
            if (File.Exists(logFile))
            {
                try
                {
                    Process.Start("notepad.exe", logFile);
                }
                catch {}
            }
            else
            {
                MessageBox.Show("Logs are not available yet.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OnForceUpdate(object sender, EventArgs e)
        {
            // Stop server first
            KillNode();
            
            // Run update and start server back
            Thread updateThread = new Thread(() =>
            {
                try
                {
                    CheckAndPerformUpdate(true);
                    StartServer();
                    WaitForServerAndOpenBrowser();
                }
                catch (Exception ex)
                {
                    try
                    {
                        File.WriteAllText(Path.Combine(stashDir, "error.log"), ex.ToString());
                    }
                    catch {}
                    MessageBox.Show("Error updating: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });
            updateThread.SetApartmentState(ApartmentState.STA);
            updateThread.IsBackground = true;
            updateThread.Start();
        }

        private void OnToggleStartup(object sender, EventArgs e)
        {
            bool isEnabled = IsRunOnStartupEnabled();
            SetRunOnStartup(!isEnabled);
            runOnStartupMenuItem.Checked = !isEnabled;
        }

        private bool IsRunOnStartupEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryStartupKey, false))
            {
                if (key != null)
                {
                    return key.GetValue(AppName) != null;
                }
            }
            return false;
        }

        private void SetRunOnStartup(bool enable)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryStartupKey, true))
            {
                if (key != null)
                {
                    if (enable)
                    {
                        key.SetValue(AppName, string.Format("\"{0}\"", Application.ExecutablePath));
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                    }
                }
            }
        }

        private void OnExit(object sender, EventArgs e)
        {
            CleanAndExit();
        }

        private void CleanAndExit()
        {
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
            KillNode();
            Application.Exit();
            Environment.Exit(0);
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            KillNode();
        }

        private void OnSessionEnding(object sender, SessionEndingEventArgs e)
        {
            KillNode();
        }
    }

    public class ProgressForm : Form
    {
        private ProgressBar progressBar;
        private Label statusLabel;

        public ProgressForm(string title)
        {
            this.Text = title;
            this.Size = new Size(400, 150);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Icon = SystemIcons.Application;
            
            // Attempt to load application icon for dialog
            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch {}

            statusLabel = new Label();
            statusLabel.Text = "Initializing...";
            statusLabel.Location = new Point(20, 20);
            statusLabel.Size = new Size(360, 30);

            progressBar = new ProgressBar();
            progressBar.Location = new Point(20, 60);
            progressBar.Size = new Size(340, 23);
            progressBar.Style = ProgressBarStyle.Continuous;

            this.Controls.Add(statusLabel);
            this.Controls.Add(progressBar);
        }

        public void UpdateProgress(int percentage, string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<int, string>(UpdateProgress), percentage, message);
                return;
            }

            progressBar.Value = Math.Max(0, Math.Min(100, percentage));
            statusLabel.Text = message;
        }
    }
}
