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
            Thread.Sleep(200);

            topBorder.Close();
            bottomBorder.Close();
            leftBorder.Close();
            rightBorder.Close();
            // await Task.Delay(200);  // Hide borders for 200ms
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
}
