namespace Beps.CatchTheDrop
{
    using System;
    using System.Collections.Generic;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Shapes;

    public class FallingThings
    {
        #region Enums
        public enum ThingState
        {
            Falling = 0,
            Bouncing = 1,
            Dissolving = 2,
            Remove = 3
        }

        public enum GameMode
        {
            Off = 0,
            Solo = 1,
            TwoPlayer = 2
        }
        #endregion

        #region Internal structs
        private struct PolyDef
        {
            public int NumSides;
            public int Skip;
        }

        // The Thing struct represents a single object that is flying through the air, and
        // all of its properties.
        private struct Thing
        {
            public Point Center;
            public double Size;
            public double Theta;
            public double SpinRate;
            public double YVelocity;
            public double XVelocity;
            public PolyType Shape;
            public Color Color;
            public Brush Brush;
            public Brush Brush2;
            public Brush BrushPulse;
            public double Dissolve;
            public ThingState State;
            public DateTime TimeLastHit;
            public double AvgTimeBetweenHits;
            public int TouchedBy; // Last player to touch this thing
            public int Hotness; // Score level
            public int FlashCount;

            #region Private fields
            #endregion

            // Hit testing between this thing and a single segment.  If hit, the center point on
            // the segment being hit is returned, along with the spot on the line from 0 to 1 if
            // a line segment was hit.

            public bool Hit(Segment seg, ref Point ptHitCenter, ref double lineHitLocation)
            {
                double minDxSquared = Size + seg.Radius;
                minDxSquared *= minDxSquared;

                // See if falling thing hit this body segment
                if (seg.IsCircle())
                {
                    if (SquaredDistance(Center.X, Center.Y, seg.X1, seg.Y1) <= minDxSquared)
                    {
                        ptHitCenter.X = seg.X1;
                        ptHitCenter.Y = seg.Y1;
                        lineHitLocation = 0;
                        return true;
                    }
                }
                else
                {
                    double sqrLineSize = SquaredDistance(seg.X1, seg.Y1, seg.X2, seg.Y2);
                    if (sqrLineSize < 0.5) // if less than 1/2 pixel apart, just check dx to an endpoint
                    {
                        return (SquaredDistance(Center.X, Center.Y, seg.X1, seg.Y1) < minDxSquared);
                    }

                    // Find dx from center to line
                    double u = ((Center.X - seg.X1) * (seg.X2 - seg.X1) + (Center.Y - seg.Y1) * (seg.Y2 - seg.Y1)) / sqrLineSize;
                    if ((u >= 0) && (u <= 1.0))
                    {
                        // Tangent within line endpoints, see if we're close enough
                        double xIntersect = seg.X1 + (seg.X2 - seg.X1) * u;
                        double yIntersect = seg.Y1 + (seg.Y2 - seg.Y1) * u;

                        if (SquaredDistance(Center.X, Center.Y, xIntersect, yIntersect) < minDxSquared)
                        {
                            lineHitLocation = u;
                            ptHitCenter.X = xIntersect;
                            ptHitCenter.Y = yIntersect;
                            return true;
                        }
                    }
                    else
                    {
                        // See how close we are to an endpoint
                        if (u < 0)
                        {
                            if (SquaredDistance(Center.X, Center.Y, seg.X1, seg.Y1) < minDxSquared)
                            {
                                lineHitLocation = 0;
                                ptHitCenter.X = seg.X1;
                                ptHitCenter.Y = seg.Y1;
                                return true;
                            }
                        }
                        else
                        {
                            if (SquaredDistance(Center.X, Center.Y, seg.X2, seg.Y2) < minDxSquared)
                            {
                                lineHitLocation = 1;
                                ptHitCenter.X = seg.X2;
                                ptHitCenter.Y = seg.Y2;
                                return true;
                            }
                        }
                    }
                    return false;
                }
                return false;
            }

            // Change our velocity based on the object's velocity, our velocity, and where we hit.

