using System.ComponentModel.DataAnnotations;

namespace Bloglytics.DTO
{
    public class Blog
    {
        public int PostId { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(300, ErrorMessage = "Title cannot exceed 300 characters")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Slug is required")]
        [StringLength(300)]
        public string Slug { get; set; }

        [Required(ErrorMessage = "Content is required")]
        public string Content { get; set; }

        [StringLength(500)]
        public string Summary { get; set; }

        public string FeaturedImage { get; set; }

        [Required]
        public int AuthorId { get; set; }

        [Required]
        public int CategoryId { get; set; }

        [Required]
        public string Status { get; set; } = "Draft"; // Draft, Published, Archived

        public int ViewCount { get; set; } = 0;

        public DateTime? PublishedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        // Navigation Properties
        public string AuthorName { get; set; }
        public string CategoryName { get; set; }
        public int CommentCount { get; set; }
        public int LikeCount { get; set; }
    }
    }
