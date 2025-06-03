// File: Views/Genres.xaml.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
// System.Text.Json.Serialization is not directly used here, but on models
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JNR.Models.LastFmModels;
using JNR.Views; // For Overview class
// For navigation
using JNR.Views.MainPage;
using JNR.Views.My_Albums;
// NO using JNR.Commands; needed if defined in this file

namespace JNR.Views.Genres
{
    // UI Display Models (specific to this Genres window)
    public class GenreAlbumItemUI
    {
        public string AlbumName { get; set; }
        public string ArtistName { get; set; }
        public string CoverArtUrl { get; set; }
        public string AlbumId { get; set; } // MBID from Last.fm
        public string ReleaseYear { get; set; }
    }

    public class GenreListItemUI
    {
        public string Name { get; set; }
        public int Count { get; set; }
    }

    // Local Last.fm API Response Models
    public class LocalLfmTag { [System.Text.Json.Serialization.JsonPropertyName("name")] public string Name { get; set; } [System.Text.Json.Serialization.JsonPropertyName("count")] public int Count { get; set; } [System.Text.Json.Serialization.JsonPropertyName("url")] public string Url { get; set; } }
    public class LocalLfmTopTagsContainer { [System.Text.Json.Serialization.JsonPropertyName("tag")] public List<LocalLfmTag> Tags { get; set; } }
    public class LocalLfmTopTagsResponse { [System.Text.Json.Serialization.JsonPropertyName("toptags")] public LocalLfmTopTagsContainer TopTags { get; set; } }

    // MODIFIED LocalLfmRankAttr: Rank is now string
    public class LocalLfmRankAttr { [System.Text.Json.Serialization.JsonPropertyName("rank")] public string Rank { get; set; } }

    public class LocalLfmAlbumForTagRanked { [System.Text.Json.Serialization.JsonPropertyName("name")] public string Name { get; set; } [System.Text.Json.Serialization.JsonPropertyName("mbid")] public string Mbid { get; set; } [System.Text.Json.Serialization.JsonPropertyName("url")] public string Url { get; set; } [System.Text.Json.Serialization.JsonPropertyName("artist")] public LastFmArtistBrief Artist { get; set; } [System.Text.Json.Serialization.JsonPropertyName("image")] public List<LastFmImage> Image { get; set; } [System.Text.Json.Serialization.JsonPropertyName("@attr")] public LocalLfmRankAttr Attr { get; set; } }
    public class LocalLfmTopAlbumsForTagContainer { [System.Text.Json.Serialization.JsonPropertyName("album")] public List<LocalLfmAlbumForTagRanked> Album { get; set; } }
    public class LocalLfmTopAlbumsByTagResponse { [System.Text.Json.Serialization.JsonPropertyName("albums")] public LocalLfmTopAlbumsForTagContainer Albums { get; set; } }


    public partial class Genres : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private ObservableCollection<GenreListItemUI> _genreList;
        public ObservableCollection<GenreListItemUI> GenreList
        {
            get => _genreList;
            set { _genreList = value; OnPropertyChanged(); }
        }

        private GenreListItemUI _selectedGenreItem;
        public GenreListItemUI SelectedGenreItem
        {
            get => _selectedGenreItem;
            set
            {
                _selectedGenreItem = value;
                OnPropertyChanged();
                (SearchAlbumsByGenreCommand as RelayCommandImplementation)?.RaiseCanExecuteChanged();
            }
        }

        private ObservableCollection<GenreAlbumItemUI> _albumsForSelectedGenre;
        public ObservableCollection<GenreAlbumItemUI> AlbumsForSelectedGenre
        {
            get => _albumsForSelectedGenre;
            set { _albumsForSelectedGenre = value; OnPropertyChanged(); }
        }

        private string _selectedGenreNameForDisplay;
        public string SelectedGenreNameForDisplay
        {
            get => _selectedGenreNameForDisplay;
            set { _selectedGenreNameForDisplay = value; OnPropertyChanged(); }
        }