            public void BounceOff(double x1, double y1, double otherSize, double fXv, double fYv)
            {
                double fX0 = Center.X;
                double fY0 = Center.Y;
                double fXv0 = XVelocity - fXv;
                double fYv0 = YVelocity - fYv;
                double dist = otherSize + Size;
                double fDx = Math.Sqrt((x1 - fX0) * (x1 - fX0) + (y1 - fY0) * (y1 - fY0));
                double xdif = x1 - fX0;
                double ydif = y1 - fY0;
                double newvx1 = 0.0;
                double newvy1 = 0.0;

                fX0 = x1 - xdif / fDx * dist;
                fY0 = y1 - ydif / fDx * dist;
                xdif = x1 - fX0;
                ydif = y1 - fY0;

                double bsq = dist * dist;
                double b = dist;
                double asq = fXv0 * fXv0 + fYv0 * fYv0;
                double a = Math.Sqrt(asq);
                if (a > 0.000001) // if moving much at all...
                {
                    double cx = fX0 + fXv0;
                    double cy = fY0 + fYv0;
                    double csq = (x1 - cx) * (x1 - cx) + (y1 - cy) * (y1 - cy);
                    double tt = asq + bsq - csq;
                    double bb = 2 * a * b;
                    double power = a * (tt / bb);
                    newvx1 -= 2 * (xdif / dist * power);
                    newvy1 -= 2 * (ydif / dist * power);
                }

                XVelocity += newvx1;
                YVelocity += newvy1;
                Center.X = fX0;
                Center.Y = fY0;
            }
        }
        #endregion

        #region Private fields
        private readonly Dictionary<PolyType, PolyDef> m_PolyDefs = new Dictionary<PolyType, PolyDef> {
            {PolyType.Circle, new PolyDef {
                NumSides = 1,
                Skip = 1
            }},
            {PolyType.Bubble, new PolyDef {
                NumSides = 0,
                Skip = 1
            }}
        };

        private readonly List<Thing> m_Things = new List<Thing>();
        private const double DissolveTime = 0.4;
        private readonly int m_MaxThings;
        private Rect m_SceneRect;
        private readonly Random m_Rnd = new Random();
        private double m_TargetFrameRate = 60;
        private double m_DropRate = 2.0;
        private double m_ShapeSize = 1.0;
        private double m_BaseShapeSize = 20;
        private GameMode m_GameMode = GameMode.Off;
        private const double BaseGravity = 0.017;
        private double m_Gravity = BaseGravity;
        private double m_GravityFactor = 1.0;
        private const double BaseAirFriction = 0.994;
        private double m_AirFriction = BaseAirFriction;
        private readonly int m_IntraFrames = 1;
        private int m_FrameCount;
        private bool m_DoRandomColors;
        private double m_ExpandingRate = 1.0;
        private Color m_BaseColor = Color.FromRgb(0, 130, 255);
        private PolyType m_PolyTypes = PolyType.Bubble;
        private readonly Dictionary<int, int> m_ScoresRight = new Dictionary<int, int>();
        private readonly Dictionary<int, int> m_ScoresLeft = new Dictionary<int, int>();
        private DateTime m_GameStartTime;
        private int m_CurrentLevel = 1;
        #endregion

        #region Methods
        public int GetPlayerScore(int playerKey, Hands type)
        {
            if (type == Hands.Right)
            {
                if (m_ScoresRight.ContainsKey(playerKey))
                {
                    return m_ScoresRight[playerKey];
                }
            }
            else if (type == Hands.Left)
            {
                if (m_ScoresLeft.ContainsKey(playerKey))
                {
                    return m_ScoresLeft[playerKey];
                }
            }
            return 0;
        }

        public FallingThings(int maxThings, double framerate, int intraFrames)
        {
            m_MaxThings = maxThings;
            m_IntraFrames = intraFrames;
            m_TargetFrameRate = framerate * intraFrames;
            SetGravity(m_GravityFactor);
            m_SceneRect.X = m_SceneRect.Y = 0;
            m_SceneRect.Width = m_SceneRect.Height = 100;
            m_ShapeSize = m_SceneRect.Height * m_BaseShapeSize / 1000.0;
            m_ExpandingRate = Math.Exp(Math.Log(6.0) / (m_TargetFrameRate * DissolveTime));
        }

