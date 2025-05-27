// You can place these in a new file like Models/LastFmGenreModels.cs
// or add them to your existing LastFm model definitions in MainPage.xaml.cs if you centralize them.

namespace JNR.Models.LastFmModels // Or JNR.Views.MainPage if you keep them there
{
    using System.Text.Json.Serialization;
    using System.Collections.Generic;
    using JNR.Views.MainPage;

    // Assuming LastFmAlbum and LastFmImage are defined in this namespace or accessible.
    // If LastFmAlbum and LastFmImage are in JNR.Views.MainPage, add:
    // using JNR.Views.MainPage;

    public class LastFmTopAlbumsByTagAttr
    {
        [JsonPropertyName("tag")]
        public string Tag { get; set; }

        [JsonPropertyName("page")]
        public string Page { get; set; }

        [JsonPropertyName("perPage")]
        public string PerPage { get; set; }

        [JsonPropertyName("totalPages")]
        public string TotalPages { get; set; }

        [JsonPropertyName("total")]
        public string Total { get; set; }
    }

    public class LastFmTopAlbumsContainer
    {
        // The 'album' property can be a single object if only one result, or an array if multiple.
        // JsonSerializer handles this by default if you expect a List<LastFmAlbum>.
        [JsonPropertyName("album")]
        public List<LastFmAlbum> Album { get; set; } // Reuses LastFmAlbum from MainPage.xaml.cs

        [JsonPropertyName("@attr")]
        public LastFmTopAlbumsByTagAttr Attr { get; set; }
    }

    public class LastFmTopAlbumsByTagResponse
    {
        [JsonPropertyName("albums")]
        public LastFmTopAlbumsContainer Albums { get; set; }

        // For handling API errors
        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("error")]
        public int? Error { get; set; }
    }
}