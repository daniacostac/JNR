using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic; // For List<string>
using System.Diagnostics; // For Debug.WriteLine

// If LastFmAlbum, LastFmImage are defined in MainPage.xaml.cs scope:
using JNR.Views.MainPage; // Provides LastFmAlbum, LastFmImage etc.
// If you created the new models in JNR.Models.LastFmModels:
// using JNR.Models.LastFmModels;


// Helper class for display, same as before
public class GenreDisplayItem : INotifyPropertyChanged
{
    private string _name;
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    private string _representativeImageUrl;
    public string RepresentativeImageUrl
    {
        get => _representativeImageUrl;
        set { _representativeImageUrl = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class GenresViewModel : INotifyPropertyChanged
{
    public ObservableCollection<GenreDisplayItem> Genres { get; set; }
    private static readonly HttpClient client = new HttpClient();
    private const string LastFmApiKey = "d8831cc3c1d4eb53011a7b268a95d028"; // Your Last.fm API Key

    // List of genres you want to display
    private readonly List<string> _targetGenres = new List<string> {
        "Pop", "Rock", "Jazz", "Blues", "Progressive Rock", "Hip Hop",
        "Electronic", "Classical", "Metal", "Folk", "Indie", "Punk",
        "Funk", "Soul", "Reggae", "Country", "Ambient", "Soundtrack"
    };

    public GenresViewModel()
    {
        Genres = new ObservableCollection<GenreDisplayItem>();
        if (client.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("JNR_WPF_App/1.0");
        }
        LoadGenresAsync();
    }

    private async Task LoadGenresAsync()
    {
        foreach (var genreName in _targetGenres)
        {
            string imageUrl = await GetRepresentativeImageForGenreLastFm(genreName);
            Genres.Add(new GenreDisplayItem
            {
                Name = genreName,
                RepresentativeImageUrl = imageUrl ?? "/Images/placeholder_album.png" // Fallback image
            });
        }
    }

    private async Task<string> GetRepresentativeImageForGenreLastFm(string genreName)
    {
        // Last.fm uses "tags" which are often genres.
        // We'll get the top album for that tag and use its image.
        string apiUrl = $"http://ws.audioscrobbler.com/2.0/?method=tag.getTopAlbums&tag={Uri.EscapeDataString(genreName)}&api_key={LastFmApiKey}&limit=1&format=json";
        Debug.WriteLine($"Fetching top album for tag (genre): {genreName} from URL: {apiUrl}");

        try
        {
            HttpResponseMessage response = await client.GetAsync(apiUrl);
            string jsonResponse = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"Last.fm API Error for tag '{genreName}': {response.StatusCode} - {jsonResponse.Substring(0, Math.Min(jsonResponse.Length, 200))}");
                return null;
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            // Use the new LastFmTopAlbumsByTagResponse model defined earlier
            var topAlbumsResponse = JsonSerializer.Deserialize<JNR.Models.LastFmModels.LastFmTopAlbumsByTagResponse>(jsonResponse, options);
            // If you put models in JNR.Views.MainPage, adjust namespace above.


            if (topAlbumsResponse?.Error != null)
            {
                Debug.WriteLine($"Last.fm API Error for tag '{genreName}': Code {topAlbumsResponse.Error}, Message: {topAlbumsResponse.Message}");
                return null;
            }

            var firstAlbum = topAlbumsResponse?.Albums?.Album?.FirstOrDefault();
            if (firstAlbum != null)
            {
                // Reuse the GetLastFmImageUrl logic from MainPage.xaml.cs
                // This helper should ideally be in a shared utility class or part of LastFm models.
                return GetLastFmImageUrlHelper(firstAlbum.image);
            }
            else
            {
                Debug.WriteLine($"No top albums found for tag '{genreName}'. Response: {jsonResponse.Substring(0, Math.Min(jsonResponse.Length, 200))}");
            }
        }
        catch (JsonException jsonEx)
        {
            Debug.WriteLine($"JSON Parsing Error for tag '{genreName}': {jsonEx.Message}");
        }
        catch (HttpRequestException httpEx)
        {
            Debug.WriteLine($"HTTP Request Error for tag '{genreName}': {httpEx.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Generic error fetching image for genre '{genreName}' from Last.fm: {ex.Message}");
        }
        return null;
    }

    // Helper function (could be moved to a utility class or be part of LastFmAlbum model)
    // This is similar to the one in MainPage.xaml.cs
    private string GetLastFmImageUrlHelper(List<LastFmImage> images, string preferredSize = "extralarge")
    {
        if (images == null || !images.Any()) return null;

        var img = images.FirstOrDefault(i => i.size == preferredSize && !string.IsNullOrWhiteSpace(i.Text));
        if (img != null) return img.Text;

        string[] fallbackSizes = { "large", "medium", "small" };
        foreach (var size in fallbackSizes)
        {
            img = images.FirstOrDefault(i => i.size == size && !string.IsNullOrWhiteSpace(i.Text));
            if (img != null) return img.Text;
        }
        img = images.LastOrDefault(i => !string.IsNullOrWhiteSpace(i.Text));
        return img?.Text;
    }

    // INotifyPropertyChanged implementation
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}