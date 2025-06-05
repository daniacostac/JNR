using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Scaffolding.Internal;

namespace JNR.Models;

public partial class JnrContext : DbContext
{
    public JnrContext()
    {
    }

    public JnrContext(DbContextOptions<JnrContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Album> Albums { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Useralbumrating> Useralbumratings { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseMySql("server=localhost;port=3306;database=jnr;user=root;password=root", Microsoft.EntityFrameworkCore.ServerVersion.Parse("11.8.2-mariadb"));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_uca1400_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Album>(entity =>
        {
            entity.HasKey(e => e.AlbumId).HasName("PRIMARY");

            entity
                .ToTable("albums")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => new { e.ExternalAlbumId, e.IdSource }, "UQ_Albums_ExternalId_Source").IsUnique();

            entity.Property(e => e.AlbumId).HasColumnType("int(11)");
            entity.Property(e => e.Artist).HasMaxLength(255);
            entity.Property(e => e.CoverArtUrl).HasMaxLength(1024);
            entity.Property(e => e.FirstAddedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp");
            entity.Property(e => e.IdSource).HasMaxLength(50);
            entity.Property(e => e.ReleaseYear).HasColumnType("int(11)");
            entity.Property(e => e.Title).HasMaxLength(255);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PRIMARY");

            entity
                .ToTable("users")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.Email, "UQ_Users_Email").IsUnique();

            entity.HasIndex(e => e.Username, "UQ_Users_Username").IsUnique();

            entity.Property(e => e.UserId).HasColumnType("int(11)");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp");
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.Username).HasMaxLength(100);
        });

        modelBuilder.Entity<Useralbumrating>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.AlbumId })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity
                .ToTable("useralbumratings")
                .UseCollation("utf8mb4_unicode_ci");

            entity.HasIndex(e => e.AlbumId, "FK_UserAlbumRatings_Albums");

            entity.Property(e => e.UserId).HasColumnType("int(11)");
            entity.Property(e => e.AlbumId).HasColumnType("int(11)");
            entity.Property(e => e.RatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp");
            entity.Property(e => e.Rating).HasColumnType("tinyint(4)");
            entity.Property(e => e.ReviewText).HasColumnType("text");

            entity.HasOne(d => d.Album).WithMany(p => p.Useralbumratings)
                .HasForeignKey(d => d.AlbumId)
                .HasConstraintName("FK_UserAlbumRatings_Albums");

            entity.HasOne(d => d.User).WithMany(p => p.Useralbumratings)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_UserAlbumRatings_Users");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
