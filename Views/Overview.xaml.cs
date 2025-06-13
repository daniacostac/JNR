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
using System.Windows.Controls;
using System.Windows.Input;
using JNR.Models.DiscogModels;
using JNR.Models.LastFmModels;
using JNR.Models;
using JNR.Helpers;
using Microsoft.EntityFrameworkCore;

namespace JNR.Views
{
    public class UserReviewDisplayItem : INotifyPropertyChanged
    {
        public int UserId { get; set; }
        private string _username;
        public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }
        private string _profilePictureUrl;
        public string ProfilePictureUrl { get => _profilePictureUrl; set { _profilePictureUrl = value; OnPropertyChanged(); } }
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
        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Bindable Properties
        private string _detailedAlbumName;
        public string DetailedAlbumName
        {
            get => _detailedAlbumName;
            private set { if (_detailedAlbumName != value) { _detailedAlbumName = value; OnPropertyChanged(); OnPropertyChanged(nameof(AlbumTitleAndArtist)); } }
        }
        private string _detailedArtistName;
        public string DetailedArtistName
        {
            get => _detailedArtistName;
            private set { if (_detailedArtistName != value) { _detailedArtistName = value; OnPropertyChanged(); OnPropertyChanged(nameof(AlbumTitleAndArtist)); } }
        }
        public string AlbumTitleAndArtist => $"{DetailedAlbumName} - {DetailedArtistName}";
        private string _detailedCoverArtUrl;
        public string DetailedCoverArtUrl
        {
            get => _detailedCoverArtUrl;
            private set { if (_detailedCoverArtUrl != value) { _detailedCoverArtUrl = value; OnPropertyChanged(); } }
        }
        private string _releaseInfo;
        public string ReleaseInfo
        {
            get => _releaseInfo;
            private set { if (_releaseInfo != value) { _releaseInfo = value; OnPropertyChanged(); } }
        }
        private string _genreInfo;
        public string GenreInfo
        {
            get => _genreInfo;
            private set { if (_genreInfo != value) { _genreInfo = value; OnPropertyChanged(); } }
        }
        private string _descriptionText;
        public string DescriptionText
        {
            get => _descriptionText;
            private set { if (_descriptionText != value) { _descriptionText = value; OnPropertyChanged(); } }
        }
        private string _listenersCount;
        public string ListenersCount
        {
            get => _listenersCount;
            private set { if (_listenersCount != value) { _listenersCount = value; OnPropertyChanged(); } }
        }
        private string _playCount;
        public string PlayCount
        {
            get => _playCount;
            private set { if (_playCount != value) { _playCount = value; OnPropertyChanged(); } }
        }
        public string LanguageInfo { get; private set; } = "English (Default)";
        private string _averageRatingDisplay = "Not Rated Yet";
        public string AverageRatingDisplay
        {
            get => _averageRatingDisplay;
            set { _averageRatingDisplay = value; OnPropertyChanged(); }
        }
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
            set { _isUserLoggedIn = value; OnPropertyChanged(); UpdateCanPostOrRate(); }
        }
        private bool _canPostOrRate;
        public bool CanPostOrRate
        {
            get => _canPostOrRate;
            set { if (_canPostOrRate != value) { _canPostOrRate = value; OnPropertyChanged(); } }
        }
        public ObservableCollection<TrackItem> AlbumTracks { get; set; }
        public ObservableCollection<UserReviewDisplayItem> AlbumUserReviews { get; set; }
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
        #endregion

        #region Private Fields
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
        private int? _currentDbAlbumId;
        #endregion

        public Overview(string albumName, string artistName, string mbid, string coverArtUrl)
        {
            InitializeComponent();
            this.DataContext = this;
            this.Closed += (s, args) => App.WindowClosed(this);

            _albumNameParam = albumName;
            _artistNameParam = artistName;
            _mbidParam = mbid;
            _initialCoverArtUrlParam = coverArtUrl;

            // Initialize UI with placeholder data
            DetailedAlbumName = _albumNameParam ?? "Album";
            DetailedArtistName = _artistNameParam ?? "Artist";
            DetailedCoverArtUrl = _initialCoverArtUrlParam ?? "/Images/placeholder_album.png";
            ReleaseInfo = "Loading...";
            GenreInfo = "Loading...";
            DescriptionText = "Loading album description...";
            ListenersCount = "Loading...";
            PlayCount = "Loading...";
            AverageRatingValue = 0;
            IsUserLoggedIn = SessionManager.CurrentUserId.HasValue;

            AlbumTracks = new ObservableCollection<TrackItem> { new TrackItem { Title = "Loading tracks..." } };
            AlbumUserReviews = new ObservableCollection<UserReviewDisplayItem>();

            // Setup HttpClient once
            if (client.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("JamNRateApp/1.0 (danielacostac03@gmail.com)");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Discogs", $"token={DiscogsApiToken}");
            }

            this.Loaded += Overview_Loaded;
        }

        private async void Overview_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadAllAlbumDetailsAsync();
                await InitializeAlbumDataForRatingAndDisplayAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FATAL OVERVIEW LOAD ERROR: {ex.ToString()}");
                DescriptionText = "A critical error occurred while loading album details. This might be due to a network issue or an API problem. Please try again later.";
                ReleaseInfo = "Error";
                GenreInfo = "Error";
                ListenersCount = "Error";
                PlayCount = "Error";
                AlbumTracks.Clear();
                AlbumTracks.Add(new TrackItem { Title = "Could not load tracklist due to an error." });
                MessageBox.Show($"An unexpected error occurred: {ex.Message}\nThe page will remain open, but data may be incomplete.", "Loading Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadAllAlbumDetailsAsync()
        {
            bool discogsSuccess = await LoadDiscogsDataAsync();
            await LoadLastFmDataAsync(discogsSuccess);
        }

        private async Task<bool> LoadDiscogsDataAsync()
        {
            if (string.IsNullOrWhiteSpace(_artistNameParam) || string.IsNullOrWhiteSpace(_albumNameParam)) return false;

            try
            {
                string searchUrl = $"{DiscogsApiBaseUrl}/database/search?artist={Uri.EscapeDataString(_artistNameParam)}&release_title={Uri.EscapeDataString(_albumNameParam)}&type=master,release&per_page=5&page=1";
                HttpResponseMessage searchResponseMsg = await client.GetAsync(searchUrl);
                if (!searchResponseMsg.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Discogs search API Error: {searchResponseMsg.StatusCode} - {await searchResponseMsg.Content.ReadAsStringAsync()}");
                    DescriptionText = $"Discogs search failed: {searchResponseMsg.ReasonPhrase}. This can happen if the API is busy. Please try again in a minute.";
                    return false;
                }

                var discogsSearchResponse = JsonSerializer.Deserialize<DiscogsSearchResponse>(await searchResponseMsg.Content.ReadAsStringAsync(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                var bestMatch = discogsSearchResponse?.Results?
                    .OrderByDescending(r => r.MasterId.HasValue && r.MasterId > 0)
                    .ThenByDescending(r => r.Community?.Have ?? 0)
                    .FirstOrDefault();

                if (bestMatch == null) return false;

                await Task.Delay(1200);

                string detailsUrl = bestMatch.MasterId.HasValue && bestMatch.MasterId > 0
                    ? $"{DiscogsApiBaseUrl}/masters/{bestMatch.MasterId}"
                    : $"{DiscogsApiBaseUrl}/releases/{bestMatch.Id}";

                HttpResponseMessage detailsResponseMsg = await client.GetAsync(detailsUrl);
                if (!detailsResponseMsg.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Discogs details API Error: {detailsResponseMsg.StatusCode} - {await detailsResponseMsg.Content.ReadAsStringAsync()}");
                    return false;
                }

                var discogsReleaseData = JsonSerializer.Deserialize<DiscogsRelease>(await detailsResponseMsg.Content.ReadAsStringAsync(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (discogsReleaseData == null) return false;

                DetailedAlbumName = discogsReleaseData.Title ?? _albumNameParam;
                DetailedArtistName = discogsReleaseData.PrimaryArtistName ?? _artistNameParam;
                if (!string.IsNullOrWhiteSpace(discogsReleaseData.PrimaryImageUrl)) DetailedCoverArtUrl = discogsReleaseData.PrimaryImageUrl;
                ReleaseInfo = !string.IsNullOrWhiteSpace(discogsReleaseData.ReleasedFormatted) ? discogsReleaseData.ReleasedFormatted : (discogsReleaseData.Year > 0 ? discogsReleaseData.Year.ToString() : "N/A");
                var genresAndStyles = (discogsReleaseData.Genres ?? new List<string>()).Concat(discogsReleaseData.Styles ?? new List<string>());
                GenreInfo = genresAndStyles.Any() ? string.Join(", ", genresAndStyles.Distinct()) : "N/A";

                if (discogsReleaseData.Tracklist?.Any(t => t.Type?.Equals("track", StringComparison.OrdinalIgnoreCase) == true) == true)
                {
                    AlbumTracks.Clear();
                    foreach (var track in discogsReleaseData.Tracklist.Where(t => t.Type?.Equals("track", StringComparison.OrdinalIgnoreCase) == true))
                        AlbumTracks.Add(new TrackItem { Number = track.Position, Title = track.Title, Duration = track.Duration });
                }

                if (!string.IsNullOrWhiteSpace(discogsReleaseData.Notes))
                {
                    string cleanedNotes = Regex.Replace(discogsReleaseData.Notes, @"\[([a-z])=(.+?)\]", "$2", RegexOptions.IgnoreCase);
                    DescriptionText = Regex.Replace(cleanedNotes, @"\[/?([a-z]+)\]", "", RegexOptions.IgnoreCase).Trim();
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
                }

                if (_currentArtistId.HasValue)
                {
                    await Task.Delay(1200);
                    await LoadArtistDiscographyAsync(_currentArtistId.Value, bestMatch.Year);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Discogs loading exception: {ex}");
                DescriptionText = "An error occurred while contacting the Discogs service.";
                return false;
            }
        }

        private async Task LoadArtistDiscographyAsync(int artistId, string bestMatchYear)
        {
            try
            {
                string artistReleasesUrl = $"{DiscogsApiBaseUrl}/artists/{artistId}/releases?sort=year&sort_order=asc&per_page=100";
                HttpResponseMessage responseMsg = await client.GetAsync(artistReleasesUrl);
                if (!responseMsg.IsSuccessStatusCode) return;

                var artistReleasesResponse = JsonSerializer.Deserialize<DiscogsArtistReleasesResponse>(await responseMsg.Content.ReadAsStringAsync(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (artistReleasesResponse?.Releases == null || !artistReleasesResponse.Releases.Any()) return;

                var artistMainAlbums = artistReleasesResponse.Releases
                    .Where(r => (r.Type?.Equals("master", StringComparison.OrdinalIgnoreCase) == true || r.Type?.Equals("release", StringComparison.OrdinalIgnoreCase) == true) &&
                                 r.Role?.Equals("Main", StringComparison.OrdinalIgnoreCase) == true &&
                                 !string.IsNullOrWhiteSpace(r.Title) && r.ParsedYear > 0)
                    .OrderBy(r => r.ParsedYear).ThenBy(r => r.Title).ToList();

                if (!artistMainAlbums.Any())
                {
                    artistMainAlbums = artistReleasesResponse.Releases
                       .Where(r => (r.Type?.Equals("master", StringComparison.OrdinalIgnoreCase) == true || r.Type?.Equals("release", StringComparison.OrdinalIgnoreCase) == true) &&
                                    !string.IsNullOrWhiteSpace(r.Title) && r.ParsedYear > 0)
                       .OrderBy(r => r.ParsedYear).ThenBy(r => r.Title).ToList();
                }

                int currentIndex = -1;

                if (currentIndex == -1 && _currentDiscogsReleaseId.HasValue)
                {
                    currentIndex = artistMainAlbums.FindIndex(a => a.Id == _currentDiscogsReleaseId.Value && a.Type?.Equals("release", StringComparison.OrdinalIgnoreCase) == true);
                }

                if (currentIndex == -1 && _currentDiscogsMasterId.HasValue)
                {
                    currentIndex = artistMainAlbums.FindIndex(a => a.MasterId == _currentDiscogsMasterId.Value || (a.Type == "master" && a.Id == _currentDiscogsMasterId.Value));
                }

                if (currentIndex == -1)
                {
                    if (int.TryParse(bestMatchYear, out int year))
                    {
                        currentIndex = artistMainAlbums.FindIndex(a => a.Title.Equals(DetailedAlbumName, StringComparison.OrdinalIgnoreCase) && a.ParsedYear == year);
                    }
                }

                if (currentIndex != -1)
                {
                    PreviousAlbum = (currentIndex > 0) ? artistMainAlbums[currentIndex - 1] : null;
                    NextAlbum = (currentIndex < artistMainAlbums.Count - 1) ? artistMainAlbums[currentIndex + 1] : null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Artist Discography loading exception: {ex}");
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
                if (!discogsDataLoadedSuccessfully && string.IsNullOrWhiteSpace(DescriptionText) || DescriptionText == "Loading album description...")
                    DescriptionText = "Not enough information to load album details from any source.";
                return;
            }

            try
            {
                HttpResponseMessage httpResponse = await client.GetAsync(apiUrl);
                if (!httpResponse.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Last.fm API Error Status: {httpResponse.StatusCode}");
                    return;
                }

                var albumInfoResponse = JsonSerializer.Deserialize<LastFmAlbumInfoResponse>(await httpResponse.Content.ReadAsStringAsync(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (albumInfoResponse?.Album == null || albumInfoResponse.error != null)
                {
                    Debug.WriteLine($"Last.fm API Error: {albumInfoResponse?.message ?? "Album not found."}");
                    return;
                }

                LastFmDetailedAlbum detailedAlbum = albumInfoResponse.Album;

                if (string.IsNullOrWhiteSpace(DetailedCoverArtUrl) || DetailedCoverArtUrl.EndsWith("placeholder_album.png"))
                {
                    DetailedCoverArtUrl = GetLastFmImageUrl(detailedAlbum.Image) ?? DetailedCoverArtUrl;
                }
                if (ReleaseInfo == "Loading..." || ReleaseInfo == "N/A")
                {
                    ReleaseInfo = !string.IsNullOrWhiteSpace(detailedAlbum.Wiki?.Published) ? detailedAlbum.Wiki.Published.Split(',').FirstOrDefault()?.Trim() ?? "N/A" : "N/A";
                }
                if (GenreInfo == "Loading..." || GenreInfo == "N/A")
                {
                    GenreInfo = detailedAlbum.Tags?.Tag?.Any() == true ? string.Join(", ", detailedAlbum.Tags.Tag.Select(t => t.Name).Take(3)) : "N/A";
                }
                if (DescriptionText == "Loading album description..." || string.IsNullOrWhiteSpace(DescriptionText))
                {
                    string summary = detailedAlbum.Wiki?.Content ?? detailedAlbum.Wiki?.Summary ?? "";
                    string cleanedSummary = Regex.Replace(summary, "<a href=.*?>Read more on Last.fm</a>\\.?$", "", RegexOptions.IgnoreCase | RegexOptions.Singleline).Trim();
                    DescriptionText = System.Net.WebUtility.HtmlDecode(Regex.Replace(cleanedSummary, "<.*?>", String.Empty).Trim());
                }

                if (long.TryParse(detailedAlbum.Listeners, out long listenersVal)) ListenersCount = $"{listenersVal:N0} listeners"; else ListenersCount = "N/A";
                if (long.TryParse(detailedAlbum.Playcount, out long playcountVal)) PlayCount = $"{playcountVal:N0} plays"; else PlayCount = "N/A";

                if (!AlbumTracks.Any() || AlbumTracks.First().Title == "Loading tracks...")
                {
                    AlbumTracks.Clear();
                    if (detailedAlbum.Tracks?.Track?.Any() == true)
                    {
                        foreach (var track in detailedAlbum.Tracks.Track.OrderBy(t => t.Attr?.Rank ?? int.MaxValue))
                            AlbumTracks.Add(new TrackItem { Number = track.Attr?.Rank.ToString() + ".", Title = track.Name, Duration = FormatTrackDuration(track.Duration) });
                    }
                    else { AlbumTracks.Add(new TrackItem { Title = "No track info on Last.fm." }); }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Last.fm loading exception: {ex}");
            }
        }

        private string GetLastFmImageUrl(List<LastFmImage> images, string preferredSize = "extralarge")
        {
            if (images == null || !images.Any()) return null;
            return images.FirstOrDefault(i => i.Size == preferredSize && !string.IsNullOrWhiteSpace(i.Text))?.Text
                ?? images.FirstOrDefault(i => i.Size == "large" && !string.IsNullOrWhiteSpace(i.Text))?.Text
                ?? images.LastOrDefault(i => !string.IsNullOrWhiteSpace(i.Text))?.Text;
        }

        private string FormatTrackDuration(int? totalSeconds)
        {
            if (!totalSeconds.HasValue || totalSeconds.Value <= 0) return "";
            return TimeSpan.FromSeconds(totalSeconds.Value).ToString(@"m\:ss");
        }

        private void UpdateCanPostOrRate() => CanPostOrRate = IsUserLoggedIn && _currentDbAlbumId.HasValue;

        private async Task InitializeAlbumDataForRatingAndDisplayAsync()
        {
            string externalId = !string.IsNullOrWhiteSpace(_mbidParam) ? _mbidParam : (_currentDiscogsMasterId ?? _currentDiscogsReleaseId)?.ToString();
            string idSource = !string.IsNullOrWhiteSpace(_mbidParam) ? "mbid" : (_currentDiscogsMasterId.HasValue ? "discogs_master" : "discogs_release");
            if (string.IsNullOrWhiteSpace(externalId) || DetailedAlbumName == "Album")
            {
                _currentDbAlbumId = null; UpdateCanPostOrRate(); return;
            }

            int.TryParse(Regex.Match(ReleaseInfo, @"\d{4}").Value, out int releaseYear);

            var optionsBuilder = new DbContextOptionsBuilder<JnrContext>();
            string connectionString = "Server=localhost;Port=3306;Database=jnr;Uid=root;Pwd=root;";
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

            using (var dbContext = new JnrContext(optionsBuilder.Options))
            {
                Album albumInDb = await dbContext.Albums.FirstOrDefaultAsync(a => a.ExternalAlbumId == externalId && a.IdSource == idSource);
                if (albumInDb == null)
                {
                    albumInDb = new Album
                    {
                        ExternalAlbumId = externalId,
                        IdSource = idSource,
                        Title = this.DetailedAlbumName,
                        Artist = this.DetailedArtistName,
                        CoverArtUrl = this.DetailedCoverArtUrl.EndsWith("placeholder_album.png") ? null : this.DetailedCoverArtUrl,
                        ReleaseYear = releaseYear > 0 ? releaseYear : (int?)null,
                        FirstAddedAt = DateTime.UtcNow
                    };
                    dbContext.Albums.Add(albumInDb);
                    await dbContext.SaveChangesAsync();
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
                AverageRatingDisplay = "N/A"; AverageRatingValue = 0; AlbumUserReviews.Clear(); return;
            }

            var optionsBuilder = new DbContextOptionsBuilder<JnrContext>();
            string connectionString = "Server=localhost;Port=3306;Database=jnr;Uid=root;Pwd=root;";
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

            using (var dbContext = new JnrContext(optionsBuilder.Options))
            {
                var validRatings = await dbContext.Useralbumratings
                    .Where(r => r.AlbumId == _currentDbAlbumId.Value && r.Rating >= 0 && r.Rating <= 10)
                    .Select(r => (int)r.Rating).ToListAsync();

                if (validRatings.Any())
                {
                    double avg = validRatings.Average();
                    AverageRatingDisplay = $"Avg: {avg:F1}/10 ({validRatings.Count} votes)";
                    AverageRatingValue = avg;
                }
                else { AverageRatingDisplay = "Not Rated Yet"; AverageRatingValue = 0; }

                if (SessionManager.CurrentUserId.HasValue)
                {
                    var currentUserRatingEntry = await dbContext.Useralbumratings
                        .FirstOrDefaultAsync(r => r.AlbumId == _currentDbAlbumId.Value && r.UserId == SessionManager.CurrentUserId.Value);
                    txtUserRating.Text = (currentUserRatingEntry?.Rating >= 0) ? currentUserRatingEntry.Rating.ToString() : "";
                }
            }
            await LoadUserReviewsAsync();
        }

        private async Task LoadUserReviewsAsync()
        {
            AlbumUserReviews.Clear();
            if (!_currentDbAlbumId.HasValue) return;

            var optionsBuilder = new DbContextOptionsBuilder<JnrContext>();
            string connectionString = "Server=localhost;Port=3306;Database=jnr;Uid=root;Pwd=root;";
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

            using (var dbContext = new JnrContext(optionsBuilder.Options))
            {
                var reviewsFromDb = await dbContext.Useralbumratings.Include(uar => uar.User)
                    .Where(uar => uar.AlbumId == _currentDbAlbumId.Value && (!string.IsNullOrEmpty(uar.ReviewText) || (uar.Rating >= 0 && uar.Rating <= 10)))
                    .OrderByDescending(uar => uar.RatedAt).ToListAsync();

                foreach (var review in reviewsFromDb)
                {
                    AlbumUserReviews.Add(new UserReviewDisplayItem
                    {
                        UserId = review.UserId,
                        Username = review.User?.Username ?? "Unknown User",
                        ProfilePictureUrl = review.User?.ProfilePicturePath,
                        ReviewText = review.ReviewText,
                        HasReviewText = !string.IsNullOrWhiteSpace(review.ReviewText),
                        RatedAtDisplay = $"Posted: {review.RatedAt:yyyy-MM-dd HH:mm}",
                        HasNumericRating = review.Rating >= 0,
                        RatingDisplay = (review.Rating >= 0) ? $"Rated: {review.Rating}/10" : ""
                    });
                }
            }
        }

        private async void HandleRatingOrReviewAction(Func<Useralbumrating, bool> updateAction)
        {
            if (!CanPostOrRate) return;
            var optionsBuilder = new DbContextOptionsBuilder<JnrContext>();
            string connectionString = "Server=localhost;Port=3306;Database=jnr;Uid=root;Pwd=root;";
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

            using (var dbContext = new JnrContext(optionsBuilder.Options))
            {
                var rating = await dbContext.Useralbumratings.FirstOrDefaultAsync(uar => uar.UserId == SessionManager.CurrentUserId.Value && uar.AlbumId == _currentDbAlbumId.Value);
                if (rating == null)
                {
                    rating = new Useralbumrating { UserId = SessionManager.CurrentUserId.Value, AlbumId = _currentDbAlbumId.Value, Rating = -1 };
                    dbContext.Useralbumratings.Add(rating);
                }
                rating.RatedAt = DateTime.UtcNow;

                if (updateAction(rating))
                {
                    await dbContext.SaveChangesAsync();
                    await RefreshRatingAndReviewDisplayAsync();
                }
            }
        }

        private void RateAlbum_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(txtUserRating.Text, out int ratingValue) || ratingValue < 0 || ratingValue > 10)
            {
                MessageBox.Show("Please enter a valid rating between 0 and 10.", "Invalid Rating", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            HandleRatingOrReviewAction(rating => { rating.Rating = (sbyte)ratingValue; return true; });
            MessageBox.Show("Your rating has been saved!", "Rating Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PostReview_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUserReviewText.Text))
            {
                MessageBox.Show("Please write a review before posting.", "Empty Review", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            HandleRatingOrReviewAction(rating => { rating.ReviewText = txtUserReviewText.Text.Trim(); return true; });
            MessageBox.Show("Your review has been posted!", "Review Posted", MessageBoxButton.OK, MessageBoxImage.Information);
            txtUserReviewText.Clear();
        }

        #region UI Event Handlers (Navigation, Window Controls, etc.)
        private void UserRating_PreviewTextInput(object sender, TextCompositionEventArgs e) => e.Handled = !Regex.IsMatch(e.Text, "[0-9]");
        private void UserRating_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) RateAlbum_Click(sender, e); }
        private void Username_Click(object sender, MouseButtonEventArgs e) { if (sender is FrameworkElement fe && fe.DataContext is UserReviewDisplayItem review) App.NavigateToProfile(this, review.UserId); }
        private void AddToFavoritesButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => HandleRatingOrReviewAction(rating => { return true; /* Simply ensures record exists */ });
        private void PreviousAlbum_Click(object sender, MouseButtonEventArgs e) { if (PreviousAlbum != null) App.NavigateToOverview(this, PreviousAlbum.DisplayAlbumName, PreviousAlbum.DisplayArtistName, null, PreviousAlbum.Thumb); }
        private void NextAlbum_Click(object sender, MouseButtonEventArgs e) { if (NextAlbum != null) App.NavigateToOverview(this, NextAlbum.DisplayAlbumName, NextAlbum.DisplayArtistName, null, NextAlbum.Thumb); }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void btnGoBackOverview_Click(object sender, RoutedEventArgs e) => App.NavigateToMainPage(this);

        // === MODIFICATION: The sidebar navigation method is now DELETED. ===
        // private void SidebarNavigation_Click(...) { ... }
        #endregion
    }
}