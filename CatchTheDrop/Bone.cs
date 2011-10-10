namespace Beps.CatchTheDrop
{
    using System;
    using Microsoft.Research.Kinect.Nui;

    public struct Bone
    {
        #region Properties
        public JointID Joint1;
        public JointID Joint2;
        #endregion

        #region Constructor
        public Bone(JointID j1, JointID j2)
        {
            Joint1 = j1;
            Joint2 = j2;
        }
        #endregion
    }

    public struct BoneData
    {
        #region Private fields
        private const double Smoothing = 0.8;
        #endregion

        #region Properties
        public Segment Seg;
        public Segment SegLast;
        public double XVel;
        public double YVel;
        public double XVel2;
        public double YVel2;
        public DateTime TimeLastUpdated;
        #endregion

        #region Constructor
        public BoneData(Segment s)
        {
            Seg = SegLast = s;
            XVel = YVel = 0;
            XVel2 = YVel2 = 0;
            TimeLastUpdated = DateTime.Now;
        }
        #endregion

        #region Methods
        // Update the segment's position and compute a smoothed velocity for the circle or the
        // endpoints of the segment based on  the time it took it to move from the last position
        // to the current one.  The velocity is in pixels per second.
        public void UpdateSegment(Segment s)
        {
            SegLast = Seg;
            Seg = s;

            DateTime cur = DateTime.Now;
            double fMs = cur.Subtract(TimeLastUpdated).TotalMilliseconds;
            if (fMs < 10.0)
            {
                fMs = 10.0;
            }

            double fFps = 1000.0 / fMs;
            TimeLastUpdated = cur;

            if (Seg.IsCircle())
            {
                XVel = XVel * Smoothing + (1.0 - Smoothing) * (Seg.X1 - SegLast.X1) * fFps;
                YVel = YVel * Smoothing + (1.0 - Smoothing) * (Seg.Y1 - SegLast.Y1) * fFps;
            }
            else
            {
                XVel = XVel * Smoothing + (1.0 - Smoothing) * (Seg.X1 - SegLast.X1) * fFps;
                YVel = YVel * Smoothing + (1.0 - Smoothing) * (Seg.Y1 - SegLast.Y1) * fFps;
                XVel2 = XVel2 * Smoothing + (1.0 - Smoothing) * (Seg.X2 - SegLast.X2) * fFps;
                YVel2 = YVel2 * Smoothing + (1.0 - Smoothing) * (Seg.Y2 - SegLast.Y2) * fFps;
            }
        }

        // Using the velocity calculated above, estimate where the segment is right now.
        public Segment GetEstimatedSegment(DateTime cur)
        {
            Segment estimate = Seg;
            double fMs = cur.Subtract(TimeLastUpdated).TotalMilliseconds;
            estimate.X1 += fMs * XVel / 1000.0;
            estimate.Y1 += fMs * YVel / 1000.0;
            if (Seg.IsCircle())
            {
                estimate.X2 = estimate.X1;
                estimate.Y2 = estimate.Y1;
            }
            else
            {
                estimate.X2 += fMs * XVel2 / 1000.0;
                estimate.Y2 += fMs * YVel2 / 1000.0;
            }
            return estimate;
        }
        #endregion
    }
}
