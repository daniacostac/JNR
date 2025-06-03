// File: Views/Charts.xaml.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using JNR.Models.DiscogModels;
using JNR.Models.LastFmModels;
using JNR.Views.Genres; // For LocalLfm models and RelayCommandImplementation

namespace JNR.Views
{
    // ChartAlbumItemUI and DecadeFilter classes remain the same as in the previous good answer.
    // (Make sure they are present and correctly defined)
    public class ChartAlbumItemUI : INotifyPropertyChanged
    { /* ... same as before ... */
        private string _albumName;
        public string AlbumName { get => _albumName; set { _albumName = value; OnPropertyChanged(); } }

        private string _artistName;
        public string ArtistName { get => _artistName; set { _artistName = value; OnPropertyChanged(); } }

        private string _coverArtUrl;
        public string CoverArtUrl { get => _coverArtUrl; set { _coverArtUrl = value; OnPropertyChanged(); } }

        private string _releaseYear;
        public string ReleaseYear { get => _releaseYear; set { _releaseYear = value; OnPropertyChanged(); } }

        public int ParsedYear { get; set; }

        public string Mbid { get; set; }
        public int? DiscogsId { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DecadeFilter
    { /* ... same as before ... */
        public string Name { get; set; }
        public int? StartYear { get; set; }
        public int? EndYear { get; set; }
        public bool IsAll => StartYear == null && EndYear == null;
    }


    public partial class Charts : Window, INotifyPropertyChanged
    {
        // ... (Properties: DisplayedPopularAlbums, _allFetchedPopularAlbums, Decades, _selectedDecade, ChartTitle, IsLoading, NoResultsMessage, ShowNoResultsMessage, SelectDecadeCommand remain the same)
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private ObservableCollection<ChartAlbumItemUI> _displayedPopularAlbums;
        public ObservableCollection<ChartAlbumItemUI> DisplayedPopularAlbums
        {
            get => _displayedPopularAlbums;
            set { _displayedPopularAlbums = value; OnPropertyChanged(); }
        }

        private List<ChartAlbumItemUI> _allFetchedPopularAlbums;

        public ObservableCollection<DecadeFilter> Decades { get; set; }

        private DecadeFilter _selectedDecade;

        private string _chartTitle = "Popular Albums";
        public string ChartTitle
        {
            get => _chartTitle;
            set { _chartTitle = value; OnPropertyChanged(); }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowNoResultsMessage)); }
        }

        private string _noResultsMessage = "No albums found for this selection.";
        public string NoResultsMessage
        {
            get => _noResultsMessage;
            set { _noResultsMessage = value; OnPropertyChanged(); }
        }

        public bool ShowNoResultsMessage => !IsLoading && (DisplayedPopularAlbums == null || !DisplayedPopularAlbums.Any());


        public ICommand SelectDecadeCommand { get; }


        private static readonly HttpClient lastFmClient = new HttpClient();
        private static readonly HttpClient discogsClient = new HttpClient();
        private const string LastFmApiKey = "d8831cc3c1d4eb53011a7b268a95d028";
        private const string DiscogsApiToken = "TMMBVQQgfXKTCEmgHqukhGLvhyCKJuLKlSqfrJCn";
        private const string DiscogsApiBaseUrl = "https://api.discogs.com";

        public Charts()
        {
            InitializeComponent();
            this.DataContext = this;

            DisplayedPopularAlbums = new ObservableCollection<ChartAlbumItemUI>();
            _allFetchedPopularAlbums = new List<ChartAlbumItemUI>();

            SetupDecades();
            SelectDecadeCommand = new JNR.Views.Genres.RelayCommandImplementation(ExecuteSelectDecade);

            if (lastFmClient.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                lastFmClient.DefaultRequestHeaders.UserAgent.ParseAdd("JNR_WPF_App/1.0");
            }
            if (discogsClient.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                discogsClient.DefaultRequestHeaders.UserAgent.ParseAdd("JNR_WPF_App/1.0");
                discogsClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Discogs", $"token={DiscogsApiToken}");
            }

            _selectedDecade = Decades.First(d => d.IsAll); // Explicitly select "All Time"
            ChartTitle = $"Popular Albums: {_selectedDecade.Name}";
            LoadPopularAlbumsAsync();
        }