        public void SetFramerate(double actualFramerate)
        {
            m_TargetFrameRate = actualFramerate * m_IntraFrames;
            m_ExpandingRate = Math.Exp(Math.Log(6.0) / (m_TargetFrameRate * DissolveTime));
            if (m_GravityFactor != 0)
            {
                SetGravity(m_GravityFactor);
            }
        }

        public void SetBoundaries(Rect r)
        {
            m_SceneRect = r;
            m_ShapeSize = r.Height * m_BaseShapeSize / 1000.0;
        }

        public void SetDropRate(double f)
        {
            m_DropRate = f;
        }

        public void SetSize(double f)
        {
            m_BaseShapeSize = f;
            m_ShapeSize = m_SceneRect.Height * m_BaseShapeSize / 1000.0;
        }

        public void SetShapesColor(Color color, bool doRandom)
        {
            m_DoRandomColors = doRandom;
            m_BaseColor = color;
        }

        public void Reset()
        {
            for (int i = 0; i < m_Things.Count; i++)
            {
                Thing thing = m_Things[i];
                if ((thing.State == ThingState.Bouncing) || (thing.State == ThingState.Falling))
                {
                    thing.State = ThingState.Dissolving;
                    thing.Dissolve = 0;
                    m_Things[i] = thing;
                }
            }
            m_GameStartTime = DateTime.Now;
            m_ScoresRight.Clear();
            m_ScoresLeft.Clear();
            SetGravity(1.0);
            m_CurrentLevel = 1;
        }

        public void SetGameMode(GameMode mode)
        {
            m_GameMode = mode;
            m_GameStartTime = DateTime.Now;
            m_CurrentLevel = 1;
            m_ScoresRight.Clear();
            m_ScoresLeft.Clear();
            SetGravity(1.0);
        }

        public void SetGravity(double f)
        {
            m_GravityFactor = f;
            m_Gravity = f * BaseGravity / m_TargetFrameRate / Math.Sqrt(m_TargetFrameRate) / Math.Sqrt(m_IntraFrames);
            m_AirFriction = (f == 0) ? 0.997 : Math.Exp(Math.Log(1.0 - (1.0 - BaseAirFriction) / f) / m_IntraFrames);

            if (f == 0) // Stop all movement as well!
            {
                for (int i = 0; i < m_Things.Count; i++)
                {
                    Thing thing = m_Things[i];
                    thing.XVelocity = thing.YVelocity = 0;
                    m_Things[i] = thing;
                }
            }
        }

        public void SetPolies(PolyType polies)
        {
            m_PolyTypes = polies;
        }

