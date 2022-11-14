using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Enums;

namespace Typedown.Universal.Models
{
    [Table("ImageUploadConfig")]
    public partial class ImageUploadConfig : INotifyPropertyChanged
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;

        public bool IsEnable { get; set; }

        public ImageUploadMethod Method { get; set; }

        public string Config { get; set; } = string.Empty;
    }
}