        private void SetupDecades() // Remains the same
        {
            Decades = new ObservableCollection<DecadeFilter>
            {
                new DecadeFilter { Name = "All Time", StartYear = null, EndYear = null }, // IsAll will be true
                new DecadeFilter { Name = "2020s", StartYear = 2020, EndYear = 2029 },
                new DecadeFilter { Name = "2010s", StartYear = 2010, EndYear = 2019 },
                new DecadeFilter { Name = "2000s", StartYear = 2000, EndYear = 2009 },
                new DecadeFilter { Name = "1990s", StartYear = 1990, EndYear = 1999 },
                new DecadeFilter { Name = "1980s", StartYear = 1980, EndYear = 1989 },
                new DecadeFilter { Name = "1970s", StartYear = 1970, EndYear = 1979 },
                new DecadeFilter { Name = "1960s", StartYear = 1960, EndYear = 1969 },
                new DecadeFilter { Name = "Older", StartYear = 0, EndYear = 1959 } // For years <= 1959 or unknown
            };
        }

        private async void LoadPopularAlbumsAsync() // Mostly the same, with Debug.WriteLine added
        {
            IsLoading = true;
            _allFetchedPopularAlbums.Clear();
            DisplayedPopularAlbums.Clear();
            NoResultsMessage = "Fetching popular albums...";
            OnPropertyChanged(nameof(ShowNoResultsMessage));

            List<string> popularTags = new List<string> { "rock", "electronic", "hip-hop", "pop", "alternative", "indie", "metal", "jazz", "folk", "soul" };
            var allLfmAlbumsFromApi = new List<LocalLfmAlbumForTagRanked>();

            var tasks = popularTags.Select(async tag =>
            {
                string apiUrl = $"http://ws.audioscrobbler.com/2.0/?method=tag.getTopAlbums&tag={Uri.EscapeDataString(tag)}&api_key={LastFmApiKey}&format=json&limit=15";
                try
                {
                    HttpResponseMessage response = await lastFmClient.GetAsync(apiUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var apiResponse = JsonSerializer.Deserialize<LocalLfmTopAlbumsByTagResponse>(jsonResponse, options);
                        if (apiResponse?.Albums?.Album != null)
                        {
                            return apiResponse.Albums.Album;
                        }
                    }
                    else { Debug.WriteLine($"Last.fm API error for tag {tag}: {response.StatusCode}"); }
                }
                catch (Exception ex) { Debug.WriteLine($"Error fetching Last.fm data for tag {tag}: {ex.Message}"); }
                return new List<LocalLfmAlbumForTagRanked>();
            }).ToList();

            var results = await Task.WhenAll(tasks);
            foreach (var list in results) { allLfmAlbumsFromApi.AddRange(list); }

            var uniqueLfmAlbums = allLfmAlbumsFromApi
                .Where(a => a.Artist != null && !string.IsNullOrWhiteSpace(a.Artist.Name) && !string.IsNullOrWhiteSpace(a.Name))
                .GroupBy(a => new { AlbumName = a.Name.ToLowerInvariant(), ArtistName = a.Artist.Name.ToLowerInvariant() })
                .Select(g => g.First())
                .Take(150)
                .ToList();

            if (!uniqueLfmAlbums.Any())
            {
                NoResultsMessage = "Could not fetch initial album list from Last.fm.";
                IsLoading = false;
                OnPropertyChanged(nameof(ShowNoResultsMessage));
                return;
            }

            NoResultsMessage = "Enriching album data (years, covers)...";

            var enrichedAlbumTasks = uniqueLfmAlbums.Select(async lfmAlbum =>
            {
                await Task.Delay(Random.Shared.Next(100, 250)); // Slightly randomized delay

                string discogsSearchUrl = $"{DiscogsApiBaseUrl}/database/search?artist={Uri.EscapeDataString(lfmAlbum.Artist.Name)}&release_title={Uri.EscapeDataString(lfmAlbum.Name)}&type=master,release&per_page=1";
                string releaseYear = "N/A";
                int parsedYear = 0;
                int? discogsId = null;
                string coverArtUrl = GetLastFmImageUrl(lfmAlbum.Image, "extralarge") ?? "/Images/placeholder_album.png";

                try
                {
                    HttpResponseMessage discogsResponse = await discogsClient.GetAsync(discogsSearchUrl);
                    if (discogsResponse.IsSuccessStatusCode)
                    {
                        string discogsJson = await discogsResponse.Content.ReadAsStringAsync();
                        var discogsSearch = JsonSerializer.Deserialize<DiscogsSearchResponse>(discogsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        var resultItem = discogsSearch?.Results?.FirstOrDefault();
                        if (resultItem != null)
                        {
                            releaseYear = string.IsNullOrWhiteSpace(resultItem.Year) || resultItem.Year == "0" ? "N/A" : resultItem.Year;
                            if (int.TryParse(resultItem.Year, out int yr) && yr >= 1000 && yr <= DateTime.Now.Year + 5) parsedYear = yr; // Basic validation for year
                            else parsedYear = 0;
                            discogsId = resultItem.MasterId ?? resultItem.Id;
                            if (!string.IsNullOrWhiteSpace(resultItem.CoverImage)) coverArtUrl = resultItem.CoverImage;
                            else if (!string.IsNullOrWhiteSpace(resultItem.Thumb)) coverArtUrl = resultItem.Thumb;
                        }
                    }
                }
                catch (HttpRequestException hrex) when (hrex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                { Debug.WriteLine($"Discogs rate limit hit for {lfmAlbum.Name}. Enrichment might be partial."); }
                catch (Exception ex) { Debug.WriteLine($"Error fetching Discogs data for {lfmAlbum.Name}: {ex.Message}"); }

                return new ChartAlbumItemUI
                {
                    AlbumName = lfmAlbum.Name,
                    ArtistName = lfmAlbum.Artist.Name,
                    Mbid = lfmAlbum.Mbid,
                    CoverArtUrl = coverArtUrl,
                    ReleaseYear = releaseYear,
                    ParsedYear = parsedYear,
                    DiscogsId = discogsId
                };
            }).ToList();

            var enrichedAlbumsArray = await Task.WhenAll(enrichedAlbumTasks);
            _allFetchedPopularAlbums.AddRange(enrichedAlbumsArray.Where(a => a != null));

            _allFetchedPopularAlbums = _allFetchedPopularAlbums
                                       .OrderByDescending(a => a.ParsedYear > 0 ? 1 : 0)
                                       .ThenByDescending(a => a.ParsedYear)
                                       .ThenBy(a => a.ArtistName).ThenBy(a => a.AlbumName)
                                       .ToList();

            // !!! CRUCIAL DEBUGGING STEP !!!
            Debug.WriteLine($"--- Parsed Years for Fetched Albums (First 20 of {_allFetchedPopularAlbums.Count}) ---");
            foreach (var album in _allFetchedPopularAlbums.Take(20))
            {
                Debug.WriteLine($"Album: {album.AlbumName,-30} | Artist: {album.ArtistName,-25} | ParsedYear: {album.ParsedYear,-5} | ReleaseYearString: {album.ReleaseYear}");
            }
            Debug.WriteLine($"--- End of Parsed Years Log ---");


            IsLoading = false;
            FilterAndDisplayAlbums(); // Apply initial filter (e.g., "All Time")
            if (!_allFetchedPopularAlbums.Any() && !IsLoading)
            {
                NoResultsMessage = "No popular albums could be processed or found.";
            }
            else if (!DisplayedPopularAlbums.Any() && !IsLoading)
            { // If all fetched, but current filter yields none
                NoResultsMessage = $"No albums found for '{_selectedDecade.Name}'.";
            }
            OnPropertyChanged(nameof(ShowNoResultsMessage));
        }

        private void ExecuteSelectDecade(object parameter) // Remains the same
        {
            if (parameter is DecadeFilter decade)
            {
                _selectedDecade = decade;
                ChartTitle = $"Popular Albums: {decade.Name}";
                FilterAndDisplayAlbums();
            }
        }

        // REVISED FilterAndDisplayAlbums for clarity and correctness
        private void FilterAndDisplayAlbums()
        {
            if (_allFetchedPopularAlbums == null)
            {
                DisplayedPopularAlbums.Clear();
                NoResultsMessage = "Album data not yet loaded.";
                OnPropertyChanged(nameof(ShowNoResultsMessage));
                return;
            }

            DisplayedPopularAlbums.Clear();
            IEnumerable<ChartAlbumItemUI> filteredResults;

            if (_selectedDecade.IsAll) // "All Time"
            {
                filteredResults = _allFetchedPopularAlbums;
            }
            else if (_selectedDecade.Name == "Older") // Specifically for "Older" (e.g., <=1959 or unknown year)
            {
                // For "Older", EndYear is 1959.
                // Include albums with ParsedYear <= 1959 OR if ParsedYear is 0 (unknown).
                filteredResults = _allFetchedPopularAlbums.Where(album =>
                    (album.ParsedYear > 0 && album.ParsedYear <= _selectedDecade.EndYear) || album.ParsedYear == 0
                );
            }
            else // Specific decades like "1990s", "2000s"
            {
                // For these, ParsedYear must be known (not 0) and fall within the decade's StartYear and EndYear.
                filteredResults = _allFetchedPopularAlbums.Where(album =>
                    album.ParsedYear >= _selectedDecade.StartYear && // StartYear and EndYear will be non-null for these
                    album.ParsedYear <= _selectedDecade.EndYear
                // Note: album.ParsedYear != 0 is implicitly handled because StartYear will be > 0.
                );
            }

            foreach (var album in filteredResults)
            {
                DisplayedPopularAlbums.Add(album);
            }

            if (!DisplayedPopularAlbums.Any() && !_isLoading)
            {
                NoResultsMessage = $"No albums found for '{_selectedDecade.Name}'.";
            }
            else
            {
                NoResultsMessage = ""; // Clear message if there are results
            }
            OnPropertyChanged(nameof(ShowNoResultsMessage));
        }


        private string GetLastFmImageUrl(List<LastFmImage> images, string preferredSize = "extralarge") // Remains the same
        {
            if (images == null || !images.Any()) return null;
            var img = images.FirstOrDefault(i => i.Size == preferredSize && !string.IsNullOrWhiteSpace(i.Text));
            if (img != null) return img.Text;
            string[] fallbackSizes = { "large", "medium", "small" };
            foreach (var sizeKey in fallbackSizes)
            {
                img = images.FirstOrDefault(i => i.Size == sizeKey && !string.IsNullOrWhiteSpace(i.Text));
                if (img != null) return img.Text;
            }
            return images.LastOrDefault(i => !string.IsNullOrWhiteSpace(i.Text))?.Text;
        }

        // Event handlers (Album_Click, CloseButton_Click, MinimizeButton_Click, SidebarNavigation_Click, FindChartsRadioButtonAndCheck, GoToSearch_Click)
        // remain the same as in the previous good answer.
        private void Album_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ChartAlbumItemUI selectedAlbum)
            {
                var overview = new Overview(
                    selectedAlbum.AlbumName,
                    selectedAlbum.ArtistName,
                    selectedAlbum.Mbid,
                    selectedAlbum.CoverArtUrl
                );
                overview.Owner = Application.Current.MainWindow;
                overview.Show();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        private void SidebarNavigation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.CommandParameter is string viewName)
            {
                if (viewName == "Charts")
                {
                    rb.IsChecked = true;
                    return;
                }

                Window newWindow = null;
                switch (viewName)
                {
                    case "MyAlbums": newWindow = new JNR.Views.My_Albums.MyAlbums(); break;
                    case "Genres": newWindow = new JNR.Views.Genres.Genres(); break;
                    case "About":
                    case "Settings":
                    case "Links":
                        MessageBox.Show($"{viewName} page not yet implemented.", "Coming Soon");
                        rb.IsChecked = false;
                        FindChartsRadioButtonAndCheck();
                        return;
                }
                if (newWindow != null) { newWindow.Show(); this.Close(); }
            }
        }

        private void FindChartsRadioButtonAndCheck()
        {
            try
            {
                if (this.Content is Border outerBorder && VisualTreeHelper.GetChild(outerBorder, 0) is Border viewboxBorder)
                {
                    if (VisualTreeHelper.GetChild(viewboxBorder, 0) is Grid topGrid)
                    {
                        if (topGrid.Children.Count > 2 && topGrid.Children[2] is StackPanel sidebarPanel) // Assuming sidebar is 3rd child
                        {
                            var chartsRadioButton = sidebarPanel.Children.OfType<RadioButton>()
                                                       .FirstOrDefault(r => r.Content?.ToString() == "Charts");
                            if (chartsRadioButton != null) chartsRadioButton.IsChecked = true;
                        }
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Error in FindChartsRadioButtonAndCheck: {ex.Message}"); }
        }
        private void GoToSearch_Click(object sender, RoutedEventArgs e)
        {
            var mainPage = new JNR.Views.MainPage.MainPage();
            mainPage.Show();
            this.Close();
        }
    }
}