namespace Bloglytics.DTO
{
    public class HomeViewModel
    {
        public List<CategoryDto> Categories { get; set; }
        public List<BlogPostDto> FeaturedPosts { get; set; }
        public List<BlogPostDto> BlogPosts { get; set; }
        public List<BlogPostDto> PopularPosts { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public int TotalPosts { get; set; }
        public string SelectedCategory { get; set; }
        public string SearchQuery { get; set; }
    }

    public class BlogPostDto
    {
        public int PostId { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string Summary { get; set; }
        public string FeaturedImage { get; set; }
        public int ViewCount { get; set; }
        public DateTime PublishedAt { get; set; }
        public string AuthorName { get; set; }
        public string CategoryName { get; set; }
        public int CommentCount { get; set; }
        public int CategoryId { get; set; }
    }

    public class BlogPostDetailDto : BlogPostDto
    {
        public string Content { get; set; }
        public string AuthorBio { get; set; }
        public List<BlogPostDto> RelatedPosts { get; set; }
        public List<CommentDto> Comments { get; set; }
    }

    public class CategoryDto
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public int PostCount { get; set; }
    }

    public class CommentDto
    {
        public int CommentId { get; set; }
        public string Content { get; set; }
        public string CommenterName { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
