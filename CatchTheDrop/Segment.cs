namespace Beps.CatchTheDrop
{
    using System.Windows.Controls;

    public struct Segment
    {
        #region Properties
        public double X1;
        public double Y1;
        public double X2;
        public double Y2;
        public double Radius;
        public Image Image;
        public Image ImageEmpty;
        public Image ImageHalf;
        public Image ImageFull;
        public Hands Hand;
        #endregion

        #region Constructor
        public Segment(double x, double y)
        {
            Radius = 1;
            X1 = X2 = x;
            Y1 = Y2 = y;
            Image = null;
            ImageEmpty = null;
            ImageHalf = null;
            ImageFull = null;
            Hand = Hands.None;
        }

        public Segment(double x, double y, Image image, Image imHalf, Image imFull, Hands type)
        {
            Radius = 1;
            X1 = X2 = x;
            Y1 = Y2 = y;
            Hand = type;
            ImageEmpty = image;
            ImageHalf = imHalf;
            ImageFull = imFull;
            Image = ImageEmpty;
        }

        public Segment(double x1, double y1, double x2, double y2)
        {
            Radius = 1;
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
            Image = null;
            ImageEmpty = null;
            ImageHalf = null;
            ImageFull = null;
            Hand = Hands.None;
        }
        #endregion

        #region Methods
        public bool IsCircle()
        {
            return ((X1 == X2) && (Y1 == Y2));
        }

        public bool IsImage()
        {
            return Image != null;
        }

        public Hands HandType()
        {
            return Hand;
        }

        public Image GetImage(int score)
        {
            if (score < (int)Level.LevelHalf)
            {
                return ImageEmpty;
            }

            if (score >= (int)Level.LevelHalf && score <= (int)Level.LevelFull)
            {
                return ImageHalf;
            }

            if (score > (int)Level.LevelFull)
            {
                return ImageFull;
            }

            return null;
        }
        #endregion
    }
}
