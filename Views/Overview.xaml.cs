// File: Views/Overview.xaml.cs
//====================
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
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using JNR.Models.DiscogModels;

namespace JNR.Views
{
    // ... (Local TrackItem and LastFm Models remain the same) ...
    public class TrackItem
    {
        public string Number { get; set; }
        public string Title { get; set; }
        public string Duration { get; set; }
    }

    public class LastFmImage
    {
        [JsonPropertyName("#text")]
        public string Text { get; set; }
        public string size { get; set; }
    }

    public class LastFmTrackAttr
    {
        public int rank { get; set; }
    }

    public class LastFmTrack
    {
        public string name { get; set; }
        public string url { get; set; }
        public int? duration { get; set; }
        [JsonPropertyName("@attr")]
        public LastFmTrackAttr Attr { get; set; }
    }

    public class LastFmTrackList
    {
        public List<LastFmTrack> track { get; set; }
    }

    public class LastFmTag
    {
        public string name { get; set; }
        public string url { get; set; }
    }

    public class LastFmTagList
    {
        public List<LastFmTag> tag { get; set; }
    }

    public class LastFmWiki
    {
        public string published { get; set; }
        public string summary { get; set; }
        public string content { get; set; }
    }

    public class LastFmDetailedAlbum
    {
        public string name { get; set; }
        public string artist { get; set; }
        public string mbid { get; set; }
        public string url { get; set; }
        public List<LastFmImage> image { get; set; }
        public LastFmTagList tags { get; set; }
        public LastFmWiki wiki { get; set; }
        public LastFmTrackList tracks { get; set; }
        public string listeners { get; set; }
        public string playcount { get; set; }
    }

    public class LastFmAlbumInfoResponse
    {
        public LastFmDetailedAlbum album { get; set; }
        public string message { get; set; }
        public int? error { get; set; }
    }


    public partial class Overview : Window, INotifyPropertyChanged
    {
        // ... (INotifyPropertyChanged, Backing fields, Properties, Constructor, LoadDiscogsDataAsync etc. up to LoadArtistDiscographyAsync remain the same) ...
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

        public ObservableCollection<TrackItem> AlbumTracks { get; set; }

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
        private const string DiscogsApiToken = "TMMBVQQgfXKTCEmgHqukhGLvhyCKJuLKlSqfrJCn";

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
            LanguageInfo = "English (Default)";
            RatingDisplay = "★★★★☆ (Sample)";

            AlbumTracks = new ObservableCollection<TrackItem>();
            AlbumTracks.Add(new TrackItem { Number = " ", Title = "Loading tracks...", Duration = "" });

