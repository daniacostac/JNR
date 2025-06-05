// File: Views/My Albums/MyAlbums.xaml.cs
using JNR.Helpers; // For SessionManager
using JNR.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input; // Required for MouseButtonEventArgs
using System.Windows.Media;

namespace JNR.Views.My_Albums
{
    public class MyAlbumDisplayItem : INotifyPropertyChanged
    {
        public string AlbumName { get; set; }
        public string ArtistName { get; set; }
        public string CoverArtUrl { get; set; }
        public string ReleaseYear { get; set; }
        public string Genre { get; set; } // Assuming a primary genre for display
        public string Mbid { get; set; } // To navigate back to Overview if needed
        public int DiscogsDbId { get; set; } // The Album.AlbumId from your DB

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class MyAlbums : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private ObservableCollection<MyAlbumDisplayItem> _userAlbums;
        public ObservableCollection<MyAlbumDisplayItem> UserAlbums
        {
            get => _userAlbums;
            set { _userAlbums = value; OnPropertyChanged(); }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }


        public MyAlbums()
        {
            InitializeComponent();
            this.DataContext = this;
            UserAlbums = new ObservableCollection<MyAlbumDisplayItem>();
            this.Loaded += MyAlbums_Loaded;
        }

        private async void MyAlbums_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadUserAlbumsAsync();
        }

        private async Task LoadUserAlbumsAsync()
        {
            if (!SessionManager.CurrentUserId.HasValue)
            {
                StatusMessage = "Not logged in. Please log in to see your albums.";
                // Optionally, close this window or redirect to login
                // MessageBox.Show("You must be logged in to view My Albums.", "Login Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                // this.Close(); // Or open login window
                return;
            }

            IsLoading = true;
            StatusMessage = "Loading your albums...";
            UserAlbums.Clear();

            var optionsBuilder = new DbContextOptionsBuilder<JnrContext>();
            string connectionString = "Server=localhost;Port=3306;Database=jnr;Uid=root;Pwd=root;";
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

            try
            {
                using (var dbContext = new JnrContext(optionsBuilder.Options))
                {
                    var favoriteAlbums = await dbContext.Useralbumratings
                        .Where(uar => uar.UserId == SessionManager.CurrentUserId.Value)
                        // Add a condition for "favorite" if you use a special rating for it, e.g., uar.Rating == 0
                        .Include(uar => uar.Album) // Eager load Album details
                        .Select(uar => new MyAlbumDisplayItem
                        {
                            DiscogsDbId = uar.Album.AlbumId,
                            AlbumName = uar.Album.Title,
                            ArtistName = uar.Album.Artist,
                            CoverArtUrl = uar.Album.CoverArtUrl ?? "/Images/placeholder_album.png", // Fallback
                            ReleaseYear = uar.Album.ReleaseYear.HasValue ? uar.Album.ReleaseYear.ToString() : "N/A",
                            // Genre: You might need to store this separately or parse from a combined field if Album model had it
                            // For now, let's leave it blank or fetch if stored.
                            // If Useralbumratings also stored genre at time of favorite, use that.
                            Genre = "N/A", // Placeholder - You'll need to decide how to source this
                            Mbid = uar.Album.IdSource == "mbid" ? uar.Album.ExternalAlbumId : null,
                        })
                        .ToListAsync();

                    if (favoriteAlbums.Any())
                    {
                        foreach (var album in favoriteAlbums)
                        {
                            UserAlbums.Add(album);
                        }
                        StatusMessage = $"{UserAlbums.Count} album(s) in your favorites.";
                    }
                    else
                    {
                        StatusMessage = "You haven't added any albums to your favorites yet.";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading user albums: {ex.Message}");
                StatusMessage = "Error loading your albums. Please try again.";
                MessageBox.Show($"Could not load your albums: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
        private void btnGoBackMyAlbums_Click(object sender, RoutedEventArgs e)
        {
            var mainPage = Application.Current.Windows.OfType<JNR.Views.MainPage.MainPage>().FirstOrDefault();
            if (mainPage == null)
            {
                mainPage = new JNR.Views.MainPage.MainPage();
                mainPage.Show();
            }
            else
            {
                mainPage.Activate();
            }
            this.Close();
        }


        private void Album_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is MyAlbumDisplayItem selectedAlbum)
            {
                // Navigate to Overview page for this album
                // Note: You might need more details (like Discogs ID if Mbid is null) for a full Overview load.
                // For simplicity, we use what MyAlbumDisplayItem has.
                var overview = new Overview(
                    selectedAlbum.AlbumName,
                    selectedAlbum.ArtistName,
                    selectedAlbum.Mbid, // This might be null if favorited via Discogs ID
                    selectedAlbum.CoverArtUrl
                );
                overview.Owner = Application.Current.MainWindow; // Or this
                overview.Show();
                // Decide if you want to close MyAlbums window or keep it open
                // this.Close(); 
            }
        }


        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void SidebarNavigation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.CommandParameter is string viewName)
            {
                if (viewName == "MyAlbums") // Current view
                {
                    rb.IsChecked = true;
                    return;
                }

                Window newWindow = null;
                switch (viewName)
                {
                    // Assuming MainPage is the search hub
                    case "Genres": newWindow = new JNR.Views.Genres.Genres(); break;
                    case "Charts": newWindow = new JNR.Views.Charts(); break;
                    case "About": newWindow = new JNR.Views.About(); break;
                    case "Settings":
                    case "Links":
                        MessageBox.Show($"{viewName} page not yet implemented.", "Coming Soon");
                        // Re-check the "My Albums" button as we are not navigating away
                        FindMyAlbumsRadioButtonAndCheck();
                        return;
                }

                if (newWindow != null)
                {
                    newWindow.Owner = Application.Current.MainWindow; // Or this.Owner if preferred
                    newWindow.Show();
                    this.Close(); // Close current MyAlbums window
                }
            }
        }

        private void FindMyAlbumsRadioButtonAndCheck()
        {
            // Similar to FindAboutRadioButtonAndCheck, find the "My Albums" radio button and check it
            var sidebar = (this.Content as FrameworkElement)?.FindName("SidebarContentPanel") as StackPanel;
            if (sidebar == null)
            {
                // A more robust way to find the StackPanel might be needed if it's not named
                // For simplicity, this assumes the first StackPanel in the first column, second row
                var mainGrid = VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(this.Content as Border, 0) as Border, 0) as Viewbox, 0) as Grid;
                if (mainGrid != null && mainGrid.Children.Count > 1)
                {
                    sidebar = mainGrid.Children.OfType<StackPanel>().FirstOrDefault(p => Grid.GetRow(p) == 1 && Grid.GetColumn(p) == 0);
                }
            }

            if (sidebar != null)
            {
                foreach (var child in sidebar.Children)
                {
                    if (child is RadioButton rb && rb.Content?.ToString() == "My Albums")
                    {
                        rb.IsChecked = true;
                        break;
                    }
                }
            }
        }
    }
}