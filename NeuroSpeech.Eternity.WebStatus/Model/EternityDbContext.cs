using Microsoft.EntityFrameworkCore;
using NeuroSpeech.EntityAccessControl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity.WebStatus.Model
{

    public class EternityDbContext: BaseDbContext<EternityDbContext>
    {

        private string tableName = "Workflows";

        public EternityDbContext(
            DbContextOptions<EternityDbContext> options,
            DbContextEvents<EternityDbContext> events,
            IServiceProvider services)
            : base(options, events, services)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<BaseEntity>()
                .ToTable(tableName)
                .HasDiscriminator((x) => x.IsWorkflow)
                .HasValue<Activity>(false)
                .HasValue<Workflow>(true)
                .IsComplete(true);

            
        }

        public DbSet<Activity> Activities { get; set; }

        public DbSet<Workflow> Workflows { get; set; }

    }
}