        public HitType LookForHits(Dictionary<Bone, BoneData> segments, int playerId)
        {
            DateTime cur = DateTime.Now;
            HitType allHits = HitType.None;

            // Zero out score if necessary
            if (!m_ScoresRight.ContainsKey(playerId))
            {
                m_ScoresRight.Add(playerId, 0);
            }

            if (!m_ScoresLeft.ContainsKey(playerId))
            {
                m_ScoresLeft.Add(playerId, 0);
            }

            foreach (var pair in segments)
            {
                for (int i = 0; i < m_Things.Count; i++)
                {
                    HitType hit = HitType.None;
                    Thing thing = m_Things[i];
                    Segment seg = pair.Value.GetEstimatedSegment(cur);
                    switch (thing.State)
                    {
                        case ThingState.Bouncing:
                        case ThingState.Falling:
                        {
                            var ptHitCenter = new Point(0, 0);
                            double lineHitLocation = 0;

                            if (thing.Hit(seg, ref ptHitCenter, ref lineHitLocation))
                            {
                                double fMs = 1000;
                                if (thing.TimeLastHit != DateTime.MinValue)
                                {
                                    fMs = cur.Subtract(thing.TimeLastHit).TotalMilliseconds;
                                    thing.AvgTimeBetweenHits = thing.AvgTimeBetweenHits * 0.8 + 0.2 * fMs;
                                }
                                thing.TimeLastHit = cur;

                                // Bounce off head and hands
                                if (seg.HandType() == Hands.Left || seg.HandType() == Hands.Right)
                                {
                                    // Bounce off of hand/head/foot
                                    thing.BounceOff(ptHitCenter.X, ptHitCenter.Y, seg.Radius, pair.Value.XVel / m_TargetFrameRate, pair.Value.YVel / m_TargetFrameRate);

                                    if (fMs > 100.0)
                                    {
                                        hit |= HitType.Captured;
                                    }
                                }

                                else // Bonce off line segment
                                {
                                    double xVel = pair.Value.XVel * (1.0 - lineHitLocation) + pair.Value.XVel2 * lineHitLocation;
                                    double yVel = pair.Value.YVel * (1.0 - lineHitLocation) + pair.Value.YVel2 * lineHitLocation;

                                    thing.BounceOff(ptHitCenter.X, ptHitCenter.Y, seg.Radius, xVel / m_TargetFrameRate, yVel / m_TargetFrameRate);

                                    if (fMs > 100.0)
                                    {
                                        hit |= HitType.Arm;
                                    }
                                }

                                if (m_GameMode == GameMode.TwoPlayer)
                                {
                                    if (thing.State == ThingState.Falling)
                                    {
                                        thing.State = ThingState.Bouncing;
                                        thing.TouchedBy = playerId;
                                        thing.Hotness = 1;
                                        thing.FlashCount = 0;
                                    }
                                    else if (thing.State == ThingState.Bouncing)
                                    {
                                        if (thing.TouchedBy != playerId)
                                        {
                                            if (seg.IsCircle())
                                            {
                                                thing.TouchedBy = playerId;
                                                thing.Hotness = Math.Min(thing.Hotness + 1, 4);
                                            }
                                        }
                                    }
                                }
                                else if (m_GameMode == GameMode.Solo)
                                {
                                    if (seg.IsCircle())
                                    {
                                        if (thing.State == ThingState.Falling)
                                        {
                                            thing.State = ThingState.Bouncing;
                                            thing.TouchedBy = playerId;
                                            thing.Hotness = 1;
                                            thing.FlashCount = 0;
                                        }
                                    }
                                }

                                m_Things[i] = thing;
                            }
                        }
                            break;
                    }

                    if ((hit & HitType.Captured) != 0)
                    {
                        thing.State = ThingState.Dissolving;
                        thing.Dissolve = 0;
                        thing.XVelocity = thing.YVelocity = 0;
                        thing.SpinRate = thing.SpinRate * 6 + 0.2;
                        m_Things[i] = thing;
                        AddToScore(thing.TouchedBy, 5, thing.Center, seg);
                    }

                    allHits |= hit;
                }
            }
            return allHits;
        }

        public static Label MakeSimpleLabel(string text, Rect bounds, Brush brush)
        {
            var label = new Label {
                Content = text
            };

            if (bounds.Width != 0)
            {
                label.SetValue(Canvas.LeftProperty, bounds.Left);
                label.SetValue(Canvas.TopProperty, bounds.Top);
                label.Width = bounds.Width;
                label.Height = bounds.Height;
            }
            label.Foreground = brush;
            label.FontFamily = new FontFamily("Arial");
            label.FontWeight = FontWeight.FromOpenTypeWeight(600);
            label.FontStyle = FontStyles.Normal;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            return label;
        }

