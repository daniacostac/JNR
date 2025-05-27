// File: Models/DiscogModels/DiscogModels.cs

using System;
using System.Collections.Generic;
using System.Linq; // Added for FirstOrDefault
using System.Text.Json.Serialization;

namespace JNR.Models.DiscogModels
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
        public int Id { get; set; }

        [JsonPropertyName("master_id")]
        public int? MasterId { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("year")]
        public string Year { get; set; } // This Year is from search results, often a string

        [JsonPropertyName("country")]
        public string Country { get; set; }

        [JsonPropertyName("genre")]
        public List<string> Genre { get; set; }

        [JsonPropertyName("style")]
        public List<string> Style { get; set; }

        [JsonPropertyName("format")]
        public List<string> Format { get; set; }

        [JsonPropertyName("label")]
        public List<string> Label { get; set; }

        [JsonPropertyName("catno")]
        public string CatNo { get; set; }

        [JsonPropertyName("thumb")]
        public string Thumb { get; set; }

        [JsonPropertyName("cover_image")]
        public string CoverImage { get; set; }

        [JsonPropertyName("resource_url")]
        public string ResourceUrl { get; set; }

        [JsonPropertyName("community")]
        public DiscogsCommunity Community { get; set; }

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
        public string Type { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("duration")]
        public string Duration { get; set; }
    }

    public class DiscogsImage
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("uri")]
        public string Uri { get; set; }

        [JsonPropertyName("uri150")]
        public string Uri150 { get; set; }

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

    public class DiscogsRelease
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("artists")]
        public List<DiscogsArtistBrief> Artists { get; set; }

        [JsonPropertyName("year")]
        public int Year { get; set; } // This Year is from release/master details, usually an int

        [JsonPropertyName("released_formatted")]
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
        public string Notes { get; set; }

        [JsonPropertyName("data_quality")]
        public string DataQuality { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }

        [JsonPropertyName("labels")]
        public List<DiscogsLabelBrief> Labels { get; set; }

        [JsonPropertyName("master_id")]
        public int? MasterId { get; set; }

        [JsonPropertyName("main_release")]
        public int? MainRelease { get; set; }

        [JsonPropertyName("uri")]
        public string Uri { get; set; }

        public string PrimaryImageUrl => Images?.FirstOrDefault(img => img.Type?.ToLower() == "primary")?.Uri ?? Images?.FirstOrDefault()?.Uri;
        public string PrimaryArtistName => Artists?.FirstOrDefault()?.Name ?? "Unknown Artist";
    }

    // --- Discogs Artist Releases Models ---
    public class DiscogsArtistReleaseItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("master_id")]
        public int? MasterId { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("artist")]
        public string Artist { get; set; }

        [JsonPropertyName("year")]
        public object Year { get; set; } // MODIFIED: Changed from string to object

        [JsonPropertyName("thumb")]
        public string Thumb { get; set; }

        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("resource_url")]
        public string ResourceUrl { get; set; }

        public string DisplayArtistName => Artist ?? "Unknown Artist";
        public string DisplayAlbumName => Title ?? "Unknown Title";

        public string DisplayYear
        {
            get
            {
                if (Year == null) return "N/A";
                string yearStr = Year.ToString(); // Convert the object to string
                // Discogs API might return "0" for year if unknown/not set for a release in this context
                return (string.IsNullOrWhiteSpace(yearStr) || yearStr == "0") ? "N/A" : yearStr;
            }
        }

        // Helper to get year as int for sorting, returns 0 if not parseable or null
        public int ParsedYear
        {
            get
            {
                if (Year == null) return 0;
                if (int.TryParse(Year.ToString(), out int parsed))
                {
                    return parsed;
                }
                return 0; // Default for unparseable years (like "Unknown")
            }
        }
    }

    public class DiscogsArtistReleasesResponse
    {
        [JsonPropertyName("pagination")]
        public DiscogsPagination Pagination { get; set; }

        [JsonPropertyName("releases")]
        public List<DiscogsArtistReleaseItem> Releases { get; set; }
    }
}