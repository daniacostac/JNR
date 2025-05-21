// Archivo: ViewModels/MainPage/MainPage.xaml.cs
//====================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
// Using System.Windows.Data; // Not explicitly used in this snippet, but often needed
// Using System.Windows.Documents; // Not explicitly used in this snippet
// Using System.Windows.Input; // Not explicitly used in this snippet
// Using System.Windows.Media; // Not explicitly used in this snippet
// Using System.Windows.Media.Imaging; // Not explicitly used in this snippet
// Using System.Windows.Shapes; // Not explicitly used in this snippet

// Added usings for API call and JSON parsing
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization; // Required for JsonPropertyName
using System.Collections.ObjectModel;
using System.Diagnostics; // For Debug.WriteLine

namespace JNR.Views.MainPage
{
    // --- Existing Model class for display ---
    public class AlbumSearchResult
    {
        public string AlbumId { get; set; } // Can be Last.fm MBID or null
        public string AlbumName { get; set; }
        public string ArtistName { get; set; }
        public string ReleaseYear { get; set; } // Often N/A from Last.fm album.search
        public string CoverArtUrl { get; set; }
        public string Genre { get; set; } // Often N/A from Last.fm album.search
    }

    // --- Existing TheAudioDB Model Classes (can be kept for reference or future use) ---
    public class TheAudioDBAlbum
    {
        public string idAlbum { get; set; }
        public string idArtist { get; set; }
        public string strAlbum { get; set; }
        public string strArtist { get; set; }
        public string intYearReleased { get; set; }
        public string strGenre { get; set; }
        public string strAlbumThumb { get; set; }
        public string strAlbumThumbHQ { get; set; }
        public string strDescriptionEN { get; set; }
    }

    public class TheAudioDBAlbumSearchResponse
    {
        public List<TheAudioDBAlbum> album { get; set; }
    }
    // --- End of TheAudioDB model classes ---

    // --- New Last.fm Model Classes ---
    public class LastFmImage
    {
        [JsonPropertyName("#text")]
        public string Text { get; set; }
        public string size { get; set; }
    }

    public class LastFmAlbum
    {
        public string name { get; set; }
        public string artist { get; set; }
        public string url { get; set; }
        public List<LastFmImage> image { get; set; }
        public string mbid { get; set; }
        // streamable, listeners, etc., can be added if needed
    }

    public class LastFmAlbumMatches
    {
        public List<LastFmAlbum> album { get; set; }
    }

    public class LastFmResults
    {
        [JsonPropertyName("opensearch:Query")]
        public LastFmQuery OpenSearchQuery { get; set; } // Optional: if you need query details

        [JsonPropertyName("opensearch:totalResults")]
        public string TotalResults { get; set; }

        [JsonPropertyName("opensearch:startIndex")]
        public string StartIndex { get; set; }

        [JsonPropertyName("opensearch:itemsPerPage")]
        public string ItemsPerPage { get; set; }

        public LastFmAlbumMatches albummatches { get; set; }

        [JsonPropertyName("@attr")]
        public LastFmAttr Attr { get; set; } // Optional: if you need attribute details
    }

    // Optional supporting classes for LastFmResults if you want to deserialize everything
    public class LastFmQuery
    {
        [JsonPropertyName("#text")]
        public string Text { get; set; }
        public string role { get; set; }
        public string searchTerms { get; set; }
        public string startPage { get; set; }
    }

    public class LastFmAttr
    {
        [JsonPropertyName("for")]
        public string For { get; set; }
    }

    public class LastFmSearchResponse
    {
        public LastFmResults results { get; set; }
        // Last.fm might also return an error object directly
        public string message { get; set; } // For errors
        public int? error { get; set; }    // For errors
    }
    // --- End of Last.fm model classes ---


