namespace Beps.CatchTheDrop
{
    using System;
    using System.Collections.Generic;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;

    public class FlyingText
    {
        #region Private fields
        private Point m_Center;
        private readonly string m_Text;
        private Brush m_Brush;
        private double m_FontSize;
        private readonly double m_FontGrow;
        private double m_Alpha;
        private Label m_Label;
        private static readonly List<FlyingText> FlyingTexts = new List<FlyingText>();
        #endregion

        #region Constructor
        public FlyingText(string s, double size, Point ptCenter)
        {
            m_Text = s;
            m_FontSize = size;
            m_FontGrow = Math.Sqrt(size) * 0.4;
            m_Center = ptCenter;
            m_Alpha = 1.0;
            m_Label = null;
            m_Brush = null;
        }
        #endregion

        #region Methods
        public static void NewFlyingText(double size, Point center, string s)
        {
            FlyingTexts.Add(new FlyingText(s, size, center));
        }

        public void Advance()
        {
            m_Alpha -= 0.004;
            if (m_Alpha < 0)
            {
                m_Alpha = 0;
            }

            if (m_Brush == null)
            {
                m_Brush = new SolidColorBrush(Color.FromArgb(255, 255, 81, 0));
            }

            if (m_Label == null)
            {
                m_Label = FallingThings.MakeSimpleLabel(m_Text, new Rect(0, 0, 0, 0), m_Brush);
            }

            m_Brush.Opacity = Math.Pow(m_Alpha, 1.5);
            m_Label.Foreground = m_Brush;
            m_FontSize += m_FontGrow;
            m_Label.FontSize = m_FontSize;
            var rRendered = new Rect(m_Label.RenderSize);
            m_Label.SetValue(Canvas.LeftProperty, m_Center.X - rRendered.Width / 2);
            m_Label.SetValue(Canvas.TopProperty, m_Center.Y - rRendered.Height / 2);
        }

        public static void Draw(UIElementCollection children)
        {
            for (int i = 0; i < FlyingTexts.Count; i++)
            {
                FlyingText flyout = FlyingTexts[i];
                if (flyout.m_Alpha <= 0)
                {
                    FlyingTexts.Remove(flyout);
                    i--;
                }
            }

            foreach (FlyingText flyout in FlyingTexts)
            {
                flyout.Advance();
                children.Add(flyout.m_Label);
            }
        }
        #endregion
    }
}
