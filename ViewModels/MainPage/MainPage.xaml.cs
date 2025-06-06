// File: ViewModels/MainPage/MainPage.xaml.cs
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using JNR.Models.DiscogModels;
using JNR.Models.LastFmModels;
using JNR.Models.NewsApiModels;

namespace JNR.Views.MainPage
{
    public class MainPageSearchResultItem : INotifyPropertyChanged // Unchanged
    {
        private string _albumName;
        public string AlbumName { get => _albumName; set { _albumName = value; OnPropertyChanged(); } }
        private string _artistName;
        public string ArtistName { get => _artistName; set { _artistName = value; OnPropertyChanged(); } }
        private string _coverArtUrl;
        public string CoverArtUrl { get => _coverArtUrl; set { _coverArtUrl = value; OnPropertyChanged(); } }
        private string _releaseYear;
        public string ReleaseYear { get => _releaseYear; set { _releaseYear = value; OnPropertyChanged(); } }
        private string _primaryGenre;
        public string PrimaryGenre { get => _primaryGenre; set { _primaryGenre = value; OnPropertyChanged(); } }
        public string Mbid { get; set; }
        public int? DiscogsId { get; set; }
        public string SourceApi { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class NewsItemUI : INotifyPropertyChanged // Unchanged
    {
        private string _title;
        public string Title { get => _title; set { _title = value; OnPropertyChanged(); } }
        private string _sourceName;
        public string SourceName { get => _sourceName; set { _sourceName = value; OnPropertyChanged(); } }
        private string _url;
        public string Url { get => _url; set { _url = value; OnPropertyChanged(); } }
        private string _imageUrl;
        public string ImageUrl { get => _imageUrl; set { _imageUrl = value; OnPropertyChanged(); } }
        private string _publishedAtDisplay;
        public string PublishedAtDisplay { get => _publishedAtDisplay; set { _publishedAtDisplay = value; OnPropertyChanged(); } }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class MainPage : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _searchQuery;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (_searchQuery != value)
                {
                    _searchQuery = value;
                    OnPropertyChanged();
                    Debug.WriteLine($"SearchQuery property in ViewModel updated to: '{_searchQuery}'");
                }
            }
        }

        private string _mainContentTitle = "Search for anything!"; // Initial value
        public string MainContentTitle
        {
            get => _mainContentTitle;
            set { _mainContentTitle = value; OnPropertyChanged(); }
        }

