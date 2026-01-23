using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Forms;

namespace AccountabilityAgent
{
    /// <summary>
    /// Screen capture service using GDI+ for immediate functionality.
    /// Can be upgraded to Windows Graphics Capture API for better performance.
    /// </summary>
    public class ScreenCaptureService : IDisposable
    {
        private bool _isCapturing = false;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly object _lockObject = new object();
        
        // Frame rate limiting (target 10 FPS = 100ms per frame)
        private const int TARGET_FPS = 10;
        private const int FRAME_INTERVAL_MS = 1000 / TARGET_FPS;
        private DateTime _lastFrameTime = DateTime.MinValue;

        // Frame statistics
        private int _framesCaptured = 0;
        private DateTime _captureStartTime;

        // Event for when frames are captured
        public event EventHandler<Bitmap>? FrameCaptured;
        public event EventHandler<string>? CaptureError;

        public bool IsCapturing => _isCapturing;
        
        public int FramesCaptured => _framesCaptured;
        public double ActualFPS { get; private set; }

        /// <summary>
        /// Starts screen capture on the primary display
        /// </summary>
        public async Task<bool> StartCaptureAsync()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_isCapturing)
                    {
                        Debug.WriteLine("Screen capture already running");
                        return true;
                    }

                    _isCapturing = true;
                    _cancellationTokenSource = new CancellationTokenSource();
                    _framesCaptured = 0;
                    _captureStartTime = DateTime.Now;
                }

                Debug.WriteLine("Starting screen capture...");

                // Start capture loop on background thread
                _ = Task.Run(() => CaptureLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

                Debug.WriteLine("Screen capture started successfully");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting screen capture: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                CaptureError?.Invoke(this, $"Failed to start capture: {ex.Message}");
                
                lock (_lockObject)
                {
                    _isCapturing = false;
                }
                
                return false;
            }
        }

        /// <summary>
        /// Stops screen capture
        /// </summary>
        public void StopCapture()
        {
            lock (_lockObject)
            {
                if (!_isCapturing) return;

                Debug.WriteLine("Stopping screen capture...");
                _isCapturing = false;
                _cancellationTokenSource?.Cancel();
                
                var captureDuration = (DateTime.Now - _captureStartTime).TotalSeconds;
                if (captureDuration > 0)
                {
                    ActualFPS = _framesCaptured / captureDuration;
                    Debug.WriteLine($"Capture stopped. Frames: {_framesCaptured}, Duration: {captureDuration:F2}s, FPS: {ActualFPS:F2}");
                }
            }
        }

        /// <summary>
        /// Main capture loop with frame rate limiting
        /// </summary>
        private async Task CaptureLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (_isCapturing && !cancellationToken.IsCancellationRequested)
                {
                    var now = DateTime.Now;
                    var timeSinceLastFrame = (now - _lastFrameTime).TotalMilliseconds;

                    // Frame rate limiting - wait if we're too fast
                    if (timeSinceLastFrame < FRAME_INTERVAL_MS)
                    {
                        var delay = (int)(FRAME_INTERVAL_MS - timeSinceLastFrame);
                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }

                    try
                    {
                        // Capture screen frame
                        var bitmap = CaptureScreenGDI();
                        if (bitmap != null)
                        {
                            _framesCaptured++;
                            _lastFrameTime = DateTime.Now;
                            
                            // Fire event - subscribers should dispose the bitmap when done
                            FrameCaptured?.Invoke(this, bitmap);
                            
                            // Note: Bitmap disposal is the responsibility of event subscribers
                            // This allows for async processing without blocking capture
                        }
                        else
                        {
                            Debug.WriteLine("Failed to capture frame - bitmap is null");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error capturing frame: {ex.Message}");
                        CaptureError?.Invoke(this, $"Frame capture error: {ex.Message}");
                    }

                    // Small delay to prevent tight loop and allow other tasks
                    await Task.Delay(5, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Capture loop cancelled");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in capture loop: {ex.Message}");
                CaptureError?.Invoke(this, $"Capture loop error: {ex.Message}");
            }
            finally
            {
                Debug.WriteLine("Capture loop exited");
            }
        }

        /// <summary>
        /// Captures the primary screen using GDI+
        /// This is a working implementation that can be replaced with Windows Graphics Capture API
        /// </summary>
        private Bitmap? CaptureScreenGDI()
        {
            try
            {
                var primaryScreen = Screen.PrimaryScreen;
                if (primaryScreen == null)
                {
                    Debug.WriteLine("Primary screen is null");
                    return null;
                }

                var bounds = primaryScreen.Bounds;
                var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
                
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                }

                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error capturing screen with GDI: {ex.Message}");
                CaptureError?.Invoke(this, $"Capture error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets current capture statistics
        /// </summary>
        public (int frames, double fps, TimeSpan duration) GetStatistics()
        {
            lock (_lockObject)
            {
                var duration = _isCapturing 
                    ? DateTime.Now - _captureStartTime 
                    : TimeSpan.Zero;
                
                var fps = duration.TotalSeconds > 0 
                    ? _framesCaptured / duration.TotalSeconds 
                    : 0.0;
                
                return (_framesCaptured, fps, duration);
            }
        }

        public void Dispose()
        {
            StopCapture();
            _cancellationTokenSource?.Dispose();
        }
    }
}

