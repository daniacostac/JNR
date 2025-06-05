using System;
using System.Collections.Generic;

namespace JNR.Models;

public partial class Useralbumrating
{
    public int UserId { get; set; }

    public int AlbumId { get; set; }

    public sbyte Rating { get; set; } 

    public string? ReviewText { get; set; }

    public DateTime RatedAt { get; set; }

    public virtual Album Album { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
