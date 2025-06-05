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
using System.Windows.Media; // Required for VisualTreeHelper
using JNR.Models.DiscogModels;
using JNR.Models.LastFmModels;
using JNR.Views.Genres; // For RelayCommandImplementation
using JNR; // <--- Add this for App class access

// Add using statements for other specific views if needed for App.NavigateTo type parameters
// using JNR.Views.My_Albums;
// using JNR.Views.About;


namespace JNR.Views
{
    public class ChartAlbumItemUI : INotifyPropertyChanged
    {
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
    {
        public string Name { get; set; }
        public int? StartYear { get; set; }
        public int? EndYear { get; set; }
        public bool IsAll => StartYear == null && EndYear == null;
    }

    public partial class Charts : Window, INotifyPropertyChanged
    {
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

        private List<ChartAlbumItemUI> _allTimeMasterList;
        private bool _allTimeMasterListLoaded = false;

        public ObservableCollection<DecadeFilter> Decades { get; set; }

        private DecadeFilter _selectedDecade;
        public DecadeFilter SelectedDecade
        {
            get => _selectedDecade;
            set
            {
                if (_selectedDecade != value)
                {
                    _selectedDecade = value;
                    OnPropertyChanged();
                }
            }
        }

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

        private string _noResultsMessage = "Select a decade to load albums.";
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
        private const int LfmAlbumsPerTagLimit = 30;
        private const int MaxUniqueLfmAlbumsToProcessInitialLoad = 300;

        public Charts()
        {
            InitializeComponent();
            this.DataContext = this;
            this.Closed += (s, args) => App.WindowClosed(this); // Step 1: Register for central tracking

            DisplayedPopularAlbums = new ObservableCollection<ChartAlbumItemUI>();
            _allTimeMasterList = new List<ChartAlbumItemUI>();

            SetupDecades();
            SelectDecadeCommand = new JNR.Views.Genres.RelayCommandImplementation(ExecuteSelectDecade);

            if (lastFmClient.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                lastFmClient.DefaultRequestHeaders.UserAgent.ParseAdd("JNR_WPF_App/1.0 (your_email_or_contact)");
            }
            if (discogsClient.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                discogsClient.DefaultRequestHeaders.UserAgent.ParseAdd("JNR_WPF_App/1.0 (your_email_or_contact)");
                discogsClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Discogs", $"token={DiscogsApiToken}");
            }

            SelectedDecade = Decades.First(d => d.IsAll);
            ChartTitle = $"Popular Albums: {SelectedDecade.Name}";
            NoResultsMessage = "Select a decade to load albums.";
            OnPropertyChanged(nameof(ShowNoResultsMessage));
        }

