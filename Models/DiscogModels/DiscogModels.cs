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

namespace JNR.Models.DiscogModels // Or just JNR.Models
{
    // --- Discogs Search Models ---
    public class DiscogsCommunity
    {
        [JsonPropertyName("want")]
        public int Want { get; set; }

        [JsonPropertyName("have")]
        public int Have { get; set; }
    }

    public class DiscogsSearchResultItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; } // This is often the Release ID

        [JsonPropertyName("master_id")]
        public int? MasterId { get; set; } // Important for getting master release info

        [JsonPropertyName("title")]
        public string Title { get; set; } // Usually "Artist - Title"

        [JsonPropertyName("year")]
        public string Year { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }

        [JsonPropertyName("genre")]
        public List<string> Genre { get; set; }

        [JsonPropertyName("style")]
        public List<string> Style
        { get;  set; }

            [JsonPropertyName("format")]
            public List<string> Format { get; set; }

        [JsonPropertyName("label")]
        public List<string> Label { get; set; }

        [JsonPropertyName("catno")]
        public string CatNo { get; set; } // Catalog Number

        [JsonPropertyName("thumb")]
        public string Thumb { get; set; } // Thumbnail image URL

        [JsonPropertyName("cover_image")]
        public string CoverImage { get; set; } // Main cover image URL

        [JsonPropertyName("resource_url")]
        public string ResourceUrl { get; set; }

        [JsonPropertyName("community")]
        public DiscogsCommunity Community { get; set; }

        // Helper to split title
        public string ArtistName => Title?.Contains(" - ") == true ? Title.Split(new[] { " - " }, 2, StringSplitOptions.None)[0] : "Unknown Artist";
        public string AlbumName => Title?.Contains(" - ") == true ? Title.Split(new[] { " - " }, 2, StringSplitOptions.None)[1] : Title;
    }

    public class DiscogsPaginationUrls
{
    [JsonPropertyName("last")]
    public string Last { get; set; }

    [JsonPropertyName("next")]
    public string Next { get; set; }
}

public class DiscogsPagination
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pages")]
    public int Pages { get; set; }

    [JsonPropertyName("per_page")]
    public int PerPage { get; set; }

    [JsonPropertyName("items")]
    public int Items { get; set; }

    [JsonPropertyName("urls")]
    public DiscogsPaginationUrls Urls { get; set; }
}

public class DiscogsSearchResponse
{
    [JsonPropertyName("pagination")]
    public DiscogsPagination Pagination { get; set; }

    [JsonPropertyName("results")]
    public List<DiscogsSearchResultItem> Results { get; set; }
}


// --- Discogs Release/Master Detail Models ---
public class DiscogsArtistBrief
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("resource_url")]
    public string ResourceUrl { get; set; }
}

public class DiscogsTrack
{
    [JsonPropertyName("position")]
    public string Position { get; set; }

    [JsonPropertyName("type_")]
    public string Type { get; set; } // "track"

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("duration")]
    public string Duration { get; set; } // e.g., "3:45"
}

public class DiscogsImage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } // "primary", "secondary"

    [JsonPropertyName("uri")]
    public string Uri { get; set; } // Full image URL

    [JsonPropertyName("uri150")]
    public string Uri150 { get; set; } // 150px thumbnail URL

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}

public class DiscogsLabelBrief
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("catno")]
    public string Catno { get; set; }
    [JsonPropertyName("id")]
    public int Id { get; set; }
}

public class DiscogsRelease // Also similar structure for MasterRelease
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("artists")]
    public List<DiscogsArtistBrief> Artists { get; set; }

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("released_formatted")] // e.g. "10 Mar 1973"
    public string ReleasedFormatted { get; set; }

    [JsonPropertyName("genres")]
    public List<string> Genres { get; set; }

    [JsonPropertyName("styles")]
    public List<string> Styles { get; set; }

    [JsonPropertyName("tracklist")]
    public List<DiscogsTrack> Tracklist { get; set; }

    [JsonPropertyName("images")]
    public List<DiscogsImage> Images { get; set; }

    [JsonPropertyName("notes")]
    public string Notes { get; set; } // Often contains detailed info, sometimes HTML

    [JsonPropertyName("data_quality")]
    public string DataQuality { get; set; }

    [JsonPropertyName("country")]
    public string Country { get; set; }

    [JsonPropertyName("labels")]
    public List<DiscogsLabelBrief> Labels { get; set; }

    // For Master Release, you'd have "main_release_url" or similar
    [JsonPropertyName("uri")]
    public string Uri { get; set; } // Discogs webpage URI for this release

    // Helper to get primary image
    public string PrimaryImageUrl => Images?.FirstOrDefault(img => img.Type?.ToLower() == "primary")?.Uri ?? Images?.FirstOrDefault()?.Uri;
    public string PrimaryArtistName => Artists?.FirstOrDefault()?.Name ?? "Unknown Artist";
}
}