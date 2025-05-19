using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace starRatingSampler
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Star_MouseEnter(object sender, MouseEventArgs e)
        {
            var star = sender as Path;
            if (star != null)
            {
                // Determine which star half was hovered
                int starNumber = GetStarNumber(star);
                int half = star.Name.Contains("Right") ? 0 : 1;
                HighlightStars(starNumber, half);
            }
        }

        private int GetStarNumber(Path star)
        {
            if (star.Name.Contains("1")) return 1;
            if (star.Name.Contains("2")) return 2;
            if (star.Name.Contains("3")) return 3;
            if (star.Name.Contains("4")) return 4;
            if (star.Name.Contains("5")) return 5;
            return 0;
        }

        private void HighlightStars(int starNumber, int half)
        {
            // Highlight all stars up to the hovered one
            for (int i = 1; i <= 5; i++)
            {
                var left = FindName($"Star{i}Left") as Path;
                var right = FindName($"Star{i}Right") as Path;

                if (i < starNumber)
                {
                    right.Fill = Brushes.Yellow;
                    left.Fill = Brushes.Yellow;
                }
                else if (i == starNumber)
                {
                    right.Fill = half == 0 ? Brushes.Yellow : Brushes.Gray;
                    left.Fill = Brushes.Yellow;
                }
                else
                {
                    right.Fill = Brushes.Gray;
                    left.Fill = Brushes.Gray;
                }
            }
        }

        private void Star_MouseLeave(object sender, MouseEventArgs e)
        {
            // Reset to the current rating
            UpdateStars(currentRating);
        }

        private double currentRating = 0;

        private void Star_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var star = sender as Path;
            if (star != null)
            {
                // Determine which star half was clicked
                int starNumber = GetStarNumber(star);
                int half = star.Name.Contains("Left") ? 0 : 1;
                currentRating = starNumber - 0.5 + half * 0.5;
                UpdateStars(currentRating);
            }
        }

        private void UpdateStars(double rating)
        {
            for (int i = 1; i <= 5; i++)
            {
                var left = FindName($"Star{i}Left") as Path;
                var right = FindName($"Star{i}Right") as Path;

                if (rating >= i - 0.5)
                {
                    left.Fill = Brushes.Yellow;
                }
                else
                {
                    left.Fill = Brushes.Gray;
                }

                if (rating >= i)
                {
                    right.Fill = Brushes.Yellow;
                }
                else
                {
                    right.Fill = Brushes.Gray;
                }
            }
        }
    }
}