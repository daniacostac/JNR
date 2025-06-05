// File: Views/MainPage/MainPage.xaml.cs
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
using System.Windows.Data; // Required for BindingOperations
using System.Windows.Input;
using JNR.Models.DiscogModels;
using JNR.Models.LastFmModels;

namespace JNR.Views.MainPage
{
    public class MainPageSearchResultItem : INotifyPropertyChanged
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

        private string _mainContentTitle = "Search for anything!";
        public string MainContentTitle
        {
            get => _mainContentTitle;
            set { _mainContentTitle = value; OnPropertyChanged(); }
        }

        public ObservableCollection<MainPageSearchResultItem> SearchResults { get; set; }

        private static readonly HttpClient discogsClient = new HttpClient();
        private const string DiscogsApiToken = "TMMBVQQgfXKTCEmgHqukhGLvhyCKJuLKlSqfrJCn";
        private const string DiscogsApiBaseUrl = "https://api.discogs.com";

        public MainPage()
        {
            InitializeComponent();
            this.DataContext = this;
            SearchResults = new ObservableCollection<MainPageSearchResultItem>();

            if (discogsClient.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                discogsClient.DefaultRequestHeaders.UserAgent.ParseAdd("JNR_WPF_App/1.0");
                discogsClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Discogs", $"token={DiscogsApiToken}");
            }
            Debug.WriteLine("MainPage Initialized. DataContext set. HttpClient configured.");
            BindingOperations.SetBinding(txtSearchAlbum, TextBox.TextProperty, new Binding("SearchQuery") { Source = this, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            Debug.WriteLine("Binding for txtSearchAlbum.Text to SearchQuery programmatically set.");
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            //Application.Current.Shutdown(); // Or this.Close(); if MainPage isn't the absolute root
            this.Close(); // More common for a sub-main window. If it's the entry point, Shutdown is fine.
        }

        private async void btnSearchAlbum_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"btnSearchAlbum_Click: Current SearchQuery value from property is '{SearchQuery}'");

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                Debug.WriteLine($"btnSearchAlbum_Click: SearchQuery is valid ('{SearchQuery}'). Calling ExecuteSearchAsync.");
                await ExecuteSearchAsync();
            }
            else
            {
                Debug.WriteLine("btnSearchAlbum_Click: SearchQuery is empty or whitespace. Search will not proceed.");
                MainContentTitle = "Please enter an album or artist name to search.";
                SearchResults.Clear();
            }
        }

        private async Task ExecuteSearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                Debug.WriteLine("ExecuteSearchAsync: Called with empty SearchQuery. Aborting.");
                return;
            }

            MainContentTitle = $"Searching for '{SearchQuery}'...";
            SearchResults.Clear();
            Debug.WriteLine($"ExecuteSearchAsync: Searching for '{SearchQuery}'. Cleared results. UI updated.");

            try
            {
                string searchUrl = $"{DiscogsApiBaseUrl}/database/search?q={Uri.EscapeDataString(SearchQuery)}&type=master,release&per_page=20";
                Debug.WriteLine($"ExecuteSearchAsync: API URL: {searchUrl}");

                HttpResponseMessage response = await discogsClient.GetAsync(searchUrl);
                Debug.WriteLine($"ExecuteSearchAsync: API call returned. Status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var discogsSearchResponse = JsonSerializer.Deserialize<DiscogsSearchResponse>(jsonResponse, options);

                    if (discogsSearchResponse?.Results != null && discogsSearchResponse.Results.Any())
                    {
                        MainContentTitle = $"Results for '{SearchQuery}':";
                        Debug.WriteLine($"ExecuteSearchAsync: Found {discogsSearchResponse.Results.Count} results.");
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
                        Debug.WriteLine($"ExecuteSearchAsync: Added {SearchResults.Count} items to SearchResults collection.");
                    }
                    else
                    {
                        MainContentTitle = $"No results found for '{SearchQuery}'.";
                        Debug.WriteLine("ExecuteSearchAsync: API success, but no results in the data.");
                    }
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    MainContentTitle = $"Error searching Discogs: {response.ReasonPhrase}";
                    Debug.WriteLine($"ExecuteSearchAsync: Discogs API Error: {response.StatusCode} - {response.ReasonPhrase}. Details: {errorContent}");
                }
            }
            catch (HttpRequestException httpEx)
            {
                MainContentTitle = "Network error occurred during search.";
                Debug.WriteLine($"ExecuteSearchAsync: HttpRequestException: {httpEx.Message}");
            }
            catch (JsonException jsonEx)
            {
                MainContentTitle = "Error parsing search results data.";
                Debug.WriteLine($"ExecuteSearchAsync: JsonException: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                MainContentTitle = "An unexpected error occurred during search.";
                Debug.WriteLine($"ExecuteSearchAsync: Generic Exception: {ex.Message}");
            }
        }

        private void AlbumSearchResult_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is MainPageSearchResultItem selectedItem)
            {
                Debug.WriteLine($"AlbumSearchResult_Click: Navigating to Overview for {selectedItem.AlbumName}");
                var overview = new JNR.Views.Overview(
                    selectedItem.AlbumName,
                    selectedItem.ArtistName,
                    selectedItem.Mbid, // This could be null if from Discogs
                    selectedItem.CoverArtUrl
                // Pass DiscogsId if Overview needs it directly and mbid is null
                );
                overview.Owner = this; // Set owner for better window management
                overview.Show();
                // Consider if MainPage should be hidden or closed
                // this.Hide(); 
            }
        }

        // --- Sidebar Navigation Event Handlers ---
        private void MyAlbumsRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            var myAlbumsWindow = new JNR.Views.My_Albums.MyAlbums();
            myAlbumsWindow.Owner = this;
            myAlbumsWindow.Show();
            if (sender is RadioButton rb) rb.IsChecked = false;
        }

        private void GenresRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            var genresWindow = new JNR.Views.Genres.Genres();
            genresWindow.Owner = this;
            genresWindow.Show();
            if (sender is RadioButton rb) rb.IsChecked = false;
        }

        private void ChartsRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            var chartsWindow = new JNR.Views.Charts();
            chartsWindow.Owner = this;
            chartsWindow.Show();
            if (sender is RadioButton rb) rb.IsChecked = false;
        }

        private void AboutRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new JNR.Views.About();
            aboutWindow.Owner = this;
            aboutWindow.Show();
            if (sender is RadioButton rb) rb.IsChecked = false;
        }

        private void SettingsRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Settings page not yet implemented.", "Coming Soon");
            if (sender is RadioButton rb) rb.IsChecked = false;
        }

        private void LinksRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Links page not yet implemented.", "Coming Soon");
            if (sender is RadioButton rb) rb.IsChecked = false;
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        public RelayCommand(Action execute, Func<bool> canExecute = null)
            : this(o => execute(), canExecute == null ? (Func<object, bool>)null : o => canExecute())
        { }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object parameter) => _execute(parameter);

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}