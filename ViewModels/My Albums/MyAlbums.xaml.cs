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
using System.Windows.Media; // Required for VisualTreeHelper and Brushes
using LiveCharts;
using LiveCharts.Wpf;

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

        private bool _hasListened;
        public bool HasListened
        {
            get => _hasListened;
            set
            {
                if (_hasListened != value)
                {
                    _hasListened = value;
                    OnPropertyChanged();
                }
            }
        }


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
            set { _isLoading = value; OnPropertyChanged(nameof(ShowStatusMessage)); OnPropertyChanged(nameof(ShowContent)); }
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        // Properties for the chart
        private SeriesCollection _userAlbumsChartSeries;
        public SeriesCollection UserAlbumsChartSeries
        {
            get => _userAlbumsChartSeries;
            set { _userAlbumsChartSeries = value; OnPropertyChanged(); }
        }

        private string[] _userAlbumsChartLabels;
        public string[] UserAlbumsChartLabels
        {
            get => _userAlbumsChartLabels;
            set { _userAlbumsChartLabels = value; OnPropertyChanged(); }
        }

        private bool _showChart;
        public bool ShowChart
        {
            get => _showChart;
            set { _showChart = value; OnPropertyChanged(); }
        }

        public Func<double, string> YFormatter { get; set; }

        // Helper properties for View visibility
        public bool ShowStatusMessage => IsLoading || UserAlbums.Count == 0;
        public bool ShowContent => !IsLoading && UserAlbums.Count > 0;

        public MyAlbums()
        {
            InitializeComponent();
            this.DataContext = this;
            UserAlbums = new ObservableCollection<MyAlbumDisplayItem>();
            UserAlbumsChartSeries = new SeriesCollection();
            UserAlbumsChartLabels = new string[] { };
            ShowChart = false;
            YFormatter = value => value.ToString("N0"); // Format Y-axis labels as whole numbers

            UserAlbums.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(ShowStatusMessage));
                OnPropertyChanged(nameof(ShowContent));
            };

            this.Loaded += MyAlbums_Loaded;
            this.Closed += (s, args) => App.WindowClosed(this);
        }

        public void EnsureCorrectRadioButtonIsChecked()
        {
            var sidebarPanel = FindVisualChild<StackPanel>(this, "SidebarContentPanel");
            if (sidebarPanel == null)
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
                    foundChild = FindVisualChild<T>(child, childName);
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
                return;
            }

            IsLoading = true;
            StatusMessage = "Loading your albums...";
            UserAlbums.Clear();
            ShowChart = false;

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
                        .OrderBy(uar => uar.Album.Artist).ThenBy(uar => uar.Album.Title)
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

                        var artistCounts = favoriteAlbums
                            .GroupBy(a => a.ArtistName)
                            .Select(g => new { Artist = g.Key, Count = g.Count() })
                            .OrderByDescending(x => x.Count)
                            .ThenBy(x => x.Artist)
                            .Take(10)
                            .ToList();

                        if (artistCounts.Any())
                        {
                            // *** MODIFICATION HERE: Create a gradient for the bars ***
                            var barGradient = new LinearGradientBrush
                            {
                                StartPoint = new Point(0.5, 0), // Top-center
                                EndPoint = new Point(0.5, 1)   // Bottom-center
                            };
                            barGradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#DA34A3"), 0.0)); // Pink at the top
                            barGradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#462AD8"), 1.0)); // Blue at the bottom
                            // **********************************************************

                            UserAlbumsChartLabels = artistCounts.Select(ac => ac.Artist).ToArray();
                            UserAlbumsChartSeries = new SeriesCollection
                            {
                                new ColumnSeries
                                {
                                    Title = "Albums",
                                    Values = new ChartValues<int>(artistCounts.Select(ac => ac.Count)),
                                    DataLabels = true,
                                    Fill = barGradient // Assign the gradient here
                                }
                            };
                            ShowChart = true;
                        }
                    }
                    else
                    {
                        StatusMessage = "You haven't added any albums to your favorites yet.";
                        ShowChart = false;
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

        private async void RemoveAlbum_Click(object sender, RoutedEventArgs e)
        {
            // Getting the DataContext which is the MyAlbumDisplayItem
            if ((sender as FrameworkElement)?.DataContext is not MyAlbumDisplayItem albumToRemove)
            {
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to remove '{albumToRemove.AlbumName}' from your collection?",
                "Confirm Removal",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                if (!SessionManager.CurrentUserId.HasValue)
                {
                    MessageBox.Show("Cannot perform this action. You are not logged in.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // The DiscogsDbId property holds the internal AlbumId from our database.
                int albumDbId = albumToRemove.DiscogsDbId;

                var optionsBuilder = new DbContextOptionsBuilder<JnrContext>();
                string connectionString = "Server=localhost;Port=3306;Database=jnr;Uid=root;Pwd=root;";
                optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

                try
                {
                    using (var dbContext = new JnrContext(optionsBuilder.Options))
                    {
                        var ratingToRemove = await dbContext.Useralbumratings.FirstOrDefaultAsync(uar =>
                            uar.UserId == SessionManager.CurrentUserId.Value &&
                            uar.AlbumId == albumDbId);

                        if (ratingToRemove != null)
                        {
                            dbContext.Useralbumratings.Remove(ratingToRemove);
                            await dbContext.SaveChangesAsync();

                            // Re-loading is the safest way to ensure both list and chart are in sync.
                            await LoadUserAlbumsAsync();

                            MessageBox.Show($"'{albumToRemove.AlbumName}' has been removed.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            // This case is unlikely if the UI is properly synced, but good to have.
                            MessageBox.Show("Could not find the album in your collection to remove. The list might be out of date.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                            // Refreshing the list anyway.
                            await LoadUserAlbumsAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error removing album: {ex.Message}");
                    MessageBox.Show($"An error occurred while removing the album: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void btnGoBackMyAlbums_Click(object sender, RoutedEventArgs e)
        {
            App.NavigateToMainPage(this);
        }

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

        private void SidebarNavigation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.CommandParameter is string viewName)
            {
                if (viewName == "MyAlbums")
                {
                    rb.IsChecked = true;
                    return;
                }

                switch (viewName)
                {
                    case "Genres": App.NavigateTo<JNR.Views.Genres.Genres>(this); break;
                    case "Charts": App.NavigateTo<JNR.Views.Charts>(this); break;
                    case "About": App.NavigateTo<JNR.Views.About>(this); break;
                    case "Settings": App.NavigateTo<JNR.Views.Settings.Settings>(this); break;
                    case "Links":
                        App.HandlePlaceholderNavigation(this, rb, viewName);
                        return;
                }
            }
        }
    }
}