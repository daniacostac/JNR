using System;
using System.Collections.Generic;

namespace JNR.Models;

public partial class Album
{
    public int AlbumId { get; set; }

    public string ExternalAlbumId { get; set; } = null!;

    public string IdSource { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Artist { get; set; } = null!;

    public string? CoverArtUrl { get; set; }

    public int? ReleaseYear { get; set; }

    public DateTime FirstAddedAt { get; set; }

    public virtual ICollection<Useralbumrating> Useralbumratings { get; set; } = new List<Useralbumrating>();
}
