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
    // ChartAlbumItemUI and DecadeFilter classes remain the same
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

        // Master list to store all initially fetched and enriched "All Time" albums
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
        // MODIFIED: Changed from 150 to 300 as per user request
        private const int MaxUniqueLfmAlbumsToProcessInitialLoad = 300;

        public Charts()
        {
            InitializeComponent();
            this.DataContext = this;

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
                // Added more diverse tags to help reach ~300 unique albums
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
            OnPropertyChanged(nameof(NoResultsMessage));
            OnPropertyChanged(nameof(ChartTitle));
            OnPropertyChanged(nameof(ShowNoResultsMessage));


            List<string> tagsToFetch = GetDefaultPopularTags();
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Fetching for All Time master list using {tagsToFetch.Count} tags (LFM limit {LfmAlbumsPerTagLimit}/tag, process {MaxUniqueLfmAlbumsToProcessInitialLoad} unique).");

            if (!tagsToFetch.Any())
            {
                NoResultsMessage = "No tags available to fetch albums for the master list.";
                IsLoading = false;
                // _allTimeMasterListLoaded remains false
                // FilterAndDisplayAlbums will be called by ExecuteSelectDecade and show this message.
                return;
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
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Last.fm API error for tag '{tag}': {response.StatusCode}");
                }
                catch (Exception ex) { Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error fetching Last.fm data for tag '{tag}': {ex.Message}"); }
                return new List<LocalLfmAlbumForTagRanked>();
            }).ToList();

            var lfmResults = await Task.WhenAll(lfmFetchTasks);
            foreach (var list in lfmResults) { allLfmAlbumsFromApi.AddRange(list); }

            var uniqueLfmAlbums = allLfmAlbumsFromApi
                .Where(a => a.Artist != null && !string.IsNullOrWhiteSpace(a.Artist.Name) && !string.IsNullOrWhiteSpace(a.Name))
                .GroupBy(a => new { AlbumName = a.Name.ToLowerInvariant(), ArtistName = a.Artist.Name.ToLowerInvariant() })
                .Select(g => g.First())
                .Take(MaxUniqueLfmAlbumsToProcessInitialLoad)
                .ToList();

            if (!uniqueLfmAlbums.Any())
            {
                NoResultsMessage = "Could not fetch any albums from Last.fm for the master list.";
                IsLoading = false;
                // _allTimeMasterListLoaded remains false
                // FilterAndDisplayAlbums will show this message.
                return;
            }

            NoResultsMessage = $"Enriching {uniqueLfmAlbums.Count} album(s) with year/cover data for master list...";
            OnPropertyChanged(nameof(NoResultsMessage));
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Attempting to enrich {uniqueLfmAlbums.Count} unique albums from Last.fm for master list.");

            var enrichedAlbumTasks = uniqueLfmAlbums.Select(async (lfmAlbum, index) =>
            {
                long initialDelayMilliseconds = Random.Shared.Next(1500, 2500); // Existing delay logic
                await Task.Delay((int)initialDelayMilliseconds);

                string discogsSearchUrl = $"{DiscogsApiBaseUrl}/database/search?artist={Uri.EscapeDataString(lfmAlbum.Artist.Name)}&release_title={Uri.EscapeDataString(lfmAlbum.Name)}&type=master,release&per_page=1";
                string releaseYearString = "N/A";
                int parsedYear = 0;
                int? discogsId = null;
                string coverArtUrl = GetLastFmImageUrl(lfmAlbum.Image, "extralarge") ?? "/Images/placeholder_album.png";
                int maxRetries = 2;
                int attemptCount = 0;
                bool discogsSuccess = false;

                while (attemptCount <= maxRetries && !discogsSuccess)
                {
                    if (attemptCount > 0)
                    {
                        int retryDelaySeconds = 5 * attemptCount;
                        await Task.Delay(retryDelaySeconds * 1000);
                    }
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
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] RATE LIMIT HIT (Attempt {attemptCount + 1}) for {lfmAlbum.Artist.Name} - {lfmAlbum.Name}.");
                            if (discogsResponse.Headers.TryGetValues("Retry-After", out var retryAfterValues))
                            {
                                if (int.TryParse(retryAfterValues.FirstOrDefault(), out int retryAfterSeconds) && attemptCount < maxRetries)
                                {
                                    await Task.Delay(Math.Max(retryAfterSeconds * 1000, 2000));
                                }
                            }
                        }
                        else { Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Discogs API error (Attempt {attemptCount + 1}) for {lfmAlbum.Artist.Name} - {lfmAlbum.Name}: {discogsResponse.StatusCode}. Not retrying this error."); break; }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Discogs enrichment exception (Attempt {attemptCount + 1}) for {lfmAlbum.Artist.Name} - {lfmAlbum.Name}: {ex.Message}. ParsedYear remains 0.");
                        if (attemptCount >= maxRetries) break;
                    }
                    attemptCount++;
                }

                return new ChartAlbumItemUI
                {
                    AlbumName = lfmAlbum.Name,
                    ArtistName = lfmAlbum.Artist.Name,
                    Mbid = lfmAlbum.Mbid,
                    CoverArtUrl = coverArtUrl,
                    ReleaseYear = releaseYearString,
                    ParsedYear = parsedYear,
                    DiscogsId = discogsId
                };
            }).ToList();

            var enrichedAlbumsArray = await Task.WhenAll(enrichedAlbumTasks);
            _allTimeMasterList.AddRange(enrichedAlbumsArray.Where(a => a != null));

            _allTimeMasterList = _allTimeMasterList
                                   .OrderByDescending(a => a.ParsedYear > 0 ? 1 : 0)
                                   .ThenByDescending(a => a.ParsedYear)
                                   .ThenBy(a => a.ArtistName).ThenBy(a => a.AlbumName)
                                   .ToList();

            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] --- Master List Enrichment Complete ---");
            Debug.WriteLine($"Total unique LFM albums targeted for master list: {uniqueLfmAlbums.Count}");
            Debug.WriteLine($"Albums in _allTimeMasterList after enrichment: {_allTimeMasterList.Count}");
            Debug.WriteLine($"Actual count from _allTimeMasterList with ParsedYear > 0: {_allTimeMasterList.Count(a => a.ParsedYear > 0)}");

            _allTimeMasterListLoaded = true;
            IsLoading = false;
            // FilterAndDisplayAlbums will be called by ExecuteSelectDecade after this completes.
        }

        private async void ExecuteSelectDecade(object parameter)
        {
            if (parameter is DecadeFilter decade)
            {
                SelectedDecade = decade;
                ChartTitle = $"Popular Albums: {(SelectedDecade.IsAll ? "All Time" : SelectedDecade.Name)}";
                OnPropertyChanged(nameof(ChartTitle));

                if (!_allTimeMasterListLoaded)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Master list not loaded. Calling LoadAllTimeMasterListAsync for {SelectedDecade.Name}.");
                    await LoadAllTimeMasterListAsync();
                }
                else
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Master list already loaded. Filtering for {SelectedDecade.Name}.");
                }

                FilterAndDisplayAlbums();
            }
        }

        private void FilterAndDisplayAlbums()
        {
            DisplayedPopularAlbums.Clear();

            if (IsLoading)
            {
                // This state (IsLoading=true when FilterAndDisplayAlbums is called)
                // should ideally not happen if ExecuteSelectDecade awaits LoadAllTimeMasterListAsync correctly.
                // However, NoResultsMessage would have been set by LoadAllTimeMasterListAsync already.
                OnPropertyChanged(nameof(ShowNoResultsMessage));
                return;
            }

            if (!_allTimeMasterListLoaded)
            {
                NoResultsMessage = "Select a decade to load albums."; // Or "Master list is not yet available."
                OnPropertyChanged(nameof(ShowNoResultsMessage));
                return;
            }

            if (_allTimeMasterList == null || !_allTimeMasterList.Any())
            {
                NoResultsMessage = $"No albums found in the master list. Source data might be unavailable or empty.";
                OnPropertyChanged(nameof(ShowNoResultsMessage));
                return;
            }

            IEnumerable<ChartAlbumItemUI> albumsToFilter = _allTimeMasterList;
            IEnumerable<ChartAlbumItemUI> albumsToDisplay;

            if (SelectedDecade != null && !SelectedDecade.IsAll)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Filtering master list ({albumsToFilter.Count()} items) for decade: {SelectedDecade.Name} ({SelectedDecade.StartYear}-{SelectedDecade.EndYear})");
                if (SelectedDecade.Name == "Older")
                {
                    albumsToDisplay = albumsToFilter.Where(album =>
                       (album.ParsedYear > 0 && album.ParsedYear <= SelectedDecade.EndYear) ||
                       (album.ParsedYear == 0 && SelectedDecade.EndYear == 1959)
                   ).ToList();
                }
                else
                {
                    albumsToDisplay = albumsToFilter.Where(album =>
                        album.ParsedYear >= SelectedDecade.StartYear &&
                        album.ParsedYear <= SelectedDecade.EndYear &&
                        album.ParsedYear != 0)
                    .ToList();
                }
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] After filtering master list for {SelectedDecade.Name}: {albumsToDisplay.Count()} albums remain.");
            }
            else // "All Time" is selected
            {
                albumsToDisplay = albumsToFilter.ToList();
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 'All Time' selected. Displaying all {albumsToDisplay.Count()} albums from master list.");
            }

            foreach (var album in albumsToDisplay)
            {
                DisplayedPopularAlbums.Add(album);
            }

            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] FilterAndDisplayAlbums: Displaying {DisplayedPopularAlbums.Count} albums for '{SelectedDecade?.Name ?? "N/A"}'.");

            if (!DisplayedPopularAlbums.Any())
            {
                NoResultsMessage = $"No albums found matching criteria for '{SelectedDecade?.Name ?? "N/A"}'.";
            }
            else
            {
                NoResultsMessage = "";
            }
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
                if (viewName == "Charts") // Current view
                {
                    rb.IsChecked = true;
                    // If "Charts" is clicked and master list not loaded, user still needs to click a decade.
                    // Or, we could auto-trigger load for the current SelectedDecade (which is "All Time" initially).
                    // For simplicity, current behavior: clicking a decade button loads.
                    return;
                }

                Window newWindow = null;
                switch (viewName)
                {
                    case "MyAlbums": newWindow = new JNR.Views.My_Albums.MyAlbums(); break;
                    case "Genres": newWindow = new JNR.Views.Genres.Genres(); break;
                    case "About": newWindow = new JNR.Views.About(); break; // Added About navigation
                    // Placeholder for Settings and Links
                    case "Settings":
                    case "Links":
                        MessageBox.Show($"{viewName} page not yet implemented.", "Coming Soon");
                        // Re-check the "Charts" button as we are not navigating away effectively
                        FindChartsRadioButtonAndCheck();
                        return; // Important to return so newWindow logic isn't hit
                }

                if (newWindow != null)
                {
                    newWindow.Owner = Application.Current.MainWindow;
                    newWindow.Show();
                    this.Close();
                }
            }
        }

        private void FindChartsRadioButtonAndCheck()
        {
            try
            {
                // Attempt to find the main Grid hosting the sidebar and content
                if (this.Content is Border outerMostBorder && outerMostBorder.Child is Border middleBorder && middleBorder.Child is Viewbox viewbox && viewbox.Child is Grid mainGrid)
                {
                    var sidebarPanel = mainGrid.Children.OfType<StackPanel>()
                                          .FirstOrDefault(p => Grid.GetColumn(p) == 0 && Grid.GetRow(p) == 1);

                    if (sidebarPanel != null)
                    {
                        var chartsRadioButton = sidebarPanel.Children.OfType<RadioButton>()
                                              .FirstOrDefault(r => r.Content?.ToString() == "Charts");
                        if (chartsRadioButton != null)
                        {
                            chartsRadioButton.IsChecked = true;
                        }
                        else { Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Charts RadioButton not found in sidebar panel."); }
                    }
                    else { Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Sidebar panel not found for re-checking Charts button."); }
                }
                else { Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Could not find main Grid structure for re-checking Charts button."); }
            }
            catch (Exception ex) { Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error in FindChartsRadioButtonAndCheck: {ex.Message}"); }
        }

        private void btnGoBackCharts_Click(object sender, RoutedEventArgs e)
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
        private void GoToSearch_Click(object sender, RoutedEventArgs e)
        {
            var mainPage = new JNR.Views.MainPage.MainPage();
            mainPage.Show();
            this.Close();
        }
    }
}