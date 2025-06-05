// File: Views/My Albums/MyAlbums.xaml.cs
using JNR;
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
using System.Windows.Input;
using System.Windows.Media; // Required for VisualTreeHelper
// Add using statements for other views if they are directly referenced for navigation parameters,
// but for App.NavigateTo, the type parameter is sufficient.
// using JNR.Views.Genres; // Example
// using JNR.Views.Charts; // Example
// using JNR.Views.About;  // Example

namespace JNR.Views.My_Albums
{
    public class MyAlbumDisplayItem : INotifyPropertyChanged
    {
        public string AlbumName { get; set; }
        public string ArtistName { get; set; }
        public string CoverArtUrl { get; set; }
        public string ReleaseYear { get; set; }
        public string Genre { get; set; }
        public string Mbid { get; set; }
        public int DiscogsDbId { get; set; } // Album.AlbumId from your DB

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
            this.Closed += (s, args) => App.WindowClosed(this); // Step 1: Register for central tracking
        }

        // Step 2: Implement EnsureCorrectRadioButtonIsChecked
        public void EnsureCorrectRadioButtonIsChecked()
        {
            var sidebarPanel = FindVisualChild<StackPanel>(this, "SidebarContentPanel");
            if (sidebarPanel == null) // Fallback if not named, assuming structure
            {
                var mainGrid = VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(this.Content as Border, 0) as Border, 0) as Viewbox, 0) as Grid;
                if (mainGrid != null)
                {
                    sidebarPanel = mainGrid.Children.OfType<StackPanel>().FirstOrDefault(p => Grid.GetRow(p) == 1 && Grid.GetColumn(p) == 0);
                }
            }

            if (sidebarPanel != null)
            {
                foreach (var child in sidebarPanel.Children.OfType<RadioButton>())
                {
                    if (child.Content?.ToString() == "My Albums")
                    {
                        child.IsChecked = true;
                        break;
                    }
                }
            }
        }

        // Helper method (can be moved to a shared utility class later)
        public static T FindVisualChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            if (parent == null) return null;
            T foundChild = null;
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                T childType = child as T;
                if (childType == null)
                {
                    foundChild = FindVisualChild<T>(child, childName); // Recurse
                    if (foundChild != null) break;
                }
                else if (!string.IsNullOrEmpty(childName))
                {
                    var frameworkElement = child as FrameworkElement;
                    if (frameworkElement != null && frameworkElement.Name == childName)
                    {
                        foundChild = (T)child;
                        break;
                    }
                    else
                    {
                        foundChild = FindVisualChild<T>(child, childName);
                        if (foundChild != null) break;
                    }
                }
                else
                {
                    foundChild = (T)child;
                    break;
                }
            }
            return foundChild;
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
                // Consider navigating to login via App.NavigateTo<LoginView>(this);
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
                        .Include(uar => uar.Album)
                        .Select(uar => new MyAlbumDisplayItem
                        {
                            DiscogsDbId = uar.Album.AlbumId,
                            AlbumName = uar.Album.Title,
                            ArtistName = uar.Album.Artist,
                            CoverArtUrl = uar.Album.CoverArtUrl ?? "/Images/placeholder_album.png",
                            ReleaseYear = uar.Album.ReleaseYear.HasValue ? uar.Album.ReleaseYear.ToString() : "N/A",
                            Genre = "N/A", // Placeholder
                            Mbid = uar.Album.IdSource == "mbid" ? uar.Album.ExternalAlbumId : null,
                        })
                        .ToListAsync();

                    if (favoriteAlbums.Any())
                    {
                        foreach (var album in favoriteAlbums) UserAlbums.Add(album);
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

        // Step 4: Update btnGoBackMyAlbums_Click
        private void btnGoBackMyAlbums_Click(object sender, RoutedEventArgs e)
        {
            App.NavigateToMainPage(this);
        }

        // Step 5: Update Album_Click
        private void Album_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is MyAlbumDisplayItem selectedAlbum)
            {
                App.NavigateToOverview(this,
                    selectedAlbum.AlbumName,
                    selectedAlbum.ArtistName,
                    selectedAlbum.Mbid,
                    selectedAlbum.CoverArtUrl);
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        // Step 3: Update SidebarNavigation_Click
        private void SidebarNavigation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.CommandParameter is string viewName)
            {
                if (viewName == "MyAlbums")
                {
                    rb.IsChecked = true; // Ensure it's checked
                    return; // Already in this view
                }

                switch (viewName)
                {
                    case "Genres": App.NavigateTo<JNR.Views.Genres.Genres>(this); break;
                    case "Charts": App.NavigateTo<JNR.Views.Charts>(this); break;
                    case "About": App.NavigateTo<JNR.Views.About>(this); break;
                    // Assuming MainPage is the search/home, handle separately if it's a sidebar option.
                    // For MyAlbums, there isn't a "Search" button in its sidebar, so this is fine.
                    case "Settings":
                    case "Links":
                        App.HandlePlaceholderNavigation(this, rb, viewName);
                        return; // Return to prevent IsChecked issues if rb isn't handled
                }
                // If navigated, the current window 'this' will be closed by App.NavigateTo
            }
        }

        // Step 6: Remove FindMyAlbumsRadioButtonAndCheck() - its functionality is now in EnsureCorrectRadioButtonIsChecked
        // and handled by App.HandlePlaceholderNavigation.
        // private void FindMyAlbumsRadioButtonAndCheck() { /* ... old code ... */ }
    }
}