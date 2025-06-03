// File: Views/Overview.xaml.cs
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
// Removed: System.Text.Json.Serialization; // JsonPropertyName is on models
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using JNR.Models.DiscogModels;
using JNR.Models.LastFmModels; // For shared Last.fm models (LastFmImage, LastFmDetailedAlbum, etc.)
using JNR.Models; // For TrackItem

namespace JNR.Views
{
    public partial class Overview : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _detailedAlbumName;
        private string _detailedArtistName;
        private string _detailedCoverArtUrl;
        private string _releaseInfo;
        private string _genreInfo;
        private string _descriptionText;
        private string _listenersCount;
        private string _playCount;
        private string _languageInfo = "English (Default)";
        private string _ratingDisplay = "★★★★☆ (Sample)";

        public ObservableCollection<TrackItem> AlbumTracks { get; set; } // Uses JNR.Models.TrackItem

        public string DetailedAlbumName
        {
            get => _detailedAlbumName;
            private set { if (_detailedAlbumName != value) { _detailedAlbumName = value; OnPropertyChanged(); OnPropertyChanged(nameof(AlbumTitleAndArtist)); } }
        }
        public string DetailedArtistName
        {
            get => _detailedArtistName;
            private set { if (_detailedArtistName != value) { _detailedArtistName = value; OnPropertyChanged(); OnPropertyChanged(nameof(AlbumTitleAndArtist)); } }
        }
        public string AlbumTitleAndArtist => $"{DetailedAlbumName} - {DetailedArtistName}";
        public string DetailedCoverArtUrl
        {
            get => _detailedCoverArtUrl;
            private set { if (_detailedCoverArtUrl != value) { _detailedCoverArtUrl = value; OnPropertyChanged(); } }
        }
        public string ReleaseInfo
        {
            get => _releaseInfo;
            private set { if (_releaseInfo != value) { _releaseInfo = value; OnPropertyChanged(); } }
        }
        public string GenreInfo
        {
            get => _genreInfo;
            private set { if (_genreInfo != value) { _genreInfo = value; OnPropertyChanged(); } }
        }
        public string DescriptionText
        {
            get => _descriptionText;
            private set { if (_descriptionText != value) { _descriptionText = value; OnPropertyChanged(); } }
        }
        public string ListenersCount
        {
            get => _listenersCount;
            private set { if (_listenersCount != value) { _listenersCount = value; OnPropertyChanged(); } }
        }
        public string PlayCount
        {
            get => _playCount;
            private set { if (_playCount != value) { _playCount = value; OnPropertyChanged(); } }
        }
        public string LanguageInfo
        {
            get => _languageInfo;
            private set { if (_languageInfo != value) { _languageInfo = value; OnPropertyChanged(); } }
        }
        public string RatingDisplay
        {
            get => _ratingDisplay;
            private set { if (_ratingDisplay != value) { _ratingDisplay = value; OnPropertyChanged(); } }
        }

        private DiscogsArtistReleaseItem _previousAlbum;
        public DiscogsArtistReleaseItem PreviousAlbum
        {
            get => _previousAlbum;
            private set { _previousAlbum = value; OnPropertyChanged(); }
        }

        private DiscogsArtistReleaseItem _nextAlbum;
        public DiscogsArtistReleaseItem NextAlbum
        {
            get => _nextAlbum;
            private set { _nextAlbum = value; OnPropertyChanged(); }
        }

        private readonly string _albumNameParam;
        private readonly string _artistNameParam;
        private readonly string _mbidParam;
        private readonly string _initialCoverArtUrlParam;

        private static readonly HttpClient client = new HttpClient();
        private const string LastFmApiKey = "d8831cc3c1d4eb53011a7b268a95d028";

        private const string DiscogsApiBaseUrl = "https://api.discogs.com";
        private const string DiscogsApiToken = "TMMBVQQgfXKTCEmgHqukhGLvhyCKJuLKlSqfrJCn"; // Replace with your actual token

        private int? _currentArtistId;
        private int? _currentDiscogsMasterId;
        private int? _currentDiscogsReleaseId;
        private DiscogsSearchResultItem _currentDiscogsBestMatch;

