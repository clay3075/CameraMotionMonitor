using System.Runtime.CompilerServices;

namespace CameraMotionMonitor;

using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;

class Program
{
    static async Task Main()
    {
        using var capture = new VideoCapture(1);  // Open default camera
        if (!capture.IsOpened())
        {
            Console.WriteLine("Camera not detected!");
            return;
        }

        Mat previousFrame = null;
        
        Console.WriteLine("Monitoring for motion...");

        while (true)
        {
            using Mat frame = new Mat();
            capture.Read(frame);
            
            if (frame.Empty()) continue;

            // Convert to grayscale for motion detection
            Cv2.CvtColor(frame, frame, ColorConversionCodes.BGR2GRAY);
            
            // Apply a blur to reduce sensitivity to small movements
            Cv2.GaussianBlur(frame, frame, new OpenCvSharp.Size(21, 21), 0);

            if (previousFrame != null && !previousFrame.Empty())
            {
                using Mat diff = new Mat();
                Cv2.Absdiff(previousFrame, frame, diff);
                Cv2.Threshold(diff, diff, 25, 255, ThresholdTypes.Binary); // Filter out small changes
                double motion = Cv2.Sum(diff).Val0;

                if (motion > 300000)  // Sensitivity threshold
                {
                    Console.WriteLine("Motion detected! Flashing screen...");
                    await FlashBorders(Color.Red);
                    await ShowVideoFeedAsync(capture); 
                }
                
                // Dispose of the previous frame after processing
                previousFrame.Dispose();
            }

            previousFrame = frame.Clone();
            await Task.Delay(30);  
        }
    }

    static async Task FlashScreenRed()
    {
        for (int i = 0; i < 1; i++)  // Flash x times
        {
            FlashFullScreen(Color.Red);
            await Task.Delay(200);  // Red for 200ms
        }
    }
    
    static async Task FlashBorders(Color color)
    {
        var borderWidth = 40;  // Width of the red border
        var screenWidth = Screen.PrimaryScreen.Bounds.Width;
        var screenHeight = Screen.PrimaryScreen.Bounds.Height;

        // Create border forms (top, bottom, left, right)
        using var topBorder = CreateBorderForm(0, 0, screenWidth, borderWidth, color);
        using var bottomBorder = CreateBorderForm(0, screenHeight - borderWidth, screenWidth, borderWidth, color);
        using var leftBorder = CreateBorderForm(0, 0, borderWidth, screenHeight, color);
        using var rightBorder = CreateBorderForm(screenWidth - borderWidth, 0, borderWidth, screenHeight, color);

        // Show borders and flash
        for (int i = 0; i < 1; i++) 
        {
            topBorder.Show();
            bottomBorder.Show();
            leftBorder.Show();
            rightBorder.Show();
            Task.Delay(200).Wait();

            topBorder.Close();
            bottomBorder.Close();
            leftBorder.Close();
            rightBorder.Close();
        }
    }

    static Form CreateBorderForm(int x, int y, int width, int height, Color color)
    {
        return new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            BackColor = color,
            StartPosition = FormStartPosition.Manual,
            Location = new System.Drawing.Point(x, y),
            Size = new System.Drawing.Size(width, height),
            TopMost = true,
            ShowInTaskbar = false
        };
    }

    static void FlashFullScreen(Color color)
    {
        // Create a form that covers the entire screen
        Form flashForm = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            BackColor = color,
            WindowState = FormWindowState.Maximized,
            TopMost = true,
            Opacity = 0.8  // Adjust opacity if needed
        };

        // Show flash effect and then close
        flashForm.Show();
        Application.DoEvents();  // Process UI events immediately
        Task.Delay(100).Wait();  // Small pause to ensure the flash shows
        flashForm.Close();
    }
    
    static async Task ShowVideoFeedAsync(VideoCapture capture)
    {
        using Form videoFeedForm = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            Location = new System.Drawing.Point(10, 10),  // Top-left corner
            Size = new System.Drawing.Size(320, 240),  // Small window size
            TopMost = true,
            ShowInTaskbar = false,
            BackColor = Color.Black
        };

        using PictureBox videoFeedBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.StretchImage
        };

        videoFeedForm.Controls.Add(videoFeedBox);
        videoFeedForm.Show();
        
        // Ensure the form is displayed before proceeding
        Application.DoEvents();

        Mat frame = new Mat();
        Bitmap bitmap = null;

        for (int i = 0; i < 50; i++)  // Display the video feed for ~5 seconds at 10 FPS
        {
            capture.Read(frame);
            if (!frame.Empty())
            {
                // Convert Mat to Bitmap
                bitmap?.Dispose();  // Dispose the previous bitmap
                bitmap = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(frame);

                // Update the PictureBox on the UI thread
                videoFeedBox.Invoke((MethodInvoker)(() =>
                {
                    videoFeedBox.Image?.Dispose();  // Dispose the previous image
                    videoFeedBox.Image = bitmap;   // Assign the new frame
                    Application.DoEvents();
                }));
            }

            Task.Delay(100).Wait();  // 10 FPS
        }

        // Clean up
        videoFeedForm.Hide();
        videoFeedBox.Image?.Dispose();  // Dispose the last frame
        bitmap?.Dispose();  // Dispose the final bitmap
        frame.Dispose();  // Dispose Mat to free OpenCV resources
    }
}
