using DvSqlGenWeb.Context.Models;
using Microsoft.EntityFrameworkCore;

namespace DvSqlGenWeb.Context
{
    public class RagDbContext : DbContext
    {
        public DbSet<Chunk> Chunks => Set<Chunk>();

        public RagDbContext(DbContextOptions<RagDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Chunk>()
                .HasIndex(c => new { c.CardType, c.SectionAlias });
            modelBuilder.Entity<Chunk>()
                .HasIndex(c => c.Field);
        }
    }
}
