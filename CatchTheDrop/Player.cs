namespace Beps.CatchTheDrop
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Shapes;
    using Microsoft.Research.Kinect.Nui;

    public class Player
    {
        #region Private fields
        private readonly Brush m_BrBones;
        private Rect m_PlayerBounds;
        private Point m_PlayerCenter;
        private double m_PlayerScale;
        private readonly int m_Id;
        private readonly int m_PlayerNum;
        private static int m_ColorId;
        private readonly Image m_ImageHead1;
        private readonly Image m_ImageHead2;
        private readonly Image m_ImageHandLeft;
        private readonly Image m_ImageHandLeftHalf;
        private readonly Image m_ImageHandLeftFull;
        private readonly Image m_ImageHandRight;
        private readonly Image m_ImageHandRightHalf;
        private readonly Image m_ImageHandRightFull;

        private const double BoneSize = 0.01;
        private const double HeadSize = 0.08;
        private const double HandSize = 0.1;
        #endregion

        #region Properties
        public bool IsAlive { get; set; }
        public DateTime LastUpdated { get; set; }
        public Dictionary<Bone, BoneData> Segments { get; set; }
        #endregion

        #region Constructor
        public Player(int skeletonSlot, int playerNum)
        {
            Segments = new Dictionary<Bone, BoneData>();

            m_Id = skeletonSlot;
            m_PlayerNum = playerNum;

            // Generate one of 7 colors for player
            int[] iMixr = {1, 1, 1, 0, 1, 0, 0};

            m_ColorId = (m_ColorId + 1) % iMixr.Count();

            m_BrBones = new SolidColorBrush(Color.FromRgb(255, 81, 00));
            LastUpdated = DateTime.Now;

            m_ImageHead1 = new Image {
                Source = new BitmapImage(new Uri("Images/head1.png", UriKind.Relative)),
                Width = 80.0,
                Height = 80.0
            };

            m_ImageHead2 = new Image {
                Source = new BitmapImage(new Uri("Images/head2.png", UriKind.Relative)),
                Width = 80.0,
                Height = 80.0
            };

            m_ImageHandLeft = new Image {
                Source = new BitmapImage(new Uri("Images/glass_empty.png", UriKind.Relative)),
                Width = 50.0,
                Height = 50.0
            };

            m_ImageHandLeftHalf = new Image {
                Source = new BitmapImage(new Uri("Images/glass_half.png", UriKind.Relative)),
                Width = 50.0,
                Height = 50.0
            };

            m_ImageHandLeftFull = new Image {
                Source = new BitmapImage(new Uri("Images/glass_full.png", UriKind.Relative)),
                Width = 50.0,
                Height = 50.0
            };

            m_ImageHandRight = new Image {
                Source = new BitmapImage(new Uri("Images/glass_empty.png", UriKind.Relative)),
                Width = 50.0,
                Height = 50.0
            };

            m_ImageHandRightHalf = new Image {
                Source = new BitmapImage(new Uri("Images/glass_half.png", UriKind.Relative)),
                Width = 50.0,
                Height = 50.0
            };

            m_ImageHandRightFull = new Image {
                Source = new BitmapImage(new Uri("Images/glass_full.png", UriKind.Relative)),
                Width = 50.0,
                Height = 50.0
            };
        }
        #endregion

        #region Methods
        public int GetID()
        {
            return m_Id;
        }

        public void SetBounds(Rect r)
        {
            m_PlayerBounds = r;
            m_PlayerCenter.X = (m_PlayerBounds.Left + m_PlayerBounds.Right) / 2;
            m_PlayerCenter.Y = (m_PlayerBounds.Top + m_PlayerBounds.Bottom) / 2;
            m_PlayerScale = Math.Min(m_PlayerBounds.Width, m_PlayerBounds.Height / 2);
        }

        public void UpdateBonePosition(JointsCollection joints, JointID j1, JointID j2)
        {
            var seg = new Segment(joints[j1].Position.X * m_PlayerScale + m_PlayerCenter.X, m_PlayerCenter.Y - joints[j1].Position.Y * m_PlayerScale, joints[j2].Position.X * m_PlayerScale + m_PlayerCenter.X, m_PlayerCenter.Y - joints[j2].Position.Y * m_PlayerScale) {
                Radius = Math.Max(3.0, m_PlayerBounds.Height * BoneSize) / 2
            };
            UpdateSegmentPosition(j1, j2, seg);
        }

        public void UpdateJointPosition(JointsCollection joints, JointID j)
        {
            switch (j)
            {
                case JointID.Head:
                {
                    var segm = new Segment(joints[j].Position.X * m_PlayerScale + m_PlayerCenter.X, m_PlayerCenter.Y - joints[j].Position.Y * m_PlayerScale, m_PlayerNum == 0 ? m_ImageHead1 : m_ImageHead2, null, null, Hands.None) {
                        Radius = m_PlayerBounds.Height * (HeadSize)
                    };
                    UpdateSegmentPosition(j, j, segm);
                }
                    break;
                case JointID.HandLeft:
                {
                    var segm = new Segment(joints[j].Position.X * m_PlayerScale + m_PlayerCenter.X, m_PlayerCenter.Y - joints[j].Position.Y * m_PlayerScale, m_ImageHandLeft, m_ImageHandLeftHalf, m_ImageHandLeftFull, Hands.Left) {
                        Radius = m_PlayerBounds.Height * (HandSize / 2)
                    };
                    UpdateSegmentPosition(j, j, segm);
                }
                    break;
                case JointID.HandRight:
                {
                    var segm = new Segment(joints[j].Position.X * m_PlayerScale + m_PlayerCenter.X, m_PlayerCenter.Y - joints[j].Position.Y * m_PlayerScale, m_ImageHandRight, m_ImageHandRightHalf, m_ImageHandRightFull, Hands.Right) {
                        Radius = m_PlayerBounds.Height * (HandSize / 2)
                    };
                    UpdateSegmentPosition(j, j, segm);
                }
                    break;
                default:
                {
                    var seg = new Segment(joints[j].Position.X * m_PlayerScale + m_PlayerCenter.X, m_PlayerCenter.Y - joints[j].Position.Y * m_PlayerScale) {
                        Radius = m_PlayerBounds.Height * ((j == JointID.Head) ? HeadSize : HandSize) / 2
                    };
                    UpdateSegmentPosition(j, j, seg);
                }
                    break;
            }
        }

        public void Draw(UIElementCollection children, int playerScoreLeft, int playerScoreRight)
        {
            if (!IsAlive)
            {
                return;
            }

            // Draw all bones first, then images (head and hands).
            DateTime cur = DateTime.Now;
            foreach (var segment in Segments)
            {
                Segment seg = segment.Value.GetEstimatedSegment(cur);
                if (seg.IsCircle())
                {
                    continue;
                }
                var line = new Line {
                    StrokeThickness = seg.Radius * 2,
                    X1 = seg.X1,
                    Y1 = seg.Y1,
                    X2 = seg.X2,
                    Y2 = seg.Y2,
                    Stroke = m_BrBones,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeStartLineCap = PenLineCap.Round
                };
                children.Add(line);
            }

            foreach (Segment seg in Segments.Select(segment => segment.Value.GetEstimatedSegment(cur)))
            {
                switch (seg.HandType())
                {
                    case Hands.Left:
                    {
                        Image img = seg.GetImage(playerScoreLeft);
                        img.SetValue(Canvas.LeftProperty, seg.X1 - seg.Radius);
                        img.SetValue(Canvas.TopProperty, seg.Y1 - seg.Radius);
                        children.Add(img);
                    }
                        break;
                    case Hands.Right:
                    {
                        Image img = seg.GetImage(playerScoreRight);
                        img.SetValue(Canvas.LeftProperty, seg.X1 - seg.Radius);
                        img.SetValue(Canvas.TopProperty, seg.Y1 - seg.Radius);
                        children.Add(img);
                    }
                        break;
                    default:
                        if (seg.IsImage())
                        {
                            seg.Image.SetValue(Canvas.LeftProperty, seg.X1 - seg.Radius);
                            seg.Image.SetValue(Canvas.TopProperty, seg.Y1 - seg.Radius);
                            children.Add(seg.Image);
                        }
                        break;
                }
            }

            // Remove unused players after 1/2 second.
            if (DateTime.Now.Subtract(LastUpdated).TotalMilliseconds > 500)
            {
                IsAlive = false;
            }
        }
        #endregion

        #region Private functions
        private void UpdateSegmentPosition(JointID j1, JointID j2, Segment seg)
        {
            var bone = new Bone(j1, j2);
            if (Segments.ContainsKey(bone))
            {
                BoneData data = Segments[bone];
                data.UpdateSegment(seg);
                Segments[bone] = data;
            }
            else
            {
                Segments.Add(bone, new BoneData(seg));
            }
        }
        #endregion
    }
}
