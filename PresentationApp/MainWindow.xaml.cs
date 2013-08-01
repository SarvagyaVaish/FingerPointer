using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

using Microsoft.Kinect;
using System.IO;
using System.Windows.Media.Media3D;

namespace PresentationApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap colorBitmap;

        /// <summary>
        /// Intermediate storage for the depth data received from the camera
        /// </summary>
        private DepthImagePixel[] depthPixels;

        /// <summary>
        /// Intermediate storage for the depth data converted to color
        /// </summary>
        private byte[] colorPixels;

        /// <summary>
        /// The current line we have extrapolated from the presenter
        /// </summary>
        private _3DLine current_line;

        /// <summary>
        /// Projection plane
        /// </summary>
        private _3DPlane projection_plane;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Moves the cursor around
        /// </summary>
        private TranslateTransform player1transform, player2transform;

        private int Player1, Player2;

        /// <summary>
        /// Enumerator for calibration corners
        /// </summary>
        public enum CalibrationCorners { NONE, TOPLEFT, TOPRIGHT, BOTTOMLEFT, BOTTOMRIGHT, TRACKING };
        public CalibrationCorners calibrationState = CalibrationCorners.NONE;

        private static DispatcherTimer timer = new DispatcherTimer();

        struct corners
        {
            public Point3D topleft;
            public Point3D topright;
            public Point3D bottomleft;
            public Point3D bottomright;
        };

        private corners foundcorners;
        public MainWindow()
        {
            InitializeComponent();
            
            //Player Moverment
            player1transform = new TranslateTransform();
            Player1Marker.RenderTransform = player1transform;
            player2transform = new TranslateTransform();
            Player2Marker.RenderTransform = player2transform;

            Player1 = -1;
            Player2 = -1;

            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += OnTimedEvent;
            KinectInit();
        }

        public void KinectInit()
        {
             // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Smoothed with some latency.
                // Filters out medium jitters.
                // Good for a menu system that needs to be smooth but
                // doesn't need the reduced latency as much as gesture recognition does.
                TransformSmoothParameters smoothingParam = new TransformSmoothParameters();
                {
                    smoothingParam.Smoothing = 0.5f;
                    smoothingParam.Correction = 0.1f;
                    smoothingParam.Prediction = 0.5f;
                    smoothingParam.JitterRadius = 0.1f;
                    smoothingParam.MaxDeviationRadius = 0.1f;
                };

                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable(smoothingParam);

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                // Turn on the depth stream to receive depth frames
                this.sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);

                // Allocate space to put the depth pixels we'll receive
                this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];

                // Allocate space to put the color pixels we'll create
                this.colorPixels = new byte[this.sensor.DepthStream.FramePixelDataLength * sizeof(int)];

                // This is the bitmap we'll display on-screen
                this.colorBitmap = new WriteableBitmap(this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                // Set the image we display to point to the bitmap where we'll put the image data
                this.DepthCamera.Source = this.colorBitmap;

                // Add an event handler to be called whenever there is new depth frame data
                this.sensor.DepthFrameReady += this.SensorDepthFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }
        }// End KinectInit()


        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            // TODO: Delete this drawing function
            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                //dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, 640.0, 480.0));

                if (skeletons.Length != 0)
                {
                    bool player1playing = false;
                    bool player2playing = false;
                    int count = 0;
                    foreach (Skeleton skel in skeletons)
                    {
                        Joint rightelbow = skel.Joints[JointType.ElbowRight];
                        Joint righthand = skel.Joints[JointType.WristRight];

                        Point3D elbowXY = SkeletonPointToScreen(rightelbow.Position);
                        Point3D handXY = SkeletonPointToScreen(righthand.Position);

                        
                        if (skel.TrackingState == SkeletonTrackingState.Tracked && (Player1 == skel.TrackingId))
                        {
                            current_line = null;
                            Console.WriteLine("Player1 " + skel.TrackingId + ",  " + skel.TrackingState);
                            Player1Marker.Visibility = System.Windows.Visibility.Visible;

                            current_line = new _3DLine(elbowXY, handXY);
                            //current_line.printCoords();

                            //current_line.drawPoints(dc);
                            projection_plane = new _3DPlane();
                            Point3D p_i = CalcIntersection(current_line, projection_plane);
                            //Console.WriteLine(p_i.X + " , " + p_i.Y + " , " + p_i.Z);

                            p_i = calcMarkPos(p_i);
                            //Console.WriteLine(p_i.X + " , " + p_i.Y + " , " + p_i.Z);

                            player1transform.X = p_i.X;
                            player1transform.Y = p_i.Y;
                            player1playing = true;
                            Player1 = skel.TrackingId;
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.Tracked && (Player2 == skel.TrackingId))
                        {
                            current_line = null;
                            Console.WriteLine("Player2 " + skel.TrackingId + ",  " + skel.TrackingState);
                            Player2Marker.Visibility = System.Windows.Visibility.Visible;

                            current_line = new _3DLine(elbowXY, handXY);
                            //current_line.printCoords();

                            //current_line.drawPoints(dc);
                            projection_plane = new _3DPlane();
                            Point3D p_i = CalcIntersection(current_line, projection_plane);
                            //Console.WriteLine(p_i.X + " , " + p_i.Y + " , " + p_i.Z);

                            p_i = calcMarkPos(p_i);
                            //Console.WriteLine(p_i.X + " , " + p_i.Y + " , " + p_i.Z);

                            player2transform.X = p_i.X;
                            player2transform.Y = p_i.Y;
                            player2playing = true;
                            Player2 = skel.TrackingId;
                            break;
                        }
                        else if(skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            if (!player1playing)
                                Player1 = skel.TrackingId;

                            if (!player2playing)
                                Player2 = skel.TrackingId;
                        }

                        ++count;
                    }// End foreach (Skeleton skel in skeletons)
                }

            }

        

        }//End skeletonFrameReady

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point3D SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point3D(depthPoint.X, depthPoint.Y, depthPoint.Depth);
        }

        private Point3D CalcIntersection(_3DLine line, _3DPlane plane)
        {
            Point3D p1 = line.Point1;
            Point3D p2 = line.Point2;
            Point3D n = plane.normal_vec;
            Point3D u = line.u_vec;

            double s_i = -dotProduct(n, p1)/dotProduct(n, u);

            Point3D diffp1p2 = subtract(p2, p1);
            Point3D slope = smultiply(s_i, diffp1p2);
            
            return add(p1, slope);
        }

       

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
            Image.Source = this.imageSource;
        }

        // Specify what you want to happen when the Elapsed event is  
        // raised. 
        private void OnTimedEvent(object source, EventArgs e)
        {
            if (current_line == null)
            {
                timer.Stop();

                Instruction_Text_Block.Text = "Calibration failed, try again.";
                Timer_Text_Block.Text = "";
                Timer_Text_Block.Visibility = System.Windows.Visibility.Collapsed;
                calibrationState = CalibrationCorners.TOPLEFT;
                return;
            }

            Console.WriteLine("bitches");
            switch (calibrationState)
            {
                case CalibrationCorners.TOPLEFT:
                    {
                        if (Timer_Text_Block.Text.Equals("3"))
                        {
                            Timer_Text_Block.Text = "2";
                        }
                        else if (Timer_Text_Block.Text.Equals("2"))
                        {
                            Timer_Text_Block.Text = "1";
                        }
                        else if (Timer_Text_Block.Text.Equals("1"))
                        {
                            Timer_Text_Block.Text = "0";
                        }
                        else if (Timer_Text_Block.Text.Equals("0"))
                        {
                            Timer_Text_Block.Text = "3";
                            Instruction_Text_Block.Text = "Point to the top right corner";
                            foundcorners.topleft = CalcIntersection(current_line, new _3DPlane());
                            calibrationState = CalibrationCorners.TOPRIGHT;
                        }
                        else
                        {
                            Timer_Text_Block.Visibility = System.Windows.Visibility.Visible;
                            Timer_Text_Block.Text = "3";
                        }
                        return;
                    }
                case CalibrationCorners.TOPRIGHT:
                    {
                        if (Timer_Text_Block.Text.Equals("3"))
                        {
                            Timer_Text_Block.Text = "2";
                        }
                        else if (Timer_Text_Block.Text.Equals("2"))
                        {
                            Timer_Text_Block.Text = "1";
                        }
                        else if (Timer_Text_Block.Text.Equals("1"))
                        {
                            Timer_Text_Block.Text = "0";
                        }
                        else if (Timer_Text_Block.Text.Equals("0"))
                        {
                            Timer_Text_Block.Text = "3";
                            Instruction_Text_Block.Text = "Point to the bottom left corner";
                            foundcorners.topright = CalcIntersection(current_line, new _3DPlane());
                            calibrationState = CalibrationCorners.BOTTOMLEFT;
                        }
                        else
                        {
                            Timer_Text_Block.Text = "3";
                        }
                        return;
                    }
                case CalibrationCorners.BOTTOMLEFT:
                    {
                        if (Timer_Text_Block.Text.Equals("3"))
                        {
                            Timer_Text_Block.Text = "2";
                        }
                        else if (Timer_Text_Block.Text.Equals("2"))
                        {
                            Timer_Text_Block.Text = "1";
                        }
                        else if (Timer_Text_Block.Text.Equals("1"))
                        {
                            Timer_Text_Block.Text = "0";
                        }
                        else if (Timer_Text_Block.Text.Equals("0"))
                        {
                            Timer_Text_Block.Text = "3";
                            Instruction_Text_Block.Text = "Point to the bottom right corner";
                            foundcorners.bottomleft = CalcIntersection(current_line, new _3DPlane());
                            calibrationState = CalibrationCorners.BOTTOMRIGHT;
                        }
                        else
                        {
                            Timer_Text_Block.Text = "3";
                        }
                        return;
                    }
                case CalibrationCorners.BOTTOMRIGHT:
                    {
                        if (Timer_Text_Block.Text.Equals("3"))
                        {

                            Timer_Text_Block.Text = "2";
                        }
                        else if (Timer_Text_Block.Text.Equals("2"))
                        {
                            Timer_Text_Block.Text = "1";
                        }
                        else if (Timer_Text_Block.Text.Equals("1"))
                        {
                            Timer_Text_Block.Text = "0";
                        }
                        else if (Timer_Text_Block.Text.Equals("0"))
                        {
                            Timer_Text_Block.Visibility = System.Windows.Visibility.Collapsed;

                            foundcorners.bottomright = CalcIntersection(current_line, new _3DPlane());
                            calibrationState = CalibrationCorners.NONE;
                            timer.Stop();
                            Instruction_Text_Block.Text = "";
                            Instruction_Text_Block.Visibility = System.Windows.Visibility.Collapsed;
                        }
                        else
                        {
                            Timer_Text_Block.Text = "3";
                        }
                        return;
                    }
                case CalibrationCorners.NONE: break;
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's DepthFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyDepthImagePixelDataTo(this.depthPixels);

                    // Get the min and max reliable depth for the current frame
                    int minDepth = depthFrame.MinDepth;
                    int maxDepth = depthFrame.MaxDepth;

                    // Convert the depth to RGB
                    int colorPixelIndex = 0;
                    for (int i = 0; i < this.depthPixels.Length; ++i)
                    {
                        // Get the depth for this pixel
                        short depth = depthPixels[i].Depth;

                        // To convert to a byte, we're discarding the most-significant
                        // rather than least-significant bits.
                        // We're preserving detail, although the intensity will "wrap."
                        // Values outside the reliable depth range are mapped to 0 (black).

                        // Note: Using conditionals in this loop could degrade performance.
                        // Consider using a lookup table instead when writing production code.
                        // See the KinectDepthViewer class used by the KinectExplorer sample
                        // for a lookup table example.
                        byte intensity = (byte)(depth >= minDepth && depth <= maxDepth ? depth : 0);

                        // Write out blue byte
                        this.colorPixels[colorPixelIndex++] = intensity;

                        // Write out green byte
                        this.colorPixels[colorPixelIndex++] = intensity;

                        // Write out red byte                        
                        this.colorPixels[colorPixelIndex++] = intensity;

                        // We're outputting BGR, the last byte in the 32 bits is unused so skip it
                        // If we were outputting BGRA, we would write alpha here.
                        ++colorPixelIndex;
                    }

                    // Write the pixel data into our bitmap
                    this.colorBitmap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels,
                        this.colorBitmap.PixelWidth * sizeof(int),
                        0);
                }
            }
        }

        private void Calibrate_Button_Click(object sender, RoutedEventArgs e)
        {
            if (Calibrate_Button.Content.Equals("Calibrate"))
            {
                this.sensor.ElevationAngle= 0;
                Calibrate_Button.Content = "Done";
                Instruction_Text_Block.Text = "Make sure Kinect can see you!";
                Instruction_Text_Block.Visibility = System.Windows.Visibility.Visible;
                Down_Button.Visibility = System.Windows.Visibility.Visible;
                Up_Button.Visibility = System.Windows.Visibility.Visible;
                DepthCamera.Visibility = System.Windows.Visibility.Visible;
            }
            else if (Calibrate_Button.Content.Equals("Done"))
            {
                projection_plane = new _3DPlane();
                Calibrate_Button.Content = "Calibrate";
                DepthCamera.Visibility = System.Windows.Visibility.Collapsed;
                Instruction_Text_Block.Text = "Point to the top left corner";
                Down_Button.Visibility = System.Windows.Visibility.Collapsed;
                Up_Button.Visibility = System.Windows.Visibility.Collapsed;
                calibrationState = CalibrationCorners.TOPLEFT;
                timer.Start();
            }
        }

        private void Lower_Elevation_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.sensor.ElevationAngle -= 3;
            }
            catch { };
        }

        private void Raise_Elevation_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.sensor.ElevationAngle += 3;
            }
            catch { };
        }
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }


        private Point3D calcMarkPos(Point3D p_i)
        {
            double screen_width = MainWin.Height;
            double screen_height = MainWin.Width;

            double x = (p_i.X - foundcorners.topleft.X) * screen_width / (foundcorners.topright.X - foundcorners.topleft.X);
            double y = (p_i.Y - foundcorners.topleft.Y) * screen_width / (foundcorners.bottomleft.Y - foundcorners.topleft.Y);

            return new Point3D(x, y, 0);
        }

        private double dotProduct(Point3D point1, Point3D point2)
        {
            return (point1.X * point2.X) + (point1.Y * point2.Y) + (point1.Z * point2.Z);
        }

        private Point3D subtract(Point3D point1, Point3D point2)
        {
            return new Point3D(point1.X - point2.X, point1.Y - point2.Y, point1.Z - point2.Z);
        }

        private Point3D add(Point3D point1, Point3D point2)
        {
            return new Point3D(point1.X + point2.X, point1.Y + point2.Y, point1.Z + point2.Z);
        }

        private Point3D smultiply(double s, Point3D p)
        {
            return new Point3D(s * p.X, s * p.Y, s * p.Z);
        }

        private void Close_Button(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }
    }
}