    /// <summary>
    /// Lógica de interacción para MainPage.xaml
    /// </summary>
    public partial class MainPage : Window
    {
        public ObservableCollection<AlbumSearchResult> SearchResults { get; set; }
        private static readonly HttpClient client = new HttpClient();
        private const string LastFmApiKey = "d8831cc3c1d4eb53011a7b268a95d028"; // Your Last.fm API Key

        public MainPage()
        {
            InitializeComponent();
            SearchResults = new ObservableCollection<AlbumSearchResult>();
            if (searchResultsList != null)
            {
                searchResultsList.ItemsSource = SearchResults;
            }
            // Set a default User-Agent header for HttpClient requests
            // Some APIs require this, and it's good practice.
            if (client.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("JNR_WPF_App/1.0");
            }
        }

        private string GetLastFmImageUrl(List<LastFmImage> images, string preferredSize = "extralarge")
        {
            if (images == null || !images.Any()) return null;

            // Try to find the preferred size
            var img = images.FirstOrDefault(i => i.size == preferredSize && !string.IsNullOrWhiteSpace(i.Text));
            if (img != null) return img.Text;

            // Fallback sizes in order of preference
            string[] fallbackSizes = { "large", "medium", "small" };
            foreach (var size in fallbackSizes)
            {
                img = images.FirstOrDefault(i => i.size == size && !string.IsNullOrWhiteSpace(i.Text));
                if (img != null) return img.Text;
            }

            // Fallback to the last image with any text if specific sizes aren't found
            img = images.LastOrDefault(i => !string.IsNullOrWhiteSpace(i.Text));
            return img?.Text;
        }

        private async void btnSearchAlbum_Click(object sender, RoutedEventArgs e)
        {
            string searchTerm = txtSearchAlbum.Text;
            if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm == "Enter album or artist name..." || searchTerm == "Enter album name...")
            {
                MessageBox.Show("Please enter an artist or album name to search.", "Search Term Empty");
                return;
            }

            // Last.fm API endpoint for album search
            string apiUrl = $"http://ws.audioscrobbler.com/2.0/?method=album.search&album={Uri.EscapeDataString(searchTerm)}&api_key={LastFmApiKey}&format=json";

            Debug.WriteLine("Requesting URL (Last.fm): " + apiUrl);

            if (lblResultsTitle != null)
            {
                lblResultsTitle.Text = $"Searching for \"{searchTerm}\" on Last.fm...";
            }
            SearchResults.Clear();

            HttpResponseMessage response = null;

            try
            {
                response = await client.GetAsync(apiUrl);
                Debug.WriteLine($"Last.fm API Response Status Code: {response.StatusCode}");

                // Last.fm might return 200 OK even for API errors (e.g., invalid API key),
                // so we check the content for an error message too.
                // However, EnsureSuccessStatusCode() is good for network-level errors.
                response.EnsureSuccessStatusCode();

                string jsonResponse = await response.Content.ReadAsStringAsync();
                // Debug.WriteLine("Last.fm API Response JSON: " + jsonResponse.Substring(0, Math.Min(jsonResponse.Length, 1000)));


                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                LastFmSearchResponse apiResponse = JsonSerializer.Deserialize<LastFmSearchResponse>(jsonResponse, options);

                if (apiResponse?.error != null)
                {
                    // Handle Last.fm specific API errors (e.g., invalid key, etc.)
                    string errorMsg = $"Last.fm API Error {apiResponse.error}: {apiResponse.message}";
                    Debug.WriteLine(errorMsg);
                    if (lblResultsTitle != null) lblResultsTitle.Text = "Last.fm API Error.";
                    MessageBox.Show(errorMsg, "Last.fm API Error");
                    return;
                }

                if (apiResponse?.results?.albummatches?.album != null && apiResponse.results.albummatches.album.Any())
                {
                    foreach (var fmAlbum in apiResponse.results.albummatches.album)
                    {
                        SearchResults.Add(new AlbumSearchResult
                        {
                            AlbumId = fmAlbum.mbid, // MBID can be null/empty
                            AlbumName = fmAlbum.name,
                            ArtistName = fmAlbum.artist,
                            ReleaseYear = "N/A", // album.search doesn't provide release year
                            CoverArtUrl = GetLastFmImageUrl(fmAlbum.image),
                        });
                    }
                    if (lblResultsTitle != null) lblResultsTitle.Text = $"Results for \"{searchTerm}\" from Last.fm:";
                }
                else
                {
                    if (lblResultsTitle != null) lblResultsTitle.Text = $"No results found for \"{searchTerm}\" on Last.fm.";
                }
            }
            catch (HttpRequestException ex)
            {
                StringBuilder errorDetails = new StringBuilder();
                errorDetails.AppendLine($"HttpRequestException occurred (Last.fm):");
                errorDetails.AppendLine($"Message: {ex.Message}");
                if (ex.StatusCode.HasValue) errorDetails.AppendLine($"Status Code from Exception: {ex.StatusCode}");
                if (response != null)
                {
                    errorDetails.AppendLine($"Actual HTTP Status Code from Response: {response.StatusCode}");
                    try
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        errorDetails.AppendLine("Response Content (if any):");
                        errorDetails.AppendLine(errorContent.Substring(0, Math.Min(errorContent.Length, 500)) + "...");
                    }
                    catch (Exception readEx) { errorDetails.AppendLine($"Could not read error response content: {readEx.Message}"); }
                }
                if (ex.InnerException != null) errorDetails.AppendLine($"Inner Exception: {ex.InnerException.Message}");

