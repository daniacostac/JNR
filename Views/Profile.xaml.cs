// File: Views/Profile.xaml.cs
using JNR.Models; // <-- ADD THIS
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
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Wpf;

namespace JNR.Views
{
    // The ProfileAlbumItem class has been REMOVED from this file.

    public partial class Profile : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private readonly int _userId;

        #region Bindable Properties
        private string _profileUsername;
        public string ProfileUsername { get => _profileUsername; set { _profileUsername = value; OnPropertyChanged(); } }

        private string _memberSince;
        public string MemberSince { get => _memberSince; set { _memberSince = value; OnPropertyChanged(); } }

        private string _totalCollectionCount = "0";
        public string TotalCollectionCount { get => _totalCollectionCount; set { _totalCollectionCount = value; OnPropertyChanged(); } }

        private string _averageRatingGiven = "N/A";
        public string AverageRatingGiven { get => _averageRatingGiven; set { _averageRatingGiven = value; OnPropertyChanged(); } }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(nameof(ShowContent)); OnPropertyChanged(nameof(ShowStatusMessage)); } }

        private string _statusMessage;
        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; OnPropertyChanged(); } }

        public bool ShowContent => !IsLoading && string.IsNullOrEmpty(StatusMessage);
        public bool ShowStatusMessage => IsLoading || !string.IsNullOrEmpty(StatusMessage);

        public ObservableCollection<ProfileAlbumItem> UserAlbums { get; set; }

        private SeriesCollection _userAlbumsChartSeries;
        public SeriesCollection UserAlbumsChartSeries { get => _userAlbumsChartSeries; set { _userAlbumsChartSeries = value; OnPropertyChanged(); } }

        private string[] _userAlbumsChartLabels;
        public string[] UserAlbumsChartLabels { get => _userAlbumsChartLabels; set { _userAlbumsChartLabels = value; OnPropertyChanged(); } }

        private bool _showChart;
        public bool ShowChart { get => _showChart; set { _showChart = value; OnPropertyChanged(); } }
        #endregion

        public Profile(int userId)
        {
            InitializeComponent();
            this.DataContext = this;
            _userId = userId;

            UserAlbums = new ObservableCollection<ProfileAlbumItem>();
            UserAlbumsChartSeries = new SeriesCollection();
            UserAlbumsChartLabels = new string[] { };
            ShowChart = false;

            this.Loaded += Profile_Loaded;
            this.Closed += (s, args) => App.WindowClosed(this);
        }

        private async void Profile_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadUserProfileAsync();
        }

        private async Task LoadUserProfileAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading user profile...";

            var optionsBuilder = new DbContextOptionsBuilder<JnrContext>();
            string connectionString = "Server=localhost;Port=3306;Database=jnr;Uid=root;Pwd=root;";
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

            try
            {
                using (var dbContext = new JnrContext(optionsBuilder.Options))
                {
                    var user = await dbContext.Users.FindAsync(_userId);
                    if (user == null)
                    {
                        StatusMessage = "User not found.";
                        IsLoading = false;
                        return;
                    }

                    ProfileUsername = user.Username;
                    MemberSince = $"Member since {user.CreatedAt:MMMM yyyy}";

                    var ratedAlbums = await dbContext.Useralbumratings
                        .Where(uar => uar.UserId == _userId)
                        .Include(uar => uar.Album)
                        .OrderByDescending(uar => uar.RatedAt)
                        .ToListAsync();

                    if (ratedAlbums.Any())
                    {
                        TotalCollectionCount = ratedAlbums.Count.ToString();

                        var ratingsWithNumericValue = ratedAlbums.Where(r => r.Rating >= 0).ToList();
                        if (ratingsWithNumericValue.Any())
                        {
                            AverageRatingGiven = $"{ratingsWithNumericValue.Average(r => r.Rating):F1}/10";
                        }

                        UserAlbums.Clear();
                        foreach (var rating in ratedAlbums)
                        {
                            UserAlbums.Add(new ProfileAlbumItem
                            {
                                AlbumName = rating.Album.Title,
                                ArtistName = rating.Album.Artist,
                                CoverArtUrl = rating.Album.CoverArtUrl ?? "/Images/placeholder_album.png",
                                Mbid = rating.Album.IdSource == "mbid" ? rating.Album.ExternalAlbumId : null,
                                UserRating = rating.Rating
                            });
                        }

                        // Prepare chart data
                        var artistCounts = ratedAlbums
                            .GroupBy(a => a.Album.Artist)
                            .Select(g => new { Artist = g.Key, Count = g.Count() })
                            .OrderByDescending(x => x.Count)
                            .ThenBy(x => x.Artist)
                            .Take(10)
                            .ToList();

                        if (artistCounts.Any())
                        {
                            var barGradient = new LinearGradientBrush
                            {
                                StartPoint = new Point(0, 0),
                                EndPoint = new Point(1, 1),
                                GradientStops = new GradientStopCollection
                                {
                                    new GradientStop((Color)ColorConverter.ConvertFromString("#DA34A3"), 0.0),
                                    new GradientStop((Color)ColorConverter.ConvertFromString("#462AD8"), 1.0)
                                }
                            };

                            UserAlbumsChartLabels = artistCounts.Select(ac => ac.Artist).ToArray();
                            UserAlbumsChartSeries = new SeriesCollection
                            {
                                new ColumnSeries
                                {
                                    Title = "Albums",
                                    Values = new ChartValues<int>(artistCounts.Select(ac => ac.Count)),
                                    Fill = barGradient
                                }
                            };
                            ShowChart = true;
                        }
                    }
                    else
                    {
                        TotalCollectionCount = "0";
                        AverageRatingGiven = "N/A";
                        StatusMessage = $"{user.Username} has not added any albums to their collection yet.";
                    }
                }
                StatusMessage = null; // Clear status message on success
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading user profile: {ex.ToString()}");
                StatusMessage = "An error occurred while loading the profile.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        #region Window and Navigation Handlers
        private void Album_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ProfileAlbumItem selectedAlbum)
            {
                App.NavigateToOverview(this, selectedAlbum.AlbumName, selectedAlbum.ArtistName, selectedAlbum.Mbid, selectedAlbum.CoverArtUrl);
            }
        }

        private void btnGoBack_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SidebarNavigation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.CommandParameter is string viewName)
            {
                switch (viewName)
                {
                    case "MyAlbums": App.NavigateTo<My_Albums.MyAlbums>(this); break;
                    case "Genres": App.NavigateTo<Genres.Genres>(this); break;
                    case "Charts": App.NavigateTo<Charts>(this); break;
                    case "About": App.NavigateTo<About>(this); break;
                    case "Settings": App.NavigateTo<Settings.Settings>(this); break;
                    case "Links": App.HandlePlaceholderNavigation(this, rb, "Links"); break;
                }
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        #endregion
    }
}