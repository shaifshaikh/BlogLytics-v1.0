namespace Bloglytics.DTO
{
    public class DashboardViewModel
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string UserEmail { get; set; }
        public string UserRole { get; set; }
        public DashboardStatistics Statistics { get; set; }
        public List<PostSummary> RecentPosts { get; set; }
        public List<PostSummary> TopPosts { get; set; }
        public List<CommentSummary> RecentComments { get; set; }
    }

    public class DashboardStatistics
    {
        public int TotalPosts { get; set; }
        public int DraftPosts { get; set; }
        public int TotalViews { get; set; }
        public int TotalComments { get; set; }
        public int TotalUsers { get; set; }
        public int PostsThisMonth { get; set; }
    }

    public class PostSummary
    {
        public int PostId { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public int ViewCount { get; set; }
        public string AuthorName { get; set; }
        public string CategoryName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
    }

    public class CommentSummary
    {
        public int CommentId { get; set; }
        public string Content { get; set; }
        public string CommenterName { get; set; }
        public string PostTitle { get; set; }
        public int PostId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsApproved { get; set; }
    }
}
