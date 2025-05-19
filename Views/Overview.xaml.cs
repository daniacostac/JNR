using System.Collections.Generic; // For List
using System.Collections.ObjectModel; // For ObservableCollection
using System.Windows;
using System.Windows.Input;
using JNR.Models; // Assuming TrackItem is in JNR.Models

namespace JNR.Views
{
    public partial class Overview : Window
    {
        // Property for data binding
        public ObservableCollection<TrackItem> AlbumTracks { get; set; }

        public Overview()
        {
            InitializeComponent();

            // Sample Data for AlbumTracks
            AlbumTracks = new ObservableCollection<TrackItem>
            {
                new TrackItem { Number = "1.", Title = "The End", Duration = "0:26"},
                new TrackItem { Number = "2.", Title = "Mercurial World", Duration = "3:01"},
                new TrackItem { Number = "3.", Title = "Dawning of the Season", Duration = "3:24"},
                new TrackItem { Number = "4.", Title = "Secrets (Your Fire)", Duration = "4:05"},
                new TrackItem { Number = "5.", Title = "You Lose!", Duration = "3:24"},
                new TrackItem { Number = "6.", Title = "Something for 2", Duration = "3:36"},
                new TrackItem { Number = "7.", Title = "Chaeri", Duration = "4:17"},
                new TrackItem { Number = "8.", Title = "Halfway", Duration = "1:58"},
                new TrackItem { Number = "9.", Title = "Hysterical Us", Duration = "3:55"},
                new TrackItem { Number = "10.", Title = "Prophecy", Duration = "3:33"},
                new TrackItem { Number = "11.", Title = "Follow The Leader", Duration = "3:04"},
                new TrackItem { Number = "12.", Title = "Domino", Duration = "3:50"},
                new TrackItem { Number = "13.", Title = "Dreamcatching", Duration = "3:27"},
                new TrackItem { Number = "14.", Title = "The Beginning", Duration = "3:27"}
            };

            // Set the DataContext for the window to itself so bindings can find AlbumTracks
            this.DataContext = this;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}