        private bool _isGenreSelectedAndSearched;
        public bool IsGenreSelectedAndSearched
        {
            get => _isGenreSelectedAndSearched;
            set { _isGenreSelectedAndSearched = value; OnPropertyChanged(); }
        }

        private bool _isLoadingAlbums;
        public bool IsLoadingAlbums
        {
            get => _isLoadingAlbums;
            set { _isLoadingAlbums = value; OnPropertyChanged(); }
        }

        private static readonly HttpClient client = new HttpClient();
        private const string LastFmApiKey = "d8831cc3c1d4eb53011a7b268a95d028"; // Your Last.fm API Key

        public ICommand SearchAlbumsByGenreCommand { get; }

        public Genres()
        {
            InitializeComponent();
            this.DataContext = this;
            GenreList = new ObservableCollection<GenreListItemUI>();
            AlbumsForSelectedGenre = new ObservableCollection<GenreAlbumItemUI>();
            IsGenreSelectedAndSearched = false;
            IsLoadingAlbums = false;
            SelectedGenreNameForDisplay = "Select a genre and click Search";

            SearchAlbumsByGenreCommand = new RelayCommandImplementation(
                async (param) => await ExecuteSearchAlbumsByGenreAsync(),
                (param) => SelectedGenreItem != null && SelectedGenreItem.Name != "-- Select a Genre --"
            );

            if (client.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("JNR_WPF_App/1.0"); // Your App Name/Version
            }
            LoadTopGenresAsync();
        }

