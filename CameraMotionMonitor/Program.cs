using System.Text.Json;
using OpenCvSharp;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

class Program
{
    private static NotifyIcon notifyIcon { get; set; }
    private static bool isPaused = false;

    // Global position variables
    public static int globalX = 100; // Default starting position
    public static int globalY = 100;

    [STAThread]
    static void Main()
    {
        // Create a mutex to ensure only one instance of the application is running
        using Mutex mutex = new Mutex(true, "{BB9C8B47-DB02-4039-8D9E-7111471BA67C}", out bool isNewInstance);
        if (!isNewInstance)
        {
            // If the mutex is already acquired by another instance, notify the user and exit
            MessageBox.Show("CameraMotionMonitor: The application is already running - check your notification area / system tray!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return; // Exit the application
        }

        // Load position from the configuration file when the app starts
        LoadPosition();

        // Initialize the application in STAThread mode (required for UI elements like NotifyIcon)
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // notification area icon (tray)
        notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "Camera Motion Monitor",
            Icon = SystemIcons.Information  // Default system icon
        };

        // Create context menu for the system tray
        ContextMenuStrip contextMenu = new();
        ToolStripMenuItem pauseItem = new("Pause Monitoring");
        ToolStripMenuItem changePositionItem = new("Change Position");
        ToolStripMenuItem exitItem = new("Exit");

        // Hook up events for menu items
        pauseItem.Click += (sender, e) =>
        {
            isPaused = !isPaused;
            pauseItem.Text = isPaused ? "Resume Monitoring" : "Pause Monitoring";
        };

        changePositionItem.Click += (sender, e) =>
        {
            // Show the form to change the position
            var changePositionForm = new ChangePositionForm();
            changePositionForm.ShowDialog();
        };

        exitItem.Click += (sender, e) =>
        {
            // Exit application
            Application.Exit();
        };

        // Add items to context menu
        contextMenu.Items.AddRange(new ToolStripItem[] { pauseItem, changePositionItem, exitItem });

        // Assign context menu to the NotifyIcon
        notifyIcon.ContextMenuStrip = contextMenu;

        // Start video capture in a separate task
        Task.Run(() => StartVideoCapture(notifyIcon));

        // Run the application to keep the tray icon active
        Application.Run();
    }

    static async Task StartVideoCapture(NotifyIcon notifyIcon)
    {
        using var capture = new VideoCapture(1);  // Open default camera
        if (!capture.IsOpened())
        {
            Console.WriteLine("Camera not detected!");
            return;
        }

        Mat? previousFrame = null;

        Console.WriteLine("Monitoring for motion...");

        while (true)
        {
            if (!isPaused)
            {
                using Mat frame = new Mat();
                capture.Read(frame);

                if (frame.Empty()) continue;

                // Convert to grayscale for motion detection
                Cv2.CvtColor(frame, frame, ColorConversionCodes.BGR2GRAY);

                // Apply a blur to reduce sensitivity to small movements
                Cv2.Blur(frame, frame, new OpenCvSharp.Size(9, 9));

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
            }

            await Task.Delay(30);
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
            Location = new System.Drawing.Point(globalX, globalY),  // Configurable!
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
                    videoFeedForm.TopMost = true;
                    videoFeedForm.Refresh();
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

    // Function to load position from a JSON file
    static void LoadPosition()
    {
        string positionFile = "position.json";

        if (File.Exists(positionFile))
        {
            // Read the position from the file
            string json = File.ReadAllText(positionFile);
            var position = JsonSerializer.Deserialize<Position>(json);
            globalX = position.X;
            globalY = position.Y;
        }
    }

    // Function to save position to a JSON file
    public static void SavePosition()
    {
        string positionFile = "position.json";

        // Create a position object
        var position = new Position { X = globalX, Y = globalY };

        // Serialize the position object to JSON and save it to the file
        string json = JsonSerializer.Serialize(position);
        File.WriteAllText(positionFile, json);
    }

    // Class for storing the position
    public class Position
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
}

class ChangePositionForm : Form
{
    private Button btnUsePosition;
    private bool isDragging = false;
    private Point lastCursor;

    public ChangePositionForm()
    {
        // Set form size and properties
        this.Size = new Size(320, 240);
        this.Text = "Change Position";
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.StartPosition = FormStartPosition.Manual;
        this.Location = new Point(Program.globalX, Program.globalY);  // Load the current position

        // Button to use the new position
        btnUsePosition = new Button()
        {
            Text = "Use This Position",
            Location = new Point(100, 60),
            Size = new Size(120, 30)
        };
        btnUsePosition.Click += BtnUsePosition_Click;
        this.Controls.Add(btnUsePosition);

        // Handle the mouse events for dragging
        this.MouseDown += ChangePositionForm_MouseDown;
        this.MouseMove += ChangePositionForm_MouseMove;
        this.MouseUp += ChangePositionForm_MouseUp;
    }

    private void BtnUsePosition_Click(object sender, EventArgs e)
    {
        // Update the global X and Y values with the current position of the form's top-left corner
        Program.globalX = this.Location.X;
        Program.globalY = this.Location.Y;

        // Save the position to the file for persistence
        Program.SavePosition();

        MessageBox.Show($"Position updated: X={Program.globalX}, Y={Program.globalY}");
        this.Close();  // Close the form after updating the position
    }

    private void ChangePositionForm_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            isDragging = true;
            lastCursor = e.Location;
        }
    }

    private void ChangePositionForm_MouseMove(object sender, MouseEventArgs e)
    {
        if (isDragging)
        {
            var deltaX = e.X - lastCursor.X;
            var deltaY = e.Y - lastCursor.Y;
            this.Location = new Point(this.Left + deltaX, this.Top + deltaY);
        }
    }

    private void ChangePositionForm_MouseUp(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            isDragging = false;
        }
    }
}