        // Step 2: Implement EnsureCorrectRadioButtonIsChecked
        public void EnsureCorrectRadioButtonIsChecked()
        {
            // Assumes the sidebar StackPanel in Charts.xaml might not have a specific x:Name
            // or relies on its position.
            StackPanel sidebarPanel = null;
            if (this.Content is Border outerMostBorder && outerMostBorder.Child is Border middleBorder && middleBorder.Child is Viewbox viewbox && viewbox.Child is Grid mainGrid)
            {
                sidebarPanel = mainGrid.Children.OfType<StackPanel>()
                                      .FirstOrDefault(p => Grid.GetColumn(p) == 0 && Grid.GetRow(p) == 1);
            }
            // If you add x:Name="SidebarContentPanel" to the StackPanel in Charts.xaml, you can use:
            // var sidebarPanel = MyAlbums.FindVisualChild<StackPanel>(this, "SidebarContentPanel"); // Using MyAlbums' helper as an example

            if (sidebarPanel != null)
            {
                var chartsRadioButton = sidebarPanel.Children.OfType<RadioButton>()
                                              .FirstOrDefault(r => r.Content?.ToString() == "Charts");
                if (chartsRadioButton != null)
                {
                    chartsRadioButton.IsChecked = true;
                }
                else { Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Charts RadioButton not found in sidebar panel for Charts view."); }
            }
            else { Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Sidebar panel not found for re-checking Charts button in Charts view."); }
        }


        private void SetupDecades()
        {
            Decades = new ObservableCollection<DecadeFilter>
            {
                new DecadeFilter { Name = "All Time", StartYear = null, EndYear = null },
                new DecadeFilter { Name = "2020s", StartYear = 2020, EndYear = DateTime.Now.Year },
                new DecadeFilter { Name = "2010s", StartYear = 2010, EndYear = 2019 },
                new DecadeFilter { Name = "2000s", StartYear = 2000, EndYear = 2009 },
                new DecadeFilter { Name = "1990s", StartYear = 1990, EndYear = 1999 },
                new DecadeFilter { Name = "1980s", StartYear = 1980, EndYear = 1989 },
                new DecadeFilter { Name = "1970s", StartYear = 1970, EndYear = 1979 },
                new DecadeFilter { Name = "1960s", StartYear = 1960, EndYear = 1969 },
                new DecadeFilter { Name = "Older", StartYear = 0, EndYear = 1959 }
            };
        }
        private List<string> GetDefaultPopularTags()
        {
            return new List<string> {
                "rock", "electronic", "hip-hop", "pop", "alternative", "indie", "metal", "jazz",
                "folk", "soul", "funk", "classical", "ambient", "punk", "reggae", "blues",
                "psychedelic rock", "progressive rock", "shoegaze", "post-punk", "new wave", "synthpop",
                "dream pop", "post-rock", "idm", "experimental rock", "art rock", "classic rock",
                "alternative metal", "black metal", "death metal", "doom metal", "thrash metal",
                "trip hop", "downtempo", "house", "techno", "drum and bass", "jungle",
                "singer-songwriter", "country", "americana", "world", "latin", "afrobeat",
                "experimental", "noise", "industrial", "ebm", "goth rock", "darkwave",
                "power pop", "garage rock", "surf rock", "ska", "dub", "dancehall"
            };
        }

        private async Task LoadAllTimeMasterListAsync()
        {
            if (_allTimeMasterListLoaded)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Master list already loaded. Skipping fetch.");
                return;
            }
            IsLoading = true;
            _allTimeMasterList.Clear();
            NoResultsMessage = $"Fetching a large set of popular albums (All Time) for the master list ({MaxUniqueLfmAlbumsToProcessInitialLoad} target)... This may take a while. Please be patient.";
            ChartTitle = $"Popular Albums: All Time (Loading Master List...)";
            OnPropertyChanged(nameof(NoResultsMessage)); OnPropertyChanged(nameof(ChartTitle)); OnPropertyChanged(nameof(ShowNoResultsMessage));

            List<string> tagsToFetch = GetDefaultPopularTags();
            if (!tagsToFetch.Any())
            {
                NoResultsMessage = "No tags available to fetch albums for the master list.";
                IsLoading = false; return;
            }

            var allLfmAlbumsFromApi = new List<LocalLfmAlbumForTagRanked>();
            var lfmFetchTasks = tagsToFetch.Select(async tag =>
            {
                string apiUrl = $"http://ws.audioscrobbler.com/2.0/?method=tag.getTopAlbums&tag={Uri.EscapeDataString(tag)}&api_key={LastFmApiKey}&format=json&limit={LfmAlbumsPerTagLimit}";
                try
                {
                    HttpResponseMessage response = await lastFmClient.GetAsync(apiUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var apiResponse = JsonSerializer.Deserialize<LocalLfmTopAlbumsByTagResponse>(jsonResponse, options);
                        return apiResponse?.Albums?.Album ?? new List<LocalLfmAlbumForTagRanked>();
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error fetching Last.fm data for tag '{tag}': {ex.Message}"); }
                return new List<LocalLfmAlbumForTagRanked>();
            }).ToList();

            var lfmResults = await Task.WhenAll(lfmFetchTasks);
            foreach (var list in lfmResults) { allLfmAlbumsFromApi.AddRange(list); }

            var uniqueLfmAlbums = allLfmAlbumsFromApi
                .Where(a => a.Artist != null && !string.IsNullOrWhiteSpace(a.Artist.Name) && !string.IsNullOrWhiteSpace(a.Name))
                .GroupBy(a => new { AlbumName = a.Name.ToLowerInvariant(), ArtistName = a.Artist.Name.ToLowerInvariant() })
                .Select(g => g.First()).Take(MaxUniqueLfmAlbumsToProcessInitialLoad).ToList();

            if (!uniqueLfmAlbums.Any())
            {
                NoResultsMessage = "Could not fetch any albums from Last.fm for the master list.";
                IsLoading = false; return;
            }

            NoResultsMessage = $"Enriching {uniqueLfmAlbums.Count} album(s) with year/cover data for master list..."; OnPropertyChanged(nameof(NoResultsMessage));

            var enrichedAlbumTasks = uniqueLfmAlbums.Select(async (lfmAlbum, index) =>
            {
                await Task.Delay(Random.Shared.Next(1500, 2500) + (index % 10 * 100)); // Stagger slightly more
                string discogsSearchUrl = $"{DiscogsApiBaseUrl}/database/search?artist={Uri.EscapeDataString(lfmAlbum.Artist.Name)}&release_title={Uri.EscapeDataString(lfmAlbum.Name)}&type=master,release&per_page=1";
                string releaseYearString = "N/A"; int parsedYear = 0; int? discogsId = null;
                string coverArtUrl = GetLastFmImageUrl(lfmAlbum.Image, "extralarge") ?? "/Images/placeholder_album.png";
                int maxRetries = 2; int attemptCount = 0; bool discogsSuccess = false;

                while (attemptCount <= maxRetries && !discogsSuccess)
                {
                    if (attemptCount > 0) await Task.Delay(5000 * attemptCount);
                    try
                    {
                        HttpResponseMessage discogsResponse = await discogsClient.GetAsync(discogsSearchUrl);
                        if (discogsResponse.IsSuccessStatusCode)
                        {
                            var discogsSearch = JsonSerializer.Deserialize<DiscogsSearchResponse>(await discogsResponse.Content.ReadAsStringAsync(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            var resultItem = discogsSearch?.Results?.FirstOrDefault();
                            if (resultItem != null)
                            {
                                releaseYearString = string.IsNullOrWhiteSpace(resultItem.Year) || resultItem.Year == "0" ? "N/A" : resultItem.Year;
                                if (int.TryParse(resultItem.Year, out int yr) && yr >= 1000 && yr <= DateTime.Now.Year + 5) { parsedYear = yr; }
                                discogsId = resultItem.MasterId ?? resultItem.Id;
                                if (!string.IsNullOrWhiteSpace(resultItem.CoverImage)) coverArtUrl = resultItem.CoverImage;
                                else if (!string.IsNullOrWhiteSpace(resultItem.Thumb)) coverArtUrl = resultItem.Thumb;
                            }
                            discogsSuccess = true;
                        }
                        else if (discogsResponse.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                        {
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] RATE LIMIT HIT (Discogs - Attempt {attemptCount + 1}) for {lfmAlbum.Artist.Name} - {lfmAlbum.Name}.");
                            if (discogsResponse.Headers.TryGetValues("Retry-After", out var retryAfterValues))
                                if (int.TryParse(retryAfterValues.FirstOrDefault(), out int retryAfterSeconds) && attemptCount < maxRetries)
                                    await Task.Delay(Math.Max(retryAfterSeconds * 1000, 2000));
                        }
                        else break;
                    }
                    catch (Exception ex) { Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Discogs enrichment exception (Attempt {attemptCount + 1}): {ex.Message}"); if (attemptCount >= maxRetries) break; }
                    attemptCount++;
                }
                return new ChartAlbumItemUI { AlbumName = lfmAlbum.Name, ArtistName = lfmAlbum.Artist.Name, Mbid = lfmAlbum.Mbid, CoverArtUrl = coverArtUrl, ReleaseYear = releaseYearString, ParsedYear = parsedYear, DiscogsId = discogsId };
            }).ToList();

            var enrichedAlbumsArray = await Task.WhenAll(enrichedAlbumTasks);
            _allTimeMasterList.AddRange(enrichedAlbumsArray.Where(a => a != null));
            _allTimeMasterList = _allTimeMasterList.OrderByDescending(a => a.ParsedYear > 0 ? 1 : 0).ThenByDescending(a => a.ParsedYear).ThenBy(a => a.ArtistName).ThenBy(a => a.AlbumName).ToList();
            _allTimeMasterListLoaded = true; IsLoading = false;
        }

        private async void ExecuteSelectDecade(object parameter)
        {
            if (parameter is DecadeFilter decade)
            {
                SelectedDecade = decade;
                ChartTitle = $"Popular Albums: {(SelectedDecade.IsAll ? "All Time" : SelectedDecade.Name)}";
                OnPropertyChanged(nameof(ChartTitle));
                if (!_allTimeMasterListLoaded) await LoadAllTimeMasterListAsync();
                FilterAndDisplayAlbums();
            }
        }

        private void FilterAndDisplayAlbums()
        {
            DisplayedPopularAlbums.Clear();
            if (IsLoading || !_allTimeMasterListLoaded || _allTimeMasterList == null || !_allTimeMasterList.Any())
            {
                NoResultsMessage = IsLoading ? "Loading..." : (_allTimeMasterListLoaded ? $"No albums found in the master list." : "Select a decade to load albums.");
                OnPropertyChanged(nameof(ShowNoResultsMessage)); return;
            }
            IEnumerable<ChartAlbumItemUI> albumsToDisplay;
            if (SelectedDecade != null && !SelectedDecade.IsAll)
            {
                albumsToDisplay = _allTimeMasterList.Where(album =>
                    SelectedDecade.Name == "Older" ?
                    ((album.ParsedYear > 0 && album.ParsedYear <= SelectedDecade.EndYear) || (album.ParsedYear == 0 && SelectedDecade.EndYear == 1959)) :
                    (album.ParsedYear >= SelectedDecade.StartYear && album.ParsedYear <= SelectedDecade.EndYear && album.ParsedYear != 0)
                ).ToList();
            }
            else albumsToDisplay = _allTimeMasterList.ToList();
            foreach (var album in albumsToDisplay) DisplayedPopularAlbums.Add(album);
            NoResultsMessage = DisplayedPopularAlbums.Any() ? "" : $"No albums found matching criteria for '{SelectedDecade?.Name ?? "N/A"}'.";
            OnPropertyChanged(nameof(ShowNoResultsMessage));
        }

        private string GetLastFmImageUrl(List<LastFmImage> images, string preferredSize = "extralarge")
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

        // Step 4: Update Album_Click
        private void Album_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ChartAlbumItemUI selectedAlbum)
            {
                App.NavigateToOverview(this,
                    selectedAlbum.AlbumName,
                    selectedAlbum.ArtistName,
                    selectedAlbum.Mbid,
                    selectedAlbum.CoverArtUrl);
               
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        // Step 3: Update SidebarNavigation_Click
        private void SidebarNavigation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.CommandParameter is string viewName)
            {
                if (viewName == "Charts")
                {
                    rb.IsChecked = true;
                    return; // Already in this view
                }

                switch (viewName)
                {
                    case "MyAlbums": App.NavigateTo<JNR.Views.My_Albums.MyAlbums>(this); break;
                    case "Genres": App.NavigateTo<JNR.Views.Genres.Genres>(this); break;
                    case "About": App.NavigateTo<JNR.Views.About>(this); break;
                    case "Settings":
                    case "Links":
                        App.HandlePlaceholderNavigation(this, rb, viewName);
                        return;
                }
            }
        }

        // Step 5: Remove FindChartsRadioButtonAndCheck (functionality moved to EnsureCorrectRadioButtonIsChecked)
        // private void FindChartsRadioButtonAndCheck() { /* ... old code ... */ }

        // Step 6: Update btnGoBackCharts_Click and GoToSearch_Click
        private void btnGoBackCharts_Click(object sender, RoutedEventArgs e)
        {
            App.NavigateToMainPage(this);
        }
        private void GoToSearch_Click(object sender, RoutedEventArgs e)
        {
            App.NavigateToMainPage(this);
        }
    }
}