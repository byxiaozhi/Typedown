using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Typedown.Universal.Models
{
    [Table("FileAccessHistory")]
    public class FileAccessHistory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public DateTime Accessed { get; set; }

        public string FilePath { get; set; }
    }
}
