using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeuroSpeech.Eternity.WebStatus.Model
{
    public class BaseEntity
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long NID { get; set; }

        public string ID { get; set; }

        public string IDHash { get; set; }

        public string Name { get; set; }

        public string Input { get; set; }

        public bool IsWorkflow { get; set; }

        public DateTime UtcETA { get; set; }

        public DateTime UtcCreated { get; set; }

        public DateTime UtcUpdated { get; set; }

        public string Response { get; set; }

        public string State { get; set; }

        public string ParentID { get; set; }

        public string ParentIDHash { get; set; }

        public int Priority { get; set; }

        public string Extra { get; set; }

        public DateTime? QueueTTL { get; set; }

        public DateTime? LockTTL { get; set; }
    }
}
