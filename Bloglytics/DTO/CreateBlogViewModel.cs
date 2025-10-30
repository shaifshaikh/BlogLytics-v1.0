using System.ComponentModel.DataAnnotations;

namespace Bloglytics.DTO
{
    public class CreateBlogViewModel
    {
        [Required(ErrorMessage = "Title is required")]
        [StringLength(300, ErrorMessage = "Title cannot exceed 300 characters")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Content is required")]
        public string Content { get; set; }

        [StringLength(500, ErrorMessage = "Summary cannot exceed 500 characters")]
        public string Summary { get; set; }

        [Required(ErrorMessage = "Please select a category")]
        public int CategoryId { get; set; }

        public IFormFile FeaturedImage { get; set; }

        public string Status { get; set; } = "Draft"; // Draft or Published
    }

    // For editing existing blog
    public class EditBlogViewModel
    {
        public int PostId { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(300, ErrorMessage = "Title cannot exceed 300 characters")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Content is required")]
        public string Content { get; set; }

        [StringLength(500)]
        public string Summary { get; set; }

        [Required(ErrorMessage = "Please select a category")]
        public int CategoryId { get; set; }

        public IFormFile FeaturedImage { get; set; }

        public string ExistingImage { get; set; }

        public string Status { get; set; }
    }

    // For displaying blog details
    public class BlogDetailViewModel
    {
        public Blog Blog { get; set; }
        public List<Comment> Comments { get; set; }
        public List<Blog> RelatedBlogs { get; set; }
        public bool HasUserLiked { get; set; }
        public int LikeCount { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }

    // For blog listing page
    public class BlogListViewModel
    {
        public List<Blog> Blogs { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalBlogs { get; set; }
        public string SelectedCategory { get; set; }
        public string SearchQuery { get; set; }
        public List<Category> Categories { get; set; }
    }

    // For user's own blogs
    public class MyBlogsViewModel
    {
        public List<Blog> PublishedBlogs { get; set; }
        public List<Blog> DraftBlogs { get; set; }
        public int TotalViews { get; set; }
        public int TotalLikes { get; set; }
        public int TotalComments { get; set; }
    }

    // For adding comment
    public class AddCommentViewModel
    {
        [Required]
        public int PostId { get; set; }

        [Required(ErrorMessage = "Please enter your comment")]
        [StringLength(1000, ErrorMessage = "Comment cannot exceed 1000 characters")]
        public string Content { get; set; }

        public int? ParentCommentId { get; set; }
    }
}
