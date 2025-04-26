using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace InfernoInjector
{
    public partial class MainWindow : Window
    {
        // P/Invoke for required Windows API functions
        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(IntPtr dwDesiredAccess, bool bInheritHandle, uint processId);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll")]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, char[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll")]
        public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, ref IntPtr lpThreadId);

        [DllImport("kernel32.dll")]
        public static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        [DllImport("kernel32.dll")]
        public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, int dwSize, IntPtr dwFreeType);

        [DllImport("psapi.dll", SetLastError = true)]
        public static extern bool EnumProcessModules(IntPtr hProcess, IntPtr[] lphModule, uint cb, ref uint lpcbNeeded);

        [DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, [Out] char[] lpBaseName, int nSize);
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(String lpClassName, String lpWindowName);


        private const uint LIST_MODULES_ALL = 0x03;
        private const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_READ = 0x0010;
        const uint INFINITE = 0xFFFFFFFF;



        private readonly string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.txt");

        public MainWindow()
        {
            InitializeComponent();
            CheckForUpdatesAsync();
            LoadLoadout();
            this.Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/fire.ico"));
        }

        public static async Task CheckForUpdatesAsync()
        {
            const string CurrentVersion = "1.0";
            const string VersionFileUrl = "https://raw.githubusercontent.com/Gav2011/Versions/refs/heads/main/InfernoInjector";
            const string LatestReleaseUrl = "https://github.com/Gav2011/InfernoInjector/releases/latest/download/InfernoInjector.exe";

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string latestVersion = (await client.GetStringAsync(VersionFileUrl)).Trim();

                    if (latestVersion != CurrentVersion)
                    {
                        MessageBoxResult result = MessageBox.Show(
                            $"A new version ({latestVersion}) is available.\nDo you want to update now?",
                            "Update Available",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        if (result == MessageBoxResult.Yes)
                        {
                            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                            string exeDir = Path.GetDirectoryName(exePath);
                            string updatePath = Path.Combine(exeDir, "InfernoInjector.exe");
                            string vbsPath = Path.Combine(exeDir, "update.vbs");
                            string vbs = $@"
Set WshShell = CreateObject(""WScript.Shell"")
WshShell.Run ""cmd /c taskkill /f /im InfernoInjector.exe"", 0, True
WScript.Sleep 5000 ' Increased sleep time to ensure process is killed
Set fso = CreateObject(""Scripting.FileSystemObject"")
If fso.FileExists(""{exePath}"") Then
    fso.DeleteFile ""{exePath}"", True
Else
    WshShell.Popup ""Old executable not found"", 0, ""Debug"", 0
End If
Set objXML = CreateObject(""WinHTTP.WinHTTPRequest.5.1"")
objXML.Open ""GET"", ""{LatestReleaseUrl}"", False
objXML.Send
Set objStream = CreateObject(""ADODB.Stream"")
objStream.Open
objStream.Type = 1 ' Binary
objStream.Write objXML.ResponseBody
objStream.SaveToFile ""{updatePath}"", 2 ' Overwrite
objStream.Close
WshShell.Run ""{updatePath}"", 0, False
WScript.Sleep 2000
fso.DeleteFile ""{vbsPath}"", True
";

                            File.WriteAllText(vbsPath, vbs);
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "wscript.exe",
                                Arguments = $"\"{vbsPath}\"",
                                CreateNoWindow = true,
                                UseShellExecute = false
                            });
                            Application.Current.Shutdown();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking for updates:\n{ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void LoadLoadout()
        {
            try
            {
                if (!File.Exists(settingsPath))
                    return;

                var lines = File.ReadAllLines(settingsPath);
                foreach (var line in lines)
                {
                    var split = line.Split('=');
                    if (split.Length != 2) continue;

                    var key = split[0].Trim();
                    var value = split[1].Trim();

                    if (key == "dll" && File.Exists(value))
                        DllPathTextBox.Text = value;

                    if (key == "process")
                        ProcessIdTextBox.Text = value;
                }

                EnableInjectButton();
            }
            catch (Exception ex)
            {
                TECTIC($"Failed to load saved config:\n{ex.Message}");
            }
        }

        private void SaveLoadout()
        {
            try
            {
                var lines = new[]
                {
                    $"dll={DllPathTextBox.Text}",
                    $"process={ProcessIdTextBox.Text}"
                };

                if (!File.Exists(settingsPath) || !File.ReadAllLines(settingsPath).SequenceEqual(lines))
                    File.WriteAllLines(settingsPath, lines);
            }
            catch (Exception ex)
            {
                TECTIC($"Failed to save settings:\n{ex.Message}");
            }   
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SelectAppButton_Click(object sender, RoutedEventArgs e)
        {
            ProcessSelectionWindow processSelectionWindow = new ProcessSelectionWindow();
            if (processSelectionWindow.ShowDialog() == true)
            {
                ProcessIdTextBox.Text = processSelectionWindow.SelectedProcessName;
                EnableInjectButton();
            }
        }

        private void SelectDllButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "DLL Files (*.dll)|*.dll|All Files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                DllPathTextBox.Text = openFileDialog.FileName;
                EnableInjectButton();
            }
        }

        private void EnableInjectButton()
        {
            if (!string.IsNullOrWhiteSpace(ProcessIdTextBox.Text) && !string.IsNullOrWhiteSpace(DllPathTextBox.Text) && File.Exists(DllPathTextBox.Text))
            {
                InjectButton.IsEnabled = true;
            }
            else
            {
                InjectButton.IsEnabled = false;
            }
        }

        private void UnInjectButton_Click(object sender, RoutedEventArgs e)
        {
            string dllPath = DllPathTextBox.Text;
            if (string.IsNullOrWhiteSpace(ProcessIdTextBox.Text) || string.IsNullOrWhiteSpace(dllPath))
            {
                TECTIC("Please provide a valid process and DLL path.");
                return;
            }

            var process = Process.GetProcessesByName(ProcessIdTextBox.Text).FirstOrDefault();
            if (process == null)
            {
                TECTIC("Process not found.");
                return;
            }

            System.Threading.Tasks.Task.Run(() => Uninject(dllPath, process.Id));
        }

        private void InjectButton_Click(object sender, RoutedEventArgs e)
        {
            SaveLoadout();
            string dllPath = DllPathTextBox.Text;
            if (string.IsNullOrWhiteSpace(ProcessIdTextBox.Text) || string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath))
            {
                TECTIC("Please provide a valid process and DLL path.");
                return;
            }
            var process = Process.GetProcessesByName(ProcessIdTextBox.Text).FirstOrDefault();
            if (process != null)
            {
                System.Threading.Tasks.Task.Run(() => Inject(dllPath, process.Id));
            }
            else
            {
                TECTIC("Process not found.");
            }
        }

        private static void Uninject(string DLLPath, int ProcessID)
        {
            try
            {
                IntPtr hProcess = OpenProcess((IntPtr)PROCESS_ALL_ACCESS, false, (uint)ProcessID);
                if (hProcess == IntPtr.Zero)
                {
                    return;
                }
                IntPtr[] modules = new IntPtr[1024];
                uint needed = 0;
                if (!EnumProcessModules(hProcess, modules, (uint)(modules.Length * IntPtr.Size), ref needed))
                {
                    CloseHandle(hProcess);
                    return;
                }
                IntPtr dllBaseAddress = IntPtr.Zero;
                for (int i = 0; i < (needed / (uint)IntPtr.Size); i++)
                {
                    char[] moduleName = new char[1024];
                    GetModuleFileNameEx(hProcess, modules[i], moduleName, moduleName.Length);
                    string moduleFileName = new string(moduleName).TrimEnd('\0');
                    if (moduleFileName.ToLower() == DLLPath.ToLower())
                    {
                        dllBaseAddress = modules[i];
                        break;
                    }
                }
                if (dllBaseAddress == IntPtr.Zero)
                {
                    CloseHandle(hProcess);
                    return;
                }
                IntPtr freeLibraryAddress = GetProcAddress(GetModuleHandle("kernel32.dll"), "FreeLibrary");
                if (freeLibraryAddress == IntPtr.Zero)
                {
                    CloseHandle(hProcess);
                    return;
                }
                IntPtr threadId = IntPtr.Zero;
                IntPtr threadHandle = CreateRemoteThread(hProcess, IntPtr.Zero, 0, freeLibraryAddress, dllBaseAddress, 0, ref threadId);
                if (threadHandle == IntPtr.Zero)
                {
                    CloseHandle(hProcess);
                    return;
                }
                WaitForSingleObject(threadHandle, INFINITE);
                CloseHandle(threadHandle);
                CloseHandle(hProcess);
                TECTIC("DLL successfully un-injected.");
            }
            catch (Exception ex)
            {
            }
        }

        private static void Inject(string DLLPath, int ProcessID)
        {
            try
            {
                try
                {
                    FileInfo fileInfo = new FileInfo(DLLPath);
                    FileSecurity fileSecurity = fileInfo.GetAccessControl();
                    fileSecurity.AddAccessRule(
                        new FileSystemAccessRule(
                            new SecurityIdentifier("S-1-15-2-1"),
                            FileSystemRights.FullControl,
                            InheritanceFlags.None,
                            PropagationFlags.NoPropagateInherit,
                            AccessControlType.Allow));
                    fileInfo.SetAccessControl(fileSecurity);
                }
                catch (Exception accessEx)
                {
                    TECTIC($"Failed to set access control: {accessEx.Message}");
                    return;
                }
                var process = Process.GetProcessById(ProcessID);
                foreach (ProcessModule module in process.Modules)
                {
                    if (module.FileName.Equals(DLLPath, StringComparison.OrdinalIgnoreCase))
                    {
                        TECTIC("DLL is already injected into the process!");
                        return;
                    }
                }
                IntPtr handle = OpenProcess((IntPtr)2035711, false, (uint)process.Id);
                if (handle == IntPtr.Zero)
                {
                    TECTIC("Failed to open process with necessary permissions.");
                    return;
                }
                IntPtr p1 = VirtualAllocEx(handle, IntPtr.Zero, (uint)(DLLPath.Length + 1), 12288U, 64U);
                if (p1 == IntPtr.Zero)
                {
                    TECTIC("Failed to allocate memory in target process.");
                    CloseHandle(handle);
                    return;
                }
                IntPtr p2;
                bool writeResult = WriteProcessMemory(handle, p1, DLLPath.ToCharArray(), DLLPath.Length, out p2);
                if (!writeResult)
                {
                    TECTIC("Failed to write DLL path to memory.");
                    CloseHandle(handle);
                    return;
                }
                IntPtr procAddress = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
                if (procAddress == IntPtr.Zero)
                {
                    TECTIC("Failed to get the address of LoadLibraryA.");
                    CloseHandle(handle);
                    return;
                }
                IntPtr threadId = IntPtr.Zero;
                IntPtr p3 = CreateRemoteThread(handle, IntPtr.Zero, 0U, procAddress, p1, 0U, ref threadId);
                if (p3 == IntPtr.Zero)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    TECTIC($"CreateRemoteThread failed. Error Code: {errorCode}");
                    CloseHandle(handle);
                    return;
                }
                uint n = WaitForSingleObject(p3, 5000);
                if (n == 128L || n == 258L)
                {
                    TECTIC("Remote thread timeout or unsuccessful.");
                    CloseHandle(p3);
                }
                else
                {
                    VirtualFreeEx(handle, p1, 0, (IntPtr)32768);
                    CloseHandle(p3);
                }
                if (handle != IntPtr.Zero)
                    CloseHandle(handle);
                IntPtr windowH = FindWindow(null, "Minecraft");
                if (windowH == IntPtr.Zero)
                {
                    TECTIC("Couldn't get window handle for Minecraft.");
                }
                else
                {
                    SetForegroundWindow(windowH);
                }
                TECTIC("DLL successfully injected.");
            }
            catch (Exception ex)
            {
                TECTIC($"Error: {ex.Message}");
            }
        }

        private static void TECTIC(string text)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var mainWindow = (MainWindow)System.Windows.Application.Current.MainWindow;
                mainWindow.TEXTIC.Text = text;
            });
        }
    }
}