        public void AdvanceFrame()
        {
            // Move all things by one step, accounting for gravity
            for (int i = 0; i < m_Things.Count; i++)
            {
                Thing thing = m_Things[i];
                thing.Center.Offset(thing.XVelocity, thing.YVelocity);
                thing.YVelocity += m_Gravity * m_SceneRect.Height;
                thing.YVelocity *= m_AirFriction;
                thing.XVelocity *= m_AirFriction;
                thing.Theta += thing.SpinRate;

                // bounce off walls
                if ((thing.Center.X - thing.Size < 0) || (thing.Center.X + thing.Size > m_SceneRect.Width))
                {
                    thing.XVelocity = -thing.XVelocity;
                    thing.Center.X += thing.XVelocity;
                }

                // Then get rid of one if any that fall off the bottom
                if (thing.Center.Y - thing.Size > m_SceneRect.Bottom)
                {
                    thing.State = ThingState.Remove;
                }

                // Get rid of after dissolving.
                if (thing.State == ThingState.Dissolving)
                {
                    thing.Dissolve += 1 / (m_TargetFrameRate * DissolveTime);
                    thing.Size *= m_ExpandingRate;
                    if (thing.Dissolve >= 1.0)
                    {
                        thing.State = ThingState.Remove;
                    }
                }
                m_Things[i] = thing;
            }

            // Then remove any that should go away now
            for (int i = 0; i < m_Things.Count; i++)
            {
                Thing thing = m_Things[i];
                if (thing.State == ThingState.Remove)
                {
                    m_Things.Remove(thing);
                    i--;
                }
            }

            // Create any new things to drop based on dropRate
            if ((m_Things.Count < m_MaxThings) && (m_Rnd.NextDouble() < m_DropRate / m_TargetFrameRate) && (m_PolyTypes != PolyType.None))
            {
                PolyType[] alltypes = {PolyType.Circle, PolyType.Bubble};
                byte r;
                byte g;
                byte b;

                if (m_DoRandomColors)
                {
                    r = (byte)(m_Rnd.Next(215) + 40);
                    g = (byte)(m_Rnd.Next(215) + 40);
                    b = (byte)(m_Rnd.Next(215) + 40);
                }
                else
                {
                    r = (byte)(Math.Min(255.0, m_BaseColor.R * (0.7 + m_Rnd.NextDouble() * 0.7)));
                    g = (byte)(Math.Min(255.0, m_BaseColor.G * (0.7 + m_Rnd.NextDouble() * 0.7)));
                    b = (byte)(Math.Min(255.0, m_BaseColor.B * (0.7 + m_Rnd.NextDouble() * 0.7)));
                }

                PolyType tryType;
                do
                {
                    tryType = alltypes[m_Rnd.Next(alltypes.Length)];
                } while ((m_PolyTypes & tryType) == 0);

                DropNewThing(tryType, m_ShapeSize, Color.FromRgb(r, g, b));
            }
        }

