// Archivo: Views\Overview.xaml.cs
//====================
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel; // Required for INotifyPropertyChanged
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices; // Required for CallerMemberName
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace JNR.Views
{
    // --- Local Model Class for Track Items ---
    // Consider using JNR.Models.TrackItem if it's identical
    public class TrackItem // This one is for display and already has string Duration
    {
        public string Number { get; set; }
        public string Title { get; set; }
        public string Duration { get; set; }
    }

    // --- Last.fm Model Classes (Ideally move to a shared Models namespace) ---
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
        public int? duration { get; set; } // MODIFIED: Changed from int to int?
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
    // --- End of Last.fm Model Classes ---


    public partial class Overview : Window, INotifyPropertyChanged // Implement INotifyPropertyChanged
    {
        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Backing fields for properties
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
        public string LanguageInfo // If this can change, apply INPC pattern too
        {
            get => _languageInfo;
            private set { if (_languageInfo != value) { _languageInfo = value; OnPropertyChanged(); } }
        }
        public string RatingDisplay // If this can change, apply INPC pattern too
        {
            get => _ratingDisplay;
            private set { if (_ratingDisplay != value) { _ratingDisplay = value; OnPropertyChanged(); } }
        }

        private readonly string _albumNameParam;
        private readonly string _artistNameParam;
        private readonly string _mbidParam;
        private readonly string _initialCoverArtUrlParam;

        private static readonly HttpClient client = new HttpClient();
        private const string LastFmApiKey = "d8831cc3c1d4eb53011a7b268a95d028";

        public Overview(string albumName, string artistName, string mbid, string coverArtUrl)
        {
            InitializeComponent();
            this.DataContext = this;

            _albumNameParam = albumName;
            _artistNameParam = artistName;
            _mbidParam = mbid;
            _initialCoverArtUrlParam = coverArtUrl;

            // Initialize properties to default/loading states (setters will trigger OnPropertyChanged)
            DetailedAlbumName = _albumNameParam ?? "Album";
            DetailedArtistName = _artistNameParam ?? "Artist";
            DetailedCoverArtUrl = _initialCoverArtUrlParam ?? "/Images/placeholder_album.png";
            ReleaseInfo = "Loading...";
            GenreInfo = "Loading...";
            DescriptionText = "Loading album description...";
            ListenersCount = "Loading...";
            PlayCount = "Loading...";

            AlbumTracks = new ObservableCollection<TrackItem>(); // ObservableCollection handles its own notifications
            AlbumTracks.Add(new TrackItem { Number = " ", Title = "Loading tracks...", Duration = "" });

            if (client.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("JNR_WPF_App/1.0_Overview");
            }

            this.Loaded += Overview_Loaded;
        }

        private async void Overview_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadAlbumDetails();
            // No need for InvalidateVisual() anymore if INotifyPropertyChanged is correctly implemented
            // for all bound properties that change.
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

        private string FormatTrackDuration(int? totalSeconds) // MODIFIED: Changed parameter type to int?
        {
            if (!totalSeconds.HasValue || totalSeconds.Value <= 0) // MODIFIED: Handle null and non-positive
            {
                return ""; // Or "N/A" or some other placeholder if you prefer
            }
            TimeSpan time = TimeSpan.FromSeconds(totalSeconds.Value); // MODIFIED: Use .Value
            return $"{(int)time.TotalMinutes}:{time.Seconds:D2}";
        }

        private async Task LoadAlbumDetails()
        {
            string apiUrl = null;
            if (!string.IsNullOrWhiteSpace(_mbidParam))
            {
                apiUrl = $"http://ws.audioscrobbler.com/2.0/?method=album.getInfo&api_key={LastFmApiKey}&mbid={_mbidParam}&format=json";
            }
            else if (!string.IsNullOrWhiteSpace(_albumNameParam) && !string.IsNullOrWhiteSpace(_artistNameParam))
            {
                apiUrl = $"http://ws.audioscrobbler.com/2.0/?method=album.getInfo&api_key={LastFmApiKey}&artist={Uri.EscapeDataString(_artistNameParam)}&album={Uri.EscapeDataString(_albumNameParam)}&format=json";
            }
            else
            {
                DescriptionText = "Not enough information to load album details.";
                AlbumTracks.Clear();
                AlbumTracks.Add(new TrackItem { Title = "Error: Insufficient information." });
                ReleaseInfo = "N/A"; GenreInfo = "N/A"; ListenersCount = "N/A"; PlayCount = "N/A";
                return;
            }

            Debug.WriteLine($"Requesting Album Info URL (Last.fm): {apiUrl}");

            try
            {
                HttpResponseMessage httpResponse = await client.GetAsync(apiUrl);
                string jsonResponse = await httpResponse.Content.ReadAsStringAsync();

                Debug.WriteLine("--- Raw Last.fm album.getInfo JSON Response (first 2000 chars) ---");
                Debug.WriteLine(jsonResponse.Substring(0, Math.Min(jsonResponse.Length, 2000)));
                Debug.WriteLine("--- End of Raw JSON ---");

                if (!httpResponse.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"API Error Status: {httpResponse.StatusCode}, Response (first 500 chars): {jsonResponse.Substring(0, Math.Min(jsonResponse.Length, 500))}");
                    DescriptionText = $"Error loading details: {httpResponse.ReasonPhrase}.";
                    AlbumTracks.Clear(); AlbumTracks.Add(new TrackItem { Title = $"API Error: {httpResponse.StatusCode}" });
                    ReleaseInfo = "Error"; GenreInfo = "Error"; ListenersCount = "Error"; PlayCount = "Error";
                    return;
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                LastFmAlbumInfoResponse albumInfoResponse = JsonSerializer.Deserialize<LastFmAlbumInfoResponse>(jsonResponse, options);

                if (albumInfoResponse?.error != null)
                {
                    Debug.WriteLine($"Last.fm API Error {albumInfoResponse.error}: {albumInfoResponse.message}");
                    DescriptionText = $"Last.fm API Error: {albumInfoResponse.message}";
                    AlbumTracks.Clear(); AlbumTracks.Add(new TrackItem { Title = "Last.fm API Error." });
                    ReleaseInfo = "API Error"; GenreInfo = "API Error"; ListenersCount = "API Error"; PlayCount = "API Error";
                    return;
                }

                if (albumInfoResponse?.album != null)
                {
                    LastFmDetailedAlbum detailedAlbum = albumInfoResponse.album;

                    // Setters will now trigger OnPropertyChanged
                    DetailedAlbumName = detailedAlbum.name ?? _albumNameParam;
                    DetailedArtistName = detailedAlbum.artist ?? _artistNameParam;
                    DetailedCoverArtUrl = GetLastFmImageUrl(detailedAlbum.image, "extralarge") ?? _initialCoverArtUrlParam;

                    if (detailedAlbum.wiki != null && !string.IsNullOrWhiteSpace(detailedAlbum.wiki.published))
                    {
                        var dateParts = detailedAlbum.wiki.published.Split(',');
                        ReleaseInfo = dateParts[0].Trim();
                    }
                    else { ReleaseInfo = "N/A"; }

                    if (detailedAlbum.tags?.tag != null && detailedAlbum.tags.tag.Any())
                    {
                        GenreInfo = string.Join(", ", detailedAlbum.tags.tag.Select(t => t.name).Take(3));
                    }
                    else { GenreInfo = "N/A"; }

                    if (long.TryParse(detailedAlbum.listeners, out long listenersVal))
                    { ListenersCount = $"{listenersVal:N0} listeners"; }
                    else { ListenersCount = "N/A"; }

                    if (long.TryParse(detailedAlbum.playcount, out long playcountVal))
                    { PlayCount = $"{playcountVal:N0} plays"; }
                    else { PlayCount = "N/A"; }

                    string summary = detailedAlbum.wiki?.summary;
                    string content = detailedAlbum.wiki?.content;
                    string tempDescription = "No description available.";
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        tempDescription = Regex.Replace(content, "<a href=.*?>Read more on Last.fm</a>\\.?$", "", RegexOptions.IgnoreCase | RegexOptions.Singleline).Trim();
                        tempDescription = Regex.Replace(tempDescription, "<.*?>", String.Empty, RegexOptions.Singleline).Trim();
                    }
                    else if (!string.IsNullOrWhiteSpace(summary))
                    {
                        tempDescription = Regex.Replace(summary, "<a href=.*?>Read more on Last.fm</a>\\.?$", "", RegexOptions.IgnoreCase | RegexOptions.Singleline).Trim();
                        tempDescription = Regex.Replace(tempDescription, "<.*?>", String.Empty, RegexOptions.Singleline).Trim();
                    }
                    DescriptionText = string.IsNullOrWhiteSpace(tempDescription) ? "No description available." : tempDescription;


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
                                Duration = FormatTrackDuration(track.duration) // track.duration is now int?
                            });
                            trackNumber++;
                        }
                    }
                    else { AlbumTracks.Add(new TrackItem { Title = "No track information available." }); }
                }
                else
                {
                    DescriptionText = "Album details not found in API response.";
                    AlbumTracks.Clear(); AlbumTracks.Add(new TrackItem { Title = "Details not found." });
                    ReleaseInfo = "N/A"; GenreInfo = "N/A"; ListenersCount = "N/A"; PlayCount = "N/A";
                }
            }
            catch (HttpRequestException httpEx)
            {
                Debug.WriteLine($"HTTP Request Error: {httpEx.ToString()}");
                DescriptionText = $"Network error. ({httpEx.Message})";
                AlbumTracks.Clear(); AlbumTracks.Add(new TrackItem { Title = "Network Error." });
                ReleaseInfo = "Network Error"; GenreInfo = "Network Error"; ListenersCount = "Network Error"; PlayCount = "Network Error";
            }
            catch (JsonException jsonEx)
            {
                Debug.WriteLine($"JSON Parsing Error: {jsonEx.ToString()}");
                DescriptionText = "Error parsing data from API.";
                AlbumTracks.Clear(); AlbumTracks.Add(new TrackItem { Title = "Data Error." });
                ReleaseInfo = "Data Error"; GenreInfo = "Data Error"; ListenersCount = "Data Error"; PlayCount = "Data Error";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Generic Error: {ex.ToString()}");
                DescriptionText = $"An unexpected error occurred: {ex.Message}";
                AlbumTracks.Clear(); AlbumTracks.Add(new TrackItem { Title = "Unexpected Error." });
                ReleaseInfo = "Unexpected Error"; GenreInfo = "Unexpected Error"; ListenersCount = "Unexpected Error"; PlayCount = "Unexpected Error";
            }
            // Removed finally block with InvalidateVisual() as INPC handles updates.
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
    }
}
//====================