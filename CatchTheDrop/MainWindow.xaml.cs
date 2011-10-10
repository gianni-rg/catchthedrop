namespace Beps.CatchTheDrop
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Threading;
    using Microsoft.Research.Kinect.Nui;

    public class Win32
    {
        [DllImport("Winmm.dll")]
        public static extern int timeBeginPeriod(UInt32 uPeriod);
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        #region Private fields
        private const int TimerResolution = 2; // ms
        private const int NumIntraFrames = 3;
        private const int MaxShapes = 80;
        private const double MaxFramerate = 70;
        private const double MinFramerate = 15;
        private const double DefaultDropRate = 2.5;
        private const double DefaultDropSize = 32.0;
        private const double DefaultDropGravity = 1.0;

        private readonly Dictionary<int, Player> m_Players = new Dictionary<int, Player>();
        private int m_PlayerNum;
        private const double DropRate = DefaultDropRate;
        private const double DropSize = DefaultDropSize;
        private const double DropGravity = DefaultDropGravity;
        private DateTime m_LastFrameDrawn = DateTime.MinValue;
        private DateTime m_PredNextFrame = DateTime.MinValue;
        private double m_ActualFrameTime;

        // Player(s) placement in scene (z collapsed):
        private Rect m_PlayerBounds;
        private Rect m_ScreenRect;

        private double m_TargetFramerate = MaxFramerate;
        private int m_FrameCount;
        private bool m_RunningGameThread;
        private bool m_NuiInitialized;
        private FallingThings m_FallingThings;
        private int m_PlayersAlive;

        private readonly Runtime m_Nui = new Runtime();
        #endregion

        #region Constructor
        public MainWindow()
        {
            InitializeComponent();
        }
        #endregion

        #region Methods
        public Point GetDisplayPosition(Joint joint)
        {
            float depthX, depthY;
            m_Nui.SkeletonEngine.SkeletonToDepthImage(joint.Position, out depthX, out depthY);
            depthX = Math.Max(0, Math.Min(depthX * 320, 320)); //convert to 320, 240 space
            depthY = Math.Max(0, Math.Min(depthY * 240, 240)); //convert to 320, 240 space
            int colorX, colorY;
            var iv = new ImageViewArea();

            // only ImageResolution.Resolution640x480 is supported at this point
            m_Nui.NuiCamera.GetColorPixelCoordinatesFromDepthPixel(ImageResolution.Resolution640x480, iv, (int)depthX, (int)depthY, 0, out colorX, out colorY);

            // map back to skeleton.Width & skeleton.Height
            return new Point((int)(playfield.Width * colorX / 640.0) - 30, (int)(playfield.Height * colorY / 480) - 30);
        }
        #endregion

        #region Private functions
        private void nui_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            SkeletonFrame skeletonFrame = e.SkeletonFrame;

            int iSkeletonSlot = 0;

            foreach (SkeletonData data in skeletonFrame.Skeletons)
            {
                if (SkeletonTrackingState.Tracked == data.TrackingState)
                {
                    Player player;
                    if (m_Players.ContainsKey(iSkeletonSlot))
                    {
                        player = m_Players[iSkeletonSlot];
                    }
                    else
                    {
                        player = new Player(iSkeletonSlot, m_PlayerNum);
                        player.SetBounds(m_PlayerBounds);
                        m_Players.Add(iSkeletonSlot, player);
                        m_PlayerNum++;
                    }

                    player.LastUpdated = DateTime.Now;

                    // Update player's bone and joint positions
                    if (data.Joints.Count > 0)
                    {
                        player.IsAlive = true;

                        // Head, hands, feet (hit testing happens in order here)
                        player.UpdateJointPosition(data.Joints, JointID.Head);

                        player.UpdateJointPosition(data.Joints, JointID.HandLeft);

                        player.UpdateJointPosition(data.Joints, JointID.HandRight);
                        player.UpdateJointPosition(data.Joints, JointID.FootLeft);
                        player.UpdateJointPosition(data.Joints, JointID.FootRight);

                        // Hands and arms
                        player.UpdateBonePosition(data.Joints, JointID.HandRight, JointID.WristRight);
                        player.UpdateBonePosition(data.Joints, JointID.WristRight, JointID.ElbowRight);
                        player.UpdateBonePosition(data.Joints, JointID.ElbowRight, JointID.ShoulderRight);

                        player.UpdateBonePosition(data.Joints, JointID.HandLeft, JointID.WristLeft);
                        player.UpdateBonePosition(data.Joints, JointID.WristLeft, JointID.ElbowLeft);
                        player.UpdateBonePosition(data.Joints, JointID.ElbowLeft, JointID.ShoulderLeft);

                        // Head and Shoulders
                        player.UpdateBonePosition(data.Joints, JointID.ShoulderCenter, JointID.Head);
                        player.UpdateBonePosition(data.Joints, JointID.ShoulderLeft, JointID.ShoulderCenter);
                        player.UpdateBonePosition(data.Joints, JointID.ShoulderCenter, JointID.ShoulderRight);

                        // Legs
                        player.UpdateBonePosition(data.Joints, JointID.HipLeft, JointID.KneeLeft);
                        player.UpdateBonePosition(data.Joints, JointID.KneeLeft, JointID.AnkleLeft);
                        player.UpdateBonePosition(data.Joints, JointID.AnkleLeft, JointID.FootLeft);

                        player.UpdateBonePosition(data.Joints, JointID.HipRight, JointID.KneeRight);
                        player.UpdateBonePosition(data.Joints, JointID.KneeRight, JointID.AnkleRight);
                        player.UpdateBonePosition(data.Joints, JointID.AnkleRight, JointID.FootRight);

                        player.UpdateBonePosition(data.Joints, JointID.HipLeft, JointID.HipCenter);
                        player.UpdateBonePosition(data.Joints, JointID.HipCenter, JointID.HipRight);

                        // Spine
                        player.UpdateBonePosition(data.Joints, JointID.HipCenter, JointID.ShoulderCenter);
                    }
                }
                iSkeletonSlot++;
            }
        }

        private void CheckPlayers()
        {
            foreach (var player in m_Players.Where(player => !player.Value.IsAlive))
            {
                // Player left scene since we aren't tracking it anymore, so remove from dictionary
                m_Players.Remove(player.Value.GetID());
                m_PlayerNum = 0;

                break;
            }

            // Count alive players
            int alive = m_Players.Count(player => player.Value.IsAlive);

            if (alive != m_PlayersAlive)
            {
                if (alive == 2)
                {
                    m_FallingThings.SetGameMode(FallingThings.GameMode.TwoPlayer);
                }
                else if (alive == 1)
                {
                    m_FallingThings.SetGameMode(FallingThings.GameMode.Solo);
                }
                else if (alive == 0)
                {
                    m_FallingThings.SetGameMode(FallingThings.GameMode.Off);
                }

                m_PlayersAlive = alive;
            }
        }

        private void nui_ColorFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            // 32-bit per pixel, RGBA image
            PlanarImage image = e.ImageFrame.Image;
            video.Source = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgr32, null, image.Bits, image.Width * image.BytesPerPixel);
        }

        private bool InitializeNui()
        {
            UninitializeNui();
            if (m_Nui == null)
            {
                return false;
            }
            try
            {
                m_Nui.Initialize(RuntimeOptions.UseDepthAndPlayerIndex | RuntimeOptions.UseSkeletalTracking | RuntimeOptions.UseColor);
            }
            catch
            {
                return false;
            }

            m_Nui.DepthStream.Open(ImageStreamType.Depth, 2, ImageResolution.Resolution320x240, ImageType.DepthAndPlayerIndex);
            m_Nui.VideoStream.Open(ImageStreamType.Video, 2, ImageResolution.Resolution640x480, ImageType.Color);
            m_Nui.SkeletonEngine.TransformSmooth = true;
            m_NuiInitialized = true;
            return true;
        }

        private void UninitializeNui()
        {
            if ((m_Nui != null) && (m_NuiInitialized))
            {
                m_Nui.Uninitialize();
            }
            m_NuiInitialized = false;
        }

        private void UpdatePlayfieldSize()
        {
            // Size of player wrt size of playfield, putting ourselves low on the screen.
            m_ScreenRect.X = 0;
            m_ScreenRect.Y = 0;
            m_ScreenRect.Width = playfield.ActualWidth;
            m_ScreenRect.Height = playfield.ActualHeight;

            m_PlayerBounds.X = 0;
            m_PlayerBounds.Width = playfield.ActualWidth;
            m_PlayerBounds.Y = playfield.ActualHeight * 0.2;
            m_PlayerBounds.Height = playfield.ActualHeight * 0.75;

            foreach (var player in m_Players)
            {
                player.Value.SetBounds(m_PlayerBounds);
            }

            Rect rFallingBounds = m_PlayerBounds;
            rFallingBounds.Y = 0;
            rFallingBounds.Height = playfield.ActualHeight;
            if (m_FallingThings != null)
            {
                m_FallingThings.SetBoundaries(rFallingBounds);
            }
        }

        private void Window_Loaded(object sender, EventArgs e)
        {
            playfield.ClipToBounds = true;

            m_FallingThings = new FallingThings(MaxShapes, m_TargetFramerate, NumIntraFrames);

            UpdatePlayfieldSize();

            m_FallingThings.SetGravity(DropGravity);
            m_FallingThings.SetDropRate(DropRate);
            m_FallingThings.SetSize(DropSize);
            m_FallingThings.SetPolies(PolyType.All);
            m_FallingThings.SetGameMode(FallingThings.GameMode.Off);

            if ((m_Nui != null) && InitializeNui())
            {
                m_Nui.VideoFrameReady += nui_ColorFrameReady;
                m_Nui.SkeletonFrameReady += nui_SkeletonFrameReady;
            }

            Win32.timeBeginPeriod(TimerResolution);
            var gameThread = new Thread(GameThread);
            gameThread.SetApartmentState(ApartmentState.STA);
            gameThread.Start();

            FlyingText.NewFlyingText(m_ScreenRect.Width / 30, new Point(m_ScreenRect.Width / 2, m_ScreenRect.Height / 2), "3 2 1 Go!!!");
        }

        private void GameThread()
        {
            m_RunningGameThread = true;
            m_PredNextFrame = DateTime.Now;
            m_ActualFrameTime = 1000.0 / m_TargetFramerate;

            // Try to dispatch at as constant of a framerate as possible by sleeping just enough since
            // the last time we dispatched.
            while (m_RunningGameThread)
            {
                // Calculate average framerate.  
                DateTime now = DateTime.Now;
                if (m_LastFrameDrawn == DateTime.MinValue)
                {
                    m_LastFrameDrawn = now;
                }
                double ms = now.Subtract(m_LastFrameDrawn).TotalMilliseconds;
                m_ActualFrameTime = m_ActualFrameTime * 0.95 + 0.05 * ms;
                m_LastFrameDrawn = now;

                // Adjust target framerate down if we're not achieving that rate
                m_FrameCount++;
                if (((m_FrameCount % 100) == 0) && (1000.0 / m_ActualFrameTime < m_TargetFramerate * 0.92))
                {
                    m_TargetFramerate = Math.Max(MinFramerate, (m_TargetFramerate + 1000.0 / m_ActualFrameTime) / 2);
                }

                if (now > m_PredNextFrame)
                {
                    m_PredNextFrame = now;
                }
                else
                {
                    double msSleep = m_PredNextFrame.Subtract(now).TotalMilliseconds;
                    if (msSleep >= TimerResolution)
                    {
                        Thread.Sleep((int)(msSleep + 0.5));
                    }
                }
                m_PredNextFrame += TimeSpan.FromMilliseconds(1000.0 / m_TargetFramerate);

                Dispatcher.Invoke(DispatcherPriority.Send, new Action<int>(HandleGameTimer), 0);
            }
        }

        private void HandleGameTimer(int param)
        {
            if (m_FallingThings == null)
            {
                return;
            }

            // Every so often, notify what our actual framerate is
            if ((m_FrameCount % 100) == 0)
            {
                m_FallingThings.SetFramerate(1000.0 / m_ActualFrameTime);
            }

            // Advance animations, and do hit testing.
            for (int i = 0; i < NumIntraFrames; ++i)
            {
                foreach (var pair in m_Players)
                {
                    m_FallingThings.LookForHits(pair.Value.Segments, pair.Value.GetID());
                }
                m_FallingThings.AdvanceFrame();
            }

            // Draw new Wpf scene by adding all objects to canvas
            playfield.Children.Clear();
            m_FallingThings.DrawFrame(playfield.Children);

            foreach (var player in m_Players)
            {
                player.Value.Draw(playfield.Children, m_FallingThings.GetPlayerScore(player.Key, Hands.Left), m_FallingThings.GetPlayerScore(player.Key, Hands.Right));
            }
            FlyingText.Draw(playfield.Children);

            CheckPlayers();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            UninitializeNui();
            Environment.Exit(0);
        }
        #endregion
    }
}
