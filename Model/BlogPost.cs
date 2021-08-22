using System;
using System.ComponentModel.DataAnnotations;

namespace Comparisons.SQLiteVSDoublets.Model
{
    /// <summary>
    /// <para>
    /// Represents the blog post.
    /// </para>
    /// <para></para>
    /// </summary>
    public class BlogPost
    {
        /// <summary>
        /// <para>
        /// Gets or sets the id value.
        /// </para>
        /// <para></para>
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// <para>
        /// Gets or sets the title value.
        /// </para>
        /// <para></para>
        /// </summary>
        [Required]
        public string Title { get; set; }

        /// <summary>
        /// <para>
        /// Gets or sets the content value.
        /// </para>
        /// <para></para>
        /// </summary>
        [Required]
        public string Content { get; set; }

        /// <summary>
        /// <para>
        /// Gets or sets the publication date time value.
        /// </para>
        /// <para></para>
        /// </summary>
        [Required]
        public DateTime PublicationDateTime { get; set; }

        /// <summary>
        /// <para>
        /// Returns the string.
        /// </para>
        /// <para></para>
        /// </summary>
        /// <returns>
        /// <para>The string</para>
        /// <para></para>
        /// </returns>
        public override string ToString() => $"ID={Id}\tTitle={Title}\tContent=<Length: {Content.Length}>\tPublicationDateTime={PublicationDateTime}";
    }
}
