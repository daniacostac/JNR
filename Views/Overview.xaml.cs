// Archivo: Views\Overview.xaml.cs
//====================
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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls; // Required for RadioButton
using System.Windows.Input;
using JNR.Models.DiscogModels;
using JNR.Models.LastFmModels;
using JNR.Models;
using JNR.Helpers; // For SessionManager
using Microsoft.EntityFrameworkCore;

namespace JNR.Views
{
    public class UserReviewDisplayItem : INotifyPropertyChanged
    {
        private string _username;
        public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }

        private string _ratingDisplay;
        public string RatingDisplay { get => _ratingDisplay; set { _ratingDisplay = value; OnPropertyChanged(); } }

        private string _reviewText;
        public string ReviewText { get => _reviewText; set { _reviewText = value; OnPropertyChanged(); } }

        private string _ratedAtDisplay;
        public string RatedAtDisplay { get => _ratedAtDisplay; set { _ratedAtDisplay = value; OnPropertyChanged(); } }

        private bool _hasNumericRating;
        public bool HasNumericRating { get => _hasNumericRating; set { _hasNumericRating = value; OnPropertyChanged(); } }

        private bool _hasReviewText;
        public bool HasReviewText { get => _hasReviewText; set { _hasReviewText = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

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

        private string _averageRatingDisplay = "Not Rated Yet";
        public string AverageRatingDisplay
        {
            get => _averageRatingDisplay;
            set { _averageRatingDisplay = value; OnPropertyChanged(); }
        }

        // NEW PROPERTY FOR THE GAUGE
        private double _averageRatingValue;
        public double AverageRatingValue
        {
            get => _averageRatingValue;
            set { _averageRatingValue = value; OnPropertyChanged(); }
        }


        private bool _isUserLoggedIn;
        public bool IsUserLoggedIn
        {
            get => _isUserLoggedIn;
            set
            {
                _isUserLoggedIn = value;
                OnPropertyChanged();
                UpdateCanPostOrRate();
            }
        }

        private bool _canPostOrRate;
        public bool CanPostOrRate
        {
            get => _canPostOrRate;
            set
            {
                if (_canPostOrRate != value)
                {
                    _canPostOrRate = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<TrackItem> AlbumTracks { get; set; }
        public ObservableCollection<UserReviewDisplayItem> AlbumUserReviews { get; set; }


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
        private int? _currentDbAlbumId;

        public Overview(string albumName, string artistName, string mbid, string coverArtUrl)
        {
            InitializeComponent();
            this.DataContext = this;

            IsUserLoggedIn = SessionManager.CurrentUserId.HasValue;
            // CanPostOrRate will be updated after _currentDbAlbumId is known

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

            // Initialize gauge value
            AverageRatingValue = 0;

            AlbumTracks = new ObservableCollection<TrackItem>();
            AlbumTracks.Add(new TrackItem { Number = " ", Title = "Loading tracks...", Duration = "" });
            AlbumUserReviews = new ObservableCollection<UserReviewDisplayItem>();

            if (client.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("JamandRateApp/1.0");
            }

            this.Loaded += Overview_Loaded;
        }

        private void UpdateCanPostOrRate()
        {
            CanPostOrRate = IsUserLoggedIn && _currentDbAlbumId.HasValue;
        }

        private async void Overview_Loaded(object sender, RoutedEventArgs e)
        {
            if (!client.DefaultRequestHeaders.Any(h => h.Key == "Authorization" && h.Value.Any(v => v.StartsWith("Discogs"))))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Discogs", $"token={DiscogsApiToken}");
            }
            await LoadAllAlbumDetailsAsync();
            await InitializeAlbumDataForRatingAndDisplayAsync();
        }

        private async Task InitializeAlbumDataForRatingAndDisplayAsync()
        {
            string externalId = null;
            string idSource = null;

            if (!string.IsNullOrWhiteSpace(_mbidParam)) { externalId = _mbidParam; idSource = "mbid"; }
            else if (_currentDiscogsMasterId.HasValue && _currentDiscogsMasterId.Value > 0) { externalId = _currentDiscogsMasterId.Value.ToString(); idSource = "discogs_master"; }
            else if (_currentDiscogsReleaseId.HasValue && _currentDiscogsReleaseId.Value > 0) { externalId = _currentDiscogsReleaseId.Value.ToString(); idSource = "discogs_release"; }

            if (string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(DetailedAlbumName) || DetailedAlbumName == "Album")
            {
                AverageRatingDisplay = "N/A (Album ID unknown)";
                AverageRatingValue = 0; // MODIFIED: Reset gauge value
                _currentDbAlbumId = null;
                UpdateCanPostOrRate();
                return;
            }

            int? releaseYearDb = null;
            if (!string.IsNullOrWhiteSpace(ReleaseInfo) && ReleaseInfo != "N/A" && ReleaseInfo != "Loading...")
            {
                var yearString = ReleaseInfo.Split(',').First().Trim().Split(' ').LastOrDefault();
                if (int.TryParse(yearString, out int parsedYearVal)) releaseYearDb = parsedYearVal;
            }

            var optionsBuilder = new DbContextOptionsBuilder<JnrContext>();
            string connectionString = "Server=localhost;Port=3306;Database=jnr;Uid=root;Pwd=root;";
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

            using (var dbContext = new JnrContext(optionsBuilder.Options))
            {
                Album albumInDb = await dbContext.Albums
                                      .FirstOrDefaultAsync(a => a.ExternalAlbumId == externalId && a.IdSource == idSource);
                if (albumInDb == null)
                {
                    albumInDb = new Album
                    {
                        ExternalAlbumId = externalId,
                        IdSource = idSource,
                        Title = this.DetailedAlbumName,
                        Artist = this.DetailedArtistName,
                        CoverArtUrl = this.DetailedCoverArtUrl.EndsWith("placeholder_album.png") ? null : this.DetailedCoverArtUrl,
                        ReleaseYear = releaseYearDb,
                        FirstAddedAt = DateTime.UtcNow
                    };
                    dbContext.Albums.Add(albumInDb);
                    try { await dbContext.SaveChangesAsync(); } catch (Exception ex) { Debug.WriteLine($"Error saving new album for rating: {ex}"); /* Handle */ }
                }
                else
                {
                    bool changed = false;
                    if ((string.IsNullOrWhiteSpace(albumInDb.Title) || albumInDb.Title == "Album") && !string.IsNullOrWhiteSpace(this.DetailedAlbumName) && this.DetailedAlbumName != "Album") { albumInDb.Title = this.DetailedAlbumName; changed = true; }
                    if ((string.IsNullOrWhiteSpace(albumInDb.Artist) || albumInDb.Artist == "Artist") && !string.IsNullOrWhiteSpace(this.DetailedArtistName) && this.DetailedArtistName != "Artist") { albumInDb.Artist = this.DetailedArtistName; changed = true; }
                    if (string.IsNullOrWhiteSpace(albumInDb.CoverArtUrl) && !string.IsNullOrWhiteSpace(this.DetailedCoverArtUrl) && !this.DetailedCoverArtUrl.EndsWith("placeholder_album.png")) { albumInDb.CoverArtUrl = this.DetailedCoverArtUrl; changed = true; }
                    if (!albumInDb.ReleaseYear.HasValue && releaseYearDb.HasValue) { albumInDb.ReleaseYear = releaseYearDb; changed = true; }
                    if (changed) { try { await dbContext.SaveChangesAsync(); } catch (Exception ex) { Debug.WriteLine($"Error updating album details: {ex}"); } }
                }
                _currentDbAlbumId = albumInDb.AlbumId;
            }
            UpdateCanPostOrRate();
            await RefreshRatingAndReviewDisplayAsync();
        }

        private async Task RefreshRatingAndReviewDisplayAsync()
        {
            if (!_currentDbAlbumId.HasValue)
            {
                AverageRatingDisplay = "N/A (Album not in local DB)";
                AverageRatingValue = 0; // MODIFIED: Reset gauge value
                if (IsUserLoggedIn) txtUserRating.Text = "";
                AlbumUserReviews.Clear(); // Clear reviews if album ID is not available
                return;
            }

            var optionsBuilder = new DbContextOptionsBuilder<JnrContext>();
            string connectionString = "Server=localhost;Port=3306;Database=jnr;Uid=root;Pwd=root;";
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

            using (var dbContext = new JnrContext(optionsBuilder.Options))
            {
                // Calculate Average Rating
                var validRatings = await dbContext.Useralbumratings
                    .Where(r => r.AlbumId == _currentDbAlbumId.Value && r.Rating >= 0 && r.Rating <= 10)
                    .Select(r => (int)r.Rating)
                    .ToListAsync();

                if (validRatings.Any())
                {
                    double avg = validRatings.Average();
                    AverageRatingDisplay = $"Avg: {avg:F1}/10 ({validRatings.Count} votes)";
                    AverageRatingValue = avg; // MODIFIED: Set gauge value
                }
                else
                {
                    AverageRatingDisplay = "Not Rated Yet";
                    AverageRatingValue = 0; // MODIFIED: Reset gauge value
                }

                // Display Current User's Rating 
                if (SessionManager.CurrentUserId.HasValue)
                {
                    var currentUserRatingEntry = await dbContext.Useralbumratings
                        .FirstOrDefaultAsync(r => r.AlbumId == _currentDbAlbumId.Value && r.UserId == SessionManager.CurrentUserId.Value);

                    if (currentUserRatingEntry != null && currentUserRatingEntry.Rating >= 0 && currentUserRatingEntry.Rating <= 10)
                    {
                        txtUserRating.Text = currentUserRatingEntry.Rating.ToString();
                    }
                    else
                    {
                        txtUserRating.Text = "";
                    }
                    // txtUserRating IsEnabled is now bound to CanPostOrRate
                    // btnRateAlbum IsEnabled is now bound to CanPostOrRate
                }
                else
                {
                    txtUserRating.Text = "";
                    // txtUserRating IsEnabled is now bound to CanPostOrRate
                    // btnRateAlbum IsEnabled is now bound to CanPostOrRate
                }
            }
            await LoadUserReviewsAsync(); // Load/Refresh reviews
        }

        private async Task LoadUserReviewsAsync()
        {
            AlbumUserReviews.Clear();
            if (!_currentDbAlbumId.HasValue) return;

            var optionsBuilder = new DbContextOptionsBuilder<JnrContext>();
            string connectionString = "Server=localhost;Port=3306;Database=jnr;Uid=root;Pwd=root;"; // Consider moving to config
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

            using (var dbContext = new JnrContext(optionsBuilder.Options))
            {
                var reviewsFromDb = await dbContext.Useralbumratings
                    .Include(uar => uar.User) // Eager load User to get Username
                    .Where(uar => uar.AlbumId == _currentDbAlbumId.Value &&
                                  (!string.IsNullOrEmpty(uar.ReviewText) || (uar.Rating >= 0 && uar.Rating <= 10))) // Show if has review OR a numeric rating
                    .OrderByDescending(uar => uar.RatedAt)
                    .ToListAsync();

                foreach (var review in reviewsFromDb)
                {
                    var displayItem = new UserReviewDisplayItem
                    {
                        Username = review.User?.Username ?? "Unknown User",
                        ReviewText = review.ReviewText,
                        HasReviewText = !string.IsNullOrWhiteSpace(review.ReviewText),
                        RatedAtDisplay = $"Posted: {review.RatedAt:yyyy-MM-dd HH:mm}"
                    };

                    if (review.Rating >= 0 && review.Rating <= 10)
                    {
                        displayItem.RatingDisplay = $"Rated: {review.Rating}/10";
                        displayItem.HasNumericRating = true;
                    }

                    else
                    {
                        displayItem.RatingDisplay = ""; // No numeric rating to display explicitly
                        displayItem.HasNumericRating = false;
                    }
                    AlbumUserReviews.Add(displayItem);
                }
            }
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
                DescriptionText = "Artist or Album name is missing for Discogs search.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(DiscogsApiToken))
            {
                Debug.WriteLine("Discogs API Token is not configured. Skipping Discogs data load.");
                DescriptionText = "Discogs API Token is not configured.";
                return false;
            }

            try
            {
                string searchUrl = $"{DiscogsApiBaseUrl}/database/search?artist={Uri.EscapeDataString(_artistNameParam)}&release_title={Uri.EscapeDataString(_albumNameParam)}&type=master,release&per_page=5&page=1";
                Debug.WriteLine($"Discogs Search URL: {searchUrl}");

                HttpResponseMessage searchResponseMsg = await client.GetAsync(searchUrl);
                if (!searchResponseMsg.IsSuccessStatusCode)
                {
                    var errorContent = await searchResponseMsg.Content.ReadAsStringAsync();
                    Debug.WriteLine($"Discogs search API Error: {searchResponseMsg.StatusCode} - {errorContent}");
                    DescriptionText = $"Discogs search API Error: {searchResponseMsg.ReasonPhrase}";
                    return false;
                }

                string searchJson = await searchResponseMsg.Content.ReadAsStringAsync();
                var searchOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var discogsSearchResponse = JsonSerializer.Deserialize<DiscogsSearchResponse>(searchJson, searchOptions);

                if (discogsSearchResponse?.Results == null || !discogsSearchResponse.Results.Any())
                {
                    Debug.WriteLine("Discogs: No search results found.");
                    DescriptionText = "No results found on Discogs.";
                    return false;
                }

                DiscogsSearchResultItem bestMatch = discogsSearchResponse.Results
                    .OrderByDescending(r => r.MasterId.HasValue && r.MasterId > 0)
                    .ThenByDescending(r => r.Title.Equals(_albumNameParam, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(r => r.Community?.Have ?? 0)
                    .FirstOrDefault(r => (r.MasterId.HasValue || r.Id > 0));

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
                    DescriptionText = "No suitable match found on Discogs.";
                    return false;
                }

                string detailsUrl;
                if (bestMatch.MasterId.HasValue && bestMatch.MasterId > 0)
                {
                    detailsUrl = $"{DiscogsApiBaseUrl}/masters/{bestMatch.MasterId}";
                    _currentDiscogsMasterId = bestMatch.MasterId;
                }
                else
                {
                    detailsUrl = $"{DiscogsApiBaseUrl}/releases/{bestMatch.Id}";
                    _currentDiscogsReleaseId = bestMatch.Id;
                }
                Debug.WriteLine($"Discogs Details URL: {detailsUrl}");

                HttpResponseMessage detailsResponseMsg = await client.GetAsync(detailsUrl);
                if (!detailsResponseMsg.IsSuccessStatusCode)
                {
                    var errorContent = await detailsResponseMsg.Content.ReadAsStringAsync();
                    Debug.WriteLine($"Discogs details API Error: {detailsResponseMsg.StatusCode} - {errorContent}");
                    DescriptionText = $"Discogs details API Error: {detailsResponseMsg.ReasonPhrase}";
                    return false;
                }

                string detailsJson = await detailsResponseMsg.Content.ReadAsStringAsync();
                var releaseOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var discogsReleaseData = JsonSerializer.Deserialize<DiscogsRelease>(detailsJson, releaseOptions);

                if (discogsReleaseData == null)
                {
                    Debug.WriteLine("Discogs: Failed to parse release/master details.");
                    DescriptionText = "Failed to parse Discogs release/master details.";
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
                else { ReleaseInfo = "N/A"; }


                var genresAndStyles = new List<string>();
                if (discogsReleaseData.Genres != null) genresAndStyles.AddRange(discogsReleaseData.Genres);
                if (discogsReleaseData.Styles != null) genresAndStyles.AddRange(discogsReleaseData.Styles);
                GenreInfo = genresAndStyles.Any() ? string.Join(", ", genresAndStyles.Distinct()) : "N/A";


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
                else { AlbumTracks.Clear(); AlbumTracks.Add(new TrackItem { Title = "Tracklist not available from Discogs." }); }


                if (!string.IsNullOrWhiteSpace(discogsReleaseData.Notes))
                {
                    DescriptionText = Regex.Replace(discogsReleaseData.Notes, @"\[([a-z])=(.+?)\]", "$2", RegexOptions.IgnoreCase);
                    DescriptionText = Regex.Replace(DescriptionText, @"\[/?([a-z]+)\]", "", RegexOptions.IgnoreCase);
                    DescriptionText = DescriptionText.Trim();
                    if (string.IsNullOrWhiteSpace(DescriptionText)) DescriptionText = "No description available from Discogs.";
                }
                else { DescriptionText = "No description available from Discogs."; }

                _currentArtistId = discogsReleaseData.Artists?.FirstOrDefault()?.Id;

                if (detailsUrl.Contains("/masters/"))
                {
                    _currentDiscogsMasterId = discogsReleaseData.Id;
                    _currentDiscogsReleaseId = discogsReleaseData.MainRelease;
                }
                else // detailsUrl.Contains("/releases/")
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

                Debug.WriteLine("Successfully loaded and mapped data from Discogs.");
                return true;
            }
            catch (HttpRequestException httpEx)
            {
                Debug.WriteLine($"Discogs HTTP Request Error: {httpEx.ToString()}");
                DescriptionText = $"Discogs API error: {httpEx.Message}";
                return false;
            }
            catch (JsonException jsonEx)
            {
                Debug.WriteLine($"Discogs JSON Parsing Error: {jsonEx.ToString()}");
                DescriptionText = $"Discogs data parsing error: {jsonEx.Message}";
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Discogs Generic Error: {ex.ToString()}");
                DescriptionText = $"Unexpected error with Discogs: {ex.Message}";
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

                List<DiscogsArtistReleaseItem> artistMainAlbums = artistReleasesResponse.Releases
                    .Where(r => (r.Type?.Equals("master", StringComparison.OrdinalIgnoreCase) == true ||
                                 r.Type?.Equals("release", StringComparison.OrdinalIgnoreCase) == true) &&
                                 r.Role?.Equals("Main", StringComparison.OrdinalIgnoreCase) == true &&
                                 !string.IsNullOrWhiteSpace(r.Title) &&
                                 r.ParsedYear > 0)
                    .OrderBy(r => r.ParsedYear)
                    .ThenBy(r => r.Title)
                    .ToList();

                if (!artistMainAlbums.Any())
                {
                    artistMainAlbums = artistReleasesResponse.Releases
                        .Where(r => (r.Type?.Equals("master", StringComparison.OrdinalIgnoreCase) == true ||
                                     r.Type?.Equals("release", StringComparison.OrdinalIgnoreCase) == true) &&
                                     !string.IsNullOrWhiteSpace(r.Title) &&
                                     r.ParsedYear >= 0) // Include year 0 if that's all we have
                        .OrderBy(r => r.ParsedYear == 0 ? int.MaxValue : r.ParsedYear) // Sort 0 year to end if others exist
                        .ThenBy(r => r.Title)
                        .ToList();
                }


                int currentIndex = -1;

                // Try matching by Master ID first (most reliable)
                if (_currentDiscogsMasterId.HasValue && _currentDiscogsMasterId.Value > 0)
                {
                    currentIndex = artistMainAlbums.FindIndex(a =>
                        a.MasterId == _currentDiscogsMasterId ||
                        (a.Type?.Equals("master", StringComparison.OrdinalIgnoreCase) == true && a.Id == _currentDiscogsMasterId));
                }

                // If not found by Master ID, try by Release ID
                if (currentIndex == -1 && _currentDiscogsReleaseId.HasValue)
                {
                    currentIndex = artistMainAlbums.FindIndex(a => a.Id == _currentDiscogsReleaseId && a.Type?.Equals("release", StringComparison.OrdinalIgnoreCase) == true);
                }

                // Fallback: if still not found, try by title and year (less reliable)
                if (currentIndex == -1)
                {
                    string currentAlbumTitlePart = DetailedAlbumName; // Use the already determined DetailedAlbumName
                    if (DetailedAlbumName.Contains(" - ") && DetailedAlbumName.Split(new[] { " - " }, 2, StringSplitOptions.None).Length > 1)
                    {
                        currentAlbumTitlePart = DetailedAlbumName.Split(new[] { " - " }, 2, StringSplitOptions.None)[1].Trim();
                    }


                    int currentAlbumYear = 0;
                    if (!string.IsNullOrWhiteSpace(ReleaseInfo) && ReleaseInfo != "N/A" && ReleaseInfo != "Loading...")
                    {
                        var yearString = ReleaseInfo.Split(',').First().Trim().Split(' ').LastOrDefault();
                        if (int.TryParse(yearString, out int parsedYr)) currentAlbumYear = parsedYr;
                    }
                    else if (_currentDiscogsBestMatch?.Year != null && int.TryParse(_currentDiscogsBestMatch.Year, out int parsedYrFromMatch))
                    {
                        currentAlbumYear = parsedYrFromMatch;
                    }

                    currentIndex = artistMainAlbums.FindIndex(a =>
                        a.Title.Equals(currentAlbumTitlePart, StringComparison.OrdinalIgnoreCase) ||
                        (a.Title.Contains(currentAlbumTitlePart, StringComparison.OrdinalIgnoreCase) &&
                         (currentAlbumYear > 0 && a.ParsedYear > 0 && Math.Abs(a.ParsedYear - currentAlbumYear) <= 1)) // Allow 1 year difference for matches
                    );
                }


                if (currentIndex != -1)
                {
                    PreviousAlbum = (currentIndex > 0) ? artistMainAlbums[currentIndex - 1] : null;
                    NextAlbum = (currentIndex < artistMainAlbums.Count - 1) ? artistMainAlbums[currentIndex + 1] : null;
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

            if (!string.IsNullOrWhiteSpace(_mbidParam))
            {
                apiUrl = $"http://ws.audioscrobbler.com/2.0/?method=album.getInfo&api_key={LastFmApiKey}&mbid={_mbidParam}&format=json";
            }
            else if (!string.IsNullOrWhiteSpace(DetailedArtistName) && DetailedArtistName != "Artist" && !string.IsNullOrWhiteSpace(DetailedAlbumName) && DetailedAlbumName != "Album")
            {
                apiUrl = $"http://ws.audioscrobbler.com/2.0/?method=album.getInfo&api_key={LastFmApiKey}&artist={Uri.EscapeDataString(DetailedArtistName)}&album={Uri.EscapeDataString(DetailedAlbumName)}&format=json";
            }
            else if (!string.IsNullOrWhiteSpace(_artistNameParam) && !string.IsNullOrWhiteSpace(_albumNameParam))
            {
                apiUrl = $"http://ws.audioscrobbler.com/2.0/?method=album.getInfo&api_key={LastFmApiKey}&artist={Uri.EscapeDataString(_artistNameParam)}&album={Uri.EscapeDataString(_albumNameParam)}&format=json";
            }
            else
            {
                if (!discogsDataLoadedSuccessfully && (DescriptionText == "Loading album description..." || string.IsNullOrWhiteSpace(DescriptionText) || DescriptionText.StartsWith("Discogs API error") || DescriptionText.StartsWith("No results found on Discogs")))
                    DescriptionText = "Not enough information to load album details from any source.";
                if ((!AlbumTracks.Any() || AlbumTracks.First().Title == "Loading tracks..." || AlbumTracks.First().Title == "Tracklist not available from Discogs.") && !discogsDataLoadedSuccessfully)
                { AlbumTracks.Clear(); AlbumTracks.Add(new TrackItem { Title = "Error: Insufficient information." }); }
                if ((ReleaseInfo == "Loading..." || string.IsNullOrWhiteSpace(ReleaseInfo) || ReleaseInfo == "N/A") && !discogsDataLoadedSuccessfully) ReleaseInfo = "N/A";
                if ((GenreInfo == "Loading..." || string.IsNullOrWhiteSpace(GenreInfo) || GenreInfo == "N/A") && !discogsDataLoadedSuccessfully) GenreInfo = "N/A";
                ListenersCount = "N/A"; PlayCount = "N/A";
                return;
            }

            Debug.WriteLine($"Requesting Album Info URL (Last.fm): {apiUrl}");

            try
            {
                var lastFmHttpClient = new HttpClient();
                if (lastFmHttpClient.DefaultRequestHeaders.UserAgent.Count == 0)
                {
                    lastFmHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("JamandRateApp/1.0");
                }

                HttpResponseMessage httpResponse = await lastFmHttpClient.GetAsync(apiUrl);
                string jsonResponse = await httpResponse.Content.ReadAsStringAsync();

                if (!httpResponse.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Last.fm API Error Status: {httpResponse.StatusCode}, Response: {jsonResponse.Substring(0, Math.Min(jsonResponse.Length, 500))}");
                    if ((string.IsNullOrWhiteSpace(DescriptionText) || DescriptionText == "Loading album description..." || DescriptionText.StartsWith("Discogs API error") || DescriptionText == "No description available from Discogs.") && !discogsDataLoadedSuccessfully)
                        DescriptionText = $"Error loading details from Last.fm: {httpResponse.ReasonPhrase}.";
                    if ((!AlbumTracks.Any() || AlbumTracks.First().Title == "Loading tracks..." || AlbumTracks.First().Title == "Tracklist not available from Discogs.") && !discogsDataLoadedSuccessfully)
                    { AlbumTracks.Clear(); AlbumTracks.Add(new TrackItem { Title = $"Last.fm API Error: {httpResponse.StatusCode}" }); }
                    if ((ReleaseInfo == "Loading..." || string.IsNullOrWhiteSpace(ReleaseInfo) || ReleaseInfo == "N/A") && !discogsDataLoadedSuccessfully) ReleaseInfo = "Error";
                    if ((GenreInfo == "Loading..." || string.IsNullOrWhiteSpace(GenreInfo) || GenreInfo == "N/A") && !discogsDataLoadedSuccessfully) GenreInfo = "Error";
                    ListenersCount = "Error"; PlayCount = "Error";
                    return;
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var albumInfoResponse = JsonSerializer.Deserialize<LastFmAlbumInfoResponse>(jsonResponse, options);

                if (albumInfoResponse?.error != null)
                {
                    Debug.WriteLine($"Last.fm API Error {albumInfoResponse.error}: {albumInfoResponse.message}");
                    if ((string.IsNullOrWhiteSpace(DescriptionText) || DescriptionText == "Loading album description..." || DescriptionText.StartsWith("Discogs API error") || DescriptionText == "No description available from Discogs.") && !discogsDataLoadedSuccessfully)
                        DescriptionText = $"Last.fm API Error: {albumInfoResponse.message}";
                    return;
                }

                if (albumInfoResponse?.Album != null)
                {
                    LastFmDetailedAlbum detailedAlbum = albumInfoResponse.Album;

                    string lastFmCover = GetLastFmImageUrl(detailedAlbum.Image, "extralarge");
                    if (string.IsNullOrWhiteSpace(DetailedCoverArtUrl) || DetailedCoverArtUrl.EndsWith("placeholder_album.png"))
                    {
                        if (!string.IsNullOrWhiteSpace(lastFmCover)) DetailedCoverArtUrl = lastFmCover;
                    }

                    if (ReleaseInfo == "Loading..." || ReleaseInfo == "N/A" || string.IsNullOrWhiteSpace(ReleaseInfo))
                    {
                        if (detailedAlbum.Wiki != null && !string.IsNullOrWhiteSpace(detailedAlbum.Wiki.Published))
                        {
                            var dateParts = detailedAlbum.Wiki.Published.Split(',');
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

                    if (DescriptionText == "Loading album description..." || DescriptionText == "No description available from Discogs." || string.IsNullOrWhiteSpace(DescriptionText))
                    {
                        string summary = detailedAlbum.Wiki?.Summary;
                        string content = detailedAlbum.Wiki?.Content;
                        string tempDescription = "No description available from Last.fm.";
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
                        DescriptionText = string.IsNullOrWhiteSpace(tempDescription) || tempDescription.Length < 10 ? "No detailed description available from Last.fm." : tempDescription;
                    }

                    if (long.TryParse(detailedAlbum.Listeners, out long listenersVal)) ListenersCount = $"{listenersVal:N0} listeners"; else ListenersCount = "N/A";
                    if (long.TryParse(detailedAlbum.Playcount, out long playcountVal)) PlayCount = $"{playcountVal:N0} plays"; else PlayCount = "N/A";

                    if (!AlbumTracks.Any() || (AlbumTracks.Count == 1 && (AlbumTracks.First().Title == "Loading tracks..." || AlbumTracks.First().Title == "Error: Insufficient information." || AlbumTracks.First().Title == "Tracklist not available from Discogs.")))
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
                        else { AlbumTracks.Add(new TrackItem { Title = "No track information available from Last.fm." }); }
                    }
                }
                else
                {
                    if ((string.IsNullOrWhiteSpace(DescriptionText) || DescriptionText == "Loading album description..." || DescriptionText.StartsWith("Discogs API error") || DescriptionText == "No description available from Discogs.") && !discogsDataLoadedSuccessfully)
                        DescriptionText = "Album details not found in Last.fm API response.";
                }
            }
            catch (HttpRequestException httpEx)
            {
                Debug.WriteLine($"Last.fm HTTP Request Error: {httpEx.ToString()}");
                if ((string.IsNullOrWhiteSpace(DescriptionText) || DescriptionText == "Loading album description..." || DescriptionText.StartsWith("Discogs API error") || DescriptionText == "No description available from Discogs.") && !discogsDataLoadedSuccessfully)
                    DescriptionText = $"Network error (Last.fm). ({httpEx.Message})";
                ListenersCount = "Network Error"; PlayCount = "Network Error";
            }
            catch (JsonException jsonEx)
            {
                Debug.WriteLine($"Last.fm JSON Parsing Error: {jsonEx.ToString()}");
                if ((string.IsNullOrWhiteSpace(DescriptionText) || DescriptionText == "Loading album description..." || DescriptionText.StartsWith("Discogs API error") || DescriptionText == "No description available from Discogs.") && !discogsDataLoadedSuccessfully)
                    DescriptionText = "Error parsing data from Last.fm API.";
                ListenersCount = "Data Error"; PlayCount = "Data Error";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Last.fm Generic Error: {ex.ToString()}");
                if ((string.IsNullOrWhiteSpace(DescriptionText) || DescriptionText == "Loading album description..." || DescriptionText.StartsWith("Discogs API error") || DescriptionText == "No description available from Discogs.") && !discogsDataLoadedSuccessfully)
                    DescriptionText = $"An unexpected error occurred (Last.fm): {ex.Message}";
                ListenersCount = "Unexpected Error"; PlayCount = "Unexpected Error";
            }
        }

        private string GetLastFmImageUrl(List<LastFmImage> images, string preferredSize = "extralarge")
        {
            if (images == null || !images.Any()) return null;
            var img = images.FirstOrDefault(i => i.Size == preferredSize && !string.IsNullOrWhiteSpace(i.Text));
            if (img != null) return img.Text;
            string[] fallbackSizes = { "mega", "large", "medium", "small" };
            foreach (var sizeKey in fallbackSizes)
            {
                img = images.FirstOrDefault(i => i.Size == sizeKey && !string.IsNullOrWhiteSpace(i.Text));
                if (img != null) return img.Text;
            }
            img = images.LastOrDefault(i => !string.IsNullOrWhiteSpace(i.Text));
            return img?.Text;
        }

        private string FormatTrackDuration(int? totalSeconds)
        {
            if (!totalSeconds.HasValue || totalSeconds.Value <= 0) return "";
            TimeSpan time = TimeSpan.FromSeconds(totalSeconds.Value);
            return $"{(int)time.TotalMinutes}:{time.Seconds:D2}";
        }

        private async void AddToFavoritesButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!CanPostOrRate) // Uses the combined check for login and album ID
            {
                MessageBox.Show("You need to be logged in and album details must be loaded to add to your collection.", "Action Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // _currentDbAlbumId is guaranteed to have value if CanPostOrRate is true

            var optionsBuilder = new DbContextOptionsBuilder<JnrContext>();
            string connectionString = "Server=localhost;Port=3306;Database=jnr;Uid=root;Pwd=root;";
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

            using (var dbContext = new JnrContext(optionsBuilder.Options))
            {
                try
                {
                    var userAlbumRating = await dbContext.Useralbumratings
                        .FirstOrDefaultAsync(uar => uar.UserId == SessionManager.CurrentUserId.Value && uar.AlbumId == _currentDbAlbumId.Value);

                    if (userAlbumRating == null)
                    {
                        userAlbumRating = new Useralbumrating
                        {
                            UserId = SessionManager.CurrentUserId.Value,
                            AlbumId = _currentDbAlbumId.Value,
                            Rating = -1,
                            ReviewText = null,
                            RatedAt = DateTime.UtcNow
                        };
                        dbContext.Useralbumratings.Add(userAlbumRating);
                        await dbContext.SaveChangesAsync();
                        MessageBox.Show($"'{DetailedAlbumName}' added to your collection!", "Collection Updated", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("This album is already in your collection.", "Already in Collection", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    await RefreshRatingAndReviewDisplayAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error adding to collection: {ex.ToString()}");
                    MessageBox.Show($"Could not add album to collection: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void RateAlbum_Click(object sender, RoutedEventArgs e)
        {
            if (!CanPostOrRate)
            {
                MessageBox.Show("You need to be logged in and album details must be loaded to rate.", "Action Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtUserRating.Text, out int ratingValue) || ratingValue < 0 || ratingValue > 10)
            {
                MessageBox.Show("Please enter a valid rating between 0 and 10.", "Invalid Rating", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var optionsBuilder = new DbContextOptionsBuilder<JnrContext>();
            string connectionString = "Server=localhost;Port=3306;Database=jnr;Uid=root;Pwd=root;";
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

            using (var dbContext = new JnrContext(optionsBuilder.Options))
            {
                try
                {
                    var userAlbumRating = await dbContext.Useralbumratings
                        .FirstOrDefaultAsync(uar => uar.UserId == SessionManager.CurrentUserId.Value && uar.AlbumId == _currentDbAlbumId.Value);

                    if (userAlbumRating != null)
                    {
                        userAlbumRating.Rating = (sbyte)ratingValue;
                        userAlbumRating.RatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        userAlbumRating = new Useralbumrating
                        {
                            UserId = SessionManager.CurrentUserId.Value,
                            AlbumId = _currentDbAlbumId.Value,
                            Rating = (sbyte)ratingValue,
                            RatedAt = DateTime.UtcNow
                            // ReviewText will be null initially
                        };
                        dbContext.Useralbumratings.Add(userAlbumRating);
                    }
                    await dbContext.SaveChangesAsync();
                    MessageBox.Show("Your rating has been saved!", "Rating Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                    await RefreshRatingAndReviewDisplayAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error saving rating: {ex.ToString()}");
                    MessageBox.Show($"Could not save your rating: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void PostReview_Click(object sender, RoutedEventArgs e)
        {
            if (!CanPostOrRate)
            {
                MessageBox.Show("You need to be logged in and album details must be loaded to post a review.", "Action Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string reviewText = txtUserReviewText.Text.Trim();
            if (string.IsNullOrWhiteSpace(reviewText))
            {
                MessageBox.Show("Please write a review before posting.", "Empty Review", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var optionsBuilder = new DbContextOptionsBuilder<JnrContext>();
            string connectionString = "Server=localhost;Port=3306;Database=jnr;Uid=root;Pwd=root;";
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

            using (var dbContext = new JnrContext(optionsBuilder.Options))
            {
                try
                {
                    var userAlbumRating = await dbContext.Useralbumratings
                        .FirstOrDefaultAsync(uar => uar.UserId == SessionManager.CurrentUserId.Value && uar.AlbumId == _currentDbAlbumId.Value);

                    if (userAlbumRating != null)
                    {
                        userAlbumRating.ReviewText = reviewText;
                        userAlbumRating.RatedAt = DateTime.UtcNow; // Update timestamp for review activity
                    }
                    else
                    {
                        // If no record exists, create one. Implicitly, this album is now "in collection".
                        userAlbumRating = new Useralbumrating
                        {
                            UserId = SessionManager.CurrentUserId.Value,
                            AlbumId = _currentDbAlbumId.Value,
                            ReviewText = reviewText,
                            Rating = -1, // Default to "in collection, not numerically rated"
                            RatedAt = DateTime.UtcNow
                        };
                        dbContext.Useralbumratings.Add(userAlbumRating);
                    }
                    await dbContext.SaveChangesAsync();
                    MessageBox.Show("Your review has been posted!", "Review Posted", MessageBoxButton.OK, MessageBoxImage.Information);
                    txtUserReviewText.Clear(); // Clear the textbox
                    await RefreshRatingAndReviewDisplayAsync(); // Refresh reviews and potentially average rating
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error posting review: {ex.ToString()}");
                    MessageBox.Show($"Could not post your review: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        private void UserRating_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "[0-9]");
        }
        private void UserRating_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                RateAlbum_Click(btnRateAlbum, new RoutedEventArgs());
                FocusManager.SetFocusedElement(this, btnRateAlbum);
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void btnGoBackOverview_Click(object sender, RoutedEventArgs e)
        {
            var mainPage = Application.Current.Windows.OfType<JNR.Views.MainPage.MainPage>().FirstOrDefault();
            if (mainPage == null) { mainPage = new JNR.Views.MainPage.MainPage(); mainPage.Show(); }
            else { mainPage.Activate(); }
            this.Close();
        }

        private void PreviousAlbum_Click(object sender, MouseButtonEventArgs e)
        {
            if (PreviousAlbum != null)
            {
                var overview = new Overview(PreviousAlbum.DisplayAlbumName, PreviousAlbum.DisplayArtistName, null, PreviousAlbum.Thumb);
                overview.Owner = Application.Current.MainWindow; overview.Show(); this.Close();
            }
        }

        private void NextAlbum_Click(object sender, MouseButtonEventArgs e)
        {
            if (NextAlbum != null)
            {
                var overview = new Overview(NextAlbum.DisplayAlbumName, NextAlbum.DisplayArtistName, null, NextAlbum.Thumb);
                overview.Owner = Application.Current.MainWindow; overview.Show(); this.Close();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        private void SidebarNavigation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.CommandParameter is string viewName)
            {
                switch (viewName)
                {
                    case "MyAlbums":
                        App.NavigateTo<JNR.Views.My_Albums.MyAlbums>(this);
                        break;
                    case "Genres":
                        App.NavigateTo<JNR.Views.Genres.Genres>(this);
                        break;
                    case "Charts":
                        App.NavigateTo<JNR.Views.Charts>(this);
                        break;
                    case "About":
                        App.NavigateTo<JNR.Views.About>(this);
                        break;
                    case "Settings":
                        App.NavigateTo<JNR.Views.Settings.Settings>(this);
                        break;
                    case "Links":
                        MessageBox.Show($"{viewName} page not yet implemented.", "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
                        // Because Overview doesn't have a "self" button to re-check, we just un-check the clicked one.
                        rb.IsChecked = false;
                        break;
                }
            }
        }
    }
}