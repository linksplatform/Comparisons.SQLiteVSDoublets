using System;
using System.ComponentModel.DataAnnotations;

namespace Comparisons.SQLiteVSDoublets.Model
{
    public class BlogPost
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        [Required]
        public DateTime PublicationDateTime { get; set; }

        public override string ToString() => $"BlogID={Id}\tTitle={Title}\tContent=<Length: {Content.Length}>\tDateTimeAdd={PublicationDateTime}";
    }
}
