using System.ComponentModel.DataAnnotations;

namespace Bloglytics.DTO
{
    public class Comment
    {
        public int CommentId { get; set; }

        [Required]
        public int PostId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required(ErrorMessage = "Comment content is required")]
        [StringLength(1000, ErrorMessage = "Comment cannot exceed 1000 characters")]
        public string Content { get; set; }

        public int? ParentCommentId { get; set; }

        public bool IsApproved { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation Properties
        public string CommenterName { get; set; }
        public string PostTitle { get; set; }
    }
}
