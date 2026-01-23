using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using SocketIOClient;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using System.Linq;

namespace AccountabilityAgent
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Handle assembly resolution for SocketIO dependencies
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            
            // Allocate console only for debug mode
            string[] args = Environment.GetCommandLineArgs();
            bool minimized = args.Length > 1 && args[1] == "/minimized";
            bool debugMode = args.Any(a => a.Equals("/debug", StringComparison.OrdinalIgnoreCase)) ||
                             Environment.GetEnvironmentVariable("ACCOUNTABILITY_DEBUG") == "1";

            if (debugMode)
            {
                AllocConsole();
                Console.WriteLine("Accountability Agent starting...");
                Console.WriteLine($"Debug mode enabled. minimized={minimized}");
            }
            
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            if (debugMode)
            {
                Console.WriteLine("Look for the system tray icon in the notification area.");
                Console.WriteLine("The app will run in the background.");
            }
            
            // Add global exception handlers to prevent crashes
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, e) =>
            {
                Console.WriteLine($"Unhandled thread exception: {e.Exception.Message}");
                Console.WriteLine($"Stack trace: {e.Exception.StackTrace}");
                MessageBox.Show($"An error occurred: {e.Exception.Message}\n\nCheck console for details.", 
                    "Application Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    Console.WriteLine($"Unhandled exception: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    MessageBox.Show($"A critical error occurred: {ex.Message}\n\nCheck console for details.", 
                        "Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            Application.Run(new MainForm(minimized));
        }
        
        private static Assembly? CurrentDomain_AssemblyResolve(object? sender, ResolveEventArgs args)
        {
            try
            {
                string assemblyName = new AssemblyName(args.Name).Name;
                Console.WriteLine($"Attempting to resolve assembly: {assemblyName} (Requested: {args.Name})");
                
                // Try to load from application directory
                string appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                
                // Try exact name first
                string assemblyPath = Path.Combine(appDir, $"{assemblyName}.dll");
                if (File.Exists(assemblyPath))
                {
                    Console.WriteLine($"Found assembly at: {assemblyPath}");
                    return Assembly.LoadFrom(assemblyPath);
                }
                
                // Try case-insensitive search in application directory
                if (Directory.Exists(appDir))
                {
                    string[] dlls = Directory.GetFiles(appDir, "*.dll");
                    string? foundDll = Array.Find(dlls, d => 
                        Path.GetFileNameWithoutExtension(d).Equals(assemblyName, StringComparison.OrdinalIgnoreCase));
                    
                    if (foundDll != null)
                    {
                        Console.WriteLine($"Found assembly (case-insensitive) at: {foundDll}");
                        return Assembly.LoadFrom(foundDll);
                    }
                    
                    // Try looking for SocketIO.Core variations (common issue)
                    if (assemblyName.Contains("SocketIO") || assemblyName.Contains("SocketIo"))
                    {
                        foundDll = Array.Find(dlls, d => 
                            Path.GetFileNameWithoutExtension(d).ToLower().Contains("socketio"));
                        
                        if (foundDll != null)
                        {
                            Console.WriteLine($"Found SocketIO assembly at: {foundDll}");
                            return Assembly.LoadFrom(foundDll);
                        }
                        
                        // List all SocketIO-related DLLs for debugging
                        Console.WriteLine("Available SocketIO-related DLLs:");
                        foreach (var dll in dlls.Where(d => Path.GetFileName(d).ToLower().Contains("socket")))
                        {
                            Console.WriteLine($"  - {Path.GetFileName(dll)}");
                        }
                    }
                }
                
                // Try with .exe extension
                assemblyPath = Path.Combine(appDir, $"{assemblyName}.exe");
                if (File.Exists(assemblyPath))
                {
                    Console.WriteLine($"Found assembly at: {assemblyPath}");
                    return Assembly.LoadFrom(assemblyPath);
                }
                
                Console.WriteLine($"Could not find assembly: {assemblyName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error resolving assembly: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            return null;
        }
        
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        static extern bool AllocConsole();
    }
}