        private async Task ExecuteSearchAlbumsByGenreAsync()
        {
            if (SelectedGenreItem == null || SelectedGenreItem.Name == "-- Select a Genre --")
            {
                MessageBox.Show("Please select a valid genre from the dropdown.", "No Genre Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SelectedGenreNameForDisplay = $"Top Albums in: {SelectedGenreItem.Name}";
            IsGenreSelectedAndSearched = true;
            await LoadAlbumsForGenreAsync(SelectedGenreItem.Name);
        }

        private async void LoadTopGenresAsync()
        {
            GenreList.Clear();
            GenreList.Add(new GenreListItemUI { Name = "-- Select a Genre --", Count = -1 });
            SelectedGenreItem = GenreList.First();

            string apiUrl = $"http://ws.audioscrobbler.com/2.0/?method=tag.getTopTags&api_key={LastFmApiKey}&format=json";
            try
            {
                HttpResponseMessage response = await client.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
                string jsonResponse = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var apiResponse = JsonSerializer.Deserialize<LocalLfmTopTagsResponse>(jsonResponse, options);

                if (apiResponse?.TopTags?.Tags != null)
                {
                    foreach (var tag in apiResponse.TopTags.Tags.OrderByDescending(t => t.Count).Take(70))
                    {
                        GenreList.Add(new GenreListItemUI { Name = tag.Name, Count = tag.Count });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadTopGenres Error: {ex.Message}");
                MessageBox.Show($"Error loading genres: {ex.Message}", "API Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadAlbumsForGenreAsync(string genreName)
        {
            if (string.IsNullOrWhiteSpace(genreName)) return;

            IsLoadingAlbums = true;
            AlbumsForSelectedGenre.Clear();

            string apiUrl = $"http://ws.audioscrobbler.com/2.0/?method=tag.getTopAlbums&tag={Uri.EscapeDataString(genreName)}&api_key={LastFmApiKey}&format=json&limit=50";
            try
            {
                HttpResponseMessage response = await client.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
                string jsonResponse = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var apiResponse = JsonSerializer.Deserialize<LocalLfmTopAlbumsByTagResponse>(jsonResponse, options); // This is where the error occurs

                if (apiResponse?.Albums?.Album != null && apiResponse.Albums.Album.Any())
                {
                    foreach (var fmAlbum in apiResponse.Albums.Album)
                    {
                        // The fmAlbum.Attr.Rank is now a string and doesn't directly affect GenreAlbumItemUI population
                        // unless you were trying to parse it here.
                        AlbumsForSelectedGenre.Add(new GenreAlbumItemUI
                        {
                            AlbumName = fmAlbum.Name,
                            ArtistName = fmAlbum.Artist?.Name ?? "Unknown Artist",
                            CoverArtUrl = GetLastFmImageUrl(fmAlbum.Image, "extralarge"),
                            AlbumId = fmAlbum.Mbid,
                            ReleaseYear = "N/A"
                        });
                    }
                }
                else
                {
                    SelectedGenreNameForDisplay = $"No albums found for: {genreName}";
                }
            }
            catch (JsonException jsonEx) // Catch specific JsonException
            {
                Debug.WriteLine($"LoadAlbumsForGenre JSON Error ({genreName}): {jsonEx.ToString()}"); // Log full exception
                SelectedGenreNameForDisplay = $"Error parsing album data for: {genreName}.";
                MessageBox.Show($"Error parsing album data for {genreName}: {jsonEx.Message}\nPath: {jsonEx.Path}\nLine: {jsonEx.LineNumber}, Pos: {jsonEx.BytePositionInLine}", "JSON Deserialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadAlbumsForGenre Error ({genreName}): {ex.Message}");
                SelectedGenreNameForDisplay = $"Error loading albums for: {genreName}";
                MessageBox.Show($"Error loading albums for {genreName}: {ex.Message}", "API Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoadingAlbums = false;
            }
        }

        private string GetLastFmImageUrl(List<LastFmImage> images, string preferredSize = "extralarge")
        {
            if (images == null || !images.Any()) return "/Images/placeholder_album.png";
            var img = images.FirstOrDefault(i => i.Size == preferredSize && !string.IsNullOrWhiteSpace(i.Text));
            if (img != null) return img.Text;
            string[] fallbackSizes = { "large", "medium", "small" };
            foreach (var sizeKey in fallbackSizes)
            {
                img = images.FirstOrDefault(i => i.Size == sizeKey && !string.IsNullOrWhiteSpace(i.Text));
                if (img != null) return img.Text;
            }
            return images.LastOrDefault(i => !string.IsNullOrWhiteSpace(i.Text))?.Text ?? "/Images/placeholder_album.png";
        }

        private void AlbumInGenre_Click(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                if (sender is FrameworkElement fe && fe.DataContext is GenreAlbumItemUI selectedAlbum)
                {
                    var overview = new Overview(
                        selectedAlbum.AlbumName,
                        selectedAlbum.ArtistName,
                        selectedAlbum.AlbumId,
                        selectedAlbum.CoverArtUrl
                    );
                    overview.Owner = Window.GetWindow(this);
                    overview.Show();
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        private void MyAlbumsRadioButton_Click(object sender, RoutedEventArgs e)
        {
            var myAlbumsWindow = new MyAlbums();
            myAlbumsWindow.Owner = this.Owner;
            myAlbumsWindow.Show();
            this.Close();
        }

        private void ChartsRadioButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Charts page not yet implemented.", "Coming Soon");
            EnsureGenresRadioButtonIsChecked();
        }

        private void AboutRadioButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("About page not yet implemented.", "Coming Soon");
            EnsureGenresRadioButtonIsChecked();
        }

        private void SettingsRadioButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Settings page not yet implemented.", "Coming Soon");
            EnsureGenresRadioButtonIsChecked();
        }

        private void LinksRadioButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Links page not yet implemented.", "Coming Soon");
            EnsureGenresRadioButtonIsChecked();
        }

        private void EnsureGenresRadioButtonIsChecked()
        {
            // Logic to ensure the "Genres" radio button remains checked if necessary
        }
    }

    public class RelayCommandImplementation : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommandImplementation(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public RelayCommandImplementation(Action execute, Func<bool> canExecute = null)
            : this(o => execute(), canExecute == null ? (Func<object, bool>)null : o => canExecute())
        { }


        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object parameter) => _execute(parameter);

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}