        public Overview(string albumName, string artistName, string mbid, string coverArtUrl)
        {
            InitializeComponent();
            this.DataContext = this;

            _albumNameParam = albumName;
            _artistNameParam = artistName;
            _mbidParam = mbid;
            _initialCoverArtUrlParam = coverArtUrl;

            DetailedAlbumName = _albumNameParam ?? "Album";
            DetailedArtistName = _artistNameParam ?? "Artist";
            DetailedCoverArtUrl = _initialCoverArtUrlParam ?? "/Images/placeholder_album.png";
            ReleaseInfo = "Loading...";
            GenreInfo = "Loading...";
            DescriptionText = "Loading album description...";
            ListenersCount = "Loading...";
            PlayCount = "Loading...";
            LanguageInfo = "English (Default)"; // Or determine from data
            RatingDisplay = "★★★★☆ (Sample)"; // Or from actual data

            AlbumTracks = new ObservableCollection<TrackItem>();
            AlbumTracks.Add(new TrackItem { Number = " ", Title = "Loading tracks...", Duration = "" });

            if (client.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("JamandRateApp/1.0"); // Replace with your app name
            }
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Discogs", $"token={DiscogsApiToken}");

            this.Loaded += Overview_Loaded;
        }

        private async void Overview_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadAllAlbumDetailsAsync();
        }

        private async Task LoadAllAlbumDetailsAsync()
        {
            bool discogsSuccess = await LoadDiscogsDataAsync();
            await LoadLastFmDataAsync(discogsSuccess); // Pass Discogs success to Last.fm load
        }

        private async Task<bool> LoadDiscogsDataAsync()
        {
            if (string.IsNullOrWhiteSpace(_artistNameParam) || string.IsNullOrWhiteSpace(_albumNameParam))
            {
                Debug.WriteLine("Discogs: Artist or Album name is missing for search.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(DiscogsApiToken))
            {
                Debug.WriteLine("Discogs API Token is not configured. Skipping Discogs data load.");
                return false;
            }

            try
            {
                string searchUrl = $"{DiscogsApiBaseUrl}/database/search?artist={Uri.EscapeDataString(_artistNameParam)}&release_title={Uri.EscapeDataString(_albumNameParam)}&type=master,release&per_page=5&page=1";
                Debug.WriteLine($"Discogs Search URL: {searchUrl}");

                HttpResponseMessage searchResponseMsg = await client.GetAsync(searchUrl);
                if (!searchResponseMsg.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Discogs search API Error: {searchResponseMsg.StatusCode} - {await searchResponseMsg.Content.ReadAsStringAsync()}");
                    return false;
                }

                string searchJson = await searchResponseMsg.Content.ReadAsStringAsync();
                var searchOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var discogsSearchResponse = JsonSerializer.Deserialize<DiscogsSearchResponse>(searchJson, searchOptions);

                if (discogsSearchResponse?.Results == null || !discogsSearchResponse.Results.Any())
                {
                    Debug.WriteLine("Discogs: No search results found.");
                    return false;
                }

                DiscogsSearchResultItem bestMatch = discogsSearchResponse.Results
                    .OrderByDescending(r => r.MasterId.HasValue && r.MasterId > 0)
                    .ThenByDescending(r => r.Community?.Have ?? 0)
                    .FirstOrDefault(r => r.Title.Contains(_albumNameParam, StringComparison.OrdinalIgnoreCase) &&
                                         (r.MasterId.HasValue || r.Id > 0));
                if (bestMatch == null)
                {
                    bestMatch = discogsSearchResponse.Results
                       .OrderByDescending(r => r.MasterId.HasValue && r.MasterId > 0)
                       .ThenByDescending(r => r.Community?.Have ?? 0)
                       .FirstOrDefault(r => (r.MasterId.HasValue || r.Id > 0));
                }
                _currentDiscogsBestMatch = bestMatch;


                if (bestMatch == null)
                {
                    Debug.WriteLine("Discogs: No suitable match found in search results.");
                    return false;
                }

                string detailsUrl;
                if (bestMatch.MasterId.HasValue && bestMatch.MasterId > 0)
                {
                    detailsUrl = $"{DiscogsApiBaseUrl}/masters/{bestMatch.MasterId}";
                }
                else
                {
                    detailsUrl = $"{DiscogsApiBaseUrl}/releases/{bestMatch.Id}";
                }
                Debug.WriteLine($"Discogs Details URL: {detailsUrl}");

                HttpResponseMessage detailsResponseMsg = await client.GetAsync(detailsUrl);
                if (!detailsResponseMsg.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Discogs details API Error: {detailsResponseMsg.StatusCode} - {await detailsResponseMsg.Content.ReadAsStringAsync()}");
                    return false;
                }

                string detailsJson = await detailsResponseMsg.Content.ReadAsStringAsync();
                var releaseOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var discogsReleaseData = JsonSerializer.Deserialize<DiscogsRelease>(detailsJson, releaseOptions);

                if (discogsReleaseData == null)
                {
                    Debug.WriteLine("Discogs: Failed to parse release/master details.");
                    return false;
                }

                DetailedAlbumName = discogsReleaseData.Title ?? _albumNameParam;
                DetailedArtistName = discogsReleaseData.PrimaryArtistName ?? _artistNameParam;

                if (!string.IsNullOrWhiteSpace(discogsReleaseData.PrimaryImageUrl))
                {
                    DetailedCoverArtUrl = discogsReleaseData.PrimaryImageUrl;
                }


                if (!string.IsNullOrWhiteSpace(discogsReleaseData.ReleasedFormatted))
                {
                    ReleaseInfo = discogsReleaseData.ReleasedFormatted;
                }
                else if (discogsReleaseData.Year > 0)
                {
                    ReleaseInfo = discogsReleaseData.Year.ToString();
                }

                var genresAndStyles = new List<string>();
                if (discogsReleaseData.Genres != null) genresAndStyles.AddRange(discogsReleaseData.Genres);
                if (discogsReleaseData.Styles != null) genresAndStyles.AddRange(discogsReleaseData.Styles);
                if (genresAndStyles.Any())
                {
                    GenreInfo = string.Join(", ", genresAndStyles.Distinct());
                }

                if (discogsReleaseData.Tracklist != null && discogsReleaseData.Tracklist.Any())
                {
                    AlbumTracks.Clear();
                    foreach (var track in discogsReleaseData.Tracklist.Where(t => t.Type?.Equals("track", StringComparison.OrdinalIgnoreCase) == true))
                    {
                        AlbumTracks.Add(new TrackItem
                        {
                            Number = track.Position,
                            Title = track.Title,
                            Duration = track.Duration
                        });
                    }
                }

                if (!string.IsNullOrWhiteSpace(discogsReleaseData.Notes))
                {
                    DescriptionText = Regex.Replace(discogsReleaseData.Notes, @"\[([a-z])=(.+?)\]", "$2", RegexOptions.IgnoreCase); // Basic [type=value] to value
                    DescriptionText = Regex.Replace(DescriptionText, @"\[/?([a-z]+)\]", "", RegexOptions.IgnoreCase); // Remove remaining [tag] or [/tag]
                    DescriptionText = DescriptionText.Trim();
                    if (string.IsNullOrWhiteSpace(DescriptionText)) DescriptionText = "No description available from Discogs.";

                }


                _currentArtistId = discogsReleaseData.Artists?.FirstOrDefault()?.Id;

                if (detailsUrl.Contains("/masters/"))
                {
                    _currentDiscogsMasterId = discogsReleaseData.Id; // Master ID is the ID of the master record itself
                    _currentDiscogsReleaseId = discogsReleaseData.MainRelease; // Main release ID associated with this master
                }
                else
                { // It's a release URL
                    _currentDiscogsReleaseId = discogsReleaseData.Id; // Release ID is the ID of the release record
                    _currentDiscogsMasterId = discogsReleaseData.MasterId; // Master ID if this release is part of one
                    if (!_currentDiscogsMasterId.HasValue && _currentDiscogsBestMatch != null)
                    {
                        _currentDiscogsMasterId = _currentDiscogsBestMatch.MasterId;
                    }
                }


                if (_currentArtistId.HasValue)
                {
                    await LoadArtistDiscographyAsync(_currentArtistId.Value);
                }

                Debug.WriteLine("Successfully loaded and mapped data from Discogs.");
                return true;

            }
            catch (HttpRequestException httpEx)
            {
                Debug.WriteLine($"Discogs HTTP Request Error: {httpEx.ToString()}");
                return false;
            }
            catch (JsonException jsonEx)
            {
                Debug.WriteLine($"Discogs JSON Parsing Error: {jsonEx.ToString()}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Discogs Generic Error: {ex.ToString()}");
                return false;
            }
        }

        private async Task LoadArtistDiscographyAsync(int artistId)
        {
            try
            {
                string artistReleasesUrl = $"{DiscogsApiBaseUrl}/artists/{artistId}/releases?sort=year&sort_order=asc&per_page=100"; // Get more items
                Debug.WriteLine($"Discogs Artist Releases URL: {artistReleasesUrl}");

                HttpResponseMessage responseMsg = await client.GetAsync(artistReleasesUrl);
                if (!responseMsg.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Discogs artist releases API Error: {responseMsg.StatusCode} - {await responseMsg.Content.ReadAsStringAsync()}");
                    return;
                }

                string jsonResponse = await responseMsg.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var artistReleasesResponse = JsonSerializer.Deserialize<DiscogsArtistReleasesResponse>(jsonResponse, options);

                if (artistReleasesResponse?.Releases == null || !artistReleasesResponse.Releases.Any())
                {
                    Debug.WriteLine("Discogs: No releases found for the artist or failed to parse.");
                    return;
                }

                List<DiscogsArtistReleaseItem> artistMainAlbums = artistReleasesResponse.Releases
                    .Where(r => (r.Type?.Equals("master", StringComparison.OrdinalIgnoreCase) == true ||
                                 r.Type?.Equals("release", StringComparison.OrdinalIgnoreCase) == true) &&
                                 r.Role?.Equals("Main", StringComparison.OrdinalIgnoreCase) == true &&
                                 !string.IsNullOrWhiteSpace(r.Title) &&
                                 r.ParsedYear > 0)
                    .OrderBy(r => r.ParsedYear)
                    .ThenBy(r => r.Title) // Secondary sort by title for same year
                    .ToList();

                if (!artistMainAlbums.Any())
                { // Fallback if "Main" role yields nothing or positive year filter is too strict
                    artistMainAlbums = artistReleasesResponse.Releases
                        .Where(r => (r.Type?.Equals("master", StringComparison.OrdinalIgnoreCase) == true ||
                                     r.Type?.Equals("release", StringComparison.OrdinalIgnoreCase) == true) &&
                                     !string.IsNullOrWhiteSpace(r.Title) &&
                                     r.ParsedYear >= 0) // Allow year 0 for unknowns
                        .OrderBy(r => r.ParsedYear == 0 ? int.MaxValue : r.ParsedYear) // Sort unknowns last if preferred, or just r.ParsedYear
                        .ThenBy(r => r.Title)
                        .ToList();
                }


                int currentIndex = -1;

                // Try matching by Master ID first, as it's more definitive for an "album" concept
                if (_currentDiscogsMasterId.HasValue && _currentDiscogsMasterId.Value > 0)
                {
                    currentIndex = artistMainAlbums.FindIndex(a =>
                        a.MasterId == _currentDiscogsMasterId || // If the item in list has MasterId set
                        (a.Type == "master" && a.Id == _currentDiscogsMasterId)); // If item itself is a master
                }

                // If not found by Master ID, or if current album is a specific release not tied to a known master
                if (currentIndex == -1 && _currentDiscogsReleaseId.HasValue)
                {
                    // Match by ReleaseId only if type is "release" to avoid master IDs clashing with release IDs
                    currentIndex = artistMainAlbums.FindIndex(a => a.Id == _currentDiscogsReleaseId && a.Type == "release");
                }

                // If still not found by ID, try a more lenient title and year match (last resort)
                if (currentIndex == -1)
                {
                    string currentAlbumTitlePart = DetailedAlbumName;
                    if (DetailedAlbumName.Contains(" - ") && DetailedAlbumName.Split(new[] { " - " }, 2, StringSplitOptions.None).Length > 1)
                    {
                        currentAlbumTitlePart = DetailedAlbumName.Split(new[] { " - " }, 2, StringSplitOptions.None)[1].Trim();
                    }

                    int currentAlbumYear = 0; // Use the Year from DiscogsRelease (int)
                    if (int.TryParse(ReleaseInfo?.Split(',').First().Trim(), out int parsedYear))
                    { // Try to parse from ReleaseInfo (e.g., "1999" or "Jan 1999")
                        currentAlbumYear = parsedYear;
                    }
                    else if (_currentDiscogsBestMatch?.Year != null && int.TryParse(_currentDiscogsBestMatch.Year, out parsedYear))
                    {
                        currentAlbumYear = parsedYear; // Fallback to search result year if available
                    }


                    currentIndex = artistMainAlbums.FindIndex(a =>
                        a.Title.Equals(currentAlbumTitlePart, StringComparison.OrdinalIgnoreCase) ||
                        (a.Title.Contains(currentAlbumTitlePart, StringComparison.OrdinalIgnoreCase) &&
                         (currentAlbumYear > 0 && a.ParsedYear > 0 && Math.Abs(a.ParsedYear - currentAlbumYear) <= 1))
                    );
                }


                if (currentIndex != -1)
                {
                    if (currentIndex > 0)
                    {
                        PreviousAlbum = artistMainAlbums[currentIndex - 1];
                    }
                    if (currentIndex < artistMainAlbums.Count - 1)
                    {
                        NextAlbum = artistMainAlbums[currentIndex + 1];
                    }
                }
                else
                {
                    Debug.WriteLine($"Could not find current album (MasterID: {_currentDiscogsMasterId}, ReleaseID: {_currentDiscogsReleaseId}, Title: {DetailedAlbumName}) in artist's discography list after filtering.");
                }
            }
            catch (HttpRequestException httpEx)
            {
                Debug.WriteLine($"Discogs Artist Discography HTTP Request Error: {httpEx.ToString()}");
            }
            catch (JsonException jsonEx)
            {
                Debug.WriteLine($"Discogs Artist Discography JSON Parsing Error: {jsonEx.ToString()}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Discogs Artist Discography Generic Error: {ex.ToString()}");
            }
        }

        private async Task LoadLastFmDataAsync(bool discogsDataLoadedSuccessfully)
        {
            string apiUrl;

            // Use MBID if available (highest priority)
            if (!string.IsNullOrWhiteSpace(_mbidParam))
            {
                apiUrl = $"http://ws.audioscrobbler.com/2.0/?method=album.getInfo&api_key={LastFmApiKey}&mbid={_mbidParam}&format=json";
            }
            // Fallback to artist and album name from Discogs if successful, or initial params if not
            else if (!string.IsNullOrWhiteSpace(DetailedAlbumName) && !string.IsNullOrWhiteSpace(DetailedArtistName) && DetailedAlbumName != "Album" && DetailedArtistName != "Artist")
            {
                apiUrl = $"http://ws.audioscrobbler.com/2.0/?method=album.getInfo&api_key={LastFmApiKey}&artist={Uri.EscapeDataString(DetailedArtistName)}&album={Uri.EscapeDataString(DetailedAlbumName)}&format=json";
            }
            // Final fallback to initial parameters if Discogs data wasn't good enough
            else if (!string.IsNullOrWhiteSpace(_artistNameParam) && !string.IsNullOrWhiteSpace(_albumNameParam))
            {
                apiUrl = $"http://ws.audioscrobbler.com/2.0/?method=album.getInfo&api_key={LastFmApiKey}&artist={Uri.EscapeDataString(_artistNameParam)}&album={Uri.EscapeDataString(_albumNameParam)}&format=json";
            }
            else
            {
                // Not enough info to call Last.fm
                if (!discogsDataLoadedSuccessfully) DescriptionText = "Not enough information to load album details.";
                if (!AlbumTracks.Any() || AlbumTracks.First().Title == "Loading tracks...") { AlbumTracks.Clear(); AlbumTracks.Add(new TrackItem { Title = "Error: Insufficient information." }); }
                if (ReleaseInfo == "Loading...") ReleaseInfo = "N/A";
                if (GenreInfo == "Loading...") GenreInfo = "N/A";
                ListenersCount = "N/A"; PlayCount = "N/A";
                return;
            }

            Debug.WriteLine($"Requesting Album Info URL (Last.fm): {apiUrl}");

            try
            {
                // Use a separate HttpClient instance or ensure the shared one is configured correctly for Last.fm if needed
                var lastFmClient = new HttpClient();
                if (lastFmClient.DefaultRequestHeaders.UserAgent.Count == 0)
                {
                    lastFmClient.DefaultRequestHeaders.UserAgent.ParseAdd("JamandRateApp/1.0"); // Replace with your app name
                }

                HttpResponseMessage httpResponse = await lastFmClient.GetAsync(apiUrl);
                string jsonResponse = await httpResponse.Content.ReadAsStringAsync();

                if (!httpResponse.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Last.fm API Error Status: {httpResponse.StatusCode}, Response: {jsonResponse.Substring(0, Math.Min(jsonResponse.Length, 500))}");
                    // Only overwrite if Discogs didn't provide data
                    if (!discogsDataLoadedSuccessfully) DescriptionText = $"Error loading details from Last.fm: {httpResponse.ReasonPhrase}.";
                    if ((!AlbumTracks.Any() || AlbumTracks.First().Title == "Loading tracks...") && !discogsDataLoadedSuccessfully) { AlbumTracks.Clear(); AlbumTracks.Add(new TrackItem { Title = $"Last.fm API Error: {httpResponse.StatusCode}" }); }
                    if ((ReleaseInfo == "Loading..." || string.IsNullOrWhiteSpace(ReleaseInfo)) && !discogsDataLoadedSuccessfully) ReleaseInfo = "Error";
                    if ((GenreInfo == "Loading..." || string.IsNullOrWhiteSpace(GenreInfo)) && !discogsDataLoadedSuccessfully) GenreInfo = "Error";
                    ListenersCount = "Error"; PlayCount = "Error";
                    return;
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var albumInfoResponse = JsonSerializer.Deserialize<LastFmAlbumInfoResponse>(jsonResponse, options);

                if (albumInfoResponse?.error != null)
                {
                    Debug.WriteLine($"Last.fm API Error {albumInfoResponse.error}: {albumInfoResponse.message}");
                    if (!discogsDataLoadedSuccessfully) DescriptionText = $"Last.fm API Error: {albumInfoResponse.message}";
                    if ((!AlbumTracks.Any() || AlbumTracks.First().Title == "Loading tracks...") && !discogsDataLoadedSuccessfully) { AlbumTracks.Clear(); AlbumTracks.Add(new TrackItem { Title = "Last.fm API Error." }); }
                    if ((ReleaseInfo == "Loading..." || string.IsNullOrWhiteSpace(ReleaseInfo)) && !discogsDataLoadedSuccessfully) ReleaseInfo = "API Error";
                    if ((GenreInfo == "Loading..." || string.IsNullOrWhiteSpace(GenreInfo)) && !discogsDataLoadedSuccessfully) GenreInfo = "API Error";
                    ListenersCount = "API Error"; PlayCount = "API Error";
                    return;
                }

                if (albumInfoResponse?.Album != null)
                {
                    LastFmDetailedAlbum detailedAlbum = albumInfoResponse.Album;

                    // Prioritize Discogs cover if already set and valid, otherwise use Last.fm
                    string lastFmCover = GetLastFmImageUrl(detailedAlbum.Image, "extralarge");
                    if (string.IsNullOrWhiteSpace(DetailedCoverArtUrl) || DetailedCoverArtUrl.EndsWith("placeholder_album.png"))
                    {
                        if (!string.IsNullOrWhiteSpace(lastFmCover)) DetailedCoverArtUrl = lastFmCover;
                    }


                    // Only update if Discogs didn't provide or if placeholder
                    if (ReleaseInfo == "Loading..." || ReleaseInfo == "N/A" || string.IsNullOrWhiteSpace(ReleaseInfo))
                    {
                        if (detailedAlbum.Wiki != null && !string.IsNullOrWhiteSpace(detailedAlbum.Wiki.Published))
                        {
                            var dateParts = detailedAlbum.Wiki.Published.Split(','); // e.g., "28 Oct 2003, 00:00"
                            ReleaseInfo = dateParts[0].Trim();
                        }
                        else { ReleaseInfo = "N/A"; }
                    }

                    if (GenreInfo == "Loading..." || GenreInfo == "N/A" || string.IsNullOrWhiteSpace(GenreInfo))
                    {
                        if (detailedAlbum.Tags?.Tag != null && detailedAlbum.Tags.Tag.Any())
                        {
                            GenreInfo = string.Join(", ", detailedAlbum.Tags.Tag.Select(t => t.Name).Take(3));
                        }
                        else { GenreInfo = "N/A"; }
                    }

                    // Update description only if Discogs didn't provide a good one
                    if (DescriptionText == "Loading album description..." || DescriptionText == "No description available from Discogs." || string.IsNullOrWhiteSpace(DescriptionText))
                    {
                        string summary = detailedAlbum.Wiki?.Summary;
                        string content = detailedAlbum.Wiki?.Content;
                        string tempDescription = "No description available.";
                        if (!string.IsNullOrWhiteSpace(content))
                        {
                            tempDescription = Regex.Replace(content, "<a href=.*?>Read more on Last.fm</a>\\.?$", "", RegexOptions.IgnoreCase | RegexOptions.Singleline).Trim();
                            tempDescription = System.Net.WebUtility.HtmlDecode(Regex.Replace(tempDescription, "<.*?>", String.Empty).Trim());
                        }
                        else if (!string.IsNullOrWhiteSpace(summary))
                        {
                            tempDescription = Regex.Replace(summary, "<a href=.*?>Read more on Last.fm</a>\\.?$", "", RegexOptions.IgnoreCase | RegexOptions.Singleline).Trim();
                            tempDescription = System.Net.WebUtility.HtmlDecode(Regex.Replace(tempDescription, "<.*?>", String.Empty).Trim());
                        }
                        DescriptionText = string.IsNullOrWhiteSpace(tempDescription) ? "No description available." : tempDescription;
                    }


                    if (long.TryParse(detailedAlbum.Listeners, out long listenersVal)) ListenersCount = $"{listenersVal:N0} listeners"; else ListenersCount = "N/A";
                    if (long.TryParse(detailedAlbum.Playcount, out long playcountVal)) PlayCount = $"{playcountVal:N0} plays"; else PlayCount = "N/A";

                    // Only populate tracks from Last.fm if Discogs didn't provide them
                    if (!AlbumTracks.Any() || (AlbumTracks.Count == 1 && (AlbumTracks.First().Title == "Loading tracks..." || AlbumTracks.First().Title == "Error: Insufficient information.")))
                    {
                        AlbumTracks.Clear();
                        if (detailedAlbum.Tracks?.Track != null && detailedAlbum.Tracks.Track.Any())
                        {
                            int trackNumber = 1;
                            foreach (var track in detailedAlbum.Tracks.Track.OrderBy(t => t.Attr?.Rank ?? int.MaxValue))
                            {
                                AlbumTracks.Add(new TrackItem
                                {
                                    Number = (track.Attr?.Rank > 0 ? track.Attr.Rank.ToString() : trackNumber.ToString()) + ".",
                                    Title = track.Name,
                                    Duration = FormatTrackDuration(track.Duration)
                                });
                                trackNumber++;
                            }
                        }
                        else { AlbumTracks.Add(new TrackItem { Title = "No track information available." }); }
                    }
                }
                else // albumInfoResponse.Album is null
                {
                    if (!discogsDataLoadedSuccessfully) DescriptionText = "Album details not found in Last.fm API response.";
                    if ((!AlbumTracks.Any() || AlbumTracks.First().Title == "Loading tracks...") && !discogsDataLoadedSuccessfully) { AlbumTracks.Clear(); AlbumTracks.Add(new TrackItem { Title = "Details not found (Last.fm)." }); }
                    if ((ReleaseInfo == "Loading..." || string.IsNullOrWhiteSpace(ReleaseInfo)) && !discogsDataLoadedSuccessfully) ReleaseInfo = "N/A";
                    if ((GenreInfo == "Loading..." || string.IsNullOrWhiteSpace(GenreInfo)) && !discogsDataLoadedSuccessfully) GenreInfo = "N/A";
                    ListenersCount = "N/A"; PlayCount = "N/A";
                }
            }
            catch (HttpRequestException httpEx)
            {
                Debug.WriteLine($"Last.fm HTTP Request Error: {httpEx.ToString()}");
                if (!discogsDataLoadedSuccessfully) DescriptionText = $"Network error (Last.fm). ({httpEx.Message})";
                // ... (similar conditional updates for other fields)
                ListenersCount = "Network Error"; PlayCount = "Network Error";
            }
            catch (JsonException jsonEx)
            {
                Debug.WriteLine($"Last.fm JSON Parsing Error: {jsonEx.ToString()}");
                if (!discogsDataLoadedSuccessfully) DescriptionText = "Error parsing data from Last.fm API.";
                // ...
                ListenersCount = "Data Error"; PlayCount = "Data Error";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Last.fm Generic Error: {ex.ToString()}");
                if (!discogsDataLoadedSuccessfully) DescriptionText = $"An unexpected error occurred (Last.fm): {ex.Message}";
                // ...
                ListenersCount = "Unexpected Error"; PlayCount = "Unexpected Error";
            }
        }

        private string GetLastFmImageUrl(List<LastFmImage> images, string preferredSize = "extralarge")
        {
            if (images == null || !images.Any()) return null; // Return null, caller handles fallback

            var img = images.FirstOrDefault(i => i.Size == preferredSize && !string.IsNullOrWhiteSpace(i.Text));
            if (img != null) return img.Text;

            string[] fallbackSizes = { "mega", "large", "medium", "small" }; // Common Last.fm sizes
            foreach (var sizeKey in fallbackSizes)
            {
                img = images.FirstOrDefault(i => i.Size == sizeKey && !string.IsNullOrWhiteSpace(i.Text));
                if (img != null) return img.Text;
            }

            img = images.LastOrDefault(i => !string.IsNullOrWhiteSpace(i.Text)); // Any image with text
            return img?.Text; // Return null if still not found
        }

        private string FormatTrackDuration(int? totalSeconds)
        {
            if (!totalSeconds.HasValue || totalSeconds.Value <= 0)
            {
                return ""; // Return empty for unknown or zero duration
            }
            TimeSpan time = TimeSpan.FromSeconds(totalSeconds.Value);
            return $"{(int)time.TotalMinutes}:{time.Seconds:D2}";
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void btnGoBackOverview_Click(object sender, RoutedEventArgs e)
        {
            // Consider which page to go back to. If MainPage is the main hub:
            var mainPage = new JNR.Views.MainPage.MainPage();
            mainPage.Show();
            this.Close();
        }

        private void PreviousAlbum_Click(object sender, MouseButtonEventArgs e)
        {
            if (PreviousAlbum != null)
            {
                var overview = new Overview(
                    PreviousAlbum.DisplayAlbumName, // From DiscogsArtistReleaseItem
                    PreviousAlbum.DisplayArtistName, // From DiscogsArtistReleaseItem
                    null, // No MBID known for prev/next from this context
                    PreviousAlbum.Thumb // From DiscogsArtistReleaseItem
                );
                overview.Show();
                this.Close();
            }
        }

        private void NextAlbum_Click(object sender, MouseButtonEventArgs e)
        {
            if (NextAlbum != null)
            {
                var overview = new Overview(
                    NextAlbum.DisplayAlbumName, // From DiscogsArtistReleaseItem
                    NextAlbum.DisplayArtistName, // From DiscogsArtistReleaseItem
                    null, // No MBID known for prev/next from this context
                    NextAlbum.Thumb // From DiscogsArtistReleaseItem
                );
                overview.Show();
                this.Close();
            }
        }
    }
}