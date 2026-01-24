using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using SocketIOClient;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Text.Json;
using System.Drawing.Imaging;
using SIPSorcery.Net;

namespace AccountabilityAgent
{
    public partial class MainForm : Form
    {
        private NotifyIcon trayIcon;
        private SocketIOClient.SocketIO? socketClient;
        private string pairingCode = "";
        private string deviceId = "";
        private readonly string pairingCodeFile;
        private bool isStreaming = false;
        private OverlayForm? overlayForm;
        private ScreenCaptureService? screenCaptureService;
        private int _framesCaptured = 0;
        private bool pairingCodeRequestPending = false;
        private WebRtcService? webRtcService;
        private int _webrtcFrameCounter = 0;
        private bool socketFrameFallbackEnabled = true;
        private bool loggedSocketFrameSend = false;
        private int socketFrameSendCount = 0;
        private string serverUrl = "http://141.148.184.72:5000";

        public MainForm(bool minimized)
        {
            try
            {
                Debug.WriteLine("MainForm constructor starting...");
                Console.WriteLine("MainForm constructor starting...");
                
                InitializeComponent();
                SetupTrayIcon();
                
                // Load or generate persistent device ID
                var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AccountabilityAgent");
                Directory.CreateDirectory(appDataDir);
                var deviceIdFile = Path.Combine(appDataDir, "device.id");
                var pairingCodeFilePath = Path.Combine(appDataDir, "pairing.code");
                pairingCodeFile = pairingCodeFilePath;
                var serverUrlFile = Path.Combine(appDataDir, "server.url");

                // Load server URL (env var > file > default)
                var envServerUrl = Environment.GetEnvironmentVariable("ACCOUNTABILITY_SERVER_URL");
                if (!string.IsNullOrWhiteSpace(envServerUrl))
                {
                    serverUrl = envServerUrl.Trim();
                }
                else if (File.Exists(serverUrlFile))
                {
                    serverUrl = File.ReadAllText(serverUrlFile).Trim();
                }
                else
                {
                    serverUrl = "http://141.148.184.72:5000";
                    File.WriteAllText(serverUrlFile, serverUrl);
                }
                Console.WriteLine($"Server URL: {serverUrl}");
                
                // Load device ID if exists, otherwise create new one
                if (File.Exists(deviceIdFile))
                {
                    deviceId = File.ReadAllText(deviceIdFile).Trim();
                    Console.WriteLine($"Loaded existing device ID: {deviceId}");
                }
                else
                {
                    deviceId = Environment.MachineName + "-" + Guid.NewGuid().ToString("N")[..8];
                    File.WriteAllText(deviceIdFile, deviceId);
                    Console.WriteLine($"Generated new device ID: {deviceId}");
                }
                
                // Load pairing code if exists
                if (File.Exists(pairingCodeFilePath))
                {
                    pairingCode = File.ReadAllText(pairingCodeFilePath).Trim();
                    Console.WriteLine($"Loaded existing pairing code: {pairingCode}");
                }
                
                Debug.WriteLine($"Accountability Agent initialized. Device ID: {deviceId}");
                Console.WriteLine($"Accountability Agent initialized. Device ID: {deviceId}");
                
                if (minimized)
                {
                    this.WindowState = FormWindowState.Minimized;
                    this.ShowInTaskbar = false;
                }
                
                // Show notification that app started (can be removed later)
                if (trayIcon != null)
                {
                    trayIcon.ShowBalloonTip(5000, "Accountability Agent", 
                        $"Agent started! Device ID: {deviceId}\nLook for the tray icon in the notification area (may be hidden).", 
                        ToolTipIcon.Info);
                    Console.WriteLine("Balloon tip shown. Check system tray notification area.");
                }
                
                _ = InitializeAsync();
                
                Console.WriteLine("MainForm initialization complete. App should be running in system tray.");
                Debug.WriteLine("MainForm initialization complete.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in MainForm constructor: {ex.Message}");
                Console.WriteLine($"Error in MainForm constructor: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Failed to initialize application: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeComponent()
        {
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Text = "Accountability Agent";
            this.Size = new Size(1, 1);
        }

        private void SetupTrayIcon()
        {
            try
            {
                Debug.WriteLine("Setting up tray icon...");
                Console.WriteLine("Setting up tray icon...");
                
                trayIcon = new NotifyIcon
                {
                    Icon = CreateTrayIcon(Color.Gray),
                    Visible = true,
                    Text = "Accountability Agent - Connecting..."
                };

                var contextMenu = new ContextMenuStrip();
                contextMenu.Items.Add("View Status", null, (s, e) => ShowStatus());
                contextMenu.Items.Add("Generate Pairing Code", null, (s, e) => GeneratePairingCode());
                contextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());
                trayIcon.ContextMenuStrip = contextMenu;
                trayIcon.DoubleClick += (s, e) => ShowStatus();
                
                Debug.WriteLine($"Tray icon created. Visible={trayIcon.Visible}, Text={trayIcon.Text}");
                Console.WriteLine($"Tray icon created. Visible={trayIcon.Visible}");
                Console.WriteLine("Note: Windows may hide tray icons. Click the '^' arrow in the system tray to see hidden icons.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting up tray icon: {ex.Message}");
                Console.WriteLine($"Error setting up tray icon: {ex.Message}");
                MessageBox.Show($"Failed to create tray icon: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Icon CreateTrayIcon(Color color)
        {
            try
            {
                // Create a more visible 32x32 icon
                Bitmap bmp = new Bitmap(32, 32);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    
                    // Draw a larger, more visible circle
                    Rectangle rect = new Rectangle(4, 4, 24, 24);
                    g.FillEllipse(new SolidBrush(color), rect);
                    
                    // Add a white border for visibility
                    using (Pen pen = new Pen(Color.White, 2))
                    {
                        g.DrawEllipse(pen, rect);
                    }
                }
                Icon icon = Icon.FromHandle(bmp.GetHicon());
                Debug.WriteLine($"Icon created successfully with color {color.Name}");
                return icon;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating icon: {ex.Message}");
                Console.WriteLine($"Error creating icon: {ex.Message}");
                // Return a default system icon as fallback
                return SystemIcons.Application;
            }
        }

        private async Task InitializeAsync()
        {
            try
            {
                Debug.WriteLine($"Attempting to connect to server at {serverUrl}");
                Console.WriteLine($"Attempting to connect to server at {serverUrl}");
                await Task.Delay(100); // Give console time to flush
                Console.Out.Flush();
                
                // Explicitly load SocketIO.Core assembly before using SocketIO client
                string appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                Console.WriteLine($"Application directory: {appDir}");
                await Task.Delay(100); // Give console time to flush
                Console.Out.Flush();
                
                // List all DLLs in the directory
                if (Directory.Exists(appDir))
                {
                    var dlls = Directory.GetFiles(appDir, "*.dll");
                    Console.WriteLine($"Total DLLs found: {dlls.Length}");
                    Console.WriteLine("SocketIO-related DLLs:");
                    var socketDlls = dlls.Where(d => Path.GetFileName(d).ToLower().Contains("socket")).ToList();
                    if (socketDlls.Any())
                    {
                        foreach (var dll in socketDlls)
                        {
                            Console.WriteLine($"  - {Path.GetFileName(dll)}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("  (none found)");
                    }
                    await Task.Delay(100); // Give console time to flush
                    Console.Out.Flush();
                    
                    // Try to explicitly load SocketIO.Core
                    var socketCoreDll = dlls.FirstOrDefault(d => 
                        Path.GetFileNameWithoutExtension(d).Equals("SocketIO.Core", StringComparison.OrdinalIgnoreCase) ||
                        Path.GetFileNameWithoutExtension(d).Equals("SocketIo.Core", StringComparison.OrdinalIgnoreCase));
                    
                    if (socketCoreDll != null)
                    {
                        Console.WriteLine($"Found SocketIO.Core at: {socketCoreDll}");
                        Console.Out.Flush();
                        try
                        {
                            var coreAssembly = Assembly.LoadFrom(socketCoreDll);
                            Console.WriteLine($"SocketIO.Core assembly loaded: {coreAssembly.FullName}");
                            
                            // Try to find the EngineIO type
                            var engineIOType = coreAssembly.GetType("SocketIO.Core.EngineIO");
                            if (engineIOType != null)
                            {
                                Console.WriteLine($"Found EngineIO type: {engineIOType.FullName}");
                            }
                            else
                            {
                                Console.WriteLine("ERROR: EngineIO type not found in SocketIO.Core!");
                                Console.WriteLine("Available public types in SocketIO.Core (first 15):");
                                try
                                {
                                    var types = coreAssembly.GetExportedTypes();
                                    foreach (var type in types.Take(15))
                                    {
                                        Console.WriteLine($"  - {type.FullName}");
                                    }
                                    if (types.Length > 15)
                                    {
                                        Console.WriteLine($"  ... and {types.Length - 15} more types");
                                    }
                                    Console.WriteLine($"Total exported types: {types.Length}");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Could not list exported types: {ex.Message}");
                                    Console.WriteLine("This indicates a serious compatibility issue.");
                                }
                            }
                            Console.Out.Flush();
                        }
                        catch (Exception loadEx)
                        {
                            Console.WriteLine($"Failed to load SocketIO.Core: {loadEx.Message}");
                            Console.WriteLine($"Stack: {loadEx.StackTrace}");
                            Console.Out.Flush();
                        }
                    }
                    else
                    {
                        Console.WriteLine("WARNING: SocketIO.Core.dll not found!");
                        Console.WriteLine("Listing all DLLs for debugging:");
                        foreach (var dll in dlls.Take(20))
                        {
                            Console.WriteLine($"  - {Path.GetFileName(dll)}");
                        }
                        await Task.Delay(100); // Give console time to flush
                        Console.Out.Flush();
                    }
                }
                else
                {
                    Console.WriteLine($"ERROR: Application directory does not exist: {appDir}");
                    await Task.Delay(100);
                    Console.Out.Flush();
                }
                
                // Force console output first
                Console.WriteLine("About to create SocketIO client...");
                await Task.Delay(200);
                Console.Out.Flush();
                
                // Load SocketIOClient type dynamically to delay loading
                try
                {
                    // Load the assembly explicitly first
                    var socketIOClientAssembly = Assembly.Load("SocketIOClient, Version=3.1.0.0, Culture=neutral, PublicKeyToken=null");
                    Console.WriteLine("SocketIOClient assembly loaded successfully");
                    await Task.Delay(100);
                    Console.Out.Flush();
                    
                    // Now get the type
                    var socketIOType = socketIOClientAssembly.GetType("SocketIOClient.SocketIO");
                    var optionsType = socketIOClientAssembly.GetType("SocketIOClient.SocketIOOptions");
                    
                    if (socketIOType == null || optionsType == null)
                    {
                        throw new Exception($"Could not find SocketIO types. SocketIO: {socketIOType != null}, Options: {optionsType != null}");
                    }
                    
                    Console.WriteLine("SocketIO types found, creating instance...");
                    await Task.Delay(100);
                    Console.Out.Flush();
                    
                    // Create options
                    dynamic options = Activator.CreateInstance(optionsType)!;
                    options.Reconnection = true;
                    options.ReconnectionDelay = 1000;
                    options.ReconnectionDelayMax = 5000;
                    try
                    {
                        var transportEnum = socketIOClientAssembly.GetType("SocketIOClient.Transport.TransportProtocol");
                        var transportProp = optionsType.GetProperty("Transport");
                        if (transportEnum != null && transportProp != null)
                        {
                            var pollingValue = Enum.Parse(transportEnum, "Polling");
                            transportProp.SetValue(options, pollingValue);
                            Console.WriteLine("SocketIO transport set to Polling");
                        }
                        else
                        {
                            Console.WriteLine("SocketIO transport property not found; using default transport");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to set SocketIO transport: {ex.Message}");
                    }
                    
                    // Create SocketIO instance
                    socketClient = Activator.CreateInstance(socketIOType, serverUrl, options);
                    Console.WriteLine("SocketIO client created successfully!");
                    await Task.Delay(100);
                    Console.Out.Flush();
                }
                catch (TypeLoadException tle)
                {
                    Console.WriteLine($"TypeLoadException: {tle.Message}");
                    Console.WriteLine($"Type: {tle.TypeName}");
                    Console.Out.Flush();
                    throw;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.GetType().Name}: {ex.Message}");
                    Console.WriteLine($"Stack: {ex.StackTrace}");
                    Console.Out.Flush();
                    throw;
                }
                
                // Cast to SocketIOClient.SocketIO now that it's loaded
                socketClient = socketClient as SocketIOClient.SocketIO;
                if (socketClient == null)
                {
                    throw new Exception("Failed to cast socketClient to SocketIOClient.SocketIO");
                }
                
                // Set up event handlers normally now
                socketClient.OnConnected += async (sender, e) =>
                {
                    Debug.WriteLine("Connected to server");
                    await socketClient!.EmitAsync("register-device", new
                    {
                        deviceId,
                        pairingCode = string.IsNullOrEmpty(pairingCode) ? null : pairingCode
                    });
                    
                    this.Invoke((System.Windows.Forms.MethodInvoker)delegate
                    {
                        trayIcon.Text = "Accountability Agent - Connected";
                        trayIcon.Icon = CreateTrayIcon(Color.Green);
                    });
                };

                socketClient.OnError += (sender, e) =>
                {
                    Debug.WriteLine($"Socket.IO error: {e}");
                    this.Invoke((System.Windows.Forms.MethodInvoker)delegate
                    {
                        trayIcon.Text = "Accountability Agent - Connection Error";
                        trayIcon.Icon = CreateTrayIcon(Color.Red);
                    });
                };

                socketClient.On("request-stream", async response =>
                {
                    try
                    {
                        string? requestId = response.GetValue<string>();
                        Console.WriteLine($"Stream request received: {requestId}");
                        Debug.WriteLine($"Stream request received: {requestId}");
                        await OnStreamRequest(requestId ?? "");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error handling stream request: {ex.Message}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                        Debug.WriteLine($"Error handling stream request: {ex.Message}");
                        Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                        
                        // Show error but don't crash
                        MessageBox.Show($"Error handling stream request: {ex.Message}\n\nCheck console for details.", 
                            "Stream Request Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                });

                socketClient.On("stop-stream", response =>
                {
                    StopStreaming();
                });

                socketClient.On("pairing-code", response =>
                {
                    try
                    {
                        var data = response.GetValue<System.Text.Json.JsonElement>();
                        if (data.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            if (data.TryGetProperty("pairingCode", out var codeElement))
                            {
                                var newCode = codeElement.GetString() ?? "";
                                
                                // Only update if we don't have a pairing code yet
                                if (string.IsNullOrEmpty(pairingCode))
                                {
                                    pairingCode = newCode;
                                    
                                    // Save pairing code persistently
                                    try
                                    {
                                        File.WriteAllText(pairingCodeFile, pairingCode);
                                        Debug.WriteLine($"Saved pairing code to: {pairingCodeFile}");
                                        Console.WriteLine($"Saved pairing code to: {pairingCodeFile}");
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"Failed to save pairing code: {ex.Message}");
                                        Console.WriteLine($"Warning: Failed to save pairing code: {ex.Message}");
                                    }
                                    
                                    Debug.WriteLine($"Received and saved pairing code: {pairingCode}");
                                    Console.WriteLine($"Pairing code received and saved: {pairingCode}");
                                }
                                else
                                {
                                    // Show existing pairing code
                                    Debug.WriteLine($"Device already has pairing code: {pairingCode}");
                                    Console.WriteLine($"Device already has pairing code: {pairingCode}");
                                }

                                if (pairingCodeRequestPending)
                                {
                                    pairingCodeRequestPending = false;
                                    this.Invoke((System.Windows.Forms.MethodInvoker)delegate
                                    {
                                        MessageBox.Show($"Pairing Code: {pairingCode}\n\nThis is your device's unique pairing code.\n\nEnter this in the parent web app.", 
                                            "Pairing Code", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error parsing pairing code: {ex.Message}");
                        Console.WriteLine($"Error parsing pairing code: {ex.Message}");
                    }
                });

                socketClient.On("parent-viewing", response =>
                {
                    try
                    {
                        string parentEmail = "Parent";
                        var data = response.GetValue<JsonElement>();
                        if (data.ValueKind == JsonValueKind.Object &&
                            data.TryGetProperty("parentEmail", out var emailElement))
                        {
                            parentEmail = emailElement.GetString() ?? "Parent";
                        }

                        this.Invoke((System.Windows.Forms.MethodInvoker)delegate
                        {
                            trayIcon.ShowBalloonTip(2000, "Parent viewing",
                                $"{parentEmail} is viewing the screen.",
                                ToolTipIcon.Info);
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error handling parent-viewing: {ex.Message}");
                    }
                });

                socketClient.On("webrtc-offer", async response =>
                {
                    try
                    {
                        var data = response.GetValue<JsonElement>();
                        if (data.ValueKind != JsonValueKind.Object)
                        {
                            return;
                        }

                        if (!data.TryGetProperty("offer", out var offerElement))
                        {
                            return;
                        }

                        var sdp = offerElement.GetProperty("sdp").GetString();
                        var type = offerElement.GetProperty("type").GetString();

                        if (string.IsNullOrEmpty(sdp) || string.IsNullOrEmpty(type))
                        {
                            return;
                        }

                        if (webRtcService == null)
                        {
                            try
                            {
                                webRtcService = new WebRtcService();
                                webRtcService.IceCandidateDiscovered += (candidate) =>
                                {
                                    if (socketClient != null && socketClient.Connected)
                                    {
                                        socketClient.EmitAsync("webrtc-ice", new
                                        {
                                            deviceId,
                                            candidate
                                        });
                                    }
                                };
                            }
                            catch (Exception ex)
                            {
                                webRtcService = null;
                                socketFrameFallbackEnabled = true;
                                Console.WriteLine($"WebRTC init failed, using Socket.IO frames: {ex.Message}");
                                Debug.WriteLine($"WebRTC init failed, using Socket.IO frames: {ex.Message}");
                            }
                        }

                        if (webRtcService != null)
                        {
                            var answer = await webRtcService.HandleOfferAsync(sdp, type);
                            await socketClient.EmitAsync("webrtc-answer", new
                            {
                                deviceId,
                                answer = new { type = "answer", sdp = answer.sdp }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error handling webrtc-offer: {ex.Message}");
                        Debug.WriteLine($"Error handling webrtc-offer: {ex.Message}");
                    }
                });

                socketClient.On("webrtc-ice", response =>
                {
                    try
                    {
                        if (webRtcService == null) return;

                        var data = response.GetValue<JsonElement>();
                        if (data.ValueKind != JsonValueKind.Object) return;

                        if (!data.TryGetProperty("candidate", out var candidateElement)) return;

                        var candidateInit = candidateElement.Deserialize<RTCIceCandidateInit>();
                        if (candidateInit != null)
                        {
                            webRtcService.AddIceCandidate(candidateInit);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error handling webrtc-ice: {ex.Message}");
                        Debug.WriteLine($"Error handling webrtc-ice: {ex.Message}");
                    }
                });

                socketClient.OnDisconnected += (sender, e) =>
                {
                    Debug.WriteLine($"Disconnected from server: {e}");
                    this.Invoke((System.Windows.Forms.MethodInvoker)delegate
                    {
                        trayIcon.Text = "Accountability Agent - Disconnected";
                        trayIcon.Icon = CreateTrayIcon(Color.Red);
                    });
                };

                Debug.WriteLine("Calling ConnectAsync...");
                await socketClient.ConnectAsync();
                Debug.WriteLine("ConnectAsync completed");
            }
            catch (Exception ex)
            {
                string errorMsg = $"Failed to connect to {serverUrl}\n\nError: {ex.Message}\n\nMake sure the backend server is running on port 5000.";
                Debug.WriteLine($"Connection exception: {ex}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                MessageBox.Show(errorMsg, "Connection Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                trayIcon.Text = "Accountability Agent - Connection Failed";
                trayIcon.Icon = CreateTrayIcon(Color.Red);
            }
        }

        private async Task OnStreamRequest(string requestId)
        {
            try
            {
                Console.WriteLine($"Processing stream request: {requestId}");
                Debug.WriteLine($"Processing stream request: {requestId}");
                
                if (isStreaming)
                {
                    Console.WriteLine("Already streaming, ignoring request");
                    return;
                }

                this.Invoke((System.Windows.Forms.MethodInvoker)delegate
                {
                    try
                    {
                        isStreaming = true;
                        _framesCaptured = 0; // Reset frame counter
                        trayIcon.Icon = CreateTrayIcon(Color.Orange);
                        trayIcon.Text = "Accountability Agent - Streaming Active";
                        
                        Console.WriteLine("Streaming status updated, showing overlay...");
                        
                        // Show overlay
                        overlayForm = new OverlayForm();
                        overlayForm.Show();
                        
                        Console.WriteLine("Overlay shown successfully");

                        // Hide overlay after 2 seconds
                        Task.Delay(TimeSpan.FromSeconds(2)).ContinueWith(_ =>
                        {
                            try
                            {
                                this.Invoke((System.Windows.Forms.MethodInvoker)delegate
                                {
                                    if (overlayForm != null)
                                    {
                                        overlayForm.Close();
                                        overlayForm = null;
                                    }
                                });
                            }
                            catch
                            {
                                // Ignore overlay close errors
                            }
                        });
                        
                        // Auto-disconnect after 15 minutes
                        Task.Delay(TimeSpan.FromMinutes(15)).ContinueWith(_ =>
                        {
                            StopStreaming();
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in UI update: {ex.Message}");
                        Debug.WriteLine($"Error in UI update: {ex.Message}");
                    }
                });

                Console.WriteLine("Starting screen capture...");
                await StartScreenCapture();
                Console.WriteLine("Screen capture started successfully");

                // Emit a small ping so backend can confirm it receives child events
                if (socketClient != null && socketClient.Connected)
                {
                    try
                    {
                        await socketClient.EmitAsync("frame-ping", new { deviceId, status = "stream-started" });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Frame ping failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnStreamRequest: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Debug.WriteLine($"Error in OnStreamRequest: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                // Don't crash - just log the error
                MessageBox.Show($"Error processing stream request: {ex.Message}\n\nCheck console for details.", 
                    "Stream Request Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task StartScreenCapture()
        {
            try
            {
                Debug.WriteLine("Initializing screen capture service...");

                // Create screen capture service
                screenCaptureService = new ScreenCaptureService();
                
                // Subscribe to frame captured events
                screenCaptureService.FrameCaptured += OnFrameCaptured;
                screenCaptureService.CaptureError += OnCaptureError;

                // Start capture
                var success = await screenCaptureService.StartCaptureAsync();
                
                if (success)
                {
                    Debug.WriteLine("Screen capture started successfully");
                    Debug.WriteLine($"Target FPS: 10, Frame interval: 100ms");
                }
                else
                {
                    MessageBox.Show("Failed to start screen capture. Check debug output for details.", 
                        "Capture Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    StopStreaming();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting screen capture: {ex.Message}");
                MessageBox.Show($"Capture error: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                StopStreaming();
            }
        }

        private void OnFrameCaptured(object? sender, Bitmap bitmap)
        {
            try
            {
                // Frame captured - ready for streaming
                // In next phase, this will be sent via WebRTC
                // For now, we just verify capture is working
                
                if (_framesCaptured++ % 100 == 0) // Log every 100th frame
                {
                    Debug.WriteLine($"Frame captured: {bitmap.Width}x{bitmap.Height}, Total frames: {_framesCaptured}");
                    
                    // Update status with FPS
                    var stats = screenCaptureService?.GetStatistics();
                    if (stats.HasValue)
                    {
                        Debug.WriteLine($"Capture stats - Frames: {stats.Value.frames}, FPS: {stats.Value.fps:F2}");
                    }
                }

                // Send frame over WebRTC data channel at ~2 FPS (every 5th frame)
                if (_webrtcFrameCounter++ % 5 == 0)
                {
                    try
                    {
                        using (var ms = new MemoryStream())
                        {
                            // Downscale to reduce payload size on low-power servers
                            using var resized = DownscaleBitmap(bitmap, 640, 360);
                            var jpegCodec = ImageCodecInfo.GetImageDecoders()
                                .First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
                            var encoderParams = new EncoderParameters(1);
                            encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 30L);
                            resized.Save(ms, jpegCodec, encoderParams);
                            var bytes = ms.ToArray();
                            var base64 = Convert.ToBase64String(bytes);
                            var dataUrl = $"data:image/jpeg;base64,{base64}";

                            var sent = false;
                            if (webRtcService != null)
                            {
                                sent = webRtcService.SendFrame(dataUrl);
                            }

                            if ((!sent || webRtcService == null) && socketFrameFallbackEnabled && socketClient != null)
                            {
                                // Fallback: send frames via Socket.IO if data channel is unavailable
                                try
                                {
                                    if (!socketClient.Connected)
                                    {
                                        if (socketFrameSendCount % 30 == 0)
                                        {
                                            Console.WriteLine("Socket frame send skipped: socket not connected");
                                        }
                                    }
                                    else
                                    {
                                        var sendTask = socketClient.EmitAsync("frame", new { deviceId, dataUrl });
                                        sendTask.ContinueWith(task =>
                                        {
                                            if (task.IsFaulted && task.Exception != null)
                                            {
                                                Console.WriteLine($"Socket frame send error: {task.Exception.GetBaseException().Message}");
                                            }
                                        });

                                        socketFrameSendCount++;
                                        if (!loggedSocketFrameSend)
                                        {
                                            loggedSocketFrameSend = true;
                                            Console.WriteLine($"Socket frame send started size={dataUrl.Length} deviceId={deviceId}");
                                        }
                                        else if (socketFrameSendCount % 10 == 0)
                                        {
                                            Console.WriteLine($"Socket frame send count={socketFrameSendCount} size={dataUrl.Length}");
                                        }

                                        if (socketFrameSendCount % 30 == 0)
                                        {
                                            _ = Task.Run(async () =>
                                            {
                                                await socketClient.EmitAsync("frame-test", new { deviceId, size = dataUrl.Length });
                                            });
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Socket frame send failed: {ex.Message}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error sending WebRTC frame: {ex.Message}");
                    }
                }

                // Dispose bitmap after processing (or pass to encoder/streamer)
                bitmap?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling frame: {ex.Message}");
                // Dispose bitmap on error
                bitmap?.Dispose();
            }
        }

        private static Bitmap DownscaleBitmap(Bitmap source, int maxWidth, int maxHeight)
        {
            if (source.Width <= maxWidth && source.Height <= maxHeight)
            {
                return (Bitmap)source.Clone();
            }

            var widthRatio = (double)maxWidth / source.Width;
            var heightRatio = (double)maxHeight / source.Height;
            var scale = Math.Min(widthRatio, heightRatio);

            var targetWidth = Math.Max(1, (int)(source.Width * scale));
            var targetHeight = Math.Max(1, (int)(source.Height * scale));

            var resized = new Bitmap(targetWidth, targetHeight);
            using (var graphics = Graphics.FromImage(resized))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.DrawImage(source, 0, 0, targetWidth, targetHeight);
            }

            return resized;
        }

        private void OnCaptureError(object? sender, string error)
        {
            Debug.WriteLine($"Capture error: {error}");
            // Don't stop streaming on occasional errors, but log them
        }

        private void StopStreaming()
        {
            if (!isStreaming) return;

            this.Invoke((System.Windows.Forms.MethodInvoker)delegate
            {
                isStreaming = false;
                trayIcon.Icon = CreateTrayIcon(Color.Green);
                trayIcon.Text = "Accountability Agent - Connected";
                
                // Stop screen capture
                if (screenCaptureService != null)
                {
                    screenCaptureService.FrameCaptured -= OnFrameCaptured;
                    screenCaptureService.CaptureError -= OnCaptureError;
                    screenCaptureService.StopCapture();
                    
                    var stats = screenCaptureService.GetStatistics();
                    Debug.WriteLine($"Screen capture stopped. Final stats - Frames: {stats.frames}, FPS: {stats.fps:F2}, Duration: {stats.duration}");
                    
                    screenCaptureService.Dispose();
                    screenCaptureService = null;
                }
                
                if (overlayForm != null)
                {
                    overlayForm.Close();
                    overlayForm = null;
                }

                if (webRtcService != null)
                {
                    webRtcService.Dispose();
                    webRtcService = null;
                }
            });
        }

        private void GeneratePairingCode()
        {
            // If we already have a pairing code, show it
            if (!string.IsNullOrEmpty(pairingCode))
            {
                MessageBox.Show($"Pairing Code: {pairingCode}\n\nThis is your device's unique pairing code.\nIt persists across restarts.\n\nEnter this in the parent web app.", 
                    "Pairing Code", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            // Request pairing code from server
            if (socketClient != null && socketClient.Connected)
            {
                Console.WriteLine($"Requesting pairing code from server for device: {deviceId}");
                Debug.WriteLine($"Requesting pairing code from server for device: {deviceId}");
                pairingCodeRequestPending = true;
                socketClient.EmitAsync("generate-pairing-code", deviceId);
                
                MessageBox.Show("Requesting pairing code from server...\n\nA message will appear with your pairing code shortly.", 
                    "Generating Pairing Code", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Cannot generate pairing code.\n\nDevice is not connected to server.", 
                    "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ShowStatus()
        {
            string status = isStreaming ? "Active - Streaming" : "Idle - Connected";
            string connectionStatus = socketClient?.Connected == true ? "Connected" : "Disconnected";
            
            string captureInfo = "";
            if (screenCaptureService != null && isStreaming)
            {
                var stats = screenCaptureService.GetStatistics();
                captureInfo = $"\n\nScreen Capture:\nFrames: {stats.frames}\nFPS: {stats.fps:F2}\nDuration: {stats.duration:mm\\:ss}";
            }
            
            MessageBox.Show($"Status: {status}\nConnection: {connectionStatus}\nDevice ID: {deviceId}{captureInfo}", 
                "Agent Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ExitApplication()
        {
            // Stop capture if running
            if (screenCaptureService != null && screenCaptureService.IsCapturing)
            {
                StopStreaming();
            }

            trayIcon.Visible = false;
            socketClient?.DisconnectAsync();
            screenCaptureService?.Dispose();
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                this.ShowInTaskbar = false;
            }
            base.OnFormClosing(e);
        }
    }
}

