using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

// Code scaffolded by EF Core assumes nullable reference types (NRTs) are not used or disabled.
// If you have enabled NRTs for your project, then un-comment the following line:
// #nullable disable

namespace Materijalno.Model.EntityModels
{
    public partial class materijalno_knjigovodstvoContext : DbContext
    {
        public materijalno_knjigovodstvoContext()
        {
        }

        public materijalno_knjigovodstvoContext(DbContextOptions<materijalno_knjigovodstvoContext> options)
            : base(options)
        {
        }

        public virtual DbSet<SifarnikKonta> SifarnikKonta { get; set; }
        public virtual DbSet<SifarnikMaterijalSkladisteKonto> SifarnikMaterijalSkladisteKonto { get; set; }
        public virtual DbSet<SifarnikMaterijala> SifarnikMaterijala { get; set; }
        public virtual DbSet<SifarnikSkladista> SifarnikSkladista { get; set; }
        public virtual DbSet<Mat> Mat { get; set; }
        public virtual DbSet<TabelaMaterijala> TabelaMaterijala { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. See http://go.microsoft.com/fwlink/?LinkId=723263 for guidance on storing connection strings.
                optionsBuilder.UseSqlServer("Server=DB-SRV-01;Database=materijalno_knjigovodstvo;Trusted_Connection=True;");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SifarnikKonta>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("Id");

                entity.ToTable("sifarnik_konta");

                entity.Property(e => e.Nazkont).HasMaxLength(50);
            });

            modelBuilder.Entity<SifarnikMaterijalSkladisteKonto>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("Id");

                entity.ToTable("sifarnik_materijal_skladiste_konto");
            });

            modelBuilder.Entity<SifarnikMaterijala>(entity =>
            {
                entity.HasKey(e => e.Ident);

                entity.ToTable("sifarnik_materijala");

                entity.Property(e => e.Ident).HasColumnName("ident");

                entity.Property(e => e.Jedm)
                    .HasColumnName("jedm")
                    .HasMaxLength(50);

                entity.Property(e => e.Konto1).HasColumnName("konto1");

                entity.Property(e => e.Konto2).HasColumnName("konto2");

                entity.Property(e => e.Nazmat)
                    .HasColumnName("nazmat")
                    .HasMaxLength(50);

                entity.Property(e => e.Siftar).HasColumnName("siftar");
            });

            modelBuilder.Entity<SifarnikSkladista>(entity =>
            {
                entity.ToTable("sifarnik_skladista");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.DevizniRacun).HasColumnName("devizni_racun");

                entity.Property(e => e.Kljnaz).HasColumnName("kljnaz");

                entity.Property(e => e.MjestoAdresa)
                    .HasColumnName("mjesto_adresa")
                    .HasMaxLength(50);

                entity.Property(e => e.NazivOrg)
                    .HasColumnName("naziv_org")
                    .HasMaxLength(50);

                entity.Property(e => e.NazivSdk)
                    .HasColumnName("naziv_sdk")
                    .HasMaxLength(50);

                entity.Property(e => e.Opstina)
                    .HasColumnName("opstina")
                    .HasMaxLength(50);

                entity.Property(e => e.PozBr).HasColumnName("poz_br");

                entity.Property(e => e.PozBrD).HasColumnName("poz_br_d");

                entity.Property(e => e.ZiroRacun).HasColumnName("ziro_racun");
            });

            modelBuilder.Entity<Mat>(entity =>
            {
                entity.HasNoKey();

                entity.ToTable("mat");

                entity.Property(e => e.Analst)
                    .HasColumnName("analst")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Brdok)
                    .HasColumnName("brdok")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Brfak)
                    .HasColumnName("brfak")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Brnar)
                    .HasColumnName("brnar")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Cartro)
                    .HasColumnName("cartro")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Datnar)
                    .HasColumnName("datnar")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Datun)
                    .HasColumnName("datun")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Fcj)
                    .HasColumnName("fcj")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Fvrijed)
                    .HasColumnName("fvrijed")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Ident).HasColumnName("ident");

                entity.Property(e => e.Kljnaz).HasColumnName("kljnaz");

                entity.Property(e => e.Kljnaz1).HasColumnName("kljnaz1");

                entity.Property(e => e.Kolic).HasColumnName("kolic");

                entity.Property(e => e.Konto1).HasColumnName("konto1");

                entity.Property(e => e.Konto2)
                    .HasColumnName("konto2")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Kontosklad).HasColumnName("kontosklad");

                entity.Property(e => e.Kontosklad1).HasColumnName("kontosklad1");

                entity.Property(e => e.Medus)
                    .HasColumnName("medus")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Mjtst).HasColumnName("mjtst");

                entity.Property(e => e.Nc)
                    .HasColumnName("nc")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Ourst).HasColumnName("ourst");

                entity.Property(e => e.Porppp)
                    .HasColumnName("porppp")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Ppp)
                    .HasColumnName("ppp")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Redbr).HasColumnName("redbr");

                entity.Property(e => e.Status)
                    .HasColumnName("status")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Tarifa)
                    .HasColumnName("tarifa")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Troskovi)
                    .HasColumnName("troskovi")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Trospe).HasColumnName("trospe");

                entity.Property(e => e.Vrijed)
                    .HasColumnName("vrijed")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Zavtro)
                    .HasColumnName("zavtro")
                    .HasMaxLength(50)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<TabelaMaterijala>(entity =>
            {
                entity.HasKey(e => e.Ident);

                entity.ToTable("tabela_materijala");

                entity.Property(e => e.Ident).HasColumnName("ident");

                entity.Property(e => e.Jedm)
                    .HasColumnName("jedm")
                    .HasMaxLength(3)
                    .IsUnicode(false);

                entity.Property(e => e.Konto1).HasColumnName("konto1");

                entity.Property(e => e.Konto2)
                    .HasColumnName("konto2")
                    .IsUnicode(false);

                entity.Property(e => e.Nazmat)
                    .HasColumnName("nazmat")
                    .HasMaxLength(38);

                entity.Property(e => e.Siftar).HasColumnName("siftar");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
