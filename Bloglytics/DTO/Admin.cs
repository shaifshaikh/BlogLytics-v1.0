namespace Bloglytics.DTO
{
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalBlogs { get; set; }
        public int TotalPublished { get; set; }
        public int TotalDrafts { get; set; }
        public int TotalComments { get; set; }
        public int PendingComments { get; set; }
        public int TotalCategories { get; set; }
        public int TotalViews { get; set; }
    }

    public class ManageBlogsViewModel
    {
        public List<Blog> Blogs { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalBlogs { get; set; }
        public string SelectedStatus { get; set; }
        public string SearchQuery { get; set; }
    }

    public class AdminUserDto
    {
        public int UserId { get; set; }
        public string Email { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public int TotalBlogs { get; set; }
        public int TotalComments { get; set; }
    }
}