            if (client.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("JamandRateApp/1.0");
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
            await LoadLastFmDataAsync(discogsSuccess);
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

                if (!string.IsNullOrWhiteSpace(discogsReleaseData.ReleasedFormatted))
                {
                    ReleaseInfo = discogsReleaseData.ReleasedFormatted;
                }
                else if (discogsReleaseData.Year > 0) // discogsReleaseData.Year is int here
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

                _currentArtistId = discogsReleaseData.Artists?.FirstOrDefault()?.Id;
                if (detailsUrl.Contains("/masters/"))
                {
                    _currentDiscogsMasterId = discogsReleaseData.Id;
                    _currentDiscogsReleaseId = discogsReleaseData.MainRelease;
                }
                else
                {
                    _currentDiscogsReleaseId = discogsReleaseData.Id;
                    _currentDiscogsMasterId = discogsReleaseData.MasterId;
                    if (!_currentDiscogsMasterId.HasValue && _currentDiscogsBestMatch != null)
                    {
                        _currentDiscogsMasterId = _currentDiscogsBestMatch.MasterId;
                    }
                }

                if (_currentArtistId.HasValue)
                {
                    await LoadArtistDiscographyAsync(_currentArtistId.Value);
                }

                Debug.WriteLine("Successfully loaded and mapped data from Discogs (excluding cover & description).");
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
                string artistReleasesUrl = $"{DiscogsApiBaseUrl}/artists/{artistId}/releases?sort=year&sort_order=asc&per_page=100";
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

                // Filter for main albums/masters and sort them
                // Using ParsedYear from the model for filtering and sorting
                List<DiscogsArtistReleaseItem> artistMainAlbums = artistReleasesResponse.Releases
                    .Where(r => (r.Type?.Equals("master", StringComparison.OrdinalIgnoreCase) == true ||
                                 r.Type?.Equals("release", StringComparison.OrdinalIgnoreCase) == true) &&
                                 r.Role?.Equals("Main", StringComparison.OrdinalIgnoreCase) == true &&
                                 !string.IsNullOrWhiteSpace(r.Title) &&
                                 r.ParsedYear > 0) // Ensure year is a parseable, positive number
                    .OrderBy(r => r.ParsedYear)
                    .ThenBy(r => r.Title)
                    .ToList();

                if (!artistMainAlbums.Any())
                {
                    // Fallback if "Main" role yields nothing or positive year filter is too strict
                    artistMainAlbums = artistReleasesResponse.Releases
                        .Where(r => (r.Type?.Equals("master", StringComparison.OrdinalIgnoreCase) == true ||
                                     r.Type?.Equals("release", StringComparison.OrdinalIgnoreCase) == true) &&
                                     !string.IsNullOrWhiteSpace(r.Title) &&
                                     r.ParsedYear >= 0) // Allow year 0 if that's how Discogs represents some "unknowns" numerically
                        .OrderBy(r => r.ParsedYear) // Will sort 0s (unknowns) first
                        .ThenBy(r => r.Title)
                        .ToList();
                }


                // Find the index of the current album
                int currentIndex = -1;
                if (_currentDiscogsMasterId.HasValue && _currentDiscogsMasterId.Value > 0)
                {
                    currentIndex = artistMainAlbums.FindIndex(a =>
                        a.MasterId == _currentDiscogsMasterId ||
                        (a.Type == "master" && a.Id == _currentDiscogsMasterId));
                }

                if (currentIndex == -1 && _currentDiscogsReleaseId.HasValue)
                {
                    // Match by ReleaseId only if type is "release"
                    currentIndex = artistMainAlbums.FindIndex(a => a.Id == _currentDiscogsReleaseId && a.Type == "release");
                }

                if (currentIndex == -1)
                {
                    // Last resort: Lenient title match if IDs failed.
                    // This is tricky because DetailedAlbumName might be "Artist - Album" or just "Album".
                    // And Discogs release titles might vary.
                    string currentAlbumTitlePart = DetailedAlbumName;
                    if (DetailedAlbumName.Contains(" - ") && DetailedAlbumName.Split(new[] { " - " }, 2, StringSplitOptions.None).Length > 1)
                    {
                        currentAlbumTitlePart = DetailedAlbumName.Split(new[] { " - " }, 2, StringSplitOptions.None)[1].Trim();
                    }

                    int currentAlbumYear = 0;
                    if (!string.IsNullOrWhiteSpace(ReleaseInfo) && ReleaseInfo.Length >= 4)
                    {
                        int.TryParse(ReleaseInfo.Substring(0, 4), out currentAlbumYear); // Try to get year from main release info
                    }


                    currentIndex = artistMainAlbums.FindIndex(a =>
                        a.Title.Equals(currentAlbumTitlePart, StringComparison.OrdinalIgnoreCase) ||
                        (a.Title.Contains(currentAlbumTitlePart, StringComparison.OrdinalIgnoreCase) &&
                         (currentAlbumYear > 0 && a.ParsedYear > 0 && Math.Abs(a.ParsedYear - currentAlbumYear) <= 1)) // Year match within +/- 1
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
                Debug.WriteLine($"Discogs Artist Discography JSON Parsing Error: {jsonEx.ToString()}"); // This is where the original error occurred
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Discogs Artist Discography Generic Error: {ex.ToString()}");
            }
        }

        private async Task LoadLastFmDataAsync(bool discogsDataLoadedSuccessfully)
        {
            string apiUrl;
            if (!string.IsNullOrWhiteSpace(_mbidParam))
            {
                apiUrl = $"http://ws.audioscrobbler.com/2.0/?method=album.getInfo&api_key={LastFmApiKey}&mbid={_mbidParam}&format=json";
            }
            else if (!string.IsNullOrWhiteSpace(DetailedAlbumName) && !string.IsNullOrWhiteSpace(DetailedArtistName) && DetailedAlbumName != "Album" && DetailedArtistName != "Artist")
            {
                apiUrl = $"http://ws.audioscrobbler.com/2.0/?method=album.getInfo&api_key={LastFmApiKey}&artist={Uri.EscapeDataString(DetailedArtistName)}&album={Uri.EscapeDataString(DetailedAlbumName)}&format=json";
            }
            else
            {
                apiUrl = $"http://ws.audioscrobbler.com/2.0/?method=album.getInfo&api_key={LastFmApiKey}&artist={Uri.EscapeDataString(_artistNameParam)}&album={Uri.EscapeDataString(_albumNameParam)}&format=json";
            }

            if (string.IsNullOrWhiteSpace(DetailedAlbumName) || string.IsNullOrWhiteSpace(DetailedArtistName) || DetailedAlbumName == "Album" || DetailedArtistName == "Artist")
            {
                if (!discogsDataLoadedSuccessfully)
                {
                    DescriptionText = "Not enough information to load album details from Last.fm.";
                    if (!AlbumTracks.Any() || AlbumTracks.First().Title == "Loading tracks...") { AlbumTracks.Clear(); AlbumTracks.Add(new TrackItem { Title = "Error: Insufficient information." }); }
                    if (ReleaseInfo == "Loading...") ReleaseInfo = "N/A";
                    if (GenreInfo == "Loading...") GenreInfo = "N/A";
                }
                ListenersCount = "N/A"; PlayCount = "N/A";
                return;
            }

            Debug.WriteLine($"Requesting Album Info URL (Last.fm): {apiUrl}");

            try
            {
                var lastFmClient = new HttpClient();
                if (lastFmClient.DefaultRequestHeaders.UserAgent.Count == 0)
                {
                    lastFmClient.DefaultRequestHeaders.UserAgent.ParseAdd("JamandRateApp/1.0");
                }

                HttpResponseMessage httpResponse = await lastFmClient.GetAsync(apiUrl);
                string jsonResponse = await httpResponse.Content.ReadAsStringAsync();

                if (!httpResponse.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Last.fm API Error Status: {httpResponse.StatusCode}, Response: {jsonResponse.Substring(0, Math.Min(jsonResponse.Length, 500))}");
                    if (!discogsDataLoadedSuccessfully)
                    {
                        DescriptionText = $"Error loading details from Last.fm: {httpResponse.ReasonPhrase}.";
                        if (!AlbumTracks.Any() || AlbumTracks.First().Title == "Loading tracks...") { AlbumTracks.Clear(); AlbumTracks.Add(new TrackItem { Title = $"Last.fm API Error: {httpResponse.StatusCode}" }); }
                        if (ReleaseInfo == "Loading...") ReleaseInfo = "Error";
                        if (GenreInfo == "Loading...") GenreInfo = "Error";
                    }
                    ListenersCount = "Error"; PlayCount = "Error";
                    return;
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var albumInfoResponse = JsonSerializer.Deserialize<LastFmAlbumInfoResponse>(jsonResponse, options);

                if (albumInfoResponse?.error != null)
                {
                    Debug.WriteLine($"Last.fm API Error {albumInfoResponse.error}: {albumInfoResponse.message}");
                    if (!discogsDataLoadedSuccessfully)
                    {
                        DescriptionText = $"Last.fm API Error: {albumInfoResponse.message}";
                        if (!AlbumTracks.Any() || AlbumTracks.First().Title == "Loading tracks...") { AlbumTracks.Clear(); AlbumTracks.Add(new TrackItem { Title = "Last.fm API Error." }); }
                        if (ReleaseInfo == "Loading...") ReleaseInfo = "API Error";
                        if (GenreInfo == "Loading...") GenreInfo = "API Error";
                    }
                    ListenersCount = "API Error"; PlayCount = "API Error";
                    return;
                }

                if (albumInfoResponse?.album != null)
                {
                    LastFmDetailedAlbum detailedAlbum = albumInfoResponse.album;

                    string lastFmCover = GetLastFmImageUrl(detailedAlbum.image, "extralarge");
                    if (!string.IsNullOrWhiteSpace(lastFmCover))
                    {
                        DetailedCoverArtUrl = lastFmCover;
                    }

                    if (ReleaseInfo == "Loading..." || ReleaseInfo == "N/A")
                    {
                        if (detailedAlbum.wiki != null && !string.IsNullOrWhiteSpace(detailedAlbum.wiki.published))
                        {
                            var dateParts = detailedAlbum.wiki.published.Split(',');
                            ReleaseInfo = dateParts[0].Trim();
                        }
                        else { ReleaseInfo = "N/A"; }
                    }

                    if (GenreInfo == "Loading..." || GenreInfo == "N/A")
                    {
                        if (detailedAlbum.tags?.tag != null && detailedAlbum.tags.tag.Any())
                        {
                            GenreInfo = string.Join(", ", detailedAlbum.tags.tag.Select(t => t.name).Take(3));
                        }
                        else { GenreInfo = "N/A"; }
                    }

                    if (long.TryParse(detailedAlbum.listeners, out long listenersVal)) ListenersCount = $"{listenersVal:N0} listeners";
                    else ListenersCount = "N/A";
                    if (long.TryParse(detailedAlbum.playcount, out long playcountVal)) PlayCount = $"{playcountVal:N0} plays";
                    else PlayCount = "N/A";

                    string summary = detailedAlbum.wiki?.summary;
                    string content = detailedAlbum.wiki?.content;
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


                    if (!AlbumTracks.Any() || (AlbumTracks.Count == 1 && AlbumTracks.First().Title == "Loading tracks..."))
                    {
                        AlbumTracks.Clear();
                        if (detailedAlbum.tracks?.track != null && detailedAlbum.tracks.track.Any())
                        {
                            int trackNumber = 1;
                            foreach (var track in detailedAlbum.tracks.track.OrderBy(t => t.Attr?.rank ?? int.MaxValue))
                            {
                                AlbumTracks.Add(new TrackItem
                                {
                                    Number = (track.Attr?.rank > 0 ? track.Attr.rank.ToString() : trackNumber.ToString()) + ".",
                                    Title = track.name,
                                    Duration = FormatTrackDuration(track.duration)
                                });
                                trackNumber++;
                            }
                        }
                        else { AlbumTracks.Add(new TrackItem { Title = "No track information available." }); }
                    }
                }
                else
                {
                    if (!discogsDataLoadedSuccessfully)
                    {
                        DescriptionText = "Album details not found in Last.fm API response.";
                        if (!AlbumTracks.Any() || AlbumTracks.First().Title == "Loading tracks...") { AlbumTracks.Clear(); AlbumTracks.Add(new TrackItem { Title = "Details not found (Last.fm)." }); }
                        if (ReleaseInfo == "Loading...") ReleaseInfo = "N/A";
                        if (GenreInfo == "Loading...") GenreInfo = "N/A";
                    }
                    ListenersCount = "N/A"; PlayCount = "N/A";
                }
            }
            catch (HttpRequestException httpEx)
            {
                Debug.WriteLine($"Last.fm HTTP Request Error: {httpEx.ToString()}");
                if (!discogsDataLoadedSuccessfully)
                {
                    DescriptionText = $"Network error (Last.fm). ({httpEx.Message})";
                    if (!AlbumTracks.Any() || AlbumTracks.First().Title == "Loading tracks...") { AlbumTracks.Clear(); AlbumTracks.Add(new TrackItem { Title = "Network Error (Last.fm)." }); }
                    if (ReleaseInfo == "Loading...") ReleaseInfo = "Network Error";
                    if (GenreInfo == "Loading...") GenreInfo = "Network Error";
                }
                ListenersCount = "Network Error"; PlayCount = "Network Error";
            }
            catch (JsonException jsonEx)
            {
                Debug.WriteLine($"Last.fm JSON Parsing Error: {jsonEx.ToString()}");
                if (!discogsDataLoadedSuccessfully)
                {
                    DescriptionText = "Error parsing data from Last.fm API.";
                    if (!AlbumTracks.Any() || AlbumTracks.First().Title == "Loading tracks...") { AlbumTracks.Clear(); AlbumTracks.Add(new TrackItem { Title = "Data Error (Last.fm)." }); }
                    if (ReleaseInfo == "Loading...") ReleaseInfo = "Data Error";
                    if (GenreInfo == "Loading...") GenreInfo = "Data Error";
                }
                ListenersCount = "Data Error"; PlayCount = "Data Error";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Last.fm Generic Error: {ex.ToString()}");
                if (!discogsDataLoadedSuccessfully)
                {
                    DescriptionText = $"An unexpected error occurred (Last.fm): {ex.Message}";
                    if (!AlbumTracks.Any() || AlbumTracks.First().Title == "Loading tracks...") { AlbumTracks.Clear(); AlbumTracks.Add(new TrackItem { Title = "Unexpected Error (Last.fm)." }); }
                    if (ReleaseInfo == "Loading...") ReleaseInfo = "Unexpected Error";
                    if (GenreInfo == "Loading...") GenreInfo = "Unexpected Error";
                }
                ListenersCount = "Unexpected Error"; PlayCount = "Unexpected Error";
            }
        }

        private string GetLastFmImageUrl(List<LastFmImage> images, string preferredSize = "extralarge")
        {
            if (images == null || !images.Any()) return null;
            var img = images.FirstOrDefault(i => i.size == preferredSize && !string.IsNullOrWhiteSpace(i.Text));
            if (img != null) return img.Text;
            string[] fallbackSizes = { "mega", "large", "medium", "small" };
            foreach (var size in fallbackSizes)
            {
                img = images.FirstOrDefault(i => i.size == size && !string.IsNullOrWhiteSpace(i.Text));
                if (img != null) return img.Text;
            }
            img = images.LastOrDefault(i => !string.IsNullOrWhiteSpace(i.Text));
            return img?.Text;
        }

        private string FormatTrackDuration(int? totalSeconds)
        {
            if (!totalSeconds.HasValue || totalSeconds.Value <= 0)
            {
                return "";
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
            var mainPage = new JNR.Views.MainPage.MainPage();
            mainPage.Show();
            this.Close();
        }

        private void PreviousAlbum_Click(object sender, MouseButtonEventArgs e)
        {
            if (PreviousAlbum != null)
            {
                var overview = new Overview(
                    PreviousAlbum.DisplayAlbumName,
                    PreviousAlbum.DisplayArtistName,
                    null,
                    PreviousAlbum.Thumb
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
                    NextAlbum.DisplayAlbumName,
                    NextAlbum.DisplayArtistName,
                    null,
                    NextAlbum.Thumb
                );
                overview.Show();
                this.Close();
            }
        }
    }
}
