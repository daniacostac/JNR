// File: Models/ProfileAlbumItem.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JNR.Models // Note the namespace is now JNR.Models
{
    public class ProfileAlbumItem : INotifyPropertyChanged
    {
        public string AlbumName { get; set; }
        public string ArtistName { get; set; }
        public string CoverArtUrl { get; set; }
        public string Mbid { get; set; }
        public sbyte UserRating { get; set; }

        public string UserRatingDisplay => $"{UserRating}/10";
        public bool HasUserRating => UserRating >= 0;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}