        public void DrawFrame(UIElementCollection children)
        {
            m_FrameCount++;

            // Draw all shapes in the scene
            for (int i = 0; i < m_Things.Count; i++)
            {
                Thing thing = m_Things[i];
                if (thing.Brush == null)
                {
                    thing.Brush = new SolidColorBrush(thing.Color);
                    double factor = 0.4 + ((double)thing.Color.R + thing.Color.G + thing.Color.B) / 1600;
                    thing.Brush2 = new SolidColorBrush(Color.FromRgb((byte)(255 - (255 - thing.Color.R) * factor), (byte)(255 - (255 - thing.Color.G) * factor), (byte)(255 - (255 - thing.Color.B) * factor)));
                    thing.BrushPulse = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                }

                if (thing.State == ThingState.Bouncing) // Pulsate edges
                {
                    double alpha = (Math.Cos(0.15 * (thing.FlashCount++) * thing.Hotness) * 0.5 + 0.5);

                    children.Add(MakeSimpleShape(m_PolyDefs[thing.Shape].NumSides, m_PolyDefs[thing.Shape].Skip, thing.Size, thing.Theta, thing.Center, thing.Brush, thing.BrushPulse, thing.Size * 0.1, alpha));
                    m_Things[i] = thing;
                }
                else
                {
                    if (thing.State == ThingState.Dissolving)
                    {
                        thing.Brush.Opacity = 1.0 - thing.Dissolve * thing.Dissolve;
                    }

                    children.Add(MakeSimpleShape(m_PolyDefs[thing.Shape].NumSides, m_PolyDefs[thing.Shape].Skip, thing.Size, thing.Theta, thing.Center, thing.Brush, (thing.State == ThingState.Dissolving) ? null : thing.Brush2, 1, 1));
                }
            }

            // Show scores
            if (m_ScoresRight.Count != 0)
            {
                int i = 0;
                foreach (var score in m_ScoresRight)
                {
                    Label label = MakeSimpleLabel(score.Value.ToString(), new Rect((0.12 + i * 0.8) * m_SceneRect.Width, 0.01 * m_SceneRect.Height, 0.09 * m_SceneRect.Width, 0.12 * m_SceneRect.Height), new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)));
                    label.HorizontalAlignment = HorizontalAlignment.Center;
                    label.VerticalAlignment = VerticalAlignment.Center;
                    label.HorizontalContentAlignment = HorizontalAlignment.Center;
                    label.VerticalContentAlignment = VerticalAlignment.Center;
                    label.FontSize = Math.Min(m_SceneRect.Width / 12, m_SceneRect.Height / 12);
                    label.Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0));
                    children.Add(label);
                    i++;
                }
            }

            if (m_ScoresLeft.Count != 0)
            {
                int i = 0;
                foreach (var score in m_ScoresLeft)
                {
                    Label label = MakeSimpleLabel(score.Value.ToString(), new Rect((0.02 + i * 0.8) * m_SceneRect.Width, 0.01 * m_SceneRect.Height, 0.09 * m_SceneRect.Width, 0.12 * m_SceneRect.Height), new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)));
                    label.HorizontalAlignment = HorizontalAlignment.Center;
                    label.VerticalAlignment = VerticalAlignment.Center;
                    label.HorizontalContentAlignment = HorizontalAlignment.Center;
                    label.VerticalContentAlignment = VerticalAlignment.Center;
                    label.FontSize = Math.Min(m_SceneRect.Width / 12, m_SceneRect.Height / 12);
                    label.Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0));
                    children.Add(label);
                    i++;
                }
            }

            // Show game timer
            if (m_GameMode != GameMode.Off)
            {
                TimeSpan span = DateTime.Now.Subtract(m_GameStartTime);
                string text = span.Minutes.ToString() + ":" + span.Seconds.ToString("00");

                Label timeText = MakeSimpleLabel(text, new Rect(0.9 * m_SceneRect.Width, 0.75 * m_SceneRect.Height, 0.1 * m_SceneRect.Width, 0.1 * m_SceneRect.Height), new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)));

                timeText.FontSize = m_SceneRect.Height / 16;
                timeText.Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0));
                timeText.HorizontalContentAlignment = HorizontalAlignment.Center;
                timeText.VerticalContentAlignment = VerticalAlignment.Center;
                children.Add(timeText);

                Label levelText = MakeSimpleLabel(string.Format("Level {0}", m_CurrentLevel), new Rect(0.8 * m_SceneRect.Width, 0.9 * m_SceneRect.Height, 0.2 * m_SceneRect.Width, 0.1 * m_SceneRect.Height), new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)));
                levelText.FontSize = m_SceneRect.Height / 16;
                levelText.Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0));
                levelText.HorizontalContentAlignment = HorizontalAlignment.Center;
                levelText.HorizontalAlignment = HorizontalAlignment.Left;

                levelText.VerticalContentAlignment = VerticalAlignment.Center;
                children.Add(levelText);
            }
        }
        #endregion

        #region Private functions
        private void AddToScore(int player, int points, Point center, Segment seg)
        {
            if (seg.HandType() == Hands.Right)
            {
                if (m_ScoresRight.ContainsKey(player))
                {
                    m_ScoresRight[player] = m_ScoresRight[player] + points;
                }
                else
                {
                    m_ScoresRight.Add(player, points);
                }
                FlyingText.NewFlyingText(m_SceneRect.Width / 300, center, "+" + points);
            }
            if (seg.HandType() == Hands.Left)
            {
                if (m_ScoresLeft.ContainsKey(player))
                {
                    m_ScoresLeft[player] = m_ScoresLeft[player] + points;
                }
                else
                {
                    m_ScoresLeft.Add(player, points);
                }
                FlyingText.NewFlyingText(m_SceneRect.Width / 300, center, "+" + points);
            }

            if (m_ScoresLeft.ContainsKey(player) && m_ScoresRight.ContainsKey(player))
            {
                if (m_ScoresLeft[player] > (int)Level.LevelChange && m_ScoresRight[player] > (int)Level.LevelChange)
                {
                    FlyingText.NewFlyingText(m_SceneRect.Width / 300, new Point(m_SceneRect.Width / 2, m_SceneRect.Height / 2), "Level Completed");
                    SetGravity(m_GravityFactor * 2.0);
                    m_CurrentLevel++;
                    m_ScoresLeft[player] = 0;
                    m_ScoresRight[player] = 0;
                }
            }
        }

        private static double SquaredDistance(double x1, double y1, double x2, double y2)
        {
            return ((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
        }

        private void DropNewThing(PolyType newShape, double newSize, Color newColor)
        {
            // Only drop within the center "square" area 
            double fDropWidth = (m_SceneRect.Bottom - m_SceneRect.Top);
            if (fDropWidth > m_SceneRect.Right - m_SceneRect.Left)
            {
                fDropWidth = m_SceneRect.Right - m_SceneRect.Left;
            }

            var newThing = new Thing {
                Size = newSize,
                YVelocity = (0.5 * m_Rnd.NextDouble() - 0.25) / m_TargetFrameRate,
                XVelocity = 0,
                Shape = newShape,
                Center = new Point(m_Rnd.NextDouble() * fDropWidth + (m_SceneRect.Left + m_SceneRect.Right - fDropWidth) / 2, m_SceneRect.Top - newSize),
                SpinRate = (m_Rnd.NextDouble() * 12.0 - 6.0) * 2.0 * Math.PI / m_TargetFrameRate / 4.0,
                Theta = 0,
                TimeLastHit = DateTime.MinValue,
                AvgTimeBetweenHits = 100,
                Color = newColor,
                Brush = null,
                Brush2 = null,
                BrushPulse = null,
                Dissolve = 0,
                State = ThingState.Falling,
                TouchedBy = 0,
                Hotness = 0,
                FlashCount = 0
            };

            m_Things.Add(newThing);
        }

        private static Shape MakeSimpleShape(int numSides, int skip, double size, double spin, Point center, Brush brush, Brush brushStroke, double strokeThickness, double opacity)
        {
            if (numSides <= 1)
            {
                var circle = new Ellipse {
                    Width = size * 2,
                    Height = size * 2,
                    Stroke = brushStroke
                };
                if (circle.Stroke != null)
                {
                    circle.Stroke.Opacity = opacity;
                }
                circle.StrokeThickness = strokeThickness * ((numSides == 1) ? 1 : 2);
                circle.Fill = (numSides == 1) ? brush : null;
                circle.SetValue(Canvas.LeftProperty, center.X - size);
                circle.SetValue(Canvas.TopProperty, center.Y - size);
                return circle;
            }

            var points = new PointCollection(numSides + 2);
            double theta = spin;
            for (int i = 0; i <= numSides + 1; ++i)
            {
                points.Add(new Point(Math.Cos(theta) * size + center.X, Math.Sin(theta) * size + center.Y));
                theta = theta + 2.0 * Math.PI * skip / numSides;
            }

            var polyline = new Polyline {
                Points = points,
                Stroke = brushStroke
            };
            if (polyline.Stroke != null)
            {
                polyline.Stroke.Opacity = opacity;
            }
            polyline.Fill = brush;
            polyline.FillRule = FillRule.Nonzero;
            polyline.StrokeThickness = strokeThickness;
            return polyline;
        }
        #endregion
    }
}