                Debug.WriteLine(errorDetails.ToString());
                if (lblResultsTitle != null) lblResultsTitle.Text = "API Request Error (Last.fm).";
                MessageBox.Show(errorDetails.ToString(), "API Request Error (Last.fm)");
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"JsonException (Last.fm): {ex.Message}\nPath: {ex.Path}, LineNumber: {ex.LineNumber}, BytePositionInLine: {ex.BytePositionInLine}");
                if (lblResultsTitle != null) lblResultsTitle.Text = "Data Parsing Error (Last.fm).";
                MessageBox.Show($"Error parsing Last.fm JSON data: {ex.Message}", "JSON Error (Last.fm)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Generic Exception (Last.fm): {ex.GetType().FullName}\nMessage: {ex.Message}\nStackTrace: {ex.StackTrace}");
                if (lblResultsTitle != null) lblResultsTitle.Text = "Unexpected Error (Last.fm).";
                MessageBox.Show($"An unexpected error occurred with Last.fm: {ex.Message}", "Error (Last.fm)");
            }
        }

        private void searchResultsList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (searchResultsList is ListBox listBox && listBox.SelectedItem is AlbumSearchResult selectedAlbum)
            {
                // Open the Overview window, passing album data
                var overview = new JNR.Views.Overview(
                    selectedAlbum.AlbumName,
                    selectedAlbum.ArtistName,
                    selectedAlbum.AlbumId,
                    selectedAlbum.CoverArtUrl
                );
                overview.Show();
                // Optionally, hide or close MainPage:
                // this.Close();
            }
        }

        private void AlbumElement_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Find the DataContext (AlbumSearchResult) of the clicked element
            if (sender is FrameworkElement fe && fe.DataContext is AlbumSearchResult selectedAlbum)
            {
                var overview = new JNR.Views.Overview(
                    selectedAlbum.AlbumName,
                    selectedAlbum.ArtistName,
                    selectedAlbum.AlbumId,
                    selectedAlbum.CoverArtUrl
                );
                overview.Show();
                this.Close();
            }
        }

        // ADDED METHOD
        private void MyAlbumsRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.IsChecked == true)
            {
                // The MyAlbums window is in namespace JNR.Views.My_Albums
                JNR.Views.My_Albums.MyAlbums myAlbumsWindow = new JNR.Views.My_Albums.MyAlbums();
                myAlbumsWindow.Show();
                this.Close(); // Close the current MainPage window
            }
        }
    }
}
//====================