        // NEW Property for placeholder visibility
        private bool _showInitialPlaceholder = true;
        public bool ShowInitialPlaceholder
        {
            get => _showInitialPlaceholder;
            set
            {
                if (_showInitialPlaceholder != value)
                {
                    _showInitialPlaceholder = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<MainPageSearchResultItem> SearchResults { get; set; }
        public ObservableCollection<NewsItemUI> MusicNewsItems { get; set; }

        private static readonly HttpClient discogsClient = new HttpClient();
        private const string DiscogsApiToken = "TMMBVQQgfXKTCEmgHqukhGLvhyCKJuLKlSqfrJCn";
        private const string DiscogsApiBaseUrl = "https://api.discogs.com";

        private static readonly HttpClient newsApiClient = new HttpClient();
        private const string NewsApiKey = "335eb9be8c7449f482c52362b10ab961";

        public MainPage()
        {
            InitializeComponent();
            this.DataContext = this;
            SearchResults = new ObservableCollection<MainPageSearchResultItem>();
            MusicNewsItems = new ObservableCollection<NewsItemUI>();

            ShowInitialPlaceholder = true; // Explicitly set initial state

            if (discogsClient.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                discogsClient.DefaultRequestHeaders.UserAgent.ParseAdd("JNR_WPF_App/1.0 (your.email@example.com)");
                discogsClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Discogs", $"token={DiscogsApiToken}");
            }
            if (newsApiClient.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                newsApiClient.DefaultRequestHeaders.UserAgent.ParseAdd("JNR_WPF_App/1.0 (your.email@example.com)");
                newsApiClient.DefaultRequestHeaders.Add("X-Api-Key", NewsApiKey);
            }

            BindingOperations.SetBinding(txtSearchAlbum, TextBox.TextProperty, new Binding("SearchQuery") { Source = this, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            this.Loaded += async (s, e) => await LoadMusicNewsAsync();
        }

        private async Task LoadMusicNewsAsync() // Unchanged
        {
            Debug.WriteLine("LoadMusicNewsAsync: Fetching music news...");
            MusicNewsItems.Clear();
            string newsApiUrl = $"https://newsapi.org/v2/top-headlines?country=us&category=entertainment&q=music&pageSize=7";
            try
            {
                HttpResponseMessage response = await newsApiClient.GetAsync(newsApiUrl);
                Debug.WriteLine($"LoadMusicNewsAsync: NewsAPI call returned. Status: {response.StatusCode}");
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var newsApiResponse = JsonSerializer.Deserialize<NewsApiResponse>(jsonResponse, options);
                    if (newsApiResponse?.Status == "ok" && newsApiResponse.Articles != null && newsApiResponse.Articles.Any())
                    {
                        Debug.WriteLine($"LoadMusicNewsAsync: Found {newsApiResponse.Articles.Count} news articles.");
                        foreach (var article in newsApiResponse.Articles)
                        {
                            MusicNewsItems.Add(new NewsItemUI
                            {
                                Title = article.Title,
                                SourceName = article.Source?.Name ?? "Unknown Source",
                                Url = article.Url,
                                ImageUrl = article.UrlToImage,
                                PublishedAtDisplay = article.PublishedAt.ToLocalTime().ToString("g")
                            });
                        }
                    }
                    else if (newsApiResponse?.Status == "error")
                    {
                        Debug.WriteLine($"LoadMusicNewsAsync: NewsAPI returned error: {newsApiResponse.Code} - {newsApiResponse.Message}"); MusicNewsItems.Add(new NewsItemUI { Title = $"News Error: {newsApiResponse.Message}", SourceName = "NewsAPI" });
                    }
                    else { Debug.WriteLine("LoadMusicNewsAsync: NewsAPI success, but no articles or unexpected status."); MusicNewsItems.Add(new NewsItemUI { Title = "No music news found.", SourceName = "NewsFeed" }); }
                }
                else { string errorContent = await response.Content.ReadAsStringAsync(); Debug.WriteLine($"LoadMusicNewsAsync: NewsAPI HTTP Error: {response.StatusCode} - {response.ReasonPhrase}. Details: {errorContent}"); MusicNewsItems.Add(new NewsItemUI { Title = $"Error fetching news: {response.ReasonPhrase}", SourceName = "NewsFeed" }); }
            }
            catch (HttpRequestException httpEx) { Debug.WriteLine($"LoadMusicNewsAsync: HttpRequestException: {httpEx.Message}"); MusicNewsItems.Add(new NewsItemUI { Title = "Network error fetching news.", SourceName = "NewsFeed" }); }
            catch (JsonException jsonEx) { Debug.WriteLine($"LoadMusicNewsAsync: JsonException: {jsonEx.Message}"); MusicNewsItems.Add(new NewsItemUI { Title = "Error parsing news data.", SourceName = "NewsFeed" }); }
            catch (Exception ex) { Debug.WriteLine($"LoadMusicNewsAsync: Generic Exception: {ex.Message}"); MusicNewsItems.Add(new NewsItemUI { Title = "Unexpected error fetching news.", SourceName = "NewsFeed" }); }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e) // Unchanged
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized; // Unchanged
        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close(); // Unchanged

        private async void btnSearchAlbum_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                ShowInitialPlaceholder = false; // Hide placeholder when a search is initiated
                await ExecuteSearchAsync();
            }
            else
            {
                ShowInitialPlaceholder = true; // Show placeholder if search query is empty
                MainContentTitle = "Please enter an album or artist name to search.";
                SearchResults.Clear();
            }
        }

        private async Task ExecuteSearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                // This case should ideally be handled by btnSearchAlbum_Click setting ShowInitialPlaceholder = true
                // But as a safeguard:
                ShowInitialPlaceholder = true;
                MainContentTitle = "Please enter an album or artist name to search.";
                SearchResults.Clear();
                return;
            }

            ShowInitialPlaceholder = false; // Ensure placeholder is hidden
            MainContentTitle = $"Searching for '{SearchQuery}'...";
            SearchResults.Clear();

            try
            {
                string searchUrl = $"{DiscogsApiBaseUrl}/database/search?q={Uri.EscapeDataString(SearchQuery)}&type=master,release&per_page=20";
                HttpResponseMessage response = await discogsClient.GetAsync(searchUrl);
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var discogsSearchResponse = JsonSerializer.Deserialize<DiscogsSearchResponse>(jsonResponse, options);

                    if (discogsSearchResponse?.Results != null && discogsSearchResponse.Results.Any())
                    {
                        MainContentTitle = $"Results for '{SearchQuery}':";
                        foreach (var item in discogsSearchResponse.Results)
                        {
                            SearchResults.Add(new MainPageSearchResultItem
                            {
                                AlbumName = item.AlbumName,
                                ArtistName = item.ArtistName,
                                CoverArtUrl = item.Thumb,
                                ReleaseYear = item.Year,
                                PrimaryGenre = item.Genre?.FirstOrDefault(),
                                DiscogsId = item.MasterId ?? item.Id,
                                SourceApi = "Discogs"
                            });
                        }
                    }
                    else
                    {
                        MainContentTitle = $"No results found for '{SearchQuery}'.";
                        // SearchResults is already empty
                    }
                }
                else
                {
                    MainContentTitle = $"Error searching Discogs: {response.ReasonPhrase}";
                }
            }
            catch (HttpRequestException) { MainContentTitle = "Network error occurred during search."; }
            catch (JsonException) { MainContentTitle = "Error parsing search results data."; }
            catch (Exception) { MainContentTitle = "An unexpected error occurred during search."; }
        }

        private void AlbumSearchResult_Click(object sender, MouseButtonEventArgs e) // Unchanged
        {
            if (sender is FrameworkElement fe && fe.DataContext is MainPageSearchResultItem selectedItem)
            {
                var overview = new JNR.Views.Overview(selectedItem.AlbumName, selectedItem.ArtistName, selectedItem.Mbid, selectedItem.CoverArtUrl);
                overview.Owner = this; overview.Show();
            }
        }

        private void NewsItem_Click(object sender, MouseButtonEventArgs e) // Unchanged
        {
            if (sender is FrameworkElement fe && fe.DataContext is NewsItemUI selectedNewsItem)
            {
                if (!string.IsNullOrWhiteSpace(selectedNewsItem.Url))
                {
                    try { Process.Start(new ProcessStartInfo(selectedNewsItem.Url) { UseShellExecute = true }); }
                    catch (Exception ex) { Debug.WriteLine($"Error opening news link: {ex.Message}"); MessageBox.Show("Could not open the link.", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
                }
            }
        }

        // Sidebar Navigation Event Handlers (Unchanged)
        private void MyAlbumsRadioButton_Checked(object sender, RoutedEventArgs e) { var myAlbumsWindow = new JNR.Views.My_Albums.MyAlbums(); myAlbumsWindow.Owner = this; myAlbumsWindow.Show(); if (sender is RadioButton rb) rb.IsChecked = false; }
        private void GenresRadioButton_Checked(object sender, RoutedEventArgs e) { var genresWindow = new JNR.Views.Genres.Genres(); genresWindow.Owner = this; genresWindow.Show(); if (sender is RadioButton rb) rb.IsChecked = false; }
        private void ChartsRadioButton_Checked(object sender, RoutedEventArgs e) { var chartsWindow = new JNR.Views.Charts(); chartsWindow.Owner = this; chartsWindow.Show(); if (sender is RadioButton rb) rb.IsChecked = false; }
        private void AboutRadioButton_Checked(object sender, RoutedEventArgs e) { var aboutWindow = new JNR.Views.About(); aboutWindow.Owner = this; aboutWindow.Show(); if (sender is RadioButton rb) rb.IsChecked = false; }
        private void SettingsRadioButton_Checked(object sender, RoutedEventArgs e) { var settingsWindow = new JNR.Views.Settings.Settings(); settingsWindow.Owner = this; settingsWindow.Show(); if (sender is RadioButton rb) rb.IsChecked = false; }
        private void LinksRadioButton_Checked(object sender, RoutedEventArgs e) { MessageBox.Show("Links page not yet implemented.", "Coming Soon"); if (sender is RadioButton rb) rb.IsChecked = false; }
    }

    public class RelayCommand : ICommand // Unchanged
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;
        public event EventHandler CanExecuteChanged { add { CommandManager.RequerySuggested += value; } remove { CommandManager.RequerySuggested -= value; } }
        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null) { _execute = execute ?? throw new ArgumentNullException(nameof(execute)); _canExecute = canExecute; }
        public RelayCommand(Action execute, Func<bool> canExecute = null) : this(o => execute(), canExecute == null ? (Func<object, bool>)null : o => canExecute()) { }
        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object parameter) => _execute(parameter);
        public void RaiseCanExecuteChanged() { CommandManager.InvalidateRequerySuggested(); }
    }
}