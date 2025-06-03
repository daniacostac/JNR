// File: Models/LastFmModel.cs
// You can place these in a new file like Models/LastFmGenreModels.cs
// or add them to your existing LastFm model definitions in MainPage.xaml.cs if you centralize them.

namespace JNR.Models.LastFmModels
{
    using System.Text.Json.Serialization;
    using System.Collections.Generic;
    // Removed: using JNR.Views.MainPage; // This was not pointing to model definitions

    // --- Common Last.fm Model Structures ---

    public class LastFmImage
    {
        [JsonPropertyName("#text")]
        public string Text { get; set; }

        [JsonPropertyName("size")]
        public string Size { get; set; }
    }

    public class LastFmArtistBrief
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("mbid")]
        public string Mbid { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }
    }

    // Basic LastFmAlbum, typically used in lists from API responses like tag.getTopAlbums
    public class LastFmAlbum
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("artist")]
        public LastFmArtistBrief Artist { get; set; } // Changed from string to LastFmArtistBrief for consistency

        [JsonPropertyName("mbid")]
        public string Mbid { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("image")]
        public List<LastFmImage> Image { get; set; }

        // This @attr can hold rank or other metadata depending on the API call
        [JsonPropertyName("@attr")]
        public Dictionary<string, string> Attr { get; set; } // e.g. for rank in tag.getTopAlbums
    }

    // --- Models for tag.getTopAlbums (as originally in this file) ---
    // This response structure might be used by one part of the app (e.g. Genres page after modification, or direct calls)

    public class LastFmTopAlbumsByTagAttr // This seems specific to the top albums by tag response's root attributes
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

    public class LastFmTopAlbumsContainer // Contains a list of LastFmAlbum
    {
        [JsonPropertyName("album")]
        public List<LastFmAlbum> Album { get; set; } // Now correctly uses the defined LastFmAlbum

        [JsonPropertyName("@attr")]
        public LastFmTopAlbumsByTagAttr Attr { get; set; }
    }

    public class LastFmTopAlbumsByTagResponse // Response for tag.getTopAlbums
    {
        [JsonPropertyName("albums")]
        public LastFmTopAlbumsContainer Albums { get; set; }

        // For handling API errors
        [JsonPropertyName("message")]
        public string Message { get; set; } // Property names capitalized, check if API returns lowercase

        [JsonPropertyName("error")]
        public int? Error { get; set; } // Property names capitalized, check if API returns lowercase
    }

    // --- Models for album.getInfo (used by Overview.xaml.cs) ---

    public class LastFmTrackAttr // For individual track attributes e.g. rank
    {
        [JsonPropertyName("rank")]
        public int Rank { get; set; }
    }

    public class LastFmTrack
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("duration")]
        public int? Duration { get; set; } // Duration in seconds

        [JsonPropertyName("@attr")]
        public LastFmTrackAttr Attr { get; set; }

        [JsonPropertyName("artist")]
        public LastFmArtistBrief Artist { get; set; }
    }

    public class LastFmTrackContainer
    {
        [JsonPropertyName("track")]
        public List<LastFmTrack> Track { get; set; }
    }

    public class LastFmTag
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }
    }

    public class LastFmTagContainer
    {
        [JsonPropertyName("tag")]
        public List<LastFmTag> Tag { get; set; }
    }

    public class LastFmWiki
    {
        [JsonPropertyName("published")]
        public string Published { get; set; }

        [JsonPropertyName("summary")]
        public string Summary { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }
    }

    public class LastFmDetailedAlbum // For album.getInfo response
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("artist")]
        public string Artist { get; set; } // album.getInfo often returns artist as a string directly under album

        [JsonPropertyName("mbid")]
        public string Mbid { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("image")]
        public List<LastFmImage> Image { get; set; }

        [JsonPropertyName("listeners")]
        public string Listeners { get; set; }

        [JsonPropertyName("playcount")]
        public string Playcount { get; set; }

        [JsonPropertyName("tracks")]
        public LastFmTrackContainer Tracks { get; set; }

        [JsonPropertyName("tags")]
        public LastFmTagContainer Tags { get; set; }

        [JsonPropertyName("wiki")]
        public LastFmWiki Wiki { get; set; }
    }

    public class LastFmAlbumInfoResponse // For album.getInfo
    {
        [JsonPropertyName("album")]
        public LastFmDetailedAlbum Album { get; set; }

        // For handling API errors (as deserialized in Overview.xaml.cs)
        // API usually returns these in lowercase for errors.
        [JsonPropertyName("error")]
        public int? error { get; set; } // C# property name matches JSON field "error"

        [JsonPropertyName("message")]
        public string message { get; set; } // C# property name matches JSON field "message"
